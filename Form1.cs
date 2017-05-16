using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;

namespace OpenCameraCSByOpenCV
{
    public partial class Form1 : Form
    {
        Thread m_FormThread;
        bool IsExitApp;
        Point ZhunXingPoint;

        public static Form1 Instance;
        //注册热键的api.
        [DllImport("user32")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint control, Keys vk);
        //解除注册热键的api.
        [DllImport("user32")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        public Form1()
        {
            InitializeComponent();
            ZhunXingZB.Text = "px: 0 py: 0";
            Instance = this;

            pcvr pcvrPtr = new pcvr();
            pcvrPtr.createCamera();

            RegisterHotKey(this.Handle, (int)Keys.F4, 0, Keys.F4);
            RegisterHotKey(this.Handle, (int)Keys.K, 0, Keys.K);

            Control.CheckForIllegalCrossThreadCalls = false;
            m_FormThread = new Thread(ShowFormInfo);
            m_FormThread.IsBackground = true;
            m_FormThread.Start();  
        }

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (m_FormThread.IsAlive)
            {
                m_FormThread.Abort();
            }
            IsExitApp = true;
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case 0x0312:    //这个是window消息定义的,注册的热键消息.
                    if (m.WParam.ToInt32() == (int)Keys.F4)
                    {
                        InitJiaoZhunZuoBiao();
                    }

                    if (m.WParam.ToInt32() == (int)Keys.K)
                    {
                        if (JiaoZhunPic.Visible)
                        {
                            pcvr.m_CamCB.ActiveJiaoZhunZuoBiao();
                        }
                    }
                    break;
            }

            base.WndProc(ref m);
        }

        void InitJiaoZhunZuoBiao()
        {
            if (JiaoZhunPic.Image != null)
            {
                return;
            }
            //MessageBox.Show("开始校准坐标!");
            ZhunXingZB.Visible = false;
            ZhunXingP1.Visible = false;
            ChangeJiaoZhunPic(1);
            pcvr.m_CamCB.InitJiaoZhunZuoBiao();
        }

        public void ChangeJiaoZhunPic(byte indexVal)
        {
            //Console.WriteLine("indexVal " + indexVal);
            switch (indexVal)
            {
                case 1:
                    JiaoZhunPic.Image = global::OpenCameraCSByOpenCV.Properties.Resources.GunJY_0;
                    break;
                case 2:
                    JiaoZhunPic.Image = global::OpenCameraCSByOpenCV.Properties.Resources.GunJY_1;
                    break;
                case 3:
                    JiaoZhunPic.Image = global::OpenCameraCSByOpenCV.Properties.Resources.GunJY_2;
                    break;
                case 4:
                    JiaoZhunPic.Image = global::OpenCameraCSByOpenCV.Properties.Resources.GunJY_3;
                    break;
                default:
                    JiaoZhunPic.Image = null;
                    ZhunXingZB.Visible = true;
                    ZhunXingP1.Visible = true;
                    break;
            }
        }

        public void UpdateZhunXingZuoBiao(Point pointVal)
        {
            int px = pointVal.X;
            px = px > 1360 ? 1360 : px;
            px = px < 0 ? 0 : px;

            int py = 768 - pointVal.Y;
            py = py > 768 ? 768 : py;
            py = py < 0 ? 0 : py;
            ZhunXingPoint = new Point(px, py);
        }

        void ShowFormInfo()
        {
            do
            {
                if (IsExitApp)
                {
                    break;
                }
                ZhunXingZB.Text = "px: " + ZhunXingPoint.X.ToString("d4") + " py: " + ZhunXingPoint.Y.ToString("d3");
                ZhunXingP1.Location = new Point(ZhunXingPoint.X, ZhunXingPoint.Y);
                Thread.Sleep(10);
            } while (!IsExitApp);
            Console.WriteLine("Exit thread!");
        }
    }
}
