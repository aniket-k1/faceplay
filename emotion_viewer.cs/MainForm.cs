﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Emotion_Detection
{
    public partial class MainForm : Form
    {
        private PXCMSession session;
        private volatile bool closing = false;
        public  volatile bool stop = false;
        private Bitmap bitmap = null;
        private string filename = null;

        private string[] EmotionLabels = {"ANGER","CONTEMPT","DISGUST","FEAR","JOY","SADNESS","SURPRISE"};
        private string[] SentimentLabels = {"NEGATIVE","POSITIVE","NEUTRAL"};
        
        public int NUM_EMOTIONS  = 10;
        public int NUM_PRIMARY_EMOTIONS = 7;

        public MainForm(PXCMSession session)
        {
            InitializeComponent();

            this.session = session;
            PopulateDeviceMenu();
            PopulateModuleMenu();

            FormClosing += new FormClosingEventHandler(MainForm_FormClosing);
            Panel2.Paint += new PaintEventHandler(Panel_Paint);
        }

        private void PopulateDeviceMenu()
        {
            PXCMSession.ImplDesc desc = new PXCMSession.ImplDesc();
            desc.group = PXCMSession.ImplGroup.IMPL_GROUP_SENSOR;
            desc.subgroup = PXCMSession.ImplSubgroup.IMPL_SUBGROUP_VIDEO_CAPTURE;
            ToolStripMenuItem sm = new ToolStripMenuItem("Device");
            for (uint i = 0; ; i++)
            {
                PXCMSession.ImplDesc desc1;
                if (session.QueryImpl(ref desc, i, out desc1) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;
                PXCMCapture capture;
                if (session.CreateImpl<PXCMCapture>(ref desc1, PXCMCapture.CUID, out capture) < pxcmStatus.PXCM_STATUS_NO_ERROR) continue;
                for (uint j = 0; ; j++)
                {
                    PXCMCapture.DeviceInfo dinfo;
                    if (capture.QueryDevice(j, out dinfo) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;
                    ToolStripMenuItem sm1 = new ToolStripMenuItem(dinfo.name.get(), null, new EventHandler(Device_Item_Click));
                    sm.DropDownItems.Add(sm1);
                }
                capture.Dispose();
            }
            if (sm.DropDownItems.Count > 0)
                (sm.DropDownItems[0] as ToolStripMenuItem).Checked = true;
            MainMenu.Items.RemoveAt(0);
            MainMenu.Items.Insert(0, sm);
        }

        private void PopulateModuleMenu()
        {
            PXCMSession.ImplDesc desc = new PXCMSession.ImplDesc();
            desc.cuids[0] = PXCMEmotion.CUID;
            ToolStripMenuItem mm = new ToolStripMenuItem("Module");
            for (uint i = 0; ; i++)
            {
                PXCMSession.ImplDesc desc1;
                if (session.QueryImpl(ref desc, i, out desc1) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;
                ToolStripMenuItem mm1 = new ToolStripMenuItem(desc1.friendlyName.get(), null, new EventHandler(Module_Item_Click));
                mm.DropDownItems.Add(mm1);
            }
            if (mm.DropDownItems.Count > 0)
                (mm.DropDownItems[0] as ToolStripMenuItem).Checked = true;
            MainMenu.Items.RemoveAt(1);
            MainMenu.Items.Insert(1, mm);
        }

        private void RadioCheck(object sender, string name)
        {
            foreach (ToolStripMenuItem m in MainMenu.Items)
            {
                if (!m.Text.Equals(name)) continue;
                foreach (ToolStripMenuItem e1 in m.DropDownItems)
                {
                    e1.Checked = (sender == e1);
                }
            }
        }

        private void Device_Item_Click(object sender, EventArgs e)
        {
            RadioCheck(sender, "Device");
        }

        private void Module_Item_Click(object sender, EventArgs e)
        {
            RadioCheck(sender, "Module");
            //PopulateProfileMenu();
        }

        private void Profile_Item_Click(object sender, EventArgs e)
        {
            RadioCheck(sender, "Landmark");
        }

        private void Start_Click(object sender, EventArgs e)
        {
            MainMenu.Enabled = false;
            Start.Enabled = false;
            Stop.Enabled = true;
            Camera.shouldStopConfig = false;
            Camera.stopped = false;
            stop = false;
            System.Threading.Thread thread = new System.Threading.Thread(DoTracking);
            thread.Start();
            System.Threading.Thread.Sleep(5);
        }

        delegate void DoTrackingCompleted();
        private void DoTracking()
        {
            EmotionDetection ft = new EmotionDetection(this);
            //if (simpleToolStripMenuItem.Checked)
            {
                ft.SimplePipeline();
            }
            //else
            {
            //    ft.AdvancedPipeline();
            }
            this.Invoke(new DoTrackingCompleted(
                delegate
                {
                    Start.Enabled = true;
                    Stop.Enabled = false;
                    MainMenu.Enabled = true;
                    if (closing) Close();
                }
            ));
        }

        public string GetCheckedDevice()
        {
            foreach (ToolStripMenuItem m in MainMenu.Items)
            {
                if (!m.Text.Equals("Device")) continue;
                foreach (ToolStripMenuItem e in m.DropDownItems)
                {
                    if (e.Checked) return e.Text;
                }
            }
            return null;
        }

        public string GetCheckedModule()
        {
            foreach (ToolStripMenuItem m in MainMenu.Items)
            {
                if (!m.Text.Equals("Module")) continue;
                foreach (ToolStripMenuItem e in m.DropDownItems)
                {
                    if (e.Checked) return e.Text;
                }
            }
            return null;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            stop = true;
            e.Cancel = Stop.Enabled;
            closing = true;
        }

        private delegate void UpdateStatusDelegate(string status);
        public void UpdateStatus(string status)
        {
            Status2.Invoke(new UpdateStatusDelegate(delegate(string s) { StatusLabel.Text = s; }), new object[] { status });
        }

        private void Stop_Click(object sender, EventArgs e)
        {
            stop = true;
            Camera.shouldStopConfig = true;
            Camera.stopped = true;
            checkBox1.Checked = false;
            this.TopMost = checkBox1.Checked;
        }

        public void DisplayBitmap(Bitmap picture)
        {
            lock (this)
            {
                bitmap = new Bitmap(picture);
            }
        }

        private void Panel_Paint(object sender, PaintEventArgs e)
        {
            lock (this)
            {
                if (bitmap == null) return;
                if (Scale2.Checked)
                {
                    /* Keep the aspect ratio */
                    Rectangle rc = (sender as PictureBox).ClientRectangle;
                    float xscale = (float)rc.Width / (float)bitmap.Width;
                    float yscale = (float)rc.Height / (float)bitmap.Height;
                    float xyscale = (xscale < yscale) ? xscale : yscale;
                    int width = (int)(bitmap.Width * xyscale);
                    int height = (int)(bitmap.Height * xyscale);
                    rc.X = (rc.Width - width) / 2;
                    rc.Y = (rc.Height - height) / 2;
                    rc.Width = width;
                    rc.Height = height;
                    e.Graphics.DrawImage(bitmap, rc);
                }
                else
                {
                    e.Graphics.DrawImageUnscaled(bitmap, 0, 0);
                }
            }
        }

        private delegate void UpdatePanelDelegate();
        public void UpdatePanel()
        {
            Panel2.Invoke(new UpdatePanelDelegate(delegate() 
                {
                    this.TopMost = checkBox1.Checked;
                    if (Mirror.Checked)
                    {
                        lock (this)
                        {
                            if (bitmap != null)
                                bitmap.RotateFlip(RotateFlipType.RotateNoneFlipX);
                        }
                    }
                    if(nearMode.Checked)
                    {
                        Camera.nearMode = true;
                        Camera.updateMode = true;
                    }
                    else
                    {
                        Camera.nearMode = false;
                        Camera.updateMode = true;
                    }
                    Panel2.Invalidate();
                }));
        }

        private void simpleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RadioCheck(sender, "Pipeline");
        }

        private void advancedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RadioCheck(sender, "Pipeline");
        }

        private void Live_Click(object sender, EventArgs e)
        {
        //    Playback.Checked = Record.Checked = false;
        //   Live.Checked = true;
        }

        private void Playback_Click(object sender, EventArgs e)
        {
        //    Live.Checked = Record.Checked = false;
        //    Playback.Checked = true;

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "All files (*.*)|*.*";
            ofd.CheckFileExists = true;
            ofd.CheckPathExists = true;
            filename = (ofd.ShowDialog() == DialogResult.OK) ? ofd.FileName : null;
        }

        public bool GetPlaybackState()
        {
            return false;//return Playback.Checked;
        }

        private void Record_Click(object sender, EventArgs e)
        {
            //Live.Checked = Playback.Checked = false;
            //Record.Checked = true;

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "All files (*.*)|*.*";
            sfd.CheckPathExists = true;
            sfd.OverwritePrompt = true;
            filename = (sfd.ShowDialog() == DialogResult.OK) ? sfd.FileName : null;
        }

        public bool GetRecordState()
        {
            return false;// return Record.Checked;
        }

        public void DrawLocation(PXCMEmotion.EmotionData[] data)
        {
            lock (this)
            {
                if (bitmap == null) return;
                Graphics g = Graphics.FromImage(bitmap);
                Pen red = new Pen(Color.Red, 3.0f);
                Brush brush = new SolidBrush(Color.Red);
                Font font = new Font(Font.FontFamily, 11, FontStyle.Bold);
                Brush brushTxt = new SolidBrush(Color.Cyan);
                //if (Location.Checked)
                {
                    Point[] points4 = new Point[]{
                        new Point((int)data[0].rectangle.x,(int)data[0].rectangle.y),
                        new Point((int)data[0].rectangle.x+(int)data[0].rectangle.w,(int)data[0].rectangle.y),
                        new Point((int)data[0].rectangle.x+(int)data[0].rectangle.w,(int)data[0].rectangle.y+(int)data[0].rectangle.h),
                        new Point((int)data[0].rectangle.x,(int)data[0].rectangle.y+(int)data[0].rectangle.h),
                        new Point((int)data[0].rectangle.x,(int)data[0].rectangle.y)};
                    g.DrawLines(red, points4);
                    //g.DrawString(data[0].fid.ToString(), font, brushTxt, (float)(data[0].rectangle.x + data[0].rectangle.w), (float)(data[0].rectangle.y));
                }

                bool emotionPresent = false;
                int epidx = -1; int maxscoreE = -3; float maxscoreI = 0;
                for (int i = 0; i < NUM_PRIMARY_EMOTIONS; i++)
                {
                    if (data[i].evidence  < maxscoreE) continue;
                    if (data[i].intensity < maxscoreI) continue;
                    maxscoreE = data[i].evidence;
                    maxscoreI = data[i].intensity;
                    epidx = i;
                }
                if ((epidx != -1) && (maxscoreI > 0.4))
                {
                    g.DrawString(EmotionLabels[epidx], font, brushTxt, (float)(data[0].rectangle.x + data[0].rectangle.w), (float)data[0].rectangle.y);
                    emotionPresent = true;
                }

                int spidx = -1;
                if (emotionPresent)
                {
                    maxscoreE = -3; maxscoreI = 0;
                    for (int i = 0; i < (NUM_EMOTIONS - NUM_PRIMARY_EMOTIONS); i++)
                    {
                        if (data[NUM_PRIMARY_EMOTIONS + i].evidence  < maxscoreE) continue;
                        if (data[NUM_PRIMARY_EMOTIONS + i].intensity < maxscoreI) continue;
                        maxscoreE = data[NUM_PRIMARY_EMOTIONS + i].evidence;
                        maxscoreI = data[NUM_PRIMARY_EMOTIONS + i].intensity;
                        spidx = i;
                    }
                    if ((spidx != -1))
                    {
                        g.DrawString(SentimentLabels[spidx], font, brushTxt, (float)(data[0].rectangle.x + data[0].rectangle.w), (float)data[0].rectangle.y + font.GetHeight());
                    }
                }

                brush.Dispose();
                brushTxt.Dispose();
                red.Dispose();
                font.Dispose();
                g.Dispose();
            }

        }

        public string GetFileName()
        {
            return filename;
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            this.TopMost = checkBox1.Checked;
        }

    }
}
