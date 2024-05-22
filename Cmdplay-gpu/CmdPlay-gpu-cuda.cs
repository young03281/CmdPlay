using FFMpegCore;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using NAudio.Wave;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
namespace CmdPlay
{



    public class CmdPlay
    {

        const string brightnessLevels0 = " .-+*wGHM#&%@";
        static char[] GetCharArray(char[,] twoDArray, int a)
        {
            char[] firstArray = new char[twoDArray.GetLength(1)];

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
            Parallel.For(0, bytes.GetLength(0), i =>
            {

                Parallel.For(0, bytes.GetLength(1), j =>
                {
                    chars[i, j] = (char)bytes[i, j];
                });
                str[i] = new string(GetCharArray(chars, i));
            });
            return str;
        }
        static void Kernel(Index3D i, ArrayView3D<int, Stride3D.DenseXY> dIndex, int w, ArrayView2D<byte, Stride2D.DenseY> framebuilder, ArrayView2D<byte, Stride2D.DenseY> bytes)
        {
            float R = (float)(int)bytes[i.Z, (i.Y * (w) + i.X) * 4] / 255f;
            float G = (float)(int)bytes[i.Z, (i.Y * (w) + i.X) * 4 + 1] / 255f;
            float B = (float)(int)bytes[i.Z, (i.Y * (w) + i.X) * 4 + 2] / 255f;
            char[] brightnessstr;
            brightnessstr = new char[] { ' ', '.', '-', '+', '*', 'w', 'G', 'H', 'M', '#', '&', '%', '@' };
            float brightnessLevel = (float)(0.2126 * R + 0.7152 * G + 0.0722 * B);
            dIndex[i.Z, i.Y, i.X] = (int)(brightnessLevel * brightnessstr.Length * 2);

            if (i.X + 1 == w)
            {
                framebuilder[i.Z, w * (i.Y + 1) + i.Y] = (byte)'\n';
            }
            if (dIndex[i.Z, i.Y, i.X] >= brightnessstr.Length)
            {
                dIndex[i.Z, i.Y, i.X] = brightnessstr.Length - 1;
            }
            framebuilder[i.Z, i.X + i.Y * (w + 1)] = (byte)brightnessstr[dIndex[i.Z, i.Y, i.X] * 2];
        }
        public static void Main_cuda(string[] args)
        {

            string inputFilename;
            Context context = Context.Create(builder => builder.Cuda());
            Accelerator accelerator = context.GetPreferredDevice(false).CreateAccelerator(context);

            Console.WriteLine("Remember, Window size will affect the resolution of the video!!");
            Console.WriteLine("Choose if you are using high or low resolution, 1. low(suggested)  2.high:");
            Console.WriteLine("This is a gpu version for the program, you cant choose the high version!");


            if (args.Length == 0)
            {
                Console.Write("Input File \"Path\" or name if its in the folder(you also have to write the extension down):");
                inputFilename = Console.ReadLine().Replace("\"", "");
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
            int currentCursorHeight = Console.CursorTop;
            var watch = Stopwatch.StartNew();

            int frameCount = Directory.GetFiles("tmp\\frames", "*.bmp").Length;

            Bitmap aa = new Bitmap("tmp\\frames\\" + 1 + ".bmp");
            int H = aa.Height;
            int W = aa.Width;
            string[] frames = new string[frameCount];
            BitmapData[] bitmapData = new BitmapData[frameCount];
            byte[,] bitmapbytes = new byte[frameCount, H * W * 4];
            Rectangle rect = new Rectangle(0, 0, W, H);

            Parallel.For(0, frameCount, a =>
            {

                bitmapData[a] = new Bitmap("tmp\\frames\\" + (a + 1).ToString() + ".bmp").LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppRgb);
                int size = W * H * 4;
                byte[] bitmapbyte = new byte[size];
                Marshal.Copy(bitmapData[a].Scan0, bitmapbyte, 0, size);
                Parallel.For(0, size, b =>
                {
                    bitmapbytes[a, b] = bitmapbyte[b];
                    bitmapbyte[b] = 0;
                });
                bitmapData[a] = null;
            });
            bitmapData = null;
            GC.Collect();

            Console.WriteLine("finish index");
            MemoryBuffer3D<int, Stride3D.DenseXY> d_Index = accelerator.Allocate3DDenseXY(new int[frameCount, targetFrameHeight, targetFrameWidth]);
            MemoryBuffer2D<byte, Stride2D.DenseY> d_framebuilder = accelerator.Allocate2DDenseY(new byte[frameCount, (H * W + H)]);
            MemoryBuffer2D<byte, Stride2D.DenseY> d_bitmapbyte = accelerator.Allocate2DDenseY(bitmapbytes);
            bitmapbytes = null;
            GC.Collect();

            Action<Index3D, ArrayView3D<int, Stride3D.DenseXY>, int, ArrayView2D<byte, Stride2D.DenseY>, ArrayView2D<byte, Stride2D.DenseY>> loadedKernel =
            accelerator.LoadAutoGroupedStreamKernel<Index3D, ArrayView3D<int, Stride3D.DenseXY>, int, ArrayView2D<byte, Stride2D.DenseY>, ArrayView2D<byte, Stride2D.DenseY>>(Kernel);

            loadedKernel((W, H, frameCount), d_Index.View, targetFrameWidth, d_framebuilder.View, d_bitmapbyte.View);
            accelerator.Synchronize();
            d_Index.Dispose();
            GC.Collect();
            Console.WriteLine("GPU finished");
            Console.WriteLine("converting bytes to char...");

            frames = Get2DCharsToString(d_framebuilder.GetAsArray2D());
            d_framebuilder.Dispose();
            d_bitmapbyte.Dispose();
            GC.Collect();
            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            Console.WriteLine(elapsedMs.ToString());
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
                GC.Collect();
            }
            Console.WriteLine("Done. Press any key to close");
            Console.ReadKey();
        }

    }
}
