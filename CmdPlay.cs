using FFMpegCore;
using ILGPU;
using ILGPU.Runtime;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
namespace CmdPlay
{



    class CmdPlay
    {

        const string brightnessLevels0 = " .-+*wGHM#&%@";

        private static readonly object Lock = new();
        private static readonly object LockBytes = new();

        static char[] GetCharArray(char[,] twoDArray, int a)
        {
            // Create a new array to store the first array (first row)
            char[] firstArray = new char[twoDArray.GetLength(1)];

            // Copy the elements of the first row to the new array
            for (int i = 0; i < twoDArray.GetLength(1); i++)
            {
                firstArray[i] = twoDArray[a, i];
            }

            return firstArray;
        }
        static string[] Get2DCharsToString(byte[,] bytes)
        {
            char[,] chars = new char[bytes.GetLength(0), bytes.GetLength(1)];
            string[] str = new string[bytes.GetLength(0)];
            Parallel.For (0, bytes.GetLength(0), i => {

                Parallel.For(0, bytes.GetLength(1), j => {
                    chars[i, j] = (char)bytes[i, j];
                });
                str[i] = new string(GetCharArray(chars, i));
            });
            return str;
        }
        static int[,] GetInt2dArray(int[,,] threeDArray, int a)
        {
            // Create a new array to store the first array (first row)
            int[,] firstArray = new int[threeDArray.GetLength(1), threeDArray.GetLength(2)];

            // Copy the elements of the first row to the new array
            for (int i = 0; i < threeDArray.GetLength(1); i++)
            {
                for (int j = 0; j < threeDArray.GetLength(2); j++)
                {
                    firstArray[i, j] = threeDArray[a-1, i, j];
                }
            }

            return firstArray;
        }
        public static byte[] GetBytes(byte[,] bytes, int a)
        {
            byte[] arr = new byte[bytes.GetLength(1)];

            for (int i = 0; i < bytes.GetLength(1); i++)
            {
                arr[i] = bytes[a, i];
            }
            return arr;
        }
        static void Kernel(Index3D i, ArrayView3D<int, Stride3D.DenseXY> dIndex, int w, ArrayView2D<byte, Stride2D.DenseY> framebuilder)
        {
            char[] brightness;
            brightness = new char[] { ' ', '.', '-', '+', '*', 'w', 'G', 'H', 'M', '#', '&', '%', '@' };
            if (i.X + 1 == w)
            {
                framebuilder[i.Z, w * (i.Y + 1) + i.Y] = (byte)'\n';
            }
            if (dIndex[i.Z, i.Y, i.X] < 0)
            {
                dIndex[i.Z, i.Y, i.X] = 0;
            }
            else if (dIndex[i.Z, i.Y, i.X] >= brightness.Length)
            {
                dIndex[i.Z, i.Y, i.X] = brightness.Length - 1;
            }
            framebuilder[i.Z, i.X + i.Y * (w + 1)] = (byte)brightness[dIndex[i.Z, i.Y, i.X] * 2];
        }
        /*
        struct FrameBytes
        {
            public byte[,] bytesarr { get; set; }
            public byte[] bytes { get; set; }
            public FrameBytes(int a, int b)
            {
                bytesarr = new byte[a, b];
                bytes = new byte[b];
            }
            public void SetBytesToArr(int a)
            {
                for (int i = 0; i < bytes.Length; i++)
                {
                    bytesarr[a, i] = bytes[i];
                }
            }
            public byte[] GetBytesFromArr(int a)
            {
                return GetBytes(bytesarr, a);
            }
        }*/
        static void Main(string[] args)
        {

            string inputFilename;
            /*Context context = Context.Create(builder => builder.Cuda());
            Accelerator accelerator = context.GetPreferredDevice(false).CreateAccelerator(context);*/
            Context context = Context.CreateDefault();
            Accelerator accelerator = context.GetPreferredDevice(false).CreateAccelerator(context);

            Console.WriteLine("Remember, Window size will affect the resolution of the video!!");
            Console.WriteLine("Choose if you are using high or low resolution, 1. low(suggested)  2.high:");
            Console.WriteLine("This is a gpu version for the program, you cant choose the high version!");

            //int choose = int.Parse(Console.ReadLine());
            int choose = 1;
            string brightnessLevels = brightnessLevels0;


            if (args.Length == 0)
            {
                Console.Write("Input File \"Path\" or name if its in the folder(you also have to write the extension down):");
                //inputFilename = Console.ReadLine().Replace("\"", "");
                inputFilename = "D:\\B.mp4";
            }
            else
            {
                inputFilename = args[0];
            }

            FileInfo file = new(Path.GetFullPath(inputFilename));

            FFOptions options = new();
            options.BinaryFolder = Path.GetDirectoryName("ffprobe.exe");

            var matadata = FFProbe.AnalyseAsync(file.FullName, options).Result;

            int vidW = matadata.VideoStreams[0].Width;
            int vidH = matadata.VideoStreams[0].Height;


            int targetFrameWidth = Console.WindowWidth - 1;
            int targetFrameHeight = Console.WindowHeight - 2;


            Console.WriteLine($"video resolution : {vidW} X {vidH}");

            double ratio = vidW / (double)vidH;


            targetFrameWidth = (int)Math.Round(targetFrameHeight * ratio * 2);

            Console.WriteLine($"your resolution: {targetFrameWidth} X {targetFrameHeight}");



            Console.WriteLine("------------------------------\n" +
                                "            Controls          \n" +
                                "      Space - Play / Pause    \n" +
                                "           Esc - Exit         \n" +
                                "------------------------------\n");
            ConsoleColor originalForegroundColor = Console.ForegroundColor; /* Preserve the old colours to print warning message */
            ConsoleColor originalBackgroundColor = Console.BackgroundColor;

            Console.ForegroundColor = ConsoleColor.Red;
            Console.BackgroundColor = ConsoleColor.Black; /* Contrast: Red on black */
            Console.WriteLine("NOTE: Do not resize the window starting from now! (Resize before program init)");
            Console.ForegroundColor = originalForegroundColor; /* Reset old colours */
            Console.BackgroundColor = originalBackgroundColor;

            Console.WriteLine("[INFO] Please wait.. Processing..");
            Console.WriteLine("[INFO] Step 1 / 4: Cleaning up...");
            Console.CursorVisible = false;

            if (Directory.Exists("tmp"))
            {
                if (Directory.Exists("tmp\\frames\\"))
                {
                    Directory.Delete("tmp\\frames\\", true);
                }
                Directory.CreateDirectory("tmp\\frames\\");
                if (File.Exists("tmp\\audio.wav"))
                {
                    File.Delete("tmp\\audio.wav");
                }
            }
            else
            {
                Directory.CreateDirectory("tmp\\");
                Directory.CreateDirectory("tmp\\frames\\");
            }

            Console.WriteLine("[INFO] Step 2 / 4: Extracting frames...");
            Process ffmpegProcess = new(); /* Launch ffmpeg process to extract the frames */
            ffmpegProcess.StartInfo.FileName = "ffmpeg.exe";
            ffmpegProcess.StartInfo.Arguments = "-i \"" + inputFilename + "\" -vf scale=" +
                                    targetFrameWidth + ":" + targetFrameHeight + " tmp\\frames\\%0d.bmp";

            ffmpegProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            ffmpegProcess.Start();
            Console.WriteLine("[INFO] Waiting for ffmpeg.exe to finish...");
            ffmpegProcess.WaitForExit();

            Console.WriteLine("[INFO] Step 3 / 4: Extracting audio...");
            ffmpegProcess = new Process();
            ffmpegProcess.StartInfo.FileName = "ffmpeg.exe";
            ffmpegProcess.StartInfo.Arguments = "-i \"" + inputFilename + "\" tmp\\audio.wav";
            ffmpegProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            ffmpegProcess.Start();
            Console.WriteLine("[INFO] Waiting for ffmpeg.exe to finish...");
            ffmpegProcess.WaitForExit();

            Console.WriteLine("[INFO] Step 4 / 4: Converting to ascii... (This can take some time!)");
            Console.Write("-> [PROGRESS] [0  %] [                    ]");
            int currentCursorHeight = Console.CursorTop;

            int frameCount = Directory.GetFiles("tmp\\frames", "*.bmp").Length;

            Bitmap[] b = new Bitmap[frameCount];
            Bitmap aa = new Bitmap("tmp\\frames\\" + 1 + ".bmp");
            int H = aa.Height;
            int W = aa.Width;
            string[] filename = new string[frameCount];
            string[] frames = new string[frameCount];
            byte[,] frameBuilder = new byte[frameCount, (H * W + H)];
            for (int a = 0; a < frameCount; a++)
            {
                frames[a] = "";
                for (int i = 0; i < H * W + H; i++)
                {
                    frameBuilder[a, i] = (byte)' ';
                }
            }
            //int frameIndex = 1;
            //int percentage;
            //FrameBytes frameBytes = new FrameBytes(frameCount, Encoding.ASCII.GetBytes(GetCharArray(frameBuilder, 1)).Length);
            int[,,] dIndex = new int[frameCount, targetFrameHeight, targetFrameWidth];

            Console.WriteLine(GetBytes(frameBuilder, 0).Length);

            Parallel.For(0, frameCount, a =>
            {

                filename[a] = "tmp\\frames\\" + (a + 1).ToString() + ".bmp";
                b[a] = new Bitmap(filename[a]);
                H = b[a].Height;
                W = b[a].Width;
                for (int y = 0; y < H; y++)
                {
                    for (int x = 0; x < W; x++)
                    {
                        dIndex[a, y, x] = (int)(b[a].GetPixel(x, y).GetBrightness() * brightnessLevels.Length);
                    }
                }
            });
            MemoryBuffer3D<int, Stride3D.DenseXY> d_Index = accelerator.Allocate3DDenseXY(dIndex);
            MemoryBuffer2D<byte, Stride2D.DenseY> d_framebuilder = accelerator.Allocate2DDenseY(frameBuilder);


            Action<Index3D, ArrayView3D<int, Stride3D.DenseXY>, int, ArrayView2D<byte, Stride2D.DenseY>> loadedKernel =
            accelerator.LoadAutoGroupedStreamKernel<Index3D, ArrayView3D<int, Stride3D.DenseXY>, int, ArrayView2D<byte, Stride2D.DenseY>>(Kernel);

            Index3D index3D = new Index3D(W, H, frameCount);

            loadedKernel(index3D, d_Index.View, W, d_framebuilder.View);
            accelerator.Synchronize();


            frames = Get2DCharsToString(d_framebuilder.GetAsArray2D());

            /*
            frameIndex++;
            percentage = (int)(frameIndex / (float)frameCount * 100);
            Console.SetCursorPosition(15, currentCursorHeight);
            Console.Write(percentage.ToString());
            Console.SetCursorPosition(21 + percentage / 5, currentCursorHeight);
            if (percentage % 5 == 0 && percentage != 0)
            {
                Console.Write("#");
            }*/


            AudioFileReader reader = new AudioFileReader("tmp\\audio.wav");
            WaveOutEvent woe = new WaveOutEvent();
            woe.Init(reader);
            Console.WriteLine("\n\nPress return to play!");
            Console.ReadLine();
            Console.Clear();
            woe.Play();

            while (true)
            {
                float Fpercentage = woe.GetPosition() / (float)reader.Length;
                int frame = (int)(Fpercentage * frameCount);
                if (frame >= frames.Length)
                    break;

                Console.SetCursorPosition(0, 0);
                Console.WriteLine(frames[frame]);

                if (Console.KeyAvailable)
                {
                    ConsoleKey pressed = Console.ReadKey().Key;
                    switch (pressed)
                    {
                        case ConsoleKey.Spacebar:
                            {
                                if (woe.PlaybackState == PlaybackState.Playing)
                                    woe.Pause();
                                else woe.Play();

                                break;
                            }
                        case ConsoleKey.Escape:
                            {
                                woe.Stop();
                                Console.WriteLine("Done. Press any key to close");
                                Console.ReadKey();
                                return;
                            }
                    }
                }
            }
            Console.WriteLine("Done. Press any key to close");
            Console.ReadKey();
        }

    }
}
