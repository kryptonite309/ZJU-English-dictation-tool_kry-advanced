using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace EnglishDictationTool
{
    internal static class V134Tests
    {
        public static int Run(string root)
        {
            Dictionary<string, object> report = new Dictionary<string, object>();
            try
            {
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "isolated-build.txt"), "v1.3.4 QA");
                string sourceVideo = Path.Combine(root, "test-source.avi");
                WriteTestAvi(sourceVideo, 192, 108, 18, 9);

                AppearanceStore appearance = new AppearanceStore(root);
                AppearanceSettings settings = appearance.CopySettings();
                BackgroundAppearance main = settings.backgrounds["main"];
                main.image = appearance.ImportVideo(sourceVideo);
                main.fit = "cover";
                main.shade = 15;
                main.panelTransparency = 63;
                appearance.Save(settings);
                AppearanceStore reloaded = new AppearanceStore(root);
                string imported = reloaded.ImagePath(reloaded.Settings.backgrounds["main"]);
                bool importOk = AppearanceStore.IsVideo(imported) && File.Exists(imported)
                    && reloaded.Settings.backgrounds["main"].panelTransparency == 63;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                int frames;
                int loops;
                using (Form form = new Form { ClientSize = new Size(640, 360),
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(-30000, -30000), ShowInTaskbar = false })
                using (Panel panel = new Panel { Dock = DockStyle.Fill })
                {
                    form.Controls.Add(panel);
                    form.Show();
                    Application.DoEvents();
                    VideoBackground.TestForeground = delegate { return form; };
                    using (VideoBackground video = new VideoBackground(panel, imported,
                        reloaded.Settings.backgrounds["main"], 10))
                    {
                        DateTime until = DateTime.UtcNow.AddSeconds(3.1d);
                        while (DateTime.UtcNow < until)
                        {
                            Application.DoEvents();
                            Thread.Sleep(16);
                        }
                        frames = video.Frames;
                        loops = video.Loops;
                        using (Bitmap sample = new Bitmap(panel.Width, panel.Height))
                        using (Graphics graphics = Graphics.FromImage(sample))
                        {
                            video.Draw(graphics, new Rectangle(Point.Empty, sample.Size));
                            sample.Save(Path.Combine(root, "video-frame.png"));
                        }
                    }
                    VideoBackground.TestForeground = null;
                    form.Close();
                }

                bool layoutOk = true;
                bool primaryActionOk = false;
                string dashboardState;
                using (Form form = new Form { ClientSize = new Size(900, 600),
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(-30000, -30000), ShowInTaskbar = false })
                using (HomeDashboardHost dashboard = new HomeDashboardHost { Dock = DockStyle.Fill })
                {
                    form.Controls.Add(dashboard);
                    int primarySignals = 0;
                    dashboard.PrimaryRequested += delegate { primarySignals++; };
                    dashboard.SetAppearance(reloaded);
                    dashboard.UpdateState(new[] { "0", "0", "0", "0" }, "free",
                        "04  FREE PRACTICE", "自由练习", "自行选择词书、单元、顺序和题型。",
                        "练习范围与题型可在设置中调整", "打开自由练习窗口", true,
                        string.Empty, false);
                    dashboard.SetPlaybackActive(true);
                    form.Show();
                    foreach (Size size in new[] { new Size(900, 600), new Size(1280, 820),
                        new Size(1100, 720), new Size(1600, 900), new Size(1920, 1080) })
                    {
                        form.ClientSize = size;
                        form.PerformLayout();
                        Application.DoEvents();
                        layoutOk = layoutOk && dashboard.Bounds == form.ClientRectangle;
                        if (size.Width == 1280 || size.Width == 1600 || size.Width == 1920)
                            dashboard.SaveSnapshot(Path.Combine(root,
                                "dashboard-" + size.Width + "x" + size.Height + ".png"));
                    }
                    DateTime until = DateTime.UtcNow.AddMilliseconds(600d);
                    while (DateTime.UtcNow < until)
                    {
                        Application.DoEvents();
                        Thread.Sleep(16);
                    }
                    dashboard.DebugActivatePrimary();
                    primaryActionOk = primarySignals == 1;
                    dashboardState = dashboard.DebugState;
                    form.Close();
                }

                bool ok = importOk && frames >= 3 && loops >= 1 && layoutOk
                    && primaryActionOk
                    && dashboardState.Contains("video=True")
                    && dashboardState.Contains("opened=True");
                report["ok"] = ok;
                report["videoImport"] = importOk;
                report["decodedFrames"] = frames;
                report["loops"] = loops;
                report["responsiveLayout"] = layoutOk;
                report["primaryAction"] = primaryActionOk;
                report["dashboard"] = dashboardState;
                File.WriteAllText(Path.Combine(root, "v134-report.json"),
                    new JavaScriptSerializer().Serialize(report), new UTF8Encoding(false));
                return ok ? 0 : 2;
            }
            catch (Exception error)
            {
                VideoBackground.TestForeground = null;
                report["ok"] = false;
                report["error"] = error.ToString();
                File.WriteAllText(Path.Combine(root, "v134-report.json"),
                    new JavaScriptSerializer().Serialize(report), new UTF8Encoding(false));
                return 1;
            }
        }

        private static void WriteFourCC(BinaryWriter writer, string value)
        {
            writer.Write(Encoding.ASCII.GetBytes(value));
        }

        private static long StartChunk(BinaryWriter writer, string id)
        {
            WriteFourCC(writer, id);
            long sizePosition = writer.BaseStream.Position;
            writer.Write(0);
            return sizePosition;
        }

        private static void EndChunk(BinaryWriter writer, long sizePosition)
        {
            long end = writer.BaseStream.Position;
            int size = checked((int)(end - sizePosition - 4));
            writer.BaseStream.Position = sizePosition;
            writer.Write(size);
            writer.BaseStream.Position = end;
            if ((size & 1) != 0) writer.Write((byte)0);
        }

        private static void WriteTestAvi(string path, int width, int height,
            int frameCount, int framesPerSecond)
        {
            int stride = (width * 3 + 3) & ~3;
            int frameBytes = stride * height;
            using (BinaryWriter writer = new BinaryWriter(File.Create(path)))
            {
                long riff = StartChunk(writer, "RIFF");
                WriteFourCC(writer, "AVI ");
                long headerList = StartChunk(writer, "LIST");
                WriteFourCC(writer, "hdrl");
                long aviHeader = StartChunk(writer, "avih");
                writer.Write(1000000 / framesPerSecond);
                writer.Write(frameBytes * framesPerSecond);
                writer.Write(0);
                writer.Write(0);
                writer.Write(frameCount);
                writer.Write(0);
                writer.Write(1);
                writer.Write(frameBytes);
                writer.Write(width);
                writer.Write(height);
                for (int index = 0; index < 4; index++) writer.Write(0);
                EndChunk(writer, aviHeader);

                long streamList = StartChunk(writer, "LIST");
                WriteFourCC(writer, "strl");
                long streamHeader = StartChunk(writer, "strh");
                WriteFourCC(writer, "vids");
                WriteFourCC(writer, "DIB ");
                writer.Write(0);
                writer.Write((short)0);
                writer.Write((short)0);
                writer.Write(0);
                writer.Write(1);
                writer.Write(framesPerSecond);
                writer.Write(0);
                writer.Write(frameCount);
                writer.Write(frameBytes);
                writer.Write(-1);
                writer.Write(0);
                writer.Write((short)0);
                writer.Write((short)0);
                writer.Write((short)width);
                writer.Write((short)height);
                EndChunk(writer, streamHeader);

                long format = StartChunk(writer, "strf");
                writer.Write(40);
                writer.Write(width);
                writer.Write(height);
                writer.Write((short)1);
                writer.Write((short)24);
                writer.Write(0);
                writer.Write(frameBytes);
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
                EndChunk(writer, format);
                EndChunk(writer, streamList);
                EndChunk(writer, headerList);

                long movieList = StartChunk(writer, "LIST");
                WriteFourCC(writer, "movi");
                byte[] frame = new byte[frameBytes];
                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        int row = y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            int offset = row + x * 3;
                            frame[offset] = (byte)((x + frameIndex * 15) % 256);
                            frame[offset + 1] = (byte)((y * 2 + frameIndex * 9) % 256);
                            frame[offset + 2] = (byte)((40 + frameIndex * 12) % 256);
                        }
                    }
                    long frameChunk = StartChunk(writer, "00db");
                    writer.Write(frame);
                    EndChunk(writer, frameChunk);
                }
                EndChunk(writer, movieList);
                EndChunk(writer, riff);
            }
        }
    }
}
