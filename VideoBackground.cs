using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MediaPlayer = System.Windows.Media.MediaPlayer;
using DrawingVisual = System.Windows.Media.DrawingVisual;
using DrawingContext = System.Windows.Media.DrawingContext;
using PixelFormats = System.Windows.Media.PixelFormats;
using System.Windows.Media.Imaging;

namespace EnglishDictationTool
{
    // Lightweight video frames for the owner-drawn learning canvases.  The main
    // page uses direct WPF composition (HomeDashboard) and never passes through
    // this bitmap bridge.  Only the foreground window decodes frames.
    internal sealed class VideoBackground : IDisposable
    {
        private static readonly List<VideoBackground> instances = new List<VideoBackground>();
        private static readonly Timer clock = new Timer { Interval = 50 };
        internal static Func<Form> TestForeground;
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();

        private readonly Control owner;
        private readonly string path;
        private readonly BackgroundAppearance style;
        private readonly int priority;
        private MediaPlayer player;
        private Bitmap frame;
        private bool playing;
        private bool failed;
        private bool opened;
        private bool disposed;
        private string error;
        private int openingTicks;

        public int Frames { get; private set; }
        public int Loops { get; private set; }
        public bool Playing { get { return playing; } }
        public string Error { get { return error; } }

        static VideoBackground()
        {
            clock.Tick += delegate { TickAll(); };
        }

        public VideoBackground(Control owner, string path,
            BackgroundAppearance style, int priority)
        {
            this.owner = owner;
            this.path = path;
            this.style = style;
            this.priority = priority;
            instances.Add(this);
            owner.Disposed += OwnerDisposed;
            clock.Start();
        }

        private void OwnerDisposed(object sender, EventArgs e) { Dispose(); }

        private static void TickAll()
        {
            IntPtr foreground = GetForegroundWindow();
            Form active = Application.OpenForms.Cast<Form>().FirstOrDefault(
                form => form.Visible && form.WindowState != FormWindowState.Minimized
                    && form.Handle == foreground);
            if (TestForeground != null) active = TestForeground();
            VideoBackground winner = instances.Where(video => !video.disposed
                    && !video.failed && video.owner.Visible && active != null
                    && video.owner.FindForm() == active)
                .OrderBy(video => video.priority).LastOrDefault();

            foreach (VideoBackground video in instances.ToArray())
            {
                try { video.Tick(video == winner); }
                catch (Exception exception) { video.Fail(exception.Message); }
            }
        }

        private void EnsurePlayer()
        {
            if (player != null) return;
            player = new MediaPlayer { Volume = 0d, IsMuted = true, ScrubbingEnabled = true };
            player.MediaOpened += delegate
            {
                if (player == null || player.NaturalVideoWidth < 1)
                {
                    Fail("视频中没有可播放的画面。");
                    return;
                }
                opened = true;
                openingTicks = 0;
            };
            player.MediaFailed += delegate(object sender,
                System.Windows.Media.ExceptionEventArgs args)
            {
                Fail(args.ErrorException == null ? "解码失败" : args.ErrorException.Message);
            };
            player.MediaEnded += delegate
            {
                if (player == null) return;
                Loops++;
                player.Position = TimeSpan.Zero;
                if (playing) player.Play();
            };
            player.Open(new Uri(path));
        }

        private void Tick(bool run)
        {
            if (disposed || failed) return;
            if (!run)
            {
                if (playing && player != null) player.Pause();
                playing = false;
                return;
            }

            EnsurePlayer();
            if (!playing)
            {
                player.Play();
                playing = true;
            }
            if (!opened)
            {
                if (++openingTicks > 200) Fail("视频解码等待超时。");
                return;
            }

            // Decode a bounded frame.  The owner is double buffered and only its
            // own surface is invalidated; no child tree or whole window repaint is
            // requested, which is the key difference from the abandoned v1.3.3.
            int naturalWidth = Math.Max(1, player.NaturalVideoWidth);
            int naturalHeight = Math.Max(1, player.NaturalVideoHeight);
            int requestedWidth = Math.Max(320, Math.Min(1280, owner.ClientSize.Width));
            double scale = Math.Min(1d, (double)requestedWidth / naturalWidth);
            int width = Math.Max(1, (int)Math.Round(naturalWidth * scale));
            int height = Math.Max(1, (int)Math.Round(naturalHeight * scale));
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext drawing = visual.RenderOpen())
                drawing.DrawVideo(player, new System.Windows.Rect(0, 0, width, height));
            RenderTargetBitmap rendered = new RenderTargetBitmap(width, height,
                96d, 96d, PixelFormats.Pbgra32);
            rendered.Render(visual);

            if (frame == null || frame.Width != width || frame.Height != height)
            {
                if (frame != null) frame.Dispose();
                frame = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            }
            BitmapData data = frame.LockBits(new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try
            {
                rendered.CopyPixels(System.Windows.Int32Rect.Empty, data.Scan0,
                    data.Stride * height, data.Stride);
            }
            finally { frame.UnlockBits(data); }
            Frames++;
            owner.Invalidate();
        }

        private void Fail(string message)
        {
            failed = true;
            playing = false;
            error = "视频背景无法播放，请换用 H.264 MP4 或 WMV。";
            if (player != null) { player.Close(); player = null; }
            owner.Invalidate();
        }

        public void Draw(Graphics graphics, Rectangle bounds)
        {
            if (frame != null)
            {
                Rectangle target = Fit(bounds, frame.Size, style.fit);
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                graphics.DrawImage(frame, target);
                using (Brush shade = new SolidBrush(Color.FromArgb(
                    style.shade * 255 / 100, 0, 0, 0)))
                    graphics.FillRectangle(shade, bounds);
            }
            if (failed)
                TextRenderer.DrawText(graphics, error, SystemFonts.MessageBoxFont,
                    new Rectangle(bounds.X + 12, bounds.Bottom - 55,
                        Math.Max(1, bounds.Width - 24), 50), Color.White,
                    Color.FromArgb(35, 35, 35), TextFormatFlags.WordBreak);
        }

        private static Rectangle Fit(Rectangle bounds, Size source, string mode)
        {
            if (mode == "stretch") return bounds;
            if (mode == "center")
                return new Rectangle(bounds.X + (bounds.Width - source.Width) / 2,
                    bounds.Y + (bounds.Height - source.Height) / 2,
                    source.Width, source.Height);
            float scale = mode == "contain"
                ? Math.Min((float)bounds.Width / source.Width,
                    (float)bounds.Height / source.Height)
                : Math.Max((float)bounds.Width / source.Width,
                    (float)bounds.Height / source.Height);
            int width = (int)Math.Ceiling(source.Width * scale);
            int height = (int)Math.Ceiling(source.Height * scale);
            return new Rectangle(bounds.X + (bounds.Width - width) / 2,
                bounds.Y + (bounds.Height - height) / 2, width, height);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            instances.Remove(this);
            owner.Disposed -= OwnerDisposed;
            if (player != null) { player.Close(); player = null; }
            if (frame != null) { frame.Dispose(); frame = null; }
            if (instances.Count == 0) clock.Stop();
        }
    }
}
