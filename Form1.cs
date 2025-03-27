using HidLibrary;
using Microsoft.VisualBasic;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace LabelPrinter
{
    public partial class Form1 : Form
    {
        Image img = Image.FromFile("128x64.png");
        public struct ImageData
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public byte[,] Data { get; set; }
        }

        public byte initFrame = 0xa1;
        public byte nextFrame = 0xa2;
        public byte endFrame = 0xa3;
        public byte startPrint = 0xa4;
        public byte getStatus = 0xa5;
        public double lineWith = 0.140625;

        public struct usbDataBlock
        {
            public byte dummyByte { get; set; }
            public byte opcode { get; set; }
            public byte fnCode { get; set; }
            public byte dataBlockNumH { get; set; }
            public byte dataBlockNumL { get; set; }
            public byte payloadSize { get; set; }
            public byte[] payloadData { get; set; }

        }
        ImageData imageData;
        HidDevice device = HidDevices.Enumerate(0x03EB, 0x2403).FirstOrDefault();

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            pictureBox1.Size = new Size(trackBar1.Value, pictureBox1.Size.Height);
            Image grayImage = ConvertToGrayscale((Bitmap)img);
            pictureBox2.Image = grayImage;
            imageData = ConvertToImageData((Bitmap)grayImage);
            checkBox1.Checked = true;
            }

        private Image ConvertToGrayscale(Bitmap original)
        {
            Bitmap grayBitmap = new Bitmap(original.Width, original.Height);

            for (int y = 0; y < original.Height; y++)
            {
                for (int x = 0; x < original.Width; x++)
                {
                    Color originalColor = original.GetPixel(x, y);
                    int grayScale = (int)((originalColor.R * 0.3) + (originalColor.G * 0.59) + (originalColor.B * 0.11));
                    Color grayColor = Color.FromArgb(originalColor.A, grayScale, grayScale, grayScale);
                    grayBitmap.SetPixel(x, y, grayColor);
                }
            }
            return grayBitmap;
        }

        private ImageData ConvertToImageData(Bitmap grayBitmap)
        {
            int width = grayBitmap.Width;
            int height = grayBitmap.Height / 8;
            byte[,] data = new byte[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height * 8; y += 8)
                {
                    byte byteValue = 0;
                    for (int bit = 0; bit < 8; bit++)
                    {
                        Color pixelColor = grayBitmap.GetPixel(x, y + bit);
                        if (pixelColor.R < 128)
                        {
                            byteValue |= (byte)(1 << bit);
                        }
                    }
                    data[x, y / 8] = byteValue;
                }
            }

            label1.Text = $"Label width: {Math.Round(width * lineWith, 1)}mm";
            return new ImageData
            {
                Width = width,
                Height = height,
                Data = data
            };
        }

        private usbDataBlock[] ConvertToUsbDataBlocks(ImageData imageData)
        {
            int totalBlocks = imageData.Width;
            usbDataBlock[] blocks = new usbDataBlock[totalBlocks];

            for (int i = 0; i < totalBlocks; i++)
            {
                int x = i % imageData.Width;
                int y = i / imageData.Width;

                blocks[i] = new usbDataBlock
                {
                    opcode = (i == 0) ? initFrame : (i == totalBlocks - 1) ? endFrame : nextFrame,
                    fnCode = 0,
                    dataBlockNumL = (byte)(i & 0xFF),
                    dataBlockNumH = (byte)((i >> 8) & 0xFF),
                    payloadSize = 8,
                    payloadData = new byte[8]
                };

                for (int j = 0; j < 8; j++)
                {
                    blocks[i].payloadData[j] = imageData.Data[x, j];
                }
            }
            return blocks;
        }

        private byte[] StructToByteArray(usbDataBlock dataBlock)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(dataBlock.dummyByte);
                    writer.Write(dataBlock.opcode);
                    writer.Write(dataBlock.fnCode);
                    writer.Write(dataBlock.dataBlockNumH);
                    writer.Write(dataBlock.dataBlockNumL);
                    writer.Write(dataBlock.payloadSize);
                    writer.Write(dataBlock.payloadData);
                }
                return stream.ToArray();
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (WritePrintingData()){
                Print();
            }
        }

        public bool WritePrintingData()
        {
            ImageData tmpData = imageData;
            usbDataBlock[] usbDataBlocks = ConvertToUsbDataBlocks(tmpData);
            //textBox1.Text = "";
            //String tmpStr = "";
            if (device ==null || !device.IsConnected)
            {
                MessageBox.Show("Device is not connected", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            device.OpenDevice();
            for (int i = 0; i < usbDataBlocks.Length; i++)
            {
                byte[] dataToSend = StructToByteArray(usbDataBlocks[i]);
                //tmpStr += ($"" +
                //    $"{dataToSend[0]},{dataToSend[1]},{dataToSend[2]},{dataToSend[3]}," +
                //    $"{dataToSend[4]},{dataToSend[5]},{dataToSend[6]},{dataToSend[7]}," +
                //    $"{dataToSend[8]},{dataToSend[9]},{dataToSend[10]},{dataToSend[11]}" + Environment.NewLine);

                var res = device.Write(dataToSend);
      
            }
            
            device.CloseDevice();
            return true;
        }
        public void Print()
        {
            usbDataBlock printCmd = new usbDataBlock
            {
                opcode = startPrint,
                fnCode = (checkBox1.Checked) ? (byte)1 : (byte)0,
                dataBlockNumL = 2,
                dataBlockNumH = 0,
                payloadSize = 8,
                payloadData = new byte[8] { (byte)((imageData.Width >> 8) & 0xff), (byte)(imageData.Width & 0xff), 0x06, 0x07, 0x08, 0x09, 0x0a, 0x0b }
            };

            byte[] dataToSend = StructToByteArray(printCmd);
            //textBox1.AppendText($"" +
            //    $"{dataToSend[0]},{dataToSend[1]},{dataToSend[2]},{dataToSend[3]}," +
            //    $"{dataToSend[4]},{dataToSend[5]},{dataToSend[6]},{dataToSend[7]}," +
            //    $"{dataToSend[8]},{dataToSend[9]},{dataToSend[10]},{dataToSend[11]}" + Environment.NewLine);
            device.OpenDevice();
            device.Write(dataToSend);
            device.CloseDevice();
        }
        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            pictureBox1.Size = new Size(trackBar1.Value, pictureBox1.Size.Height);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            Point screenLocation = pictureBox1.PointToScreen(Point.Empty);
            Bitmap screenshot = new Bitmap(pictureBox1.Width, pictureBox1.Height);
            using (Graphics g = Graphics.FromImage(screenshot))
            {
                g.CopyFromScreen(screenLocation, Point.Empty, pictureBox1.Size);
            }

            Image grayImage = ConvertToGrayscale((Bitmap)screenshot);
            pictureBox2.Image = grayImage;
            imageData = ConvertToImageData((Bitmap)grayImage);

        }

        private void button4_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                string initialDirectory = Properties.Settings.Default.LastOpenedDirectory;
                if (string.IsNullOrEmpty(initialDirectory))
                {
                    initialDirectory = AppDomain.CurrentDomain.BaseDirectory;
                }
                openFileDialog.InitialDirectory = initialDirectory;
                openFileDialog.Filter = "Image Files|*.bmp;*.jpg;*.jpeg;*.png;*.gif;*.tif;*.tiff";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = openFileDialog.FileName;
                    Image img = Image.FromFile(filePath);
 
                    Properties.Settings.Default.LastOpenedDirectory = System.IO.Path.GetDirectoryName(filePath);
                    Properties.Settings.Default.Save();

                    if (img.Width > 512)
                    {
                        MessageBox.Show("Max width - 512pix", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    if (img.Height > 64)
                    {
                        float scale = 64f / img.Height;
                        int newWidth = (int)(img.Width * scale);
                        int newHeight = 64;

                        Bitmap scaledImage = new Bitmap(newWidth, newHeight);
                        using (Graphics g = Graphics.FromImage(scaledImage))
                        {
                            g.DrawImage(img, 0, 0, newWidth, newHeight);
                        }
                        img = scaledImage;
                    }

                    Image grayImage = ConvertToGrayscale((Bitmap)img);
                    pictureBox2.Image = grayImage;
                    imageData = ConvertToImageData((Bitmap)grayImage);
                }
            }
        }
    }
}
