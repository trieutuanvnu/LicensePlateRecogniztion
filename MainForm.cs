using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
//using System.Threading.Tasks;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.ML;
using Emgu.CV.ML.Structure;
using Emgu.CV.UI;
using Emgu.Util;
using System.Diagnostics;
using Emgu.CV.CvEnum;
using System.IO;
using System.IO.Ports;
using tesseract;
using System.Collections;
using System.Threading;
using System.Media;
using System.Runtime.InteropServices;

namespace Auto_parking
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }


        #region định nghĩa
        List<Image<Bgr, byte>> PlateImagesList = new List<Image<Bgr, byte>>();
        Image Plate_Draw;
        List<string> PlateTextList = new List<string>();
        List<Rectangle> listRect = new List<Rectangle>();
        PictureBox[] box = new PictureBox[12];

        public TesseractProcessor full_tesseract = null;
        public TesseractProcessor ch_tesseract = null;
        public TesseractProcessor num_tesseract = null;
        private string m_path = Application.StartupPath + @"\data\";
        private List<string> lstimages = new List<string>();
        private const string m_lang = "eng";

        //int current = 0;
        Capture capture = null;
        #endregion


        #region di chuyển
        bool mouseDown = false;
        Point lastLocation;
        private void button_Leave(object sender, EventArgs e)
        {
            Button bsen = (Button)sender;
            bsen.ForeColor = Color.Black;
        }

        private void button_Enter(object sender, EventArgs e)
        {
            Button bsen = (Button)sender;
            bsen.ForeColor = Color.White;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
            this.Close();
        }

        private void panel1_MouseDown(object sender, MouseEventArgs e)
        {
            if (mouseDown == false && e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                mouseDown = true;
                lastLocation = e.Location;
            }
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                //contextMenuStrip1.Show(this.DesktopLocation.X + e.X, this.DesktopLocation.Y + e.Y);	
            }
        }

        private void panel1_MouseMove(object sender, MouseEventArgs e)
        {
            if (mouseDown)
            {
                this.SetDesktopLocation(this.DesktopLocation.X - lastLocation.X + e.X, this.DesktopLocation.Y - lastLocation.Y + e.Y);
                this.Update();
            }
        }

        private void panel1_MouseUp(object sender, MouseEventArgs e)
        {
            mouseDown = false;
        }
        private void panel1_MouseHover(object sender, EventArgs e)
        {
            panel1.BackColor = Color.FromArgb(15, 15, 15);
        }
        private void panel1_MouseLeave(object sender, EventArgs e)
        {
            panel1.BackColor = Color.FromArgb(64, 64, 64);
        }
        #endregion

        ImageForm IF;
        private void MainForm_Load(object sender, EventArgs e)
        {
            capture = new Emgu.CV.Capture();
            timer1.Enabled = true;

            IF = new ImageForm();
           
            full_tesseract = new TesseractProcessor();
            bool succeed = full_tesseract.Init(m_path, m_lang, 3);
            if (!succeed)
            {
                MessageBox.Show("Tesseract initialization failed. The application will exit.");
                Application.Exit();
            }
            full_tesseract.SetVariable("tessedit_char_whitelist", "ABCDEFHKLMNPRSTVXY1234567890").ToString();

            ch_tesseract = new TesseractProcessor();
            succeed = ch_tesseract.Init(m_path, m_lang, 3);
            if (!succeed)
            {
                MessageBox.Show("Tesseract initialization failed. The application will exit.");
                Application.Exit();
            }
            ch_tesseract.SetVariable("tessedit_char_whitelist", "ABCDEFHKLMNPRSTUVXY").ToString();

            num_tesseract = new TesseractProcessor();
            succeed = num_tesseract.Init(m_path, m_lang, 3);
            if (!succeed)
            {
                MessageBox.Show("Tesseract initialization failed. The application will exit.");
                Application.Exit();
            }
            num_tesseract.SetVariable("tessedit_char_whitelist", "1234567890").ToString();


            m_path = System.Environment.CurrentDirectory + "\\";
            string[] ports = SerialPort.GetPortNames();
            for (int i = 0; i < box.Length; i++)
            {
                box[i] = new PictureBox();
            }
        }
        private void button5_Click(object sender, EventArgs e)
        {
            if (IF.Visible == false)
            {
                IF.Show();
            }
            else
            {
                IF.Hide();
            }
        }
        bool success = true;
        private void timer1_Tick(object sender, EventArgs e)
        {
            if (success == true)
            {
                success = false;
                new Thread(() =>
                {
                    try
                    {
                        capture.SetCaptureProperty(CAP_PROP.CV_CAP_PROP_FRAME_WIDTH, 640);
                        capture.SetCaptureProperty(CAP_PROP.CV_CAP_PROP_FRAME_HEIGHT, 480);
                        Image<Bgr, byte> cap = capture.QueryFrame();
                        if (cap != null)
                        {
                            MethodInvoker mi = delegate
                            {
                                try
                                {
                                    Bitmap bmp = cap.ToBitmap();
                                    pictureBox_WC.Image = bmp;
                                    IF.pictureBox4.Image = bmp;
                                    pictureBox_WC.Update();
                                    IF.pictureBox4.Update();
                                }
                                catch (Exception ex)
                                { }
                            };
                            if (InvokeRequired)
                                Invoke(mi);
                        }
                    }
                    catch (Exception) { }
                    success = true;
                }).Start();
                
            }
            
            
        }

        public void ProcessImage(string urlImage)
        {
            PlateImagesList.Clear();
            PlateTextList.Clear();
            FileStream fs = new FileStream(urlImage, FileMode.Open, FileAccess.Read);
            Image img = Image.FromStream(fs);
            Bitmap image = new Bitmap(img);
            //pictureBox2.Image = image;
            IF.pictureBox2.Image = image;
            fs.Close();

            FindLicensePlate4(image, out Plate_Draw);

        }
        public static Bitmap RotateImage(Image image, float angle)
        {
            if (image == null)
                throw new ArgumentNullException("image");

            PointF offset = new PointF((float)image.Width / 2, (float)image.Height / 2);

            //create a new empty bitmap to hold rotated image
            Bitmap rotatedBmp = new Bitmap(image.Width, image.Height);
            rotatedBmp.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            //make a graphics object from the empty bitmap
            Graphics g = Graphics.FromImage(rotatedBmp);

            //Put the rotation point in the center of the image
            g.TranslateTransform(offset.X, offset.Y);

            //rotate the image
            g.RotateTransform(angle);

            //move the image back
            g.TranslateTransform(-offset.X, -offset.Y);

            //draw passed in image onto graphics object
            g.DrawImage(image, new PointF(0, 0));

            return rotatedBmp;
        }

        private string Ocr(Bitmap image_s, bool isFull, bool isNum = false)
        {
            string temp = "";
            Image<Gray, byte> src = new Image<Gray, byte>(image_s);
            double ratio = 1;
            while (true)
            {
                ratio = (double)CvInvoke.cvCountNonZero(src) / (src.Width * src.Height);
                if (ratio > 0.5) break;
                src = src.Dilate(2);
            }
            Bitmap image = src.ToBitmap();

            TesseractProcessor ocr;
            if (isFull)
                ocr = full_tesseract;
            else if (isNum)
                ocr = num_tesseract;
            else
                ocr = ch_tesseract;

            int cou = 0;
            ocr.Clear();
            ocr.ClearAdaptiveClassifier();
            temp = ocr.Apply(image);
            while (temp.Length > 3)
            {
                Image<Gray, byte> temp2 = new Image<Gray, byte>(image);
                temp2 = temp2.Erode(2);
                image = temp2.ToBitmap();
                ocr.Clear();
                ocr.ClearAdaptiveClassifier();
                temp = ocr.Apply(image);
                cou++;
                if (cou > 10)
                {
                    temp = "";
                    break;
                }
            }
            return temp;

        }

        public void FindLicensePlate2(Bitmap image)
        {
            if (image == null)
                return;
            Bitmap src;
            Image dst = image;
            Image<Bgr, byte> frame_b = null;
            Image<Bgr, byte> plate_b = null;
            double sum_b = 0;
            for (float i = -45; i <= 45; i = i + 5)
            {
                src = RotateImage(dst, i);
                PlateImagesList.Clear();
                Image<Bgr, byte> frame = new Image<Bgr, byte>(src);
                using (Image<Gray, byte> grayframe = new Image<Gray, byte>(src))
                {


                    var faces =
                           grayframe.DetectHaarCascade(
                                   new HaarCascade(Application.StartupPath + "\\traningfile.xml"), 1.1, 8,
                                   HAAR_DETECTION_TYPE.DO_CANNY_PRUNING,
                                   new Size(0, 0)
                                   )[0];
                    foreach (var face in faces)
                    {
                        Image<Bgr, byte> tmp = frame.Copy();
                        tmp.ROI = face.rect;

                        frame.Draw(face.rect, new Bgr(Color.Blue), 2);

                        PlateImagesList.Add(tmp.Resize(500, 500, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC, true));


                    }

                }
                if (PlateImagesList.Count != 0)
                {
                    Image<Gray, byte> gr = new Image<Gray, byte>(PlateImagesList[0].Resize(100, 100, Emgu.CV.CvEnum.INTER.CV_INTER_LINEAR).ToBitmap());
                    Gray cannyThreshold = new Gray(gr.GetAverage().Intensity);
                    Gray cannyThresholdLinking = new Gray(gr.GetAverage().Intensity);
                    Image<Gray, byte> cannyEdges = gr.Canny(cannyThreshold, cannyThresholdLinking);

                    double sum = 0;
                    for (int j = 0; j < cannyEdges.Height - 1; j++)
                    {
                        for (int k = 0; k < cannyEdges.Width - 1; k++)
                        {
                            if (j < 20 || j > 180 || k < 20 || k > 180)
                            {
                                sum += cannyEdges.Data[j, k, 0]; // tính tổng các điểm trắng ở viền ngoài
                            }
                            //else
                            //{
                            //    cannyEdges.Data[j, k, 0] = 0;
                            //}
                        }
                    }
                    //pictureBox4.Image = cannyEdges.ToBitmap();
                    //pictureBox4.Update();
                    if (sum_b == 0 || sum > sum_b)
                    {
                        frame_b = frame.Clone();
                        plate_b = PlateImagesList[0].Resize(400, 400, Emgu.CV.CvEnum.INTER.CV_INTER_LINEAR).Clone();
                        sum_b = sum;
                    }
                }

            }
            if (plate_b != null)
            {
                PlateImagesList.Add(plate_b);
                pictureBox_WC.Image = frame_b.ToBitmap();
                pictureBox_WC.Update();
            }

        }
        public void FindLicensePlate(Bitmap image, out Image plateDraw)
        {
            plateDraw = null;
            Image<Bgr, byte> frame = new Image<Bgr, byte>(image);
            bool isface = false;
            using (Image<Gray, byte> grayframe = new Image<Gray, byte>(image))
            {


                var faces =
                       grayframe.DetectHaarCascade(
                               new HaarCascade(Application.StartupPath + "\\traningfile.xml"), 1.1, 8,
                               HAAR_DETECTION_TYPE.DO_CANNY_PRUNING,
                               new Size(0, 0)
                               )[0];
                foreach (var face in faces)
                {
                    Image<Bgr, byte> tmp = frame.Copy();
                    tmp.ROI = face.rect;

                    frame.Draw(face.rect, new Bgr(Color.Blue), 2);

                    PlateImagesList.Add(tmp);

                    isface = true;
                }
                if (isface)
                {
                    Image<Bgr, byte> showimg = frame.Clone();
                    plateDraw = (Image)showimg.ToBitmap();
                    //showimg = frame.Resize(imageBox1.Width, imageBox1.Height, 0);
                    //pictureBox1.Image = showimg.ToBitmap();
                    IF.pictureBox2.Image = showimg.ToBitmap();
                    if(PlateImagesList.Count > 1)
                    {
                        for(int i = 1; i < PlateImagesList.Count; i++)
                        {
                            if(PlateImagesList[0].Width < PlateImagesList[i].Width)
                            {
                                PlateImagesList[0] = PlateImagesList[i];
                            }
                        }
                    }
                    PlateImagesList[0] = PlateImagesList[0].Resize(400, 400, Emgu.CV.CvEnum.INTER.CV_INTER_LINEAR);
                }


            }
        }
        public void FindLicensePlate4(Bitmap image, out Image plateDraw)
        {
            plateDraw = null;
            Image<Bgr, byte> frame;
            bool isface = false;
            Bitmap src;
            //pictureBox2.Image = new Image<Gray, byte>(image).ToBitmap();
            Image dst = image;
            HaarCascade haar = new HaarCascade(Application.StartupPath + "\\traningfile.xml");
            for (float i = 0; i <= 20; i = i + 3)
            {
                for (float s = -1; s <= 1 && s + i != 1; s += 2)
                {
                    src = RotateImage(dst, i * s);
                    PlateImagesList.Clear();
                    frame = new Image<Bgr, byte>(src);
                    using (Image<Gray, byte> grayframe = new Image<Gray, byte>(src))
                    {
                        var faces =
                       grayframe.DetectHaarCascade(haar, 1.1, 8, HAAR_DETECTION_TYPE.DO_CANNY_PRUNING, new Size(0, 0))[0];
                        foreach (var face in faces)
                        {
                            Image<Bgr, byte> tmp = frame.Copy();
                            tmp.ROI = face.rect;

                            frame.Draw(face.rect, new Bgr(Color.Blue), 2);

                            PlateImagesList.Add(tmp);

                            isface = true;
                        }
                        if (isface)
                        {
                            Image<Bgr, byte> showimg = frame.Clone();
                            plateDraw = (Image)showimg.ToBitmap();
                            //showimg = frame.Resize(imageBox1.Width, imageBox1.Height, 0);
                            //pictureBox1.Image = showimg.ToBitmap();
                            IF.pictureBox2.Image = showimg.ToBitmap();
                            if (PlateImagesList.Count > 1)
                            {
                                for (int k = 1; k < PlateImagesList.Count; k++)
                                {
                                    if (PlateImagesList[0].Width < PlateImagesList[k].Width)
                                    {
                                        PlateImagesList[0] = PlateImagesList[k];
                                    }
                                }
                            }
                            PlateImagesList[0] = PlateImagesList[0].Resize(400, 400, Emgu.CV.CvEnum.INTER.CV_INTER_LINEAR);
                            return;
                        }


                    }
                }
            }

            
        }
        public void FindLicensePlate3(Bitmap image)
        {
            if (image == null)
                return;
            Bitmap src;
            Image dst = image;
            Image<Bgr, byte> frame_b = null;
            Image<Bgr, byte> plate_b = null;
            double sum_b = 1000;
            HaarCascade haar = new HaarCascade(Application.StartupPath + "\\traningfile.xml");
            for (float i = 0; i <= 35; i = i + 3)
            {
                for (float s = -1; s <= 1 && s+i != 1; s += 2)
                {
                    src = RotateImage(dst, i * s);
                    PlateImagesList.Clear();
                    Image<Bgr, byte> frame = new Image<Bgr, byte>(src);
                    using (Image<Gray, byte> grayframe = new Image<Gray, byte>(src))
                    {


                        var faces = grayframe.DetectHaarCascade(haar, 1.1, 8, HAAR_DETECTION_TYPE.DO_CANNY_PRUNING, new Size(0, 0))[0];
                        foreach (var face in faces)
                        {
                            Image<Bgr, byte> tmp = frame.Copy();
                            tmp.ROI = face.rect;

                            frame.Draw(face.rect, new Bgr(Color.Blue), 2);

                            PlateImagesList.Add(tmp.Resize(400, 400, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC));

                            //imageBox1.Image = tmp;
                            //imageBox1.Update();

                        }
                        //Image<Bgr, Byte> showimg = new Image<Bgr, Byte>(image.Size);
                        //showimg = frame.Resize(imageBox1.Width, imageBox1.Height, 0);
                        //pictureBox1.Image = grayframe.ToBitmap();
                    }
                    if (PlateImagesList.Count != 0)
                    {
                        Image<Gray, byte> src2 = new Image<Gray, byte>(PlateImagesList[0].ToBitmap());
                        double thr = src2.GetAverage().Intensity;

                        double min = 0, max = 255;
                        if (thr - 50 > 0)
                        {
                            min = thr - 50;
                        }
                        if (thr + 50 < 255)
                        {
                            max = thr + 50;
                        }
                        for (double value = min; value <= max; value += 5)
                        {
                            src2 = new Image<Gray, byte>(PlateImagesList[0].ToBitmap());
                            int c = 0;
                            List<Rectangle> listR = new List<Rectangle>();
                            using (MemStorage storage = new MemStorage())
                            {
                                src2 = src2.ThresholdBinary(new Gray(value), new Gray(255));
                                Contour<Point> contours = src2.FindContours(Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE, Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_LIST, storage);
                                while (contours != null)
                                {

                                    Rectangle rect = contours.BoundingRectangle;
                                    double ratio = (double)rect.Width / rect.Height;
                                    if (rect.Width > 20 && rect.Width < 150
                                        && rect.Height > 80 && rect.Height < 180
                                        && ratio > 0.2 && ratio < 1.1)
                                    {
                                        c++;
                                        listR.Add(contours.BoundingRectangle);
                                    }
                                    contours = contours.HNext;
                                }
                            }
                            double sum = 1000;
                            if (c >= 2)
                            {
                                for (int u = 0; u < c; u++)
                                {
                                    for (int v = u + 1; v < c; v++)
                                    {
                                        if (Math.Abs(listR[u].Y - listR[v].Y) < sum)
                                        {

                                            sum = Math.Abs(listR[u].Y - listR[v].Y);
                                            if (sum < 4)
                                            {
                                                PlateImagesList.Add(PlateImagesList[0].Resize(400, 400, Emgu.CV.CvEnum.INTER.CV_INTER_LINEAR).Clone());
                                                pictureBox_XeRA.Image = frame.ToBitmap();
                                                pictureBox_XeRA.Update();
                                                return;
                                            }
                                        }
                                    }
                                }

                            }

                            if (sum < sum_b)
                            {
                                frame_b = frame.Clone();
                                plate_b = PlateImagesList[0].Resize(400, 400, Emgu.CV.CvEnum.INTER.CV_INTER_LINEAR).Clone();
                                sum_b = sum;
                            }
                        }
                    }
                }


            }
            if (plate_b != null)
            {
                PlateImagesList.Add(plate_b);
                pictureBox_XeRA.Image = frame_b.ToBitmap();
                pictureBox_XeRA.Update();
            }

        }

        private void Reconize(string link, out Image hinhbienso, out string bienso, out string bienso_text)
        {
            for (int i = 0; i < box.Length; i++)
            {
                this.Controls.Remove(box[i]);
            }

            hinhbienso = null;
            bienso = "";
            bienso_text = "";
            ProcessImage(link);
            if (PlateImagesList.Count != 0)
            {
                Image<Bgr, byte> src = new Image<Bgr, byte>(PlateImagesList[0].ToBitmap());
                Bitmap grayframe;
                FindContours con = new FindContours();
                Bitmap color;
                int c = con.IdentifyContours(src.ToBitmap(), 50, false, out grayframe, out color, out listRect);  // find contour
                //int z = con.count;
                pictureBox_BiensoVAO.Image = color;
                IF.pictureBox1.Image = color;
                hinhbienso = Plate_Draw;
                pictureBox_BiensoRA.Image = grayframe;
                IF.pictureBox3.Image = grayframe;
                //textBox2.Text = c.ToString();
                Image<Gray, byte> dst = new Image<Gray, byte>(grayframe);
                //dst = dst.Dilate(2);
                //dst = dst.Erode(3);
                grayframe = dst.ToBitmap();
                //pictureBox2.Image = grayframe.Clone(listRect[2], grayframe.PixelFormat);
                string zz = "";

                // lọc và sắp xếp số
                List<Bitmap> bmp = new List<Bitmap>();
                List<int> erode = new List<int>();
                List<Rectangle> up = new List<Rectangle>();
                List<Rectangle> dow = new List<Rectangle>();
                int up_y = 0, dow_y = 0;
                bool flag_up = false;

                int di = 0;

                if (listRect == null) return;

                for (int i = 0; i < listRect.Count; i++)
                {
                    Bitmap ch = grayframe.Clone(listRect[i], grayframe.PixelFormat);                    
                    int cou = 0;
                    full_tesseract.Clear();
                    full_tesseract.ClearAdaptiveClassifier();
                    string temp = full_tesseract.Apply(ch);
                    while (temp.Length > 3)
                    {
                        Image<Gray, byte> temp2 = new Image<Gray, byte>(ch);
                        temp2 = temp2.Erode(2);
                        ch = temp2.ToBitmap();
                        full_tesseract.Clear();
                        full_tesseract.ClearAdaptiveClassifier();
                        temp = full_tesseract.Apply(ch);
                        cou++;
                        if (cou > 10)
                        {
                            listRect.RemoveAt(i);
                            i--;
                            di = 0;
                            break;
                        }
                        di = cou;
                    }
                }

                for (int i = 0; i < listRect.Count; i++)
                {
                    for (int j = i; j < listRect.Count; j++)
                    {
                        if (listRect[i].Y > listRect[j].Y + 100)
                        {
                            flag_up = true;
                            up_y = listRect[j].Y;
                            dow_y = listRect[i].Y;
                            break;
                        }
                        else if (listRect[j].Y > listRect[i].Y + 100)
                        {
                            flag_up = true;
                            up_y = listRect[i].Y;
                            dow_y = listRect[j].Y;
                            break;
                        }
                        if (flag_up == true) break;
                    }
                }

                for (int i = 0; i < listRect.Count; i++)
                {
                    if (listRect[i].Y < up_y + 50 && listRect[i].Y > up_y - 50)
                    {
                        up.Add(listRect[i]);
                    }
                    else if (listRect[i].Y < dow_y + 50 && listRect[i].Y > dow_y - 50)
                    {
                        dow.Add(listRect[i]);
                    }
                }

                if (flag_up == false) dow = listRect;

                for (int i = 0; i < up.Count; i++)
                {
                    for (int j = i; j < up.Count; j++)
                    {
                        if (up[i].X > up[j].X)
                        {
                            Rectangle w = up[i];
                            up[i] = up[j];
                            up[j] = w;
                        }
                    }
                }
                for (int i = 0; i < dow.Count; i++)
                {
                    for (int j = i; j < dow.Count; j++)
                    {
                        if (dow[i].X > dow[j].X)
                        {
                            Rectangle w = dow[i];
                            dow[i] = dow[j];
                            dow[j] = w;
                        }
                    }
                }

                int x = 12;
                int c_x = 0;

                for (int i = 0; i < up.Count; i++)
                {
                    Bitmap ch = grayframe.Clone(up[i], grayframe.PixelFormat);
                    Bitmap o = ch;
                    //ch = con.Erodetion(ch);
                    string temp;
                    if (i < 2)
                    {
                        temp = Ocr(ch, false, true); // nhan dien so
                    }
                    else
                    {
                        temp = Ocr(ch, false, false);// nhan dien chu
                    }

                    zz += temp;
                    box[i].Location = new Point(x + i * 50, 290);
                    box[i].Size = new Size(50, 100);
                    box[i].SizeMode = PictureBoxSizeMode.StretchImage;
                    box[i].Image = ch;
                    box[i].Update();
                    //this.Controls.Add(box[i]);
                    IF.Controls.Add(box[i]);
                    c_x++;
                }
                zz += "\r\n";
                for (int i = 0; i < dow.Count; i++)
                {
                    Bitmap ch = grayframe.Clone(dow[i], grayframe.PixelFormat);
                    //ch = con.Erodetion(ch);
                    string temp = Ocr(ch, false, true); // nhan dien so
                    zz += temp;
                    box[i + c_x].Location = new Point(x + i * 50, 390);
                    box[i + c_x].Size = new Size(50, 100);
                    box[i + c_x].SizeMode = PictureBoxSizeMode.StretchImage;
                    box[i + c_x].Image = ch;
                    box[i + c_x].Update();
                    //this.Controls.Add(box[i + c_x]);
                    IF.Controls.Add(box[i + c_x]);
                }
                bienso = zz.Replace("\n", "");
                bienso = bienso.Replace("\r", "");
                IF.textBox6.Text = zz;
                bienso_text = zz;

            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            //while (true) ;
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Image (*.bmp; *.jpg; *.jpeg; *.png) |*.bmp; *.jpg; *.jpeg; *.png|All files (*.*)|*.*||";
            dlg.InitialDirectory = Application.StartupPath + "\\ImageTest";
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.Cancel)
            {
                return;
            }
            string startupPath = dlg.FileName;

            Image temp1;
            string temp2, temp3;
            Reconize(startupPath, out temp1, out temp2, out temp3);
            pictureBox_XeVAO.Image = temp1;
            if (temp3 == "")
                text_BiensoVAO.Text = "Cannot recognize license plate !";
            else
                text_BiensoVAO.Text = temp3;

           
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (capture != null)
            {
                timer1.Enabled = false;
                pictureBox_XeRA.Image = null;
                IF.pictureBox2.Image = null;
                capture.QueryFrame().Save("aa.bmp");
                FileStream fs = new FileStream(m_path + "aa.bmp", FileMode.Open, FileAccess.Read);
                Image temp = Image.FromStream(fs);
                fs.Close();
                pictureBox_XeRA.Image = temp;
                IF.pictureBox2.Image = temp;
                pictureBox_XeRA.Update();
                IF.pictureBox2.Update();
                Image temp1;
                string temp2, temp3;
                Reconize(m_path + "aa.bmp", out temp1, out temp2, out temp3);
                pictureBox_XeVAO.Image = temp1;
                if(temp3 == "")
                    text_BiensoVAO.Text = "Cannot recognize license plate !";
                else
                    text_BiensoVAO.Text = temp3;

                timer1.Enabled = true;
            }
            
        }

        #region RESIZE
        private void MainForm_Resize(object sender, EventArgs e)
        {
            //Thread.Sleep(100);
            //Size a = Size;
            panel1.Size = new Size(Size.Width, panel1.Size.Height);
            button1.Location = new Point(panel1.Size.Width - button1.Size.Width, button1.Location.Y);
            button6.Location = new Point(button1.Location.X - button6.Size.Width - 1, button6.Location.Y);
            button8.Location = new Point(button6.Location.X - button8.Size.Width - 1, button8.Location.Y);

            panel_VAO.Location = new Point(0, 0);
            panel_VAO.Size = new Size((Size.Width - 184) / 2, Size.Height);
            panel_RA.Location = new Point(panel_VAO.Size.Width, 0);
            panel_RA.Size = new Size((Size.Width - 184) / 2, Size.Height);
            panel_WC.Location = new Point(panel_VAO.Size.Width + panel_RA.Size.Width, 0);
            panel_WC.Size = new Size(184, Size.Height);

            panel5.Location = new Point(Size.Width - panel5.Width, Size.Height - panel5.Height);

            pictureBox_XeVAO.Size = new Size(panel_VAO.Size.Width * 294 / 306, panel_VAO.Size.Width * 260 / 306);
            pictureBox_XeVAO.Location = new Point(panel_VAO.Size.Width / 2 - pictureBox_XeVAO.Size.Width / 2, pictureBox_XeVAO.Location.Y);
            groupBox1.Location = new Point(groupBox1.Location.X, pictureBox_XeVAO.Location.Y + pictureBox_XeVAO.Size.Height + 6);

            int h = panel_VAO.Height - groupBox1.Location.Y - 10;
            int w = h * 294 / 194;
            if( h > 194 && w < panel_VAO.Width - 12)
                groupBox1.Size = new Size(w, h);
            else
            {
                w = panel_VAO.Width - 12;
                h = w * 194 / 294;
                if(w > 294 && h < panel_VAO.Height - groupBox1.Location.Y - 10)
                    groupBox1.Size = new Size(w, h);
            }
            pictureBox_BiensoVAO.Size = new Size(groupBox1.Size.Width - 14 - pictureBox_BiensoVAO.Location.X
                , groupBox1.Size.Height - 67 - pictureBox_BiensoVAO.Location.Y);

            ///////////////////////

            pictureBox_XeRA.Size = new Size(panel_RA.Size.Width * 294 / 306, panel_RA.Size.Width * 260 / 306);
            pictureBox_XeRA.Location = new Point(panel_RA.Size.Width / 2 - pictureBox_XeRA.Size.Width / 2, pictureBox_XeRA.Location.Y);
            groupBox2.Location = new Point(groupBox2.Location.X, pictureBox_XeRA.Location.Y + pictureBox_XeRA.Size.Height + 6);

            h = panel_RA.Height - groupBox2.Location.Y - 10;
            w = h * 294 / 194;
            if (h > 194 && w < panel_RA.Width - 12)
                groupBox2.Size = new Size(w, h);
            else
            {
                w = panel_RA.Width - 12;
                h = w * 194 / 294;
                if (w > 294 && h < panel_RA.Height - groupBox2.Location.Y - 10)
                    groupBox2.Size = new Size(w, h);
            }
            pictureBox_BiensoRA.Size = new Size(groupBox2.Size.Width - 14 - pictureBox_BiensoRA.Location.X
                , groupBox2.Size.Height - 67 - pictureBox_BiensoRA.Location.Y);
        }
        private void resizeInGr(GroupBox gr, ref TextBox tx, ref Label lb, int dis_d, int dis_r_t, int dis_r_l, bool t)
        {
            if(dis_r_t < 0)
            {
                tx.Location = new Point(tx.Location.X, gr.Size.Height - dis_d);
                lb.Location = new Point(lb.Location.X, gr.Size.Height - dis_d);
            }
            else
            {
                tx.Location = new Point(gr.Size.Width - dis_r_t, gr.Size.Height - dis_d);
                lb.Location = new Point(gr.Size.Width - dis_r_l, gr.Size.Height - dis_d);
            }
            if (t)
                tx.Size = new Size(gr.Size.Width - 3 - tx.Location.X, tx.Size.Height);
        }
        private void button6_Click(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Normal)
                this.WindowState = FormWindowState.Maximized;
            else if (this.WindowState == FormWindowState.Maximized)
                this.WindowState = FormWindowState.Normal;
        }

        private void button8_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void splitter1_MouseMove(object sender, MouseEventArgs e)
        {
            
            if (mouseDown)
            {
                int w = Size.Width + (e.X - lastLocation.X);
                if (w < 796)
                {
                    w = 796;
                }
                this.Size = new Size(w, Size.Height);
                this.Update();
            }
        }

        private void splitter2_MouseMove(object sender, MouseEventArgs e)
        {
            if (mouseDown)
            {
                int h = Size.Height + (e.Y - lastLocation.Y);
                if (h < 504)
                {
                    h = 504;
                }
                this.Size = new Size(Size.Width, h);
                this.Update();
            }
        }

        private void panel5_MouseMove(object sender, MouseEventArgs e)
        {
            if (mouseDown)
            {
                int w = Size.Width + (e.X - lastLocation.X);
                if (w < 796)
                {
                    w = 796;
                }
                int h = Size.Height + (e.Y - lastLocation.Y);
                if (h < 504)
                {
                    h = 504;
                }
                this.Size = new Size(w, h);
                this.Update();
            }
        }

        #endregion

        #region WEBCAM
        WEBCAM[] cam = new WEBCAM[3];
        private void pictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                PictureBox p = (PictureBox)sender;
                for (int i = 0; i < cam.Length; i++)
                {
                    if (cam[i] != null && cam[i].status == "run" && cam[i].pb == p.Name)
                    {
                        cam[i].Stop();
                        cam[i] = null;
                    }
                }
                ContextMenu m = new ContextMenu();
                List<string> ls = WEBCAM.get_all_cam();
                for(int i = 0; i<=2 & i < ls.Count; i++)
                {
                    m.MenuItems.Add(ls[i], (s, e2) => {
                        MenuItem menuItem = s as MenuItem;
                        ContextMenu owner = menuItem.Parent as ContextMenu;
                        PictureBox pb = (PictureBox)owner.SourceControl;
                        if (cam[menuItem.Index] != null && cam[menuItem.Index].status == "run")
                        {
                            cam[menuItem.Index].Stop();
                            //cam[menuItem.Index] = null;
                        }
                        cam[menuItem.Index] = new WEBCAM();
                        cam[menuItem.Index].Start(menuItem.Index);
                        cam[menuItem.Index].put_picturebox(pb.Name);
                    });
                }
                m.Show(p, new Point(e.X, e.Y));
            }
        }
        private void timer3_Tick(object sender, EventArgs e)
        {
            try
            {
                for (int i = 0; i < cam.Length; i++)
                {
                    if (cam[i] != null && cam[i].status == "run" && cam[i].image != null)
                    {
                        MethodInvoker mi = delegate
                        {
                            PictureBox pb = this.Controls.Find(cam[i].pb, true).FirstOrDefault() as PictureBox;
                            pb.Image = cam[i].image;
                            pb.Update();
                            pb.Invalidate();
                        };
                        if (InvokeRequired)
                        {
                            Invoke(mi);
                            return;
                        }
                            
                        PictureBox pb2 = this.Controls.Find(cam[i].pb, true).FirstOrDefault() as PictureBox;
                        pb2.Image = cam[i].image;
                        pb2.Update();
                        pb2.Invalidate();
                    }
                }
            }
            catch (Exception) { }
        }

        #endregion
    }
}
