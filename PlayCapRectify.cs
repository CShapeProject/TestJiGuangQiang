using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using DirectShowLib;
using OpenCvSharp;
using System.Windows.Forms;
using System.Runtime.InteropServices.ComTypes;

namespace OpenCameraCSByOpenCV
{
    class PlayCapRectify
    {
        // a small enum to record the graph state
        enum PlayState
        {
            Stopped,
            Paused,
            Running,
            Init
        };
        //IVideoWindow videoWindow = null;
        IMediaControl mediaControl = null;
        //IMediaEventEx mediaEventEx = null;
        IGraphBuilder graphBuilder = null;
        ICaptureGraphBuilder2 captureGraphBuilder = null;
        //PlayState currentState = PlayState.Stopped;

        //public CSampleGrabberCB CB = null;

        ///// <summary> sample callback, NOT USED. </summary>
        //int ISampleGrabberCB.SampleCB(double SampleTime, IMediaSample pSample)
        //{
        //    return 0;
        //}
        
        ///// <summary> buffer callback, COULD BE FROM FOREIGN THREAD. </summary>
        //int ISampleGrabberCB.BufferCB(double SampleTime, IntPtr pBuffer, int BufferLen)
        //{
        //    return 0;
        //}

        public void CaptureVideo()
        {
            int hr = 0;
            IBaseFilter sourceFilter = null;
            try
            {
                //CB = new CSampleGrabberCB(1);
                // Get DirectShow interfaces
                GetInterfaces();

                // Attach the filter graph to the capture graph
                hr = this.captureGraphBuilder.SetFiltergraph(this.graphBuilder);
                DsError.ThrowExceptionForHR(hr);

                // Use the system device enumerator and class enumerator to find
                // a video capture/preview device, such as a desktop USB video camera.
                sourceFilter = FindCaptureDevice();

                // Add Capture filter to our graph.
                hr = this.graphBuilder.AddFilter(sourceFilter, "Video Capture");
                DsError.ThrowExceptionForHR(hr);

                // Render the preview pin on the video capture filter
                // Use this instead of this.graphBuilder.RenderFile
                hr = this.captureGraphBuilder.RenderStream(PinCategory.Preview, MediaType.Video, sourceFilter, null, null);
                DsError.ThrowExceptionForHR(hr);

                // Now that the filter has been added to the graph and we have
                // rendered its stream, we can release this reference to the filter.
                Marshal.ReleaseComObject(sourceFilter);

                // Set video window style and position
                //SetupVideoWindow();

                // Add our graph to the running object table, which will allow
                // the GraphEdit application to "spy" on our graph
                //rot = new DsROTEntry(this.graphBuilder);

                // Start previewing video data
                hr = this.mediaControl.Run();
                DsError.ThrowExceptionForHR(hr);

                // Remember current state
                //this.currentState = PlayState.Running;
            }
            catch
            {
                MessageBox.Show("An unrecoverable error has occurred.");
            }
        }

        public void GetInterfaces()
        {
            //int hr = 0;
            // An exception is thrown if cast fail
            this.graphBuilder = (IGraphBuilder)new FilterGraph();
            this.captureGraphBuilder = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
            this.mediaControl = (IMediaControl)this.graphBuilder;
            //this.videoWindow = (IVideoWindow)this.graphBuilder;
            //this.mediaEventEx = (IMediaEventEx)this.graphBuilder;

            //hr = this.mediaEventEx.SetNotifyWindow(this.Handle, WM_GRAPHNOTIFY, IntPtr.Zero);
            //DsError.ThrowExceptionForHR(hr);
        }

        // This version of FindCaptureDevice is provide for education only.
        // A second version using the DsDevice helper class is define later.
        public IBaseFilter FindCaptureDevice()
        {
            int hr = 0;
#if USING_NET11
      UCOMIEnumMoniker classEnum = null;
      UCOMIMoniker[] moniker = new UCOMIMoniker[1];
#else
            IEnumMoniker classEnum = null;
            IMoniker[] moniker = new IMoniker[1];
#endif
            object source = null;

            // Create the system device enumerator
            ICreateDevEnum devEnum = (ICreateDevEnum)new CreateDevEnum();

            // Create an enumerator for the video capture devices
            hr = devEnum.CreateClassEnumerator(FilterCategory.VideoInputDevice, out classEnum, 0);
            DsError.ThrowExceptionForHR(hr);

            // The device enumerator is no more needed
            Marshal.ReleaseComObject(devEnum);

            // If there are no enumerators for the requested type, then 
            // CreateClassEnumerator will succeed, but classEnum will be NULL.
            if (classEnum == null)
            {
                throw new ApplicationException("No video capture device was detected.\r\n\r\n" +
                                               "This sample requires a video capture device, such as a USB WebCam,\r\n" +
                                               "to be installed and working properly.  The sample will now close.");
            }

            // Use the first video capture device on the device list.
            // Note that if the Next() call succeeds but there are no monikers,
            // it will return 1 (S_FALSE) (which is not a failure).  Therefore, we
            // check that the return code is 0 (S_OK).
#if USING_NET11
      int i;
      if (classEnum.Next (moniker.Length, moniker, IntPtr.Zero) == 0)
#else
            if (classEnum.Next(moniker.Length, moniker, IntPtr.Zero) == 0)
#endif
            {
                // Bind Moniker to a filter object
                Guid iid = typeof(IBaseFilter).GUID;
                moniker[0].BindToObject(null, null, ref iid, out source);
            }
            else
            {
                throw new ApplicationException("Unable to access video capture device!");
            }

            // Release COM objects
            Marshal.ReleaseComObject(moniker[0]);
            Marshal.ReleaseComObject(classEnum);

            // An exception is thrown if cast fail
            return (IBaseFilter)source;
        }

        public void CloseInterfaces()
        {
            // Stop previewing data
            if (this.mediaControl != null)
                this.mediaControl.StopWhenReady();

            //this.currentState = PlayState.Stopped;

            // Stop receiving events
            //if (this.mediaEventEx != null)
            //    this.mediaEventEx.SetNotifyWindow(IntPtr.Zero, WM_GRAPHNOTIFY, IntPtr.Zero);

            // Relinquish ownership (IMPORTANT!) of the video window.
            // Failing to call put_Owner can lead to assert failures within
            // the video renderer, as it still assumes that it has a valid
            // parent window.
            //if (this.videoWindow != null)
            //{
            //    this.videoWindow.put_Visible(OABool.False);
            //    this.videoWindow.put_Owner(IntPtr.Zero);
            //}

            // Remove filter graph from the running object table
            //if (rot != null)
            //{
            //    rot.Dispose();
            //    rot = null;
            //}

            // Release DirectShow interfaces
            Marshal.ReleaseComObject(this.mediaControl); this.mediaControl = null;
            //Marshal.ReleaseComObject(this.mediaEventEx); this.mediaEventEx = null;
            //Marshal.ReleaseComObject(this.videoWindow); this.videoWindow = null;
            Marshal.ReleaseComObject(this.graphBuilder); this.graphBuilder = null;
            Marshal.ReleaseComObject(this.captureGraphBuilder); this.captureGraphBuilder = null;
        }

        //GdiplusStartupInput gdiplusStartupInput;
        //ulong gdiplusToken;
        //Guid CLSID_VideoInputDeviceCategory = new Guid("{860BB310-5D01-11d0-BD3B-00A0C911CE86}");
        //Guid CLSID_FilterGraph = new Guid("{e436ebb3-524f-11ce-9f53-0020af0ba770}");
        //Guid CLSID_CaptureGraphBuilder2 = new Guid("{BF87B6E1-8C27-11d0-B3F0-00AA003761C5}");
        //Guid CLSID_SystemDeviceEnum = new Guid("{62BE5D10-60EB-11d0-BD3B-00A0C911CE86}");
        //Guid CLSID_SampleGrabber = new Guid("{C1F400A0-3F08-11d3-9F0B-006008039E37}");
        //Guid CLSID_NullRenderer = new Guid("{C1F400A4-3F08-11d3-9F0B-006008039E37}");
        //Guid IID_IBaseFilter = typeof(IBaseFilter).GUID;
        //const int BITSPIXEL = 12;
        //const int IPL_DEPTH_8U = 8;
        
        //static uint ID_CAMERA1 = 1;
        //IVideoWindow[] g_pVW = new IVideoWindow[1];
        //IMediaControl[] g_pMC = new IMediaControl[1];
        //IMediaEventEx[] g_pME = new IMediaEventEx[1];
        //IGraphBuilder[] g_pGraph = new IGraphBuilder[1];
        //ICaptureGraphBuilder2[] g_pCapture = new ICaptureGraphBuilder2[1];

        //public CSampleGrabberCB CB = null;
        //bool restartCamera = false;

        //[DllImport("gdi32.dll")]
        //public static extern int GetDeviceCaps(IntPtr hdc, int nIndex);
        //[DllImport("User32.dll")]
        //public static extern IntPtr GetDC(IntPtr hwnd);
        //[DllImport("User32.dll")]
        //static extern int ReleaseDC(IntPtr hWnd,  IntPtr hDC);

        //void Msg(string str)
        //{
        //    MessageBox.Show(str);
        //}

        //bool CaptureVideo()
        //{
        //    bool hr = false;
        //    CB = new CSampleGrabberCB(ID_CAMERA1);
        //    if(CB == null)
        //    {
        //        Msg("Failed to get CB!");
        //        return hr;
        //    }

        //    hr = GetInterfaces();
        //    if (!hr)
        //    {
        //        Msg("Failed to get video interfaces!");
        //        return hr;
        //    }

        //    hr = g_pCapture[0].SetFiltergraph(g_pGraph[0]) == 0 ? true : false;
        //    if (!hr)
        //    {
        //        Msg("Failed to set capture filter graph!");
        //        return hr;
        //    }
            
        //    IBaseFilter[] pSrcFilter = null;
        //    pSrcFilter = FindCaptureDevice();
        //    hr = pSrcFilter == null ? false : true;
        //    if (!hr)
        //    {
        //        Msg("Failed to find capture filter!");
        //        return hr;
        //    }

        //    hr = g_pGraph[0].AddFilter(pSrcFilter[0], "Video Capture0") == 0 ? true : false;
        //    if (!hr)
        //    {
        //        Msg("Couldn't add the capture filter to the graph!\r\n\r\n"
        //            + "If you have a working video capture device, please make sure\r\n"
        //            + "that it is connected and is not being used by another application.\r\n\r\n"
        //            + "The sample will now close.");
        //        Marshal.ReleaseComObject(pSrcFilter[0]);
        //        return hr;
        //    }

        //    IBaseFilter[] pF = new IBaseFilter[1];
        //    ISampleGrabber[] pGrabber = new ISampleGrabber[1];
        //    pGrabber[0] = (ISampleGrabber) new SampleGrabber();

        //    Type comtype = null;
        //    object comobj = null;
        //    comtype = Type.GetTypeFromCLSID(CLSID_SampleGrabber);
        //    if (comtype == null)
        //    {
        //        Msg("DirectX (8.1 or higher) not installed?");
        //    }

        //    comobj = Activator.CreateInstance(comtype);
        //    pF[0] = (IBaseFilter)comobj;
        //    hr = pF[0] == null ? false : true;
        //    if (!hr)
        //    {
        //        Msg("Couldn't create sample grabber filter!\r\n\r\n");
        //        Marshal.ReleaseComObject(pF[0]);
        //        Marshal.ReleaseComObject(pSrcFilter[0]);
        //        return hr;
        //    }

        //    hr = pGrabber[0] == null ? false : true;
        //    if (!hr)
        //    {
        //        Msg("Couldn't create sample grabber filter!\r\n\r\n");
        //        Marshal.ReleaseComObject(pGrabber[0]);
        //        Marshal.ReleaseComObject(pF[0]);
        //        Marshal.ReleaseComObject(pSrcFilter[0]);
        //        return hr;
        //    }

        //    hr = g_pGraph[0].AddFilter(pF[0], "SampleGrabber0") == 0 ? true : false;
        //    if (!hr)
        //    {
        //        Msg("Couldn't add the grabber filter to the graph!\r\n\r\n"
        //            + "If you have a working video capture device, please make sure\r\n"
        //            + "that it is connected and is not being used by another application.\r\n\r\n"
        //            + "The sample will now close.");
        //        Marshal.ReleaseComObject(pGrabber[0]);
        //        Marshal.ReleaseComObject(pF[0]);
        //        Marshal.ReleaseComObject(pSrcFilter[0]);
        //        return hr;
        //    }

        //    IntPtr hdc = GetDC(IntPtr.Zero);
        //    int iBitDepth = GetDeviceCaps(hdc, BITSPIXEL);
        //    ReleaseDC(IntPtr.Zero, hdc);

        //    AMMediaType mt = new AMMediaType();
        //    mt.majorType = MediaType.Video;
        //    switch (iBitDepth)
        //    {
        //        case 8:
        //            mt.subType = MediaSubType.RGB8;
        //            break;
        //        case 16:
        //            mt.subType = MediaSubType.RGB555;
        //            break;
        //        case 24:
        //            mt.subType = MediaSubType.RGB24;
        //            break;
        //        case 32:
        //            mt.subType = MediaSubType.RGB24;
        //            break;
        //        default:
        //            return false;
        //    }

        //    hr = pGrabber[0].SetMediaType(mt) == 0 ? true : false;
        //    if (!hr)
        //    {
        //        Msg("Couldn't set media type!\r\n\r\n");
        //        Marshal.ReleaseComObject(pGrabber[0]);
        //        Marshal.ReleaseComObject(pF[0]);
        //        Marshal.ReleaseComObject(pSrcFilter[0]);
        //        return hr;
        //    }

        //    IBaseFilter[] pNull = new IBaseFilter[1];
        //    comtype = null;
        //    comobj = null;
        //    comtype = Type.GetTypeFromCLSID(CLSID_NullRenderer);
        //    if (comtype == null)
        //    {
        //        Msg("DirectX (8.1 or higher) not installed?");
        //    }

        //    comobj = Activator.CreateInstance(comtype);
        //    pNull[0] = (IBaseFilter)comobj;
        //    hr = pNull[0] == null ? false : true;
        //    if (!hr)
        //    {
        //        Msg("Couldn't create null renderer filter!\r\n\r\n");
        //        Marshal.ReleaseComObject(pGrabber[0]);
        //        Marshal.ReleaseComObject(pF[0]);
        //        Marshal.ReleaseComObject(pSrcFilter[0]);
        //        return hr;
        //    }

        //    hr = g_pGraph[0].AddFilter(pNull[0], "NullRenderer0") == 0 ? true : false;
        //    if (!hr)
        //    {
        //        Msg("Couldn't add the pNull filter to the graph!\r\n\r\n"
        //            +"If you have a working video capture device, please make sure\r\n"
        //            +"that it is connected and is not being used by another application.\r\n\r\n"
        //            +"The sample will now close.");
        //        Marshal.ReleaseComObject(pGrabber[0]);
        //        Marshal.ReleaseComObject(pF[0]);
        //        Marshal.ReleaseComObject(pSrcFilter[0]);
        //        Marshal.ReleaseComObject(pNull[0]);
        //        return hr;
        //    }

        //    hr = g_pCapture[0].RenderStream(PinCategory.Capture, MediaType.Video, pSrcFilter[0], pF[0], pNull[0]) == 0 ? true : false;
        //    if (!hr)
        //    {
        //        Msg("Couldn't render the video capture stream.\r\n"
        //            +"The capture device may already be in use by another application.\r\n\r\n"
        //            +"The sample will now close.");
        //        Marshal.ReleaseComObject(pGrabber[0]);
        //        Marshal.ReleaseComObject(pF[0]);
        //        Marshal.ReleaseComObject(pSrcFilter[0]);
        //        Marshal.ReleaseComObject(pNull[0]);
        //        return hr;
        //    }

        //    AMMediaType mt1 = new AMMediaType();
        //    hr = pGrabber[0].GetConnectedMediaType(mt1) == 0 ? true : false;
        //    if (mt1 != null)
        //    {
        //        // copy out the videoinfoheader
        //        //VideoInfoHeader v = new VideoInfoHeader();
        //        //Marshal.PtrToStructure(mt1.formatPtr, v);
        //        //CB.Width = v.BmiHeader.Width;
        //        //CB.Height = v.BmiHeader.Height;
        //        CB.Width = 320;
        //        CB.Height = 240;
        //        pGrabber[0].SetBufferSamples(true);
        //        pGrabber[0].SetOneShot(false);
        //        //pGrabber[0].SetCallback(CB, 1);

        //        //CB.image = Cv.CreateImage(Cv.Size(CB.Width, CB.Height), BitDepth.U8, 1);
        //        //CB.image.Origin = ImageOrigin.BottomLeft;

        //        //VIDEOINFOHEADER* vih = (VIDEOINFOHEADER*)mt1.pbFormat;
        //        //CB->Width = vih->bmiHeader.biWidth;
        //        //CB->Height = vih->bmiHeader.biHeight;

        //        //CB->image = cvCreateImage(cvSize(CB->Width, CB->Height), IPL_DEPTH_8U, 1);
        //        //CB->image->origin = 1;

        //        //pGrabber[0]->SetBufferSamples(TRUE);
        //        //pGrabber[0]->SetOneShot(FALSE);
        //        //pGrabber[0]->SetCallback(CB, 1);
        //    }

        //    //AM_MEDIA_TYPE mt1;
        //    //hr = pGrabber[0]->GetConnectedMediaType( &mt1 );
        //    //if( hr == S_OK )
        //    //{
        //    //    VIDEOINFOHEADER * vih = (VIDEOINFOHEADER*) mt1.pbFormat;
        //    //    CB->Width  = vih->bmiHeader.biWidth;
        //    //    CB->Height = vih->bmiHeader.biHeight;

        //    //    CB->image = cvCreateImage(cvSize( CB->Width, CB->Height ), IPL_DEPTH_8U, 1);
        //    //    CB->image->origin = 1;

        //    //    pGrabber[0]->SetBufferSamples( TRUE );
        //    //    pGrabber[0]->SetOneShot( FALSE );
        //    //    pGrabber[0]->SetCallback(CB, 1);	
        //    //}

        //    Marshal.ReleaseComObject(pGrabber[0]);
        //    Marshal.ReleaseComObject(pF[0]);
        //    Marshal.ReleaseComObject(pNull[0]);
        //    Marshal.ReleaseComObject(pSrcFilter[0]);

        //    hr = g_pMC[0].Stop() == 0 ? true : false;
        //    if (!hr)
        //    {
        //        Msg("Couldn't stop the graph!");
        //        return hr;
        //    }

        //    //SAFE_RELEASE( pGrabber[0] );
        //    //SAFE_RELEASE( pF[0] );
        //    //SAFE_RELEASE( pNull[0] );
        //    //SAFE_RELEASE( pSrcFilter[0] );

        //    //hr = g_pMC[0]->Stop();
        //    //if ( FAILED( hr ) )
        //    //{
        //    //    Msg(TEXT("Couldn't stop the graph!  hr=0x%x"), hr);
        //    //    return hr;
        //    //}
        //    return true;
        //}

        //bool GetInterfaces()
        //{
        //    //Type comtype = null;
        //    //object comobj = null;
        //    //comtype = Type.GetTypeFromCLSID(CLSID_FilterGraph);
        //    //if (comtype == null)
        //    //{
        //    //    Msg("DirectX (8.1 or higher) not installed?");
        //    //}
        //    //comobj = Activator.CreateInstance(comtype);
        //    //IGraphBuilder graphBuilder = (IGraphBuilder)comobj;
        //    //comobj = null;
        //    //IMediaControl mediaCtrl = (IMediaControl)graphBuilder;

        //    bool hr = true;
        //    //Type comtype = null;
        //    //object comobj = null;
        //    //comtype = Type.GetTypeFromCLSID(CLSID_FilterGraph);
        //    //if (comtype == null)
        //    //{
        //    //    Msg("DirectX (8.1 or higher) not installed?");
        //    //}

        //    //comobj = Activator.CreateInstance(comtype);
        //    //g_pGraph[0] = (IGraphBuilder)comobj;

        //    g_pGraph[0] = (IGraphBuilder)new FilterGraph();
        //    if (g_pGraph[0] == null)
        //    {
        //        hr = false;
        //    }
        //    else {
        //        g_pCapture[0] = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
        //        g_pMC[0] = (IMediaControl)g_pGraph[0];
        //        g_pVW[0] = (IVideoWindow)g_pGraph[0];
        //        g_pME[0] = (IMediaEventEx)g_pGraph[0];
        //        if (g_pMC[0] == null
        //            || g_pVW[0] == null
        //            || g_pME[0] == null
        //            || g_pCapture[0] == null)
        //        {
        //            hr = false;
        //        }
        //    }

        //    //comtype = null;
        //    //comobj = null;
        //    //comtype = Type.GetTypeFromCLSID(CLSID_CaptureGraphBuilder2);
        //    //if (comtype == null)
        //    //{
        //    //    Msg("DirectX (8.1 or higher) not installed?");
        //    //}

        //    //comobj = Activator.CreateInstance(comtype);
        //    //g_pCapture[0] = (ICaptureGraphBuilder2)comobj;
        //    //if (g_pCapture[0] == null)
        //    //{
        //    //    hr = false;
        //    //}

        //    //g_pGraph[0] = (IGraphBuilder)new FilterGraph();
        //    //g_pMC[0] = (IMediaControl)g_pGraph[0];


        //    //g_pGraph[0];
        //    //bool hr = true;
        //    //hr = CoCreateInstance (CLSID_FilterGraph, NULL, CLSCTX_INPROC,
        //    //    IID_IGraphBuilder, (void **) &g_pGraph[0]);
        //    //if (FAILED(hr))
        //    //    return hr;

        //    //hr = CoCreateInstance(CLSID_CaptureGraphBuilder2, NULL, CLSCTX_INPROC,
        //    //    IID_ICaptureGraphBuilder2, (void**)&g_pCapture[0]);
        //    //if (FAILED(hr))
        //    //    return hr;

        //    //hr = g_pGraph[0].QueryInterface(IID_IMediaControl, (LPVOID*)&g_pMC[0]);
        //    //if (!hr)
        //    //{
        //    //    return hr;
        //    //}

        //    //hr = g_pGraph[0].QueryInterface(IID_IVideoWindow, (LPVOID *) &g_pVW[0]);
        //    //if (FAILED(hr))
        //    //    return hr;

        //    //hr = g_pGraph[0].QueryInterface(IID_IMediaEvent, (LPVOID *) &g_pME[0]);
        //    //if (FAILED(hr))
        //    //{
        //    //    return hr;
        //    //}
        //    return hr;
        //}


        ////void CloseInterfaces(void)
        ////{
        ////    if (g_pMC[0])
        ////    {
        ////        g_pMC[0]->StopWhenReady();
        ////    }

        ////    if(g_pVW[0])
        ////    {
        ////        g_pVW[0]->put_Visible(OAFALSE);
        ////        g_pVW[0]->put_Owner(NULL);
        ////    }

        ////    if ( !restartCamera )
        ////    {
        ////        SAFE_RELEASE(g_pMC[0]);
        ////        SAFE_RELEASE(g_pME[0]);
        ////        SAFE_RELEASE(g_pVW[0]);
        ////        SAFE_RELEASE(g_pGraph[0]);
        ////        SAFE_RELEASE(g_pCapture[0]);
        ////    }
        ////}

        //IBaseFilter[] FindCaptureDevice()
        //{
        //    IBaseFilter[] pSrc = new IBaseFilter[1];
        //    IMoniker[] pMoniker = new IMoniker[1];
        //    ICreateDevEnum pDevEnum = null;
        //    IEnumMoniker pClassEnum = null;
        //    object source = null;
        //    bool hr = true;
        //    Type comtype = null;
        //    object comobj = null;
        //    comtype = Type.GetTypeFromCLSID(CLSID_SystemDeviceEnum);
        //    if (comtype == null)
        //    {
        //        Msg("DirectX (8.1 or higher) not installed?");
        //    }

        //    comobj = Activator.CreateInstance(comtype);
        //    pDevEnum = (ICreateDevEnum)comobj;
        //    if (pDevEnum == null)
        //    {
        //        hr = false;
        //    }

        //    comtype = null;
        //    comobj = null;
        //    comtype = Type.GetTypeFromCLSID(CLSID_SystemDeviceEnum);
        //    if (comtype == null)
        //    {
        //        Msg("DirectX (8.1 or higher) not installed?");
        //    }

        //    comobj = Activator.CreateInstance(comtype);
        //    pDevEnum = (ICreateDevEnum)comobj;
        //    if (pDevEnum == null)
        //    {
        //        hr = false;
        //    }

        //    if (hr)
        //    {
        //        int rv = pDevEnum.CreateClassEnumerator(CLSID_VideoInputDeviceCategory,
        //                        out pClassEnum, 0);
        //        hr = rv == 0 ? true : false;
        //        if (!hr)
        //        {
        //            Msg("Couldn't create class enumerator!");
        //        }
        //    }

        //    if (hr)
        //    {
        //        if (pClassEnum == null)
        //        {
        //            Msg("No video capture device was detected.\r\n\r\n"
        //                +"This sample requires a video capture device, such as a USB WebCam,\r\n"
        //                +"to be installed and working properly.  The sample will now close."
        //                +"No Video Capture Hardware");
        //            hr = false;
        //        }
        //    }

        //    comtype = null;
        //    comobj = null;
        //    comtype = Type.GetTypeFromCLSID(CLSID_SampleGrabber);
        //    if (comtype == null)
        //    {
        //        Msg("DirectX (8.1 or higher) not installed?");
        //    }

        //    comobj = Activator.CreateInstance(comtype);
        //    pSrc[0] = (IBaseFilter)comobj;
        //    if (pSrc[0] == null)
        //    {
        //        hr = false;
        //    }

        //    IntPtr pMonikerPtr = IntPtr.Zero;
        //    for( int i = 0; i < 1 && hr == true; i++ )
        //    {
        //        if (hr)
        //        {
        //            hr = pClassEnum.Next(1, pMoniker, pMonikerPtr) == 0 ? true : false;
        //            if (hr == false)
        //            {
        //                if (pSrc[0] == null)
        //                {
        //                    Msg("Unable to access video capture device!");
        //                }
        //                hr = false;
        //            }
        //        }

        //        if (hr)
        //        {
        //            IBindCtx pbc = null;
        //            string pDisplayName = "";
        //            pMoniker[0].GetDisplayName(pbc, null, out pDisplayName);
        //            if (pbc != null) {
        //                Marshal.ReleaseComObject(pbc);
        //            }

        //            if (pDisplayName.Contains("vid_04fc") &&
        //                (pDisplayName.Contains("pid_fa02") || pDisplayName.Contains("pid_fa09")))
        //            {
        //                //Bind Moniker to a filter object
        //                pMoniker[0].BindToObject(null, null, ref IID_IBaseFilter, out source);
        //                if (source != null)
        //                {
        //                    pSrc[0] = (IBaseFilter)source;
        //                }
        //                else {
        //                    Msg("Couldn't bind moniker to filter object!");
        //                }
        //            }
        //            else {
        //                Msg("Camera id error!");
        //            }
        //        }
        //    }

        //    Marshal.ReleaseComObject(pMoniker[0]);
        //    Marshal.ReleaseComObject(pDevEnum);
        //    Marshal.ReleaseComObject(pClassEnum);
        //    return pSrc;
        //}

        //string szWindowClass = "CalibrationForm";
        //string szTitle = "form";
        //public bool RunGun()
        //{
        //    //GdiplusStartup(&gdiplusToken, &gdiplusStartupInput, NULL);
        //    //if(FAILED(CoInitialize(NULL)))
        //    //{
        //    //    MessageBox(NULL, L"Can't init COM!", L"error", MB_OK);
        //    //    exit(1);
        //    //}

        //    bool hr = CaptureVideo();
        //    if (!hr)
        //    {
        //        //CloseGun();
        //        Msg("Camera error!");
        //        return false;
        //    }
        //    return true;
        //}

        ////    int StopVideo()
        ////    {
        ////        HRESULT hr = g_pMC[0]->Pause();
        ////        if ( FAILED( hr ) )
        ////        {
        ////            //Msg(TEXT("Couldn't run the graph!  hr=0x%x"), hr);
        ////            return hr;
        ////        }
        ////        return 1;
        ////    }

        //public bool PlayVideo()
        //{
        //    bool hr = g_pMC[0].Run() == 0 ? true : false;
        //    if (!hr)
        //    {
        //        Msg("Couldn't run the graph!");
        //        return hr;
        //    }
        //    return hr;
        //}

        ////    CSampleGrabberCB* SampleGrabberFun(int id)
        ////    {
        ////        if(ID_CAMERA1 == id)
        ////        {
        ////            if (CB)
        ////            {
        ////                //MessageBox(NULL, L"CB1", L"OK", MB_OK);
        ////                return CB;
        ////            }
        ////            else
        ////            {
        ////                MessageBox(NULL , L"camera1 error!", L"error", MB_OK);
        ////                return NULL;
        ////            }
        ////        }
        ////        return NULL;
        ////    }

        ////    void CloseGun()
        ////    {
        ////        CloseInterfaces();

        ////        CoUninitialize();

        ////        GdiplusShutdown(gdiplusToken);

        ////        if(CB != NULL)
        ////        {
        ////            delete CB;
        ////            CB = NULL;
        ////            //MessageBox(NULL, L"CB release!", L"CB delete", MB_OK);
        ////        }
        ////    }

        //public void GetCaptureSupportSize(ICaptureGraphBuilder2 capGraph, IBaseFilter captureFilter)
        //{
        //    object streamConfig;
        //    //获取配置接口
        //    int hr = capGraph.FindInterface(PinCategory.Capture,
        //                                    MediaType.Video,
        //                                    captureFilter,
        //                                    typeof(IAMStreamConfig).GUID,
        //                                    out streamConfig);
        //    DsError.ThrowExceptionForHR(hr);
        //    var videoStreamConfig = streamConfig as IAMStreamConfig;
        //    if (videoStreamConfig == null)
        //    {
        //        throw new Exception("Failed to get IAMStreamConfig");
        //    }
            
        //    //好吧，我承认这是C++的变量命名方法，可我就是不想改~
        //    int iCount;
        //    int iSize;
        //    VideoStreamConfigCaps vscc = new VideoStreamConfigCaps();
        //    hr = videoStreamConfig.GetNumberOfCapabilities(out iCount, out iSize);
        //    DsError.ThrowExceptionForHR(hr);
        //    //判断为正确获取的信息
        //    if (Marshal.SizeOf(vscc) == iSize)
        //    {
        //        for (int i = 0; i < iCount; ++i)
        //        {
        //            //分配非托管内存
        //            IntPtr pVscc = Marshal.AllocCoTaskMem(Marshal.SizeOf(vscc));
        //            AMMediaType amMediaType;
        //            hr = videoStreamConfig.GetStreamCaps(i, out amMediaType, pVscc);
        //            DsError.ThrowExceptionForHR(hr);
        //            //如果为视频信息
        //            if (amMediaType.majorType == MediaType.Video &&
        //                amMediaType.formatType == FormatType.VideoInfo)
        //            {
        //                Marshal.StructureToPtr(vscc, pVscc, false);

        //                var videoInfoHeader = new VideoInfoHeader();
        //                Marshal.PtrToStructure(amMediaType.formatPtr, videoInfoHeader);

        //                //获取摄像头所支持的分辨率
        //                int width = videoInfoHeader.BmiHeader.Width;
        //                int height = videoInfoHeader.BmiHeader.Height;
        //            }
        //            //释放非托管内存
        //            Marshal.FreeCoTaskMem(pVscc);
        //            DsUtils.FreeAMMediaType(amMediaType);
        //        }
        //    }
        //    return;
        //}
    }
}