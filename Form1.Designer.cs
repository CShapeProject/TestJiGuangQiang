namespace OpenCameraCSByOpenCV
{
    partial class Form1
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        //protected override void Dispose(bool disposing)
        //{
        //    if (disposing && (components != null))
        //    {
        //        components.Dispose();
        //    }

        //    base.Dispose(disposing);
        //}

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.JiaoZhunPic = new System.Windows.Forms.PictureBox();
            this.ZhunXingZB = new System.Windows.Forms.Label();
            this.ZhunXingP1 = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.JiaoZhunPic)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ZhunXingP1)).BeginInit();
            this.SuspendLayout();
            // 
            // JiaoZhunPic
            // 
            this.JiaoZhunPic.Location = new System.Drawing.Point(0, 0);
            this.JiaoZhunPic.Name = "JiaoZhunPic";
            this.JiaoZhunPic.Size = new System.Drawing.Size(1360, 768);
            this.JiaoZhunPic.TabIndex = 0;
            this.JiaoZhunPic.TabStop = false;
            // 
            // ZhunXingZB
            // 
            this.ZhunXingZB.Font = new System.Drawing.Font("宋体", 35F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.ZhunXingZB.Location = new System.Drawing.Point(67, 59);
            this.ZhunXingZB.Name = "ZhunXingZB";
            this.ZhunXingZB.Size = new System.Drawing.Size(500, 50);
            this.ZhunXingZB.TabIndex = 1;
            this.ZhunXingZB.Text = "px: 1360 py: 768";
            // 
            // ZhunXingP1
            // 
            this.ZhunXingP1.BackColor = System.Drawing.SystemColors.Control;
            this.ZhunXingP1.Image = global::OpenCameraCSByOpenCV.Properties.Resources.ZhunXingP1;
            this.ZhunXingP1.Location = new System.Drawing.Point(165, 166);
            this.ZhunXingP1.Name = "ZhunXingP1";
            this.ZhunXingP1.Size = new System.Drawing.Size(165, 166);
            this.ZhunXingP1.TabIndex = 2;
            this.ZhunXingP1.TabStop = false;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1357, 766);
            this.Controls.Add(this.ZhunXingP1);
            this.Controls.Add(this.ZhunXingZB);
            this.Controls.Add(this.JiaoZhunPic);
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)(this.JiaoZhunPic)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ZhunXingP1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox JiaoZhunPic;
        private System.Windows.Forms.Label ZhunXingZB;
        private System.Windows.Forms.PictureBox ZhunXingP1;
    }
}

