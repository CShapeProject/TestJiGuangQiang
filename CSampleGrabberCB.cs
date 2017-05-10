using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using OpenCvSharp;
using DirectShowLib;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Runtime.InteropServices.ComTypes;

namespace OpenCameraCSByOpenCV
{
    //typedef void (__stdcall ashPINTPROC)(Point);
    class CSampleGrabberCB : ISampleGrabberCB
    {
        #region Member variables

        /// <summary> graph builder interface. </summary>
        private IFilterGraph2 m_FilterGraph = null;
        IMediaControl m_mediaCtrl = null;

        /// <summary> Set by async routine when it captures an image </summary>
        private bool m_bRunning = false;

        /// <summary> Dimensions of the image, calculated once in constructor. </summary>
        private int m_videoWidth;
        private int m_videoHeight;
        private int m_stride;
        #endregion

        /// zero based device index, and some device parms, plus the file name to save to
        public CSampleGrabberCB(int iDeviceNum)
        {
            DsDevice[] capDevices;
            // Get the collection of video devices
            capDevices = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
            if (iDeviceNum + 1 > capDevices.Length)
            {
                throw new Exception("No video capture devices found at that index!");
            }

            if (!CheckCameraIdInfo(capDevices[iDeviceNum]))
            {
                return;
            }

            try
            {
                // Set up the capture graph
                SetupGraph(capDevices[iDeviceNum]);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary> release everything. </summary>
        public void Dispose()
        {
            CloseInterfaces();
        }

        // Destructor
        ~CSampleGrabberCB()
        {
            CloseInterfaces();
        }

        void Msg(string str)
        {
            MessageBox.Show(str);
        }

        /// <summary> capture the next image </summary>
        public void Start()
        {
            if (!m_bRunning)
            {
                int hr = m_mediaCtrl.Run();
                DsError.ThrowExceptionForHR( hr );
                m_bRunning = true;
            }
        }

        // Pause the capture graph.
        // Running the graph takes up a lot of resources.  Pause it when it
        // isn't needed.
        public void Pause()
        {
            if (m_bRunning)
            {
                int hr = m_mediaCtrl.Pause();
                DsError.ThrowExceptionForHR( hr );
                m_bRunning = false;
            }
        }

        /// <summary> build the capture graph for grabber. </summary>
        private void SetupGraph(DsDevice dev)
        {
            int hr = -1;
            ISampleGrabber sampGrabber = null;
            IBaseFilter baseGrabFlt = null;
            IBaseFilter capFilter = null;
            IBaseFilter muxFilter = null;
            IFileSinkFilter fileWriterFilter = null;
            ICaptureGraphBuilder2 capGraph = null;

            // Get the graphbuilder object
            m_FilterGraph = new FilterGraph() as IFilterGraph2;
            m_mediaCtrl = m_FilterGraph as IMediaControl;
            try
            {
                // Get the ICaptureGraphBuilder2
                capGraph = (ICaptureGraphBuilder2) new CaptureGraphBuilder2();

                // Get the SampleGrabber interface
                sampGrabber = (ISampleGrabber) new SampleGrabber();

                // Start building the graph
                hr = capGraph.SetFiltergraph( m_FilterGraph );
                DsError.ThrowExceptionForHR( hr );

                // Add the video device
                hr = m_FilterGraph.AddSourceFilterForMoniker(dev.Mon, null, dev.Name, out capFilter);
                DsError.ThrowExceptionForHR( hr );

                baseGrabFlt = (IBaseFilter) sampGrabber;
                ConfigureSampleGrabber(sampGrabber);

                // Add the frame grabber to the graph
                hr = m_FilterGraph.AddFilter( baseGrabFlt, "Ds.NET Grabber" );
                DsError.ThrowExceptionForHR( hr );

                // Connect everything together
                //开始渲染采集器的图像,但是不打开渲染窗口"ActiveMovie".
                hr = capGraph.RenderStream(PinCategory.Capture, MediaType.Video, capFilter, null, baseGrabFlt);
                //开始渲染采集器的图像,并且打开渲染窗口"ActiveMovie".
                //hr = capGraph.RenderStream(PinCategory.Capture, MediaType.Video, capFilter, baseGrabFlt, muxFilter);
                DsError.ThrowExceptionForHR(hr);

                // Now that sizes are fixed, store the sizes
                SaveSizeInfo(sampGrabber);
            }
            finally
            {
                if (fileWriterFilter != null)
                {
                    Marshal.ReleaseComObject(fileWriterFilter);
                    fileWriterFilter = null;
                }
                if (muxFilter != null)
                {
                    Marshal.ReleaseComObject(muxFilter);
                    muxFilter = null;
                }
                if (capFilter != null)
                {
                    Marshal.ReleaseComObject(capFilter);
                    capFilter = null;
                }
                if (sampGrabber != null)
                {
                    Marshal.ReleaseComObject(sampGrabber);
                    sampGrabber = null;
                }
            }
        }

        bool CheckCameraIdInfo(DsDevice dev)
        {
            bool isFindCamera = false;
            string pDisplayName = "";
            dev.Mon.GetDisplayName(null, null, out pDisplayName);
            if (pDisplayName.Contains("vid_04fc") &&
                (pDisplayName.Contains("pid_fa02") || pDisplayName.Contains("pid_fa09")))
            {
                isFindCamera = true;
            }
            else
            {
                Msg("Camera vid or pid error!");
            }
            return isFindCamera;
        }

        /// <summary> Read and store the properties </summary>
        private void SaveSizeInfo(ISampleGrabber sampGrabber)
        {
            int hr = -1;
            // Get the media type from the SampleGrabber
            AMMediaType media = new AMMediaType();
            hr = sampGrabber.GetConnectedMediaType( media );
            DsError.ThrowExceptionForHR( hr );

            if( (media.formatType != FormatType.VideoInfo) || (media.formatPtr == IntPtr.Zero) )
            {
                throw new NotSupportedException( "Unknown Grabber Media Format" );
            }

            // Grab the size info
            VideoInfoHeader videoInfoHeader = (VideoInfoHeader) Marshal.PtrToStructure( media.formatPtr, typeof(VideoInfoHeader) );
            m_videoWidth = videoInfoHeader.BmiHeader.Width;
            m_videoHeight = videoInfoHeader.BmiHeader.Height;
            m_stride = m_videoWidth * (videoInfoHeader.BmiHeader.BitCount / 8);

            DsUtils.FreeAMMediaType(media);
            media = null;
        }

        /// <summary> Set the options on the sample grabber </summary>
        private void ConfigureSampleGrabber(ISampleGrabber sampGrabber)
        {
            int hr = -1;
            AMMediaType media = new AMMediaType();

            // Set the media type to Video/RBG24
            media.majorType = MediaType.Video;
            media.subType = MediaSubType.RGB24;
            media.formatType = FormatType.VideoInfo;
            hr = sampGrabber.SetMediaType( media );
            DsError.ThrowExceptionForHR( hr );

            DsUtils.FreeAMMediaType(media);
            media = null;

            // Configure the samplegrabber callback.
            hr = sampGrabber.SetCallback( this, 1 );
            DsError.ThrowExceptionForHR( hr );
        }

        /// <summary> Shut down capture </summary>
        private void CloseInterfaces()
        {
            int hr = -1;
            try
            {
                if( m_mediaCtrl != null )
                {
                    // Stop the graph
                    hr = m_mediaCtrl.Stop();
                    m_mediaCtrl = null;
                    m_bRunning = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            if (m_FilterGraph != null)
            {
                Marshal.ReleaseComObject(m_FilterGraph);
                m_FilterGraph = null;
            }
            GC.Collect();
        }

        /// <summary> sample callback, NOT USED. </summary>
        int ISampleGrabberCB.SampleCB(double SampleTime, IMediaSample pSample)
        {
            Marshal.ReleaseComObject(pSample);
            return 0;
        }

        double LastTimeVal = 0;
        /// <summary> buffer callback, COULD BE FROM FOREIGN THREAD. </summary>
        int ISampleGrabberCB.BufferCB(double SampleTime, IntPtr pBuffer, int BufferLen)
        {
            double dTime = SampleTime - LastTimeVal;
            LastTimeVal = SampleTime;
            Console.WriteLine("dTime " + dTime);

            return 0;
        }

//        uint _ID;
//        int m_nMoveRadius;	

//        public int Width;
//        public int Height;

//        long lClientWidth;
//        long lClientHeight;

//        int m_nBrightDotCount;
//        int m_nSmoothPoints, m_nSmoothingCount;

//        float[] m_fSmoothingX = new float[20];
//        float[] m_fSmoothingY = new float[20];

//        bool m_bSmoothState;

//        float m_fMark;
//        float m_fExsmothX, m_fExsmothY;

//        Point m_curMousePoint;

//        long m_lTickCount;

//        bool m_bSwitch;

//        public MODE m_mode;

//        long m_lLastFrameNumber;

//        long m_lFps;

//        Point m_pointMouseDown;
//        Point m_pointMouseMove;

//        Point m_pointLight, m_pointLight1;

//        Point[] m_p4 = new Point[4];

//        bool m_bYellowCon;
//        int m_nYellowIndex;

//        CvMat m_translate;

//        bool m_bConform;

//        Warper m_warp;

//        public IplImage image;

//        bool g_bBeginDrawRectangle;

//        bool g_bled;

//        int m_nFirstInst;

//        int m_nLed;

//        int m_bRectifyState;
//        bool m_bCurPointModified;

//        int m_nLightcount, m_ncount, m_nPointToConvert;
//        int m_ntempled;

//        bool b_getUnwantedLightSource;
//        Point[] unwantedPoint = new Point[76800];
//        long unwantedPointNum;
//        int getFrameNum;

//        const int SM_CXSCREEN = 0;
//        const int SM_CYSCREEN = 1;
//        [DllImport("user32")]
//        static extern bool GetWindowRect(IntPtr hWnd, ref CvRect rect);
//        [DllImport("user32")]
//        static extern IntPtr GetDesktopWindow();
//        [DllImport("user32")]
//        static extern int GetSystemMetrics(int nIndex);

//        //public int SampleCB(double SampleTime, IMediaSample pSample)
//        /// <summary> sample callback, NOT USED. </summary>
//        int ISampleGrabberCB.SampleCB(double SampleTime, IMediaSample pSample)
//        {
//            return 0;
//        }
        
//        public CSampleGrabberCB(uint camid)
//        {
//            _ID = camid;
//            Width = 0;
//            Height = 0;
//            lClientWidth = GetSystemMetrics(SM_CXSCREEN);
//            lClientHeight = GetSystemMetrics(SM_CYSCREEN);
//            m_nBrightDotCount = 0;
//            m_curMousePoint.X = -1;
//            m_curMousePoint.Y = -1;

//            m_mode = MODE.MODE_SET_CALIBRATION;
//            m_bSwitch = false;
//            m_lTickCount = 0;
//            m_lLastFrameNumber = 0;
//            m_lFps = 0;
//            m_ncount = 0;
//            m_nPointToConvert = 0;

//            m_bCurPointModified = false;
//            m_nMoveRadius = 0;
//            //ZeroMemory( m_p4, sizeof( m_p4 ) );

//            ResetRectify();
//            ResetSmoothing();
//            InitRectifyCfg();

//            m_nFirstInst = 0;
//            b_getUnwantedLightSource = false;
//            //ZeroMemory( unwantedPoint, sizeof( unwantedPoint ) );
//            unwantedPointNum = 0;
//            getFrameNum = 0;
//        }

//        void InitRectifyCfg()
//        {
//            CvPoint2D32f[] cvsrc = new CvPoint2D32f[4];
//            string szwcKey;
//            string strTitle = "Camera1";
//            bool bRet = false;
//            CvRect rc = new CvRect();
//            //ZeroMemory(szwcKey, sizeof(szwcKey));
//            //ZeroMemory(strTitle, sizeof(strTitle));
//            for( int i = 0; i < 4; i++ )
//            {
//                szwcKey = "DataSrc"+i;
//                //bRet = GetPrivateProfileStruct(strTitle, szwcKey, ( LPVOID )&cvsrc[ i ],
//                //            sizeof( CvPoint2D32f ), L".//Rectangle.vro" );
//                if ( !bRet )
//                {
//                    m_p4[ 0 ].X = 20;
//                    m_p4[ 0 ].Y = 20;
//                    m_p4[ 1 ].X = 60;
//                    m_p4[ 1 ].Y = 20;
//                    m_p4[ 2 ].X = 60;
//                    m_p4[ 2 ].Y = 60;
//                    m_p4[ 3 ].X = 20;
//                    m_p4[ 3 ].Y = 60;
//                    break;
//                }

//                //GetWindowRect( GetDesktopWindow(), &rc );
//                if( m_p4[ i ].X > rc.Right - rc.Left || m_p4[ i ].Y > rc.Bottom - rc.Top)
//                {
//                    m_p4[ 0 ].X = 20;
//                    m_p4[ 0 ].Y = 20;
//                    m_p4[ 1 ].X = 60;
//                    m_p4[ 1 ].Y = 20;
//                    m_p4[ 2 ].X = 60;
//                    m_p4[ 2 ].Y = 60;
//                    m_p4[ 3 ].X = 20;
//                    m_p4[ 3 ].Y = 60;
//                    break;
//                }
//                else
//                {
//                    m_p4[ i ].X = (int)cvsrc[ i ].X;
//                    m_p4[ i ].Y = (int)cvsrc[ i ].Y;
//                }
//            }

//            m_bYellowCon = false;
//            m_nYellowIndex = -1;
//            m_bConform = false;
//            //m_translate = cvCreateMat(3,3,CV_32FC1);
//        }

//        void ResetRectify()
//        {
//            m_ntempled = 999;
//            g_bBeginDrawRectangle = false;
//            m_bRectifyState = 0;
//            m_nLed = -1;
//            m_nPointToConvert = 0;
//            m_ncount = 0;
//            g_bled = false;
//        }

//        void getUnwantedPoint( byte[] pBuffer, long BufferSize )
//        {
//            float fGray = 0.0f;
//            unwantedPointNum = 0;
//            for( int y = 0; y < Height; y++ )
//            {
//                for( int x = 0; x < Width * 3; x += 3 )
//                {
//                    //Gray = (R*299 + G*587 + B*114 + 500) / 1000; //整数运算效率高于浮点运算.
//                    /*fGray = ( float )( 299 * pBuffer[ x + 2 + Width * 3 * y ] + 
//                            587 * pBuffer[ x + 1 + Width * 3 * y ] +				
//                            114 * pBuffer[ x + 0 + Width * 3 * y ] ) / 1000.0;*/
//                    //Gray = (R*19595 + G*38469 + B*7472) >> 16; //移位法效率更高.
//                    fGray = (float)((pBuffer[ x + 2 + Width * 3 * y ] * 19595
//                                                    + pBuffer[ x + 1 + Width * 3 * y ] * 38469
//                                                    + pBuffer[ x + 0 + Width * 3 * y ] * 7472) >> 16);	

//                    if( fGray > /*m_nGrayThreshold*/200 ) 
//                    {									
//                        unwantedPoint[unwantedPointNum].X = x;
//                        unwantedPoint[unwantedPointNum].Y = y;
//                        unwantedPointNum++;
//                    }
//                }
//            }
//        }

//        void subUnWantedPoint( byte[] pBuffer, long BuferSize )
//        {
//            if ( unwantedPointNum == 0 )
//            {
//                return;
//            }

//            for ( int index = 0; index < unwantedPointNum; index++ )
//            {
//                int x = unwantedPoint[ index ].X;
//                int y = unwantedPoint[ index ].Y;
//                if ( ( x + 2 + Width * y * 3 ) >= BuferSize )
//                {
//                    return;
//                }
//                pBuffer[ x + 2 + Width * y * 3 ] = 0;
//                pBuffer[ x + 1 + Width * y * 3 ] = 0;
//                pBuffer[ x + 0 + Width * y * 3 ] = 0;
//            }
//        }

//        //public int BufferCB(double SampleTime, IntPtr pBuffer, int BuferSize)
//        /// <summary> buffer callback, COULD BE FROM FOREIGN THREAD. </summary>
//        int ISampleGrabberCB.BufferCB(double SampleTime, IntPtr pBuffer, int BufferLen)
//        {
//            return 0;
//            //getFrameNum++;
//            //if ( getFrameNum == 9000 )
//            //{
//            //    getUnwantedPoint( pBuffer, BuferSize );
//            //    getFrameNum = 0;
//            //    return 0;
//            //}

//            //if (unwantedPointNum > 0)
//            //{
//            //    subUnWantedPoint( pBuffer, BuferSize );
//            //}

//            Point point = new Point();
//            switch( m_mode )
//            {
//            case MODE.MODE_SET_CALIBRATION:
//                if(g_bBeginDrawRectangle)
//                {
//                    if(1 == m_bRectifyState)
//                    {	 
//                        int ax = 0, ay = 0, nled = -10;
//                        nled = m_nLed;
//                        //GetPointToConvert(pBuffer, &ax, &ay, m_nLed);
//                        //Con::printf("nled %d, m_nled %d",nled, m_nLed);

//                        m_nPointToConvert++;
//                        //find point
//                        if(nled != m_nLed)
//                        {
//                            if(m_nLed > 3)
//                            {
//                                m_bRectifyState = 0;
//                            }
//                            else
//                            {
//                                m_p4[3 - m_nLed].X = ax;
//                                m_p4[3 - m_nLed].Y = ay;
//                                m_bRectifyState = 0;
//                            }
//                            //添加代码,改变校准图片信息.
//                            //Con::executef("onChangeCalibration",  Con::getIntArg(m_nLed));
//                        }
//                    }
                    
//                    if(3 == m_nLed)
//                    {
//                        if(_ID == 1)
//                        {
//                            g_bled = true; 
//                            g_bBeginDrawRectangle = false;
//                        }
//                    }
//                }
//                //DisplayRectifyImage(pBuffer, BuferSize);
//                m_nFirstInst = 1;
//                break;

//            case MODE.MODE_MOTION:
//                if(m_nFirstInst == 0)
//                {
//                    //DisplayRectifyImage(pBuffer, BuferSize);
//                    m_nFirstInst = 1;
//                }
//                //Convert2GrayBitmap(pBuffer);

//                if(m_bCurPointModified)
//                {
//                    point.X = m_curMousePoint.X;
//                    point.Y = m_curMousePoint.Y;
//                    m_bCurPointModified = false;

//                    //改变准星坐标.
//                    /*if(m_funPointProc)
//                    {
//                        if(point.x < 1 || point.y < 1)
//                        {
//                            point.x = 0;
//                            point.y = 0;
//                        }
//                        m_funPointProc(this->_ID, point);
//                        //MessageBox(NULL, L"m_funPointProcid", L"OK", MB_OK);
//                    }*/
//                }
//                /*else
//                {
//                    if(m_funPointProc)
//                    {
//                        point.x = -1;
//                        point.y = -1;
//                        m_funPointProc(this->_ID, point);
//                    }
//                }*/
//                break;
//            default:
//                break;
//            }
//            return 0;
//        }

//        void DisplayRectifyImage(byte[] pBuffer, long BuferSize)
//        {
//            CvPoint2D32f[] cvsrc = new CvPoint2D32f[4];
//            CvPoint2D32f[] cvdst = new CvPoint2D32f[4];

//            cvdst[ 0 ].X = 0;
//            cvdst[ 0 ].Y = 0;
//            cvdst[ 1 ].X = lClientWidth;
//            cvdst[ 1 ].Y = 0;
//            cvdst[ 2 ].X = lClientWidth;
//            cvdst[ 2 ].Y = lClientHeight;
//            cvdst[ 3 ].X = 0;
//            cvdst[ 3 ].Y = lClientHeight;

//            Point[] points = new Point[4];
//            //memcpy( points, m_p4, sizeof( Point ) * 4 );

//            //for( int i = 0; i < 4; i++ )
//            //{
//            //    cvsrc[ i ].x = m_p4[ i ].X;
//            //    cvsrc[ i ].y = m_p4[ i ].Y;

//            //    cvdst[ i ].x = cvdst[ i ].x * ( ( float )Width / lClientWidth); 
//            //    cvdst[ i ].y = cvdst[ i ].y * ( ( float )Height / lClientHeight);

//            //    if( m_mode == MODE_SET_CALIBRATION )
//            //    {
//            //        WCHAR szwcKey[ 50 ], strTitle[50];
//            //        swprintf( szwcKey, sizeof( szwcKey ), L"DataSrc%d", i );
//            //        swprintf(strTitle, sizeof(strTitle), L"Camera%d", _ID);
//            //        WritePrivateProfileStruct(strTitle, szwcKey, &cvsrc[ i ],
//            //                sizeof( CvPoint2D32f ), L".//Rectangle.vro" );
//            //        swprintf( szwcKey, sizeof( szwcKey ), L"DataDst%d", i );
//            //        WritePrivateProfileStruct(strTitle, szwcKey, &cvdst[ i ],
//            //                sizeof( CvPoint2D32f ), L".//Rectangle.vro" );
//            //    }
//            //}

//            m_warp.setSource( cvsrc[ 0 ].X, cvsrc[ 0 ].Y, 
//                cvsrc[ 1 ].X, cvsrc[ 1 ].Y,
//                cvsrc[ 2 ].X, cvsrc[ 2 ].Y, 
//                cvsrc[ 3 ].X, cvsrc[ 3 ].Y);

//            m_warp.setDestination( cvdst[ 0 ].X, cvdst[ 0 ].Y, 
//                cvdst[ 1 ].X, cvdst[ 1 ].Y,
//                cvdst[ 2 ].X, cvdst[ 2 ].Y, 
//                cvdst[ 3 ].X, cvdst[ 3 ].Y);
//        }

//        void Convert2GrayBitmap( byte[] pBuffer )
//        {
//            int nMax_x = 0;
//            int nMax_y = 0;
//            float nMaxx1 = 0.0f;
//            float nMaxy1 = 0.0f;
//            float fGray = 0.0f;

//            float ax = 0.0f;
//            float b = 0.0f;
//            float ay = 0.0f;
//            float X = 0.0f;
//            float Y = 0.0f;
//            bool bIsMouseInClient = false;
//            m_nBrightDotCount = 0;

//            //for( int y = 0; y < Height; y++ )
//            //{
//            //    for( int x = 0; x < Width * 3; x += 3 )
//            //    {
//            //        //Gray = (R*299 + G*587 + B*114 + 500) / 1000; //整数运算效率高于浮点运算.
//            //        /*fGray = ( float )( 299 * pBuffer[ x + 2 + Width * 3 * y ] + 
//            //                587 * pBuffer[ x + 1 + Width * 3 * y ] +				
//            //                114 * pBuffer[ x + 0 + Width * 3 * y ] ) / 1000.0;*/
//            //        //Gray = (R*19595 + G*38469 + B*7472) >> 16; //移位法效率更高.
//            //        fGray = (float)((pBuffer[ x + 2 + Width * 3 * y ] * 19595
//            //                                        + pBuffer[ x + 1 + Width * 3 * y ] * 38469
//            //                                        + pBuffer[ x + 0 + Width * 3 * y ] * 7472) >> 16);

//            //        if( fGray > 250 ) 
//            //        {									
//            //            fGray = 255;			
//            //            m_nBrightDotCount++;	
//            //            bIsMouseInClient = true;
//            //        }
//            //        else
//            //        {
//            //            fGray = 0;
//            //        }
//            //        (image->imageData + image->widthStep * y)[ x / 3] = fGray;
//            //    }
//            //}

//            //for( int j = 0; j < image.Height; j++ )
//            //{
//            //    for( int i = 0; i < image.WidthStep; i++ )
//            //    {
//            //        if ((byte)((image.ImageData + image.WidthStep * j)[i]) > 0)
//            //        {
//            //            ax += (byte)((image.ImageData + image.WidthStep * j)[i]) * (i);
//            //            ay += (byte)((image.ImageData + image.WidthStep * j)[i]) * (j);
//            //            b += (byte)((image.ImageData + image.WidthStep * j)[i]);
//            //        }
//            //    }
//            //}

//            if( b != 0 )
//            {
//                X = ax / b;
//                Y = ay / b;
//            }
//            nMaxx1 = X;
//            nMaxy1 = Y;

//            float nx = 0.0f;
//            float ny = 0.0f;
//            m_warp.warp(nMaxx1, nMaxy1, nx, ny);

//            nMaxx1 = nx;
//            nMaxy1 = ny;
//            Exponentialsmoothing(nMaxx1, nMaxy1);

//            //if(m_nBrightDotCount > 0)
//            //{
//            //    CvRect rc;
//            //    IntPtr hWnd = GetDesktopWindow();
//            //    GetWindowRect(hWnd, ref rc);

//            //    nMax_x = (int)( ( ( float )Math.Abs( rc.Right - rc.Left ) / (float)Width ) * nMaxx1 );
//            //    nMax_y = (int)( ( ( float )Math.Abs( rc.Bottom - rc.Top ) / (float)Height ) * nMaxy1 );

//            //    int d1 =  (int)Math.Abs(m_curMousePoint.X - (int)nMax_x );
//            //    if(d1 > m_nMoveRadius)
//            //    {
//            //        m_curMousePoint.X = nMax_x;
//            //        m_bCurPointModified = true;
//            //    }

//            //    int d2 = (int)Math.Abs(m_curMousePoint.Y  - (int)nMax_y);
//            //    if(d2 > m_nMoveRadius)
//            //    {
//            //        m_curMousePoint.Y = nMax_y;
//            //        m_bCurPointModified = true;
//            //    }
//            //}

//            if( !bIsMouseInClient )
//            {
//                m_curMousePoint.X = -1;
//                m_curMousePoint.Y = -1;
//            }
//        }

////STDMETHODIMP_(ULONG) CSampleGrabberCB::Release()
////{
////    cvReleaseImage(&image);

////    if(m_translate)
////    {
////        cvReleaseMat( &m_translate );
////        m_translate = NULL;
////    }
////    return 1;
////}

//        int GetPointToConvert( byte[] pBuffer, int nAxle_x, int nAxle_y, int id_led)
//        {
//            int nx = 0;
//            int ny = 0;
//            float fGray = 0.0f;
//            bool bIsMouseInClient = false;
//            m_nBrightDotCount = 0;
//            int GrayThreshold;

//            if(_ID == 1)
//            {
//                GrayThreshold = 250;
//            }
//            else
//            {
//                GrayThreshold = 250;
//            }

//            for( int y = 0; y < Height; y++ )
//            {
//                for( int x = 0; x < Width * 3; x += 3 )
//                {
//                    //Gray = (R*299 + G*587 + B*114 + 500) / 1000; //整数运算效率高于浮点运算.
//                    /*fGray = ( float )( 299 * pBuffer[ x + 2 + Width * 3 * y ] + 
//                            587 * pBuffer[ x + 1 + Width * 3 * y ] +				
//                            114 * pBuffer[ x + 0 + Width * 3 * y ] ) / 1000.0;*/
//                    //Gray = (R*19595 + G*38469 + B*7472) >> 16; //移位法效率更高.
//                    fGray = (float)((pBuffer[ x + 2 + Width * 3 * y ] * 19595
//                                                    + pBuffer[ x + 1 + Width * 3 * y ] * 38469
//                                                    + pBuffer[ x + 0 + Width * 3 * y ] * 7472) >> 16);
        			
//                    if( fGray >  GrayThreshold) 
//                    {
//                        nx = x / 3;
//                        ny = y;
//                        bIsMouseInClient = true;
//                        m_nBrightDotCount++;	
//                        break;
//                    }
//                }
//            }

//            if(bIsMouseInClient)
//            {
//                nAxle_x = nx;
//                nAxle_y = ny;
//                m_nLed++;
//                m_ncount++;
//            }
//            return 1;
//        }

//        void ResetSmoothing() 
//        {
//            m_bSmoothState = false;
//            m_nSmoothingCount = 0;
//            m_fMark = 0.05f;
//            m_fExsmothX = 0.0f;
//            m_fExsmothY = 0.0f;
//        }

//        void Exponentialsmoothing(float warpedX, float warpedY)
//        {
//            if(m_nSmoothingCount == 0)
//            {
//                m_fExsmothX = warpedX;
//                m_fExsmothY = warpedY;
//                m_nSmoothingCount = 1;
//            }
//            else
//            {
//                m_fExsmothX = m_fMark * warpedX + (1 - m_fMark) * m_fExsmothX;
//                m_fExsmothY = m_fMark * warpedY + (1 - m_fMark) * m_fExsmothY;
//            }
//            warpedX = m_fExsmothX;
//            warpedY = m_fExsmothY;
//        }
    }
}
