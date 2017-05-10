using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace OpenCameraCSByOpenCV
{
    class pcvr
    {
        CSampleGrabberCB m_CamCB = null;
        public void createCamera()
        {
            m_CamCB = new CSampleGrabberCB(0);
            m_CamCB.Start();
            //PlayCapRectify playCap = new PlayCapRectify();
            //playCap.CaptureVideo();


            //if( playCap.RunGun() )
            //{
            //    CSampleGrabberCB CSgcb1 = playCap.CB;
            //    CSgcb1.m_mode = MODE.MODE_MOTION;
            //    playCap.PlayVideo();
            //    //mCameraEnable = true;
            //    //setCamPointCallBackFun();
            //}
            //else
            //{
            //    MessageBox.Show("Can not find Camera!");
            //}
        }
    }
}
