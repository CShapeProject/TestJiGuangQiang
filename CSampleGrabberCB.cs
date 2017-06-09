#define CHECK_CAMERA_ZHENLV
//#define CHECK_CAMERA_ID
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Runtime.InteropServices.ComTypes;

using DirectShowLib;

namespace OpenCameraCSByOpenCV
{
    public enum MODE
    {
        MODE_SET_CALIBRATION,
        MODE_MOTION,
    };

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
        private int m_CamID;
        #endregion

        /// zero based device index, and some device parms, plus the file name to save to
        public CSampleGrabberCB(int iDeviceNum)
        {
            InitFindPlayerPoint();

            DsDevice[] capDevices;
            // Get the collection of video devices
            capDevices = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
            if (iDeviceNum + 1 > capDevices.Length)
            {
                throw new Exception("No video capture devices found at that index!");
            }
            m_CamID = iDeviceNum;

#if CHECK_CAMERA_ID
            if (!CheckCameraIdInfo(capDevices[iDeviceNum]))
            {
                return;
            }
#endif

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
            VideoInfoHeader videoInfoHeader = (VideoInfoHeader) Marshal.PtrToStructure( media.formatPtr,
                                                                         typeof(VideoInfoHeader) );
            m_videoWidth = videoInfoHeader.BmiHeader.Width;
            m_videoHeight = videoInfoHeader.BmiHeader.Height;
            m_stride = m_videoWidth * (videoInfoHeader.BmiHeader.BitCount / 8);

            Width = videoInfoHeader.BmiHeader.Width;
            Height = videoInfoHeader.BmiHeader.Height;
            GrayValues = new byte[Width * Height];
            unwantedPoint = new Point[Width * Height];

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

        public static int CamZhenLvVal = 30;
#if CHECK_CAMERA_ZHENLV
        long LastTimeVal = 0;
        int FramNum = 0;
#endif
        /// <summary> buffer callback, COULD BE FROM FOREIGN THREAD. </summary>
        unsafe int ISampleGrabberCB.BufferCB(double sampleTime, IntPtr pBuffer, int bufferLen)
        {
#if CHECK_CAMERA_ZHENLV
            //检测采集器的刷新帧率信息.
            DateTime startTime = TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1, 0, 0, 0, 0));
            DateTime nowTime = DateTime.Now;
            long unixTime = (long)Math.Round((nowTime - startTime).TotalMilliseconds, MidpointRounding.AwayFromZero);
            if (LastTimeVal == 0) {
                LastTimeVal = unixTime;
            }
            else {
                FramNum++;
                long dTimeVal = unixTime - LastTimeVal;
                if (dTimeVal >= 1000) {
                    CamZhenLvVal = FramNum;
                    FramNum = 0;
                    LastTimeVal = unixTime;
                    //Console.WriteLine("dTime " + unixTime + ", camZhenLv " + CamZhenLvVal);
                }
            }
#endif

            UpdateCameraBfferCB(sampleTime);
            CheckBufferCB(pBuffer, bufferLen);
            return 0;
        }

        #region Find Player Cross Point
        int m_nMoveRadius;
        //采集器输出图像的大小信息.
        int Width;
        int Height;

        //显示器的大小信息.
        int lClientWidth;
        int lClientHeight;
        //定义转化为灰度后需要存储的数组. 
        byte[] GrayValues;
        int m_nSmoothingCount;

        float m_fMark;
        float m_fExsmothX, m_fExsmothY;

        MODE m_mode;
        //激光器枪在显示器上投射的坐标信息.
        Point m_curMousePoint;
        //存储显示器四个角在采集器图像里的坐标信息.
        Point[] m_p4 = new Point[4];

        Warper m_warp;
        bool g_bBeginDrawRectangle;
        bool m_bFirstInst;

        //校准光枪第几个点信息.
        byte m_nLed;
        //用于校准光枪坐标.
        bool m_bRectifyState;
        bool m_bCurPointModified;

        //76800 = 320 * 240; -> 采集器像素的宽*高.
        Point[] unwantedPoint; //干扰光源坐标信息.
        long unwantedPointNum;
        int getFrameNum;

        const int SM_CXSCREEN = 0;
        const int SM_CYSCREEN = 1;
        Rectangle m_Rect;
        double TimeUpdateLast;

        #region API函数声明开始.
        [DllImport("kernel32")]//返回0表示失败，非0为成功
        private static extern int WritePrivateProfileStruct(string lpszSections,
            string lpszKey, byte[] lpStruct, int uSizeStruct, string szFile);

        [DllImport("kernel32")]//返回取得字符串缓冲区的长度
        private static extern int GetPrivateProfileStruct(string lpszSections,
            string lpszKey, byte[] lpStruct, int uSizeStruct, string szFile);

        [DllImport("kernel32")]
        private static extern int GetPrivateProfileInt(string lpAppName,
            string lpKeyName, int nDefault, string lpFileName);
        [DllImport("kernel32")]
        private static extern bool WritePrivateProfileString(string lpAppName,
            string lpKeyName, string lpString, string lpFileName);

        [DllImport("user32")]
        static extern bool GetWindowRect(IntPtr hWnd, ref Rectangle rect);
        [DllImport("user32")]
        static extern IntPtr GetDesktopWindow();
        [DllImport("user32")]
        static extern int GetSystemMetrics(int nIndex);
        #endregion API函数声明结束.
        
        string FFileName = "test.ini";
        /// <summary>
        /// 读出INI文件(int)
        /// </summary>
        /// <param name="Section">项目名称(如 [TypeName] )</param>
        /// <param name="Key">键</param>
        public int ReadInt(string section, string key, int def)
        {
            return GetPrivateProfileInt(section, key, def, FFileName);
        }
        
        /// <summary>
        /// 写入INI文件(int)
        /// </summary>
        /// <param name="Section">项目名称(如 [TypeName] )</param>
        /// <param name="Key">键</param>
        /// <param name="Value">值</param>
        public void WriteInt(string section, string key, int iVal)
        {
            WritePrivateProfileString(section, key, iVal.ToString(), FFileName);
        }

        void InitFindPlayerPoint()
        {
            FFileName = Application.StartupPath + "\\test.ini";
            int rv = ReadInt("TestSec", "GrayVal", -1);
            if (rv < 0 || rv > 255)
            {
                GrayThreshold = 125;
                WriteInt("TestSec", "GrayVal", GrayThreshold);
            }
            else {
                GrayThreshold = (byte)rv;
            }

            Width = 0;
            Height = 0;
            lClientWidth = GetSystemMetrics(SM_CXSCREEN);
            lClientHeight = GetSystemMetrics(SM_CYSCREEN);
            m_curMousePoint.X = -1;
            m_curMousePoint.Y = -1;

            ResetMP4Info();
            m_warp = new Warper();
            m_mode = MODE.MODE_MOTION;

            m_bCurPointModified = false;
            m_nMoveRadius = 0;
            m_Rect = Rectangle.Empty;

            ResetRectify();
            ResetSmoothing();
            InitRectifyCfg();

            m_bFirstInst = false;
            unwantedPointNum = 0;
            getFrameNum = 0;
        }

        void InitRectifyCfg()
        {
            int rv = -1;
            GetWindowRect(GetDesktopWindow(), ref m_Rect);
            /************************************************************************/
            /* cvSrcBt[0] -> x高字节,cvSrcBt[1] -> x低字节.                         */
            /* cvSrcBt[2] -> y高字节,cvSrcBt[3] -> y低字节.                         */
            /************************************************************************/
            byte[] cvSrcBt = new byte[4];
            string szwcKey = "";
            string strTitle = "Camera" + m_CamID;

            for (int i = 0; i < 4; i++)
            {
                szwcKey = "DataSrc" + i;
                rv = GetPrivateProfileStruct(strTitle, szwcKey, cvSrcBt, cvSrcBt.Length, ".//Rectangle.vro");
                if (rv == 1)
                {
                    m_p4[i].X = (cvSrcBt[0] << 8) + cvSrcBt[1];
                    m_p4[i].Y = (cvSrcBt[2] << 8) + cvSrcBt[3];
                }

                if (rv != 1 || m_p4[i].X > m_Rect.Right - m_Rect.Left || m_p4[i].Y > m_Rect.Bottom - m_Rect.Top)
                {
                    ResetMP4Info();
                    break;
                }
            }
        }

        void ResetMP4Info()
        {
            m_p4[0].X = 20;
            m_p4[0].Y = 20;
            m_p4[1].X = 60;
            m_p4[1].Y = 20;
            m_p4[2].X = 60;
            m_p4[2].Y = 60;
            m_p4[3].X = 20;
            m_p4[3].Y = 60;
        }

        void ResetRectify()
        {
            g_bBeginDrawRectangle = false;
            m_bRectifyState = false;
            m_nLed = 0;
        }

        unsafe void getUnwantedPoint(byte* pBuffer)
        {
            byte fGray = 0;
            unwantedPointNum = 0;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width * 3; x += 3)
                {
                    //Gray = (R*299 + G*587 + B*114 + 500) / 1000; //整数运算效率高于浮点运算.
                    /*fGray = ( float )( 299 * pBuffer[ x + 2 + Width * 3 * y ] + 
                            587 * pBuffer[ x + 1 + Width * 3 * y ] +				
                            114 * pBuffer[ x + 0 + Width * 3 * y ] ) / 1000.0;*/
                    //Gray = (R*19595 + G*38469 + B*7472) >> 16; //移位法效率更高.
                    fGray = (byte)((pBuffer[x + 2 + Width * 3 * y] * 19595
                                                    + pBuffer[x + 1 + Width * 3 * y] * 38469
                                                    + pBuffer[x + 0 + Width * 3 * y] * 7472) >> 16);

                    if (fGray > /*m_nGrayThreshold*/200)
                    {
                        //记录干扰光源坐标信息.
                        unwantedPoint[unwantedPointNum].X = x;
                        unwantedPoint[unwantedPointNum].Y = y;
                        unwantedPointNum++;
                    }
                }
            }
        }
        
        unsafe void subUnWantedPoint(byte* pBuffer, int buferSize)
        {
            if (unwantedPointNum == 0)
            {
                return;
            }

            for (int index = 0; index < unwantedPointNum; index++)
            {
                int x = unwantedPoint[index].X;
                int y = unwantedPoint[index].Y;
                if ((x + 2 + Width * y * 3) >= buferSize)
                {
                    return;
                }
                //将RGB设置为零,起到屏蔽干扰光源的目的.
                pBuffer[x + 2 + Width * y * 3] = 0;
                pBuffer[x + 1 + Width * y * 3] = 0;
                pBuffer[x + 0 + Width * y * 3] = 0;
            }
        }

        //初始化校准显示器四个角在采集器输出图像的坐标.
        //校准之前务必关闭一下所有激光器,便于准确抓拍干扰光源.
        public void InitJiaoZhunZuoBiao()
        {
            if (m_mode == MODE.MODE_SET_CALIBRATION)
            {
                return;
            }
            m_nLed = 0;
            g_bBeginDrawRectangle = true;
            m_mode = MODE.MODE_SET_CALIBRATION;
            getFrameNum = 0;
        }

        //接收校准按键消息,打开查找光点开关.
        public void ActiveJiaoZhunZuoBiao()
        {
            if (m_mode != MODE.MODE_SET_CALIBRATION)
            {
                return;
            }
            m_bRectifyState = true;
        }

        void CallGameChangeJiaoZhunPic()
        {
            //通知游戏更新校准图片信息.
            Form1.Instance.ChangeJiaoZhunPic((byte)(m_nLed + 1));
        }

        void OpenPlayerJiGuangQi()
        {
            //通知硬件IO打开玩家激光器.
        }

        void CallGameUpdateZhunXingZuoBiao(Point pointVal)
        {
            //通知游戏更新准星坐标信息.
            Form1.Instance.UpdateZhunXingZuoBiao(pointVal);
        }

        void UpdateCameraBfferCB(double timeVal)
        {
            //每隔一定时间更新一次数据信息.
            if (timeVal - TimeUpdateLast < 3)
            {
                return;
            }
            TimeUpdateLast = timeVal;
            GetWindowRect(GetDesktopWindow(), ref m_Rect);
        }

        unsafe void CheckBufferCB(IntPtr pBuffer, int bufferLen)
        {
            //CallGameUpdateZhunXingZuoBiao(new Point(getFrameNum, getFrameNum + 2)); //test.
            if (m_mode == MODE.MODE_MOTION)
            {
                if (getFrameNum >= 625)
                {
                    getFrameNum = 0;
                }

                //625 = 10000 / (1000 / 60); -> 每隔10秒获取一次干扰光源信息,60是采集器的刷新帧率.
                if (getFrameNum % 625 == 0)
                {
                    getUnwantedPoint((byte*)pBuffer);
                }
                getFrameNum++;
            }
            else {
                if (getFrameNum == 0)
                {
                    getFrameNum = 1;
                    getUnwantedPoint((byte*)pBuffer);
                    OpenPlayerJiGuangQi();
                    return;
                }
            }

            if (unwantedPointNum > 0)
            {
                subUnWantedPoint((byte*)pBuffer, bufferLen);
            }

            switch (m_mode)
            {
                case MODE.MODE_SET_CALIBRATION:
                    if (g_bBeginDrawRectangle)
                    {
                        if (m_bRectifyState)
                        {
                            int nled = m_nLed;
                            Point pointVal =  GetPointToConvert((byte*)pBuffer);
                            //Con::printf("nled %d, m_nled %d",nled, m_nLed);

                            //find pointJiGuangQi
                            if (nled != m_nLed && 4 >= m_nLed)
                            {
                                //m_p4的坐标顺序与光枪校准顺序相反.
                                m_p4[4 - m_nLed].X = pointVal.X;
                                m_p4[4 - m_nLed].Y = pointVal.Y;
                                m_bRectifyState = false;
                                //添加代码,改变校准图片信息.
                                CallGameChangeJiaoZhunPic();
                            }
                        }

                        //光枪校准已经完成.
                        if (4 == m_nLed)
                        {
                            SetCalibrationInfo();
                            g_bBeginDrawRectangle = false;
                            m_mode = MODE.MODE_MOTION;
                        }
                    }
                    break;

                case MODE.MODE_MOTION:
                    if (!m_bFirstInst)
                    {
                        SetCalibrationInfo();
                    }
                    ConvertGrayBitmapFindPoint((byte*)pBuffer);

                    if (m_bCurPointModified)
                    {
                        //找到激光器亮点坐标信息.
                        m_bCurPointModified = false;
                        //改变准星坐标.
                        CallGameUpdateZhunXingZuoBiao(m_curMousePoint);
                    }
                    break;
                default:
                    break;
            }
            return;
        }

        //设置透视变换类信息.
        void SetCalibrationInfo()
        {
            string szwcKey = "";
            string strTitle = "Camera"+m_CamID;
            /************************************************************************/
            /* cvSrcBt[0] -> x高字节,cvSrcBt[1] -> x低字节.                         */
            /* cvSrcBt[2] -> y高字节,cvSrcBt[3] -> y低字节.                         */
            /************************************************************************/
            byte[] cvSrcBt = new byte[4];
            Point[] cvsrc = new Point[4]; //采集器点信息.
            Point[] cvdst = new Point[4]; //显示器屏幕信息.

            m_bFirstInst = true; //设置透视变换类信息状态.

            cvdst[0].X = 0;
            cvdst[0].Y = 0;
            cvdst[1].X = lClientWidth;
            cvdst[1].Y = 0;
            cvdst[2].X = lClientWidth;
            cvdst[2].Y = lClientHeight;
            cvdst[3].X = 0;
            cvdst[3].Y = lClientHeight;

            //test.
            //m_p4[0].X = 0;
            //m_p4[0].Y = 62;
            //m_p4[1].X = 285;
            //m_p4[1].Y = 56;
            //m_p4[2].X = 266;
            //m_p4[2].Y = 165;
            //m_p4[3].X = 33;
            //m_p4[3].Y = 180;
            //test.
            for( int i = 0; i < 4; i++ )
            {
                cvsrc[i].X = m_p4[i].X;
                cvsrc[i].Y = m_p4[i].Y;

                cvdst[i].X = (int)(cvdst[i].X * ((float)Width / lClientWidth));
                cvdst[i].Y = (int)(cvdst[i].Y * ((float)Height / lClientHeight));

                if( m_mode == MODE.MODE_SET_CALIBRATION )
                {
                    szwcKey = "DataSrc" + i;
                    cvSrcBt[0] = (byte)((m_p4[i].X & 0xff00) >> 8);
                    cvSrcBt[1] = (byte)((m_p4[i].X & 0x00ff) >> 0);
                    cvSrcBt[2] = (byte)((m_p4[i].Y & 0xff00) >> 8);
                    cvSrcBt[3] = (byte)((m_p4[i].Y & 0x00ff) >> 0);

                    szwcKey = "DataSrc" + i;
                    WritePrivateProfileStruct(strTitle, szwcKey, cvSrcBt, cvSrcBt.Length, ".//Rectangle.vro");
                }
            }

            //设置透视变换类信息.
            m_warp.setSource(cvsrc[0].X, cvsrc[0].Y,
                cvsrc[1].X, cvsrc[1].Y,
                cvsrc[2].X, cvsrc[2].Y,
                cvsrc[3].X, cvsrc[3].Y);

            m_warp.setDestination(cvdst[0].X, cvdst[0].Y,
                cvdst[1].X, cvdst[1].Y,
                cvdst[2].X, cvdst[2].Y,
                cvdst[3].X, cvdst[3].Y);
        }

        unsafe void ConvertGrayBitmapFindPoint(byte* pBuffer)
        {
            int nMax_x = 0;
            int nMax_y = 0;
            float nMaxx1 = 0.0f;
            float nMaxy1 = 0.0f;
            byte fGray = 0;

            float ax = 0.0f;
            float ay = 0.0f;
            float b = 0.0f;
            int indexVal = 0;
            bool bIsMouseInClient = false;

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width * 3; x += 3)
                {
                    //Gray = (R*299 + G*587 + B*114 + 500) / 1000; //整数运算效率高于浮点运算.
                    /*fGray = ( float )( 299 * pBuffer[ x + 2 + Width * 3 * y ] + 
                            587 * pBuffer[ x + 1 + Width * 3 * y ] +				
                            114 * pBuffer[ x + 0 + Width * 3 * y ] ) / 1000.0;*/
                    //Gray = (R*19595 + G*38469 + B*7472) >> 16; //移位法效率更高.
                    fGray = (byte)((pBuffer[x + 2 + Width * 3 * y] * 19595
                                                    + pBuffer[x + 1 + Width * 3 * y] * 38469
                                                    + pBuffer[x + 0 + Width * 3 * y] * 7472) >> 16);

                    if (fGray > GrayThreshold)
                    {
                        fGray = 255;
                        bIsMouseInClient = true;
                    }
                    else
                    {
                        fGray = 0;
                    }

                    GrayValues[indexVal] = fGray;
                    indexVal++;
                }
            }

            if (!bIsMouseInClient)
            {
                return;
            }

            indexVal = 0;
            for (int j = 0; j < Height; j++)
            {
                for (int i = 0; i < Width; i++)
                {
                    if (GrayValues[indexVal] > 0)
                    {
                        //这里是重点,暂时不知道为什么要这样计算.
                        ax += GrayValues[indexVal] * i;
                        ay += GrayValues[indexVal] * j;
                        b += GrayValues[indexVal];
                    }
                    indexVal++;
                }
            }

            if (b != 0)
            {
                ax = ax / b;
                ay = ay / b;
            }
            nMaxx1 = ax;
            nMaxy1 = ay;

            //重点,透视变换类.
            PointF pointNV = m_warp.warp(nMaxx1, nMaxy1);
            nMaxx1 = pointNV.X;
            nMaxy1 = pointNV.Y;
            //坐标平滑处理.
            //PointF pointVal = Exponentialsmoothing(nMaxx1, nMaxy1);
            //nMaxx1 = pointVal.X;
            //nMaxy1 = pointVal.Y;

            if (bIsMouseInClient)
            {
                nMax_x = (int)(((float)Math.Abs(m_Rect.Right - m_Rect.Left) / (float)Width) * nMaxx1);
                nMax_y = (int)(((float)Math.Abs(m_Rect.Bottom - m_Rect.Top) / (float)Height) * nMaxy1);

                int d1 = (int)Math.Abs(m_curMousePoint.X - (int)nMax_x);
                if (d1 > m_nMoveRadius)
                {
                    m_curMousePoint.X = nMax_x;
                    m_bCurPointModified = true;
                }

                int d2 = (int)Math.Abs(m_curMousePoint.Y - (int)nMax_y);
                if (d2 > m_nMoveRadius)
                {
                    m_curMousePoint.Y = nMax_y;
                    m_bCurPointModified = true;
                }
            }

            //if (!bIsMouseInClient)
            //{
            //    m_curMousePoint.X = -1;
            //    m_curMousePoint.Y = -1;
            //}
        }

        byte GrayThreshold = 120;
        unsafe Point GetPointToConvert(byte* pBuffer)
        {
            byte fGray = 0;
            bool isStopCheck = false;
            Point pointVal = Point.Empty;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width * 3; x += 3)
                {
                    //Gray = (R*299 + G*587 + B*114 + 500) / 1000; //整数运算效率高于浮点运算.
                    /*fGray = ( float )( 299 * pBuffer[ x + 2 + Width * 3 * y ] + 
                            587 * pBuffer[ x + 1 + Width * 3 * y ] +				
                            114 * pBuffer[ x + 0 + Width * 3 * y ] ) / 1000.0;*/
                    //Gray = (R*19595 + G*38469 + B*7472) >> 16; //移位法效率更高.
                    fGray = (byte)((pBuffer[x + 2 + Width * 3 * y] * 19595
                                                    + pBuffer[x + 1 + Width * 3 * y] * 38469
                                                    + pBuffer[x + 0 + Width * 3 * y] * 7472) >> 16);
                    //找到激光器亮点.
                    if (fGray > GrayThreshold)
                    //if (fGray > GrayThreshold || fGray > 0) //test
                    {
                        isStopCheck = true;
                        pointVal.X = x / 3;
                        pointVal.Y = y;
                        m_nLed++;
                        break;
                    }
                }

                if (isStopCheck)
                {
                    break;
                }
            }
            return pointVal;
        }

        void ResetSmoothing()
        {
            m_nSmoothingCount = 0;
            m_fMark = 0.05f;
            m_fExsmothX = 0.0f;
            m_fExsmothY = 0.0f;
        }

        PointF Exponentialsmoothing(float warpedX, float warpedY)
        {
            PointF pointVal = PointF.Empty;
            if (m_nSmoothingCount == 0)
            {
                m_fExsmothX = warpedX;
                m_fExsmothY = warpedY;
                m_nSmoothingCount = 1;
            }
            else
            {
                m_fExsmothX = m_fMark * warpedX + (1 - m_fMark) * m_fExsmothX;
                m_fExsmothY = m_fMark * warpedY + (1 - m_fMark) * m_fExsmothY;
            }
            warpedX = m_fExsmothX;
            warpedY = m_fExsmothY;

            pointVal.X = warpedX;
            pointVal.Y = warpedY;
            return pointVal;
        }
        #endregion
    }
}