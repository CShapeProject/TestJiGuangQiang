using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace OpenCameraCSByOpenCV
{
    class pcvr
    {
        public static CSampleGrabberCB m_CamCB = null;
        public void createCamera()
        {
            m_CamCB = new CSampleGrabberCB(0);
            m_CamCB.Start();
        }
    }
}
