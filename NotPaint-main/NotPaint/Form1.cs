using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.XImgproc;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NotPaint
{
    public partial class Form1 : Form
    {
        bool mouseDown = false;

        int? initX = null;
        int? initY = null;

        Graphics gfx;

        List<(Point, Point)> lines;

        public Form1()
        {
            InitializeComponent();
            gfx = pictureBox1.CreateGraphics();
            lines = new List<(Point, Point)>();
            p.Width = 3;
            p.EndCap = p.StartCap = System.Drawing.Drawing2D.LineCap.Round;
        }

        private void PenBtn_Click(object sender, EventArgs e)
        {

        }

        private void EraseBtn_Click(object sender, EventArgs e)
        {
            lines = new List<(Point, Point)>();
        }

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            mouseDown = true;
            stopdraw = false;
        }

        private void pictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            mouseDown = false;
            initX = null;
            initY = null;
        }
        
        Pen p = new Pen(Color.Black, 15f);
        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (mouseDown)
            {
                lines.Add((new Point(initX ?? e.X, initY ?? e.Y), new Point(e.X, e.Y)));
                initX = e.X;
                initY = e.Y;
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (stopdraw) return;

            Bitmap bm = new Bitmap(pictureBox1.Width, pictureBox1.Height);
            Graphics bmGfx = Graphics.FromImage(bm);
            bmGfx.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            foreach (var line in lines)
            {
                bmGfx.DrawLine(p, line.Item1, line.Item2);
            }

            pictureBox1.Image = bm;
        }

        bool stopdraw = false;
        private void LoadBtn_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog of = new OpenFileDialog() { Filter = "Image Files (*.bmp;*.jpg;*.jpeg,*.png)|*.BMP;*.JPG;*.JPEG;*.PNG" })
            {
                if (of.ShowDialog() != DialogResult.OK)
                    return;

                using (Mat img = CvInvoke.Imread(of.FileName, Emgu.CV.CvEnum.ImreadModes.Color))
                using (Image<Gray, byte> bmImage = img.ToImage<Gray, byte>().ThresholdBinary(new Gray(100), new Gray(255)).Not())
                {
                    XImgprocInvoke.Thinning(bmImage, bmImage, ThinningTypes.ZhangSuen);

                    pictureBox1.Image = bmImage.ToBitmap();
                    stopdraw = true;

                    ConcurrentBag<(Point, Point)> newLines = new ConcurrentBag<(Point, Point)>();

                    var data = bmImage.ThresholdBinary(new Gray(254), new Gray(255)).Data;

                    Parallel.For(1, bmImage.Width, (i) =>
                    {
                        for (int j = 1; j < bmImage.Height - 1; j ++)
                        {
                            if (data[i, j, 0] != 0)
                            {
                                if (data[i, j - 1, 0] != 0)
                                {
                                    newLines.Add((new Point(j, i), new Point(j, i - 1)));
                                }
                                if (data[i - 1, j, 0] != 0)
                                {
                                    newLines.Add((new Point(j, i), new Point(j - 1, i)));
                                }
                                if (data[i - 1, j - 1, 0] != 0)
                                {
                                    newLines.Add((new Point(j, i), new Point(j - 1, i - 1)));
                                }
                                if (data[i + 1, j + 1, 0] != 0)
                                {
                                    newLines.Add((new Point(j, i), new Point(j + 1, i + 1)));
                                }
                                if (data[i + 1, j, 0] != 0)
                                {
                                    newLines.Add((new Point(j, i), new Point(j + 1, i)));
                                }
                            }
                        }
                    });

                    lines.AddRange(newLines);
                }
            }
        }

        
        private void GoBtn_Click(object sender, EventArgs e)
        {
            List<List<(Point, Point)>> lineSegments = new List<List<(Point, Point)>>();
            List<(Point, Point)> segment;

            int i = 0;
            while(i < lines.Count - 1)
            {
                segment = new List<(Point, Point)>();
                while (i < lines.Count - 1 && lines[i].Item2 == lines[++i].Item1)
                {
                    segment.Add(lines[i]);
                }
                lineSegments.Add(segment);
            }

            try
            {
                SerialPort serialPort = new SerialPort();
                serialPort.PortName = comboBox1.Text;
                serialPort.BaudRate = 9600;
                serialPort.Open();

                Task.Run(() => {

                    foreach (var lineSegment in lineSegments)
                    {
                        //sample
                        foreach (var line in lineSegment)
                        {
                            serialPort.WriteLine($"X: {line.Item1} Y: {line.Item2}");
                            Task.Delay(50 + (int)line.Item1.Distance(line.Item2));
                        }
                        serialPort.WriteLine($"PEN UP");
                    }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void portBtn_Click(object sender, EventArgs e)
        {
            string[] ArrayComPortsNames = null;
            int index = -1;
            string ComPortName = null;

            ArrayComPortsNames = SerialPort.GetPortNames();
            do
            {
                index += 1;
                comboBox1.Items.Add(ArrayComPortsNames[index]);
            }

            while (!((ArrayComPortsNames[index] == ComPortName)
                          || (index == ArrayComPortsNames.GetUpperBound(0))));
            Array.Sort(ArrayComPortsNames);

            if (index == ArrayComPortsNames.GetUpperBound(0))
            {
                ComPortName = ArrayComPortsNames[0];
            }
            comboBox1.Text = ArrayComPortsNames[0];
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }
    }
}
