using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Linq;
using System.Drawing.Imaging; // for ImageFormat
using System.IO;//輸入讀取
using System.Globalization;

namespace WindowsApplication1
{
    public partial class Form1 : Form
    {
        int[,,] RGBdata;//壓縮後的陣列
        int[,,] COMdata;//壓縮後的陣列
        String Filename;//檔案名稱
        String new_filename = @"C:\Temp\Output.txt";

        public Form1()
        {
            InitializeComponent();
        }
        // Load 按鈕事件處理函式 
        private void button1_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() != DialogResult.Cancel)
            {
                Filename = openFileDialog1.FileName;                
                ImageForm MyImage = new ImageForm(openFileDialog1.FileName); // 建立秀圖物件
                MyImage.Show();// 顯示秀圖照片 
            }

        }
        // 壓縮 按鈕事件處理函式 
        private void button2_Click(object sender, EventArgs e)
        {
            Compression Compression = new Compression(openFileDialog1.FileName, out RGBdata, out COMdata);// 建立秀圖物件
            Compression.Show();// 顯示秀圖照片
        }
        //show 壓縮率&失真率
        private void button3_Click(object sender, EventArgs e)
        {
            int result;//失真率
            //計算失真率
            int temp = 0, count = 0;
            for (int i = 0; i < COMdata.GetUpperBound(0); i++)
            {
                for (int j = 0; j < COMdata.GetUpperBound(1); j++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        temp += Math.Abs(COMdata[i, j, k] - RGBdata[i, j, k]);
                        count++;
                    }

                }
            }
            result = temp / count;

            //計算壓縮率
            String x = Filename;            
            Double s1 = new FileInfo(x).Length;            
            Double s2 = new FileInfo(new_filename).Length;
            int compress = (int)(s2 / s1 * 100);            
            MessageBox.Show("失真率：" + result + "%" + "\n壓縮率：" + compress + "%");
        }
        // 建立一個專門秀圖的 Form 類別
        class ImageForm : Form
        {
            Image image; // 建構子 
            public ImageForm(String Filename)
            {
                LoadImage(Filename);
                InitializeMyScrollBar();

            }

            public void LoadImage(String Filename)
            {   //載入檔案
                image = Image.FromFile(Filename);
                this.Text = Filename;
                //調整視窗大小
                this.Height = image.Height;
                this.Width = image.Width;
            }
            //ScrollBar視窗滾動
            private void InitializeMyScrollBar()
            {
                VScrollBar vScrollBar1 = new VScrollBar();
                HScrollBar hScrollBar1 = new HScrollBar();
                vScrollBar1.Dock = DockStyle.Right;
                hScrollBar1.Dock = DockStyle.Bottom;
                Controls.Add(vScrollBar1);
                Controls.Add(hScrollBar1);
                
            }
            //顯示圖片
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.DrawImage(image, 0, 0, image.Width, image.Height);
            }
        }
        class Compression : Form
        {
            Image image; // 建構子
            int dim = 4;
            int Height, Width;//圖片的寬跟高
            int new_h, new_w;//剪除過後的寬跟高
            Bitmap bimage;
            String Filename;//讀入的檔案名稱
            String new_filename = @"C:\Temp\Output.txt";
            int[] zig_zag;//zigzag過後的一維陣列
            enum zig_dir { UP, DOWN };
            public int m_maxrow, m_maxcol;
            public int m_row, m_col;//row直的，col橫的
            public int m_dir;
            int outputlength;//RLE編碼後的陣列長度

            public Compression(String Filename, out int[,,] RGB, out int[,,] Comdata)//
            {
                LoadImage(Filename);

                double[,] arr_y = new double[Height, Width];
                double[,] arr_u = new double[Height, Width];
                double[,] arr_v = new double[Height, Width];

                RGB = new int[Height, Width, 3];
                RGB = getRGBData();
                rgb_to_yuv(RGB, arr_y, arr_u, arr_v);//第一步：色彩轉換
                sampling(arr_u, arr_v);//第二步：取樣

                double[,] dct_y = new double[new_h, new_w];
                double[,] dct_u = new double[new_h, new_w];
                double[,] dct_v = new double[new_h, new_w];

                FDCT(arr_y, arr_u, arr_v, dct_y, dct_u, dct_v);//第三步：DCT轉換

                int[,] int_y = new int[new_h, new_w];
                int[,] int_u = new int[new_h, new_w];
                int[,] int_v = new int[new_h, new_w];

                quantized(dct_y, dct_u, dct_v, int_y, int_u, int_v);//第四步：量化

                int[] data_y = new int[new_h * new_w];
                int[] data_u = new int[new_h * new_w];
                int[] data_v = new int[new_h * new_w];

                en_zigzag(int_y, int_u, int_v, data_y, data_u, data_v);//第五步：zigzag編碼
                
                int[] rle_data_y = new int[new_h * new_w];
                int[] rle_data_u = new int[new_h * new_w];
                int[] rle_data_v = new int[new_h * new_w];
                
                //第六步：RLE編碼
                encode_RLE(data_y, rle_data_y);
                Array.Resize(ref rle_data_y, outputlength);
                encode_RLE(data_u, rle_data_u);
                Array.Resize(ref rle_data_u, outputlength);
                encode_RLE(data_v, rle_data_v);
                Array.Resize(ref rle_data_v, outputlength);

                //將長度資料儲存成一個string陣列                
                int i, j, count = 2;
                int[] length_int = new int[] { rle_data_y.Length, rle_data_u.Length, rle_data_v.Length, new_h, new_w };
                string[] length_str = length_int.Select(x => x.ToString()).ToArray();
                string[] spilt = new string[50];

                spilt[1] = length_str[0].Length.ToString();
                for (i = 0; i < 5; i++)
                {
                    for (j = 0; j < length_str[i].Length; j++)
                    {
                        spilt[count] = length_str[i].Substring(j, 1);
                        count++;
                    }

                    if (i < length_str.Length - 1)
                    {
                        spilt[count] = length_str[i + 1].Length.ToString();
                        count++;
                    }
                }
                spilt[0] = count.ToString();
                int[] information = new int[count];
                for (i = 0; i < count; i++)
                {
                    information[i] = Convert.ToInt16(spilt[i]);
                }
                
                //存檔
                BinWrite(new_filename, rle_data_y, rle_data_u, rle_data_v, information);
                //讀檔                
                int[] decode_y = new int[0];
                int[] decode_u = new int[0];
                int[] decode_v = new int[0];
                BinRead(new_filename, ref decode_y, ref decode_u, ref decode_v, new_h, new_w);
                
                //RLE解碼            
                Array.Resize(ref data_y, new_h * new_w);
                decode_RLE(rle_data_y, data_y);
                Array.Resize(ref data_u, new_h * new_w);
                decode_RLE(rle_data_u, data_u);
                Array.Resize(ref data_v, new_h * new_w);
                decode_RLE(rle_data_v, data_v);

                de_zigzag(data_y, data_u, data_v, int_y, int_u, int_v);//zigzag解碼

                D_quantized(int_y, int_u, int_v, dct_y, dct_u, dct_v);//量化解碼

                IDCT(dct_y, dct_u, dct_v, arr_y, arr_u, arr_v);//DCT解碼

                Comdata = new int[new_h, new_w, 3];
                Comdata = yuv_rgb(arr_y, arr_u, arr_v);
            }

            public void LoadImage(String Filename)
            {   //載入檔案
                image = Image.FromFile(Filename);
                this.Text = Filename;
                //調整視窗大小
                this.Height = image.Height;
                this.Width = image.Width;
            }
            public int[,,] getRGBData()
            {
                // Step 1: 利用 Bitmap 將 image 包起來
                bimage = new Bitmap(image);
                Height = bimage.Height;
                Width = bimage.Width;
                //初始化陣列
                int[,,] rgbData = new int[Height, Width, 3];

                // Step 2: 取得像點顏色資訊
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        Color color = bimage.GetPixel(x, y);
                        rgbData[y, x, 0] = color.R;
                        rgbData[y, x, 1] = color.G;
                        rgbData[y, x, 2] = color.B;
                    }
                }
                return rgbData;
            }

            /*以下為色彩轉換副程式*/
            public void rgb_to_yuv(int[,,] rgbData, Double[,] arr_y, Double[,] arr_u, Double[,] arr_v)
            {
                //設定像點資料
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        arr_y[y, x] = 0.299 * rgbData[y, x, 0] + 0.587 * rgbData[y, x, 1] + 0.114 * rgbData[y, x, 2];//亮度轉換
                        arr_u[y, x] = 0.5 * rgbData[y, x, 2] - 0.169 * rgbData[y, x, 0] - 0.331 * rgbData[y, x, 1] + 128;//藍色色度差轉換
                        arr_v[y, x] = 0.5 * rgbData[y, x, 0] - 0.419 * rgbData[y, x, 1] - 0.081 * rgbData[y, x, 2] + 128;//紅色色度差轉換

                        if (arr_y[y, x] < 0) arr_y[y, x] = 0;
                        if (arr_y[y, x] > 255) arr_y[y, x] = 255;
                        if (arr_u[y, x] < 0) arr_u[y, x] = 0;
                        if (arr_u[y, x] > 255) arr_u[y, x] = 255;
                        if (arr_v[y, x] < 0) arr_v[y, x] = 0;
                        if (arr_v[y, x] > 255) arr_v[y, x] = 255;
                    }
                }
            }
            /*以下為取樣*/
            public void sampling(Double[,] arr_u, Double[,] arr_v)
            {
                new_h = Height - Height % dim;
                new_w = Width - Width % dim;
                for (int i = 0; i < Height; i++)
                {
                    for (int j = 0; j < Width; j++)
                    {
                        if (j % 2 == 0)
                        {
                            if (i % 2 == 0)
                            {
                                if (i + 1 < Height)
                                    arr_v[i + 1, j] = arr_v[i, j];
                                if (i + 1 < Height && j + 1 < Width)
                                    arr_v[i + 1, j + 1] = arr_v[i, j];
                                if (j + 1 < Width)
                                    arr_v[i, j + 1] = arr_v[i, j];
                            }
                            else
                            {
                                arr_u[i - 1, j] = arr_u[i, j];
                                if (j + 1 < Width)
                                {
                                    arr_u[i - 1, j + 1] = arr_u[i, j];
                                    arr_u[i, j + 1] = arr_u[i, j];
                                }
                            }
                        }
                    }
                }
            }
            /*以下為正離散餘弦轉換(FDCT)*/
            public void FDCT(Double[,] input_y, Double[,] input_u, Double[,] input_v, Double[,] output_y, Double[,] output_u, Double[,] output_v)
            {
                Double[,] temp_y = new Double[dim, dim];
                Double[,] temp_u = new Double[dim, dim];
                Double[,] temp_v = new Double[dim, dim];
                double[,] A = new double[,]{ { 1, 1, 1, 1 },
                                         { 1, 1,-1,-1 },
                                         { 1,-1,-1, 1 },
                                         { 1,-1, 1,-1 } };
                for (int m = 0; m < new_h; m += dim)
                {
                    for (int n = 0; n < new_w; n += dim)
                    {
                        for (int i = 0; i < dim; i++)
                        {
                            for (int j = 0; j < dim; j++)
                            {
                                temp_y[i, j] = 0;
                                temp_u[i, j] = 0;
                                temp_v[i, j] = 0;

                                for (int k = 0; k < dim; k++)
                                {
                                    temp_y[i, j] += A[i, k] * input_y[k + m, j + n];
                                    temp_u[i, j] += A[i, k] * input_u[k + m, j + n];
                                    temp_v[i, j] += A[i, k] * input_v[k + m, j + n];
                                }
                            }
                        }
                        for (int i = 0; i < dim; i++)
                        {
                            for (int j = 0; j < dim; j++)
                            {
                                for (int k = 0; k < dim; k++)
                                {
                                    output_y[i + m, j + n] += temp_y[i, k] * A[k, j];
                                    output_u[i + m, j + n] += temp_u[i, k] * A[k, j];
                                    output_v[i + m, j + n] += temp_v[i, k] * A[k, j];
                                }
                                output_y[i + m, j + n] = output_y[i + m, j + n] / dim;
                                output_u[i + m, j + n] = output_u[i + m, j + n] / dim;
                                output_v[i + m, j + n] = output_v[i + m, j + n] / dim;
                            }
                        }
                    }
                }
            }
            /*以下為逆離散餘弦轉換(IDCT)*/
            public void IDCT(Double[,] in_y, Double[,] in_u, Double[,] in_v, Double[,] out_y, Double[,] out_u, Double[,] out_v)
            {
                Double[,] temp_y = new Double[dim, dim];
                Double[,] temp_u = new Double[dim, dim];
                Double[,] temp_v = new Double[dim, dim];
                double[,] A = new double[,]{ { 1, 1, 1, 1 },
                                         { 1, 1,-1,-1 },
                                         { 1,-1,-1, 1 },
                                         { 1,-1, 1,-1 } };

                for (int m = 0; m < new_h; m += dim)
                {
                    for (int n = 0; n < new_w; n += dim)
                    {
                        for (int i = 0; i < dim; i++)
                        {
                            for (int j = 0; j < dim; j++)
                            {

                                temp_y[i, j] = 0;
                                temp_u[i, j] = 0;
                                temp_v[i, j] = 0;

                                for (int k = 0; k < dim; k++)
                                {
                                    temp_y[i, j] += A[i, k] * in_y[k + m, j + n];
                                    temp_u[i, j] += A[i, k] * in_u[k + m, j + n];
                                    temp_v[i, j] += A[i, k] * in_v[k + m, j + n];
                                }
                            }
                        }

                        for (int i = 0; i < dim; i++)
                        {
                            for (int j = 0; j < dim; j++)
                            {
                                for (int k = 0; k < dim; k++)
                                {
                                    out_y[i + m, j + n] += temp_y[i, k] * A[k, j];
                                    out_u[i + m, j + n] += temp_u[i, k] * A[k, j];
                                    out_v[i + m, j + n] += temp_v[i, k] * A[k, j];
                                }
                                out_y[i + m, j + n] = out_y[i + m, j + n] / dim - 28;
                                out_u[i + m, j + n] = out_u[i + m, j + n] / dim - 38;
                                out_v[i + m, j + n] = out_v[i + m, j + n] / dim - 28;
                            }
                        }
                    }
                }
            }
            /*以下為量化*/
            public void quantized(Double[,] ten_y, Double[,] ten_u, Double[,] ten_v, int[,] tem_y, int[,] tem_u, int[,] tem_v)
            {
                int[,] uv_table = { {16,11,10,16,24,40,51,61 },
                                {12,12,14,19,26,58,60,55 },
                                {14,13,16,24,40,57,69,56 },
                                {14,17,22,29,51,87,80,62 },
                                {18,22,37,56,68,109,103,77 },
                                {24,35,55,64,81,104,113,92 },
                                {49,64,78,87,103,121,120,101 },
                                {72,92,95,98,112,100,103,99 },};

                int[,] y_table = { {17,18,24,47,99,99,99,99 },
                               {18,21,26,66,99,99,99,99 },
                               {24,26,56,99,99,99,99,99 },
                               {47,66,99,99,99,99,99,99 },
                               {99,99,99,99,99,99,99,99 },
                               {99,99,99,99,99,99,99,99 },
                               {99,99,99,99,99,99,99,99 },
                               {99,99,99,99,99,99,99,99 }};

                for (int m = 0; m < new_h; m += dim)
                {
                    for (int n = 0; n < new_w; n += dim)
                    {
                        for (int i = 0; i < dim; i++)
                        {
                            for (int j = 0; j < dim; j++)
                            {
                                tem_y[i + m, j + n] = (int)(ten_y[i + m, j + n] / y_table[i, j]);
                                tem_u[i + m, j + n] = (int)(ten_u[i + m, j + n] / uv_table[i, j]);
                                tem_v[i + m, j + n] = (int)(ten_v[i + m, j + n] / uv_table[i, j]);
                            }
                        }
                    }
                }
            }
            /*以下為zigzag編碼和解碼*/
            //Zigzag前的初始化(編碼解碼共用)
            void MatrixInit(int rows, int cols)
            {
                m_maxrow = rows - 1;
                m_maxcol = cols - 1;//記著Height,Width的最大最小值
                m_row = m_col = 0;//從位置[0,0]開始走
                m_dir = (int)zig_dir.UP;//指標指示最開始要往上(數字大的位置)走
            }
            //計算位置的副程式(編碼解碼共用)
            bool NextIdx()
            {
                if (m_row == m_maxrow && m_col == m_maxcol) return false;//走到最後，要停止
                if (m_dir == (int)zig_dir.UP)
                {
                    if (m_col == m_maxcol)
                    {
                        ++m_row;
                        m_dir = (int)zig_dir.DOWN;
                    }//遇到上邊界要折返
                    else if (m_row == 0)
                    {
                        ++m_col;
                        m_dir = (int)zig_dir.DOWN;
                    }//遇到左邊界要折返
                    else
                    {
                        --m_row;
                        ++m_col;
                    }
                }
                else
                {//m_dir==CMatrixIdx::DOWN
                    if (m_row == m_maxrow)
                    {
                        ++m_col;
                        m_dir = (int)zig_dir.UP;
                    }//遇到下邊界要折返
                    else if (m_col == 0)
                    {
                        ++m_row;
                        m_dir = (int)zig_dir.UP;
                    }//遇到右邊界要折返
                    else
                    {
                        ++m_row;
                        --m_col;
                    }
                }
                return true;
            }
            //zigzag編碼
            public void en_zigzag(int[,] y, int[,] u, int[,] v, int[] data_y, int[] data_u, int[] data_v)
            {
                int j = 0;
                for (int m = 0; m < new_h; m += dim)
                {
                    for (int n = 0; n < new_w; n += dim)
                    {
                        MatrixInit(dim, dim);
                        do
                        {
                            data_y[j] = y[m_row + m, m_col + n];
                            data_u[j] = u[m_row + m, m_col + n];
                            data_v[j] = v[m_row + m, m_col + n];
                            j++;
                        } while (NextIdx());
                    }
                }
            }
            //zigzag解碼
            public void de_zigzag(int[] data_y, int[] data_u, int[] data_v, int[,] y, int[,] u, int[,] v)
            {
                int j = 0;
                for (int m = 0; m < new_h; m += dim)
                {
                    for (int n = 0; n < new_w; n += dim)
                    {
                        MatrixInit(dim, dim);
                        do
                        {
                            y[m_row + m, m_col + n] = data_y[j];
                            u[m_row + m, m_col + n] = data_u[j];
                            v[m_row + m, m_col + n] = data_v[j];
                            j++;
                        } while (NextIdx());
                    }
                }
            }
            /*以下為RLE編碼*/
            public void encode_RLE(int[] input, int[] output)
            {
                int i, j = 0, count = 1;
                int inputlength = input.Length;
                for (i = 1; i < inputlength; i++)
                {
                    if (input[i] != input[(i - 1)])
                    {
                        output[j] = count;
                        output[(j + 1)] = input[(i - 1)];
                        count = 1;
                        j += 2;
                    }
                    else
                    {
                        count++;
                    }
                    //陣列最後一個數字個別判斷 
                    if (i == (inputlength - 1))
                    {
                        output[j] = count;
                        output[(j + 1)] = input[i];
                        j += 2;
                        output[j] = 0;
                    }
                }
                outputlength = j;
            }
            /*以下為RLE解碼*/
            public void decode_RLE(int[] input, int[] output)
            {
                int i, j = 0, count = 0;
                int inputlength = input.Length;                
                
                for (i = 0; i < inputlength; i += 2)
                {
                    do
                    {
                        output[j] = input[(i + 1)];
                        count++;
                        j++;
                    } while (count < input[i]);

                    count = 0;
                }
            }
            /*量化解碼*/
            public void D_quantized(int[,] tem_y, int[,] tem_u, int[,] tem_v, Double[,] ten_y, Double[,] ten_u, Double[,] ten_v)
            {
                //Dz_y zigzag解碼後的y
                //Dz_u zigzag解碼後的u
                //Dz_v zigzag解碼後的v
                int[,] uv_table = { {16,11,10,16,24,40,51,61 },
                        {12,12,14,19,26,58,60,55 },
                        {14,13,16,24,40,57,69,56 },
                        {14,17,22,29,51,87,80,62 },
                        {18,22,37,56,68,109,103,77 },
                        {24,35,55,64,81,104,113,92 },
                        {49,64,78,87,103,121,120,101 },
                        {72,92,95,98,112,100,103,99 }};

                int[,] y_table = { {17,18,24,47,99,99,99,99 },
                       {18,21,26,66,99,99,99,99 },
                       {24,26,56,99,99,99,99,99 },
                       {47,66,99,99,99,99,99,99 },
                       {99,99,99,99,99,99,99,99 },
                       {99,99,99,99,99,99,99,99 },
                       {99,99,99,99,99,99,99,99 },
                       {99,99,99,99,99,99,99,99 }};

                for (int m = 0; m < new_h; m += dim)
                {
                    for (int n = 0; n < new_w; n += dim)
                    {
                        for (int i = 0; i < dim; i++)
                        {
                            for (int j = 0; j < dim; j++)
                            {
                                ten_y[i + m, j + n] = tem_y[i + m, j + n] * y_table[i, j];
                                ten_u[i + m, j + n] = tem_u[i + m, j + n] * uv_table[i, j];
                                ten_v[i + m, j + n] = tem_v[i + m, j + n] * uv_table[i, j];
                            }
                        }
                    }
                }
            }
            //YUV->RGB
            public int[,,] yuv_rgb(Double[,] D_Y, Double[,] D_U, Double[,] D_V)
            {
                int[,,] rgbData = new int[Height, Width, 3];
                //D_Y是IDCT後的色度
                //D_U是IDCT後的亮度
                //D_V是IDCT後的飽和度
                for (int i = 0; i < Height; i++)
                {
                    for (int j = 0; j < Width; j++)
                    {
                        rgbData[i, j, 0] = (int)(D_Y[i, j] + 1.13983 * (D_V[i, j] - 128));
                        rgbData[i, j, 1] = (int)(D_Y[i, j] - 0.39465 * (D_U[i, j] - 128) - 0.58060 * (D_V[i, j] - 128));
                        rgbData[i, j, 2] = (int)(D_Y[i, j] + 2.03211 * (D_U[i, j] - 128));
                        //因為運算時小數點進位，讓數字未在0~255範圍內，超出皆視為0或255
                        if (rgbData[i, j, 2] < 0) rgbData[i, j, 2] = 0;
                        if (rgbData[i, j, 2] > 255) rgbData[i, j, 2] = 255;
                        if (rgbData[i, j, 1] < 0) rgbData[i, j, 1] = 0;
                        if (rgbData[i, j, 1] > 255) rgbData[i, j, 1] = 255;
                        if (rgbData[i, j, 0] < 0) rgbData[i, j, 0] = 0;
                        if (rgbData[i, j, 0] > 255) rgbData[i, j, 0] = 255;

                        bimage.SetPixel(j, i, Color.FromArgb(rgbData[i, j, 0], rgbData[i, j, 1], rgbData[i, j, 2]));
                    }
                }
                // Step 3: 更新顯示影像 
                image = bimage;                
                return rgbData;
            }            
            //陣列寫入
            public static int BinWrite(string fileName, int[] InData_y, int[] InData_u, int[] InData_v, int[] InData_I)
            {                
                byte[] bytes_y = InData_y.Select(x => (byte)x).ToArray();
                byte[] bytes_u = InData_u.Select(x => (byte)x).ToArray();
                byte[] bytes_v = InData_v.Select(x => (byte)x).ToArray();
                byte[] bytes_i = InData_I.Select(x => (byte)x).ToArray();               
                //串接陣列
                byte[] newArray = new byte[bytes_i.Length + bytes_y.Length + bytes_u.Length + bytes_v.Length];               
                Array.Copy(bytes_i, 0, newArray, 0, bytes_i.Length);
                Array.Copy(bytes_y, 0, newArray, bytes_i.Length, bytes_y.Length);                
                Array.Copy(bytes_u, 0, newArray, bytes_i.Length + bytes_y.Length, bytes_u.Length);
                Array.Copy(bytes_v, 0, newArray, bytes_i.Length + bytes_y.Length + bytes_u.Length, bytes_v.Length);
                                
                try
                {
                   FileStream myFile = new FileStream(fileName, FileMode.Create);
                   BinaryWriter bwStream = new BinaryWriter(myFile);
                   myFile.Close();
                   //開啟建立檔案

                   FileStream myFile1 = File.Open(fileName, FileMode.Open, FileAccess.ReadWrite);
                   BinaryWriter myWriter = new BinaryWriter(myFile1, Encoding.Default, true);

                   myWriter.Write(newArray);
                   myWriter.Close();
                   myFile1.Close();
                   return 1;

               }
               catch (InvalidCastException e)
               {
                   return -1;
               }
              
            }
            //陣列讀取
            public static int BinRead(string OpenFileName, ref int[] decode_y, ref int[] decode_u, ref int[] decode_v, int height, int width)
            {               
                try
                {
                    //開啟檔案
                    FileStream myFile = File.Open(OpenFileName, FileMode.Open, FileAccess.ReadWrite);
                    //引用myReader類別
                    BinaryReader myReader = new BinaryReader(myFile);
                    //取得陣列長度
                    int dl = Convert.ToInt32(myFile.Length);
                    //讀取位元陣列並轉成整數
                    byte[] InData = myReader.ReadBytes(dl);
                    int[] convertInt = InData.Select(x => (int)x).ToArray();
                    //判斷是否為負數，並用2的補數方式讀取                   
                    for (int n = 0; n < convertInt.Length; n ++)
                    {
                        if (convertInt[n] > 128)
                        {
                            convertInt[n] = convertInt[n] - 256;
                        }
                    }
                    //切割陣列
                    int i, k = 0, j = 1, length = convertInt[0];
                    string[] getlong = new string[5];

                    int[] decode_i = new int[length - 1];
                    for (i = 0; i < length - 1; i++)
                    {
                        decode_i[i] = convertInt[j];
                        j++;
                    }
                    
                    string search = string.Join("", decode_i);                    
                    for (i = 0; i < length - 1; i ++)
                    {                        
                        getlong[k] = search.Substring(i + 1, decode_i[i]);
                        k++;
                        i += decode_i[i];
                    }                    
                    
                    length = Convert.ToInt32(getlong[0]);
                    decode_y = new int[length];
                    for (i = 0; i < length; i++)
                    {
                        decode_y[i] = convertInt[j];
                        j++;
                    }
                    length = Convert.ToInt32(getlong[1]);
                    decode_u = new int[length];
                    for (i = 0; i < length; i++)
                    {
                        decode_u[i] = convertInt[j];
                        j++;
                    }
                    length = Convert.ToInt32(getlong[2]);
                    decode_v = new int[length];
                    for (i = 0; i < length; i++)
                    {
                        decode_v[i] = convertInt[j];
                        j++;
                    }

                    height = Convert.ToInt16(getlong[3]);
                    width = Convert.ToInt16(getlong[4]);
                    //釋放資源
                    myReader.Close();
                    myFile.Close();
                    return 1;
                }
                catch (InvalidCastException e)
                {
                    return -1;
                }                
            }

            public void doGray(int[,,] rgbData)
            {
                // Step 1: 建立 Bitmap 元件
                Bitmap bimage = new Bitmap(image);
                int Height = bimage.Height;
                int Width = bimage.Width;
                // Step 2: 設定像點資料
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        int gray = (rgbData[x, y, 0] + rgbData[x, y, 1] + rgbData[x, y, 2]) / 3;
                        bimage.SetPixel(x, y, Color.FromArgb(gray, gray, gray));
                    }
                }
                // Step 3: 更新顯示影像 
                image = bimage;
                this.Refresh();
            }
            //ScrollBar視窗滾動
            private void InitializeMyScrollBar()
            {
                VScrollBar vScrollBar1 = new VScrollBar();
                HScrollBar hScrollBar1 = new HScrollBar();
                vScrollBar1.Dock = DockStyle.Right;
                hScrollBar1.Dock = DockStyle.Bottom;
                Controls.Add(vScrollBar1);
                Controls.Add(hScrollBar1);
            }
            //顯示圖片
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.DrawImage(image, 0, 0, image.Width, image.Height);
            }
            //壓縮率
            public void Compress()
            {
                String x = Path.GetFileName(Filename);               
                long s1 = new FileInfo(x).Length;                
                long s2 = new FileInfo(new_filename).Length;
                MessageBox.Show(s2 / s1 * 100 + "%");
            }
        }
    }
}