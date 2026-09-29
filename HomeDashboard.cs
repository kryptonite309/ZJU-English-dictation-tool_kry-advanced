using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Forms.Integration;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfColors = System.Windows.Media.Colors;
using WpfImage = System.Windows.Media.ImageSource;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;

namespace EnglishDictationTool
{
    // A single WPF composition surface for the complete home page.  Video,
    // translucent panels and opaque text are composed by WPF in one pass, so
    // resizing and wheel animation never trigger recursive WinForms repaints.
    internal sealed class HomeDashboardHost : ElementHost
    {
        private readonly HomeDashboardView dashboard;

        public event Action<string> ModeRequested;
        public event Action PrimaryRequested;
        public event Action SecondaryRequested;
        public event Action SettingsRequested;
        public event Action HelpPageRequested;
        public event Action GithubRequested;
        public event Action EmailRequested;
        public event Action WheelUsed;

        public HomeDashboardHost()
        {
            dashboard = new HomeDashboardView();
            Child = dashboard;
            dashboard.ModeRequested += delegate(string key)
            {
                if (ModeRequested != null) ModeRequested(key);
            };
            dashboard.PrimaryRequested += delegate
            {
                if (PrimaryRequested != null) PrimaryRequested();
            };
            dashboard.SecondaryRequested += delegate
            {
                if (SecondaryRequested != null) SecondaryRequested();
            };
            dashboard.SettingsRequested += delegate
            {
                if (SettingsRequested != null) SettingsRequested();
            };
            dashboard.HelpRequested += delegate
            {
                if (HelpPageRequested != null) HelpPageRequested();
            };
            dashboard.GithubRequested += delegate
            {
                if (GithubRequested != null) GithubRequested();
            };
            dashboard.EmailRequested += delegate
            {
                if (EmailRequested != null) EmailRequested();
            };
            dashboard.WheelUsed += delegate
            {
                if (WheelUsed != null) WheelUsed();
            };
        }

        public void SetAppearance(AppearanceStore appearance)
        {
            dashboard.SetAppearance(appearance, "main");
        }

        public void SetPlaybackActive(bool active)
        {
            dashboard.SetPlaybackActive(active && Visible);
        }

        public string DebugState { get { return dashboard.DebugState; } }

        public System.Drawing.Rectangle GuideTargetScreen(string key)
        {
            WpfRect bounds = dashboard.GuideBounds(key);
            if (bounds.IsEmpty || !dashboard.IsVisible) return System.Drawing.Rectangle.Empty;
            try
            {
                WpfPoint topLeft = dashboard.PointToScreen(bounds.TopLeft);
                WpfPoint bottomRight = dashboard.PointToScreen(bounds.BottomRight);
                return System.Drawing.Rectangle.FromLTRB(
                    (int)Math.Floor(topLeft.X), (int)Math.Floor(topLeft.Y),
                    (int)Math.Ceiling(bottomRight.X), (int)Math.Ceiling(bottomRight.Y));
            }
            catch { return System.Drawing.Rectangle.Empty; }
        }

        public void SaveSnapshot(string path)
        {
            dashboard.SaveSnapshot(path);
        }

        internal void DebugActivatePrimary()
        {
            dashboard.DebugActivatePrimary();
        }

        public void UpdateState(string[] metrics, string selectedKey,
            string eyebrow, string title, string description, string status,
            string primaryText, bool primaryEnabled, string secondaryText,
            bool secondaryVisible)
        {
            dashboard.UpdateState(metrics, selectedKey, eyebrow, title,
                description, status, primaryText, primaryEnabled,
                secondaryText, secondaryVisible);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) dashboard.Dispose();
            base.Dispose(disposing);
        }
    }

    internal sealed class HomeDashboardView : FrameworkElement, IDisposable
    {
        private readonly string[] keys = { "new", "list_review", "problem_review", "free" };
        private readonly string[] names = { "新学", "历史列表复习", "错题与易错词复习", "自由练习" };
        private readonly DispatcherTimer motionTimer;
        private readonly Typeface regular = new Typeface(new FontFamily("Segoe UI"),
            FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        private readonly Typeface semibold = new Typeface(new FontFamily("Segoe UI"),
            FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        private readonly Typeface bold = new Typeface(new FontFamily("Segoe UI"),
            FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private readonly WpfBrush white = Freeze(new SolidColorBrush(WpfColor.FromRgb(246, 247, 249)));
        private readonly WpfBrush muted = Freeze(new SolidColorBrush(WpfColor.FromRgb(220, 225, 233)));
        private readonly WpfBrush soft = Freeze(new SolidColorBrush(WpfColor.FromRgb(196, 204, 216)));
        private readonly WpfBrush border = Freeze(new SolidColorBrush(WpfColor.FromArgb(54, 148, 160, 178)));
        private readonly WpfBrush focus = Freeze(new SolidColorBrush(WpfColor.FromArgb(42, 225, 232, 244)));
        private readonly WpfBrush primary = Freeze(new SolidColorBrush(WpfColor.FromRgb(246, 247, 249)));
        private readonly WpfBrush primaryText = Freeze(new SolidColorBrush(WpfColor.FromRgb(13, 16, 21)));

        private AppearanceStore appearance;
        private BackgroundAppearance background;
        private BitmapImage image;
        private System.Windows.Media.MediaPlayer player;
        private bool mediaOpened;
        private bool playbackActive;
        private bool disposed;
        private int selectedIndex;
        private double wheelOffset;
        private DateTime lastWheel = DateTime.MinValue;
        private string[] metrics = { "0", "0", "0", "0" };
        private string eyebrow = "01  NEW WORDS";
        private string heading = "学习新词";
        private string description = "按词书抽取，先看后练。";
        private string status = "完成展示后进入所选题型练习";
        private string primaryLabel = "开始新学";
        private bool primaryEnabled = true;
        private string secondaryLabel = "结束列表";
        private bool secondaryVisible;
        private WpfRect helpHit, settingsHit, githubHit, emailHit;
        private WpfRect primaryHit, secondaryHit;
        private readonly WpfRect[] modeHits = new WpfRect[4];
        private WpfRect guideWheelHit;
        private string hover = string.Empty;
        private string pressedHit = string.Empty;
        private string lastLayout = "not-rendered";

        public event Action<string> ModeRequested;
        public event Action PrimaryRequested;
        public event Action SecondaryRequested;
        public event Action SettingsRequested;
        public event Action HelpRequested;
        public event Action GithubRequested;
        public event Action EmailRequested;
        public event Action WheelUsed;

        public WpfRect GuideBounds(string key)
        {
            if (string.Equals(key, "mode_wheel", StringComparison.Ordinal))
                return guideWheelHit;
            if (string.Equals(key, "settings", StringComparison.Ordinal))
                return settingsHit;
            return WpfRect.Empty;
        }

        public string DebugState
        {
            get
            {
                string path = appearance == null || background == null
                    ? null : appearance.ImagePath(background);
                return "path=" + (path ?? "null") + "; image=" + (image != null)
                    + "; video=" + (player != null) + "; opened=" + mediaOpened
                    + "; active=" + playbackActive + "; layout=" + lastLayout;
            }
        }

        public void SaveSnapshot(string path)
        {
            if (string.IsNullOrEmpty(path) || ActualWidth < 1d || ActualHeight < 1d) return;
            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            int width = Math.Max(1, (int)Math.Round(ActualWidth * dpi.DpiScaleX));
            int height = Math.Max(1, (int)Math.Round(ActualHeight * dpi.DpiScaleY));
            RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height,
                96d * dpi.DpiScaleX, 96d * dpi.DpiScaleY, PixelFormats.Pbgra32);
            bitmap.Render(this);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(path)) encoder.Save(stream);
        }

        public HomeDashboardView()
        {
            Focusable = true;
            SnapsToDevicePixels = true;
            motionTimer = new DispatcherTimer(DispatcherPriority.Render)
            { Interval = TimeSpan.FromMilliseconds(16) };
            motionTimer.Tick += delegate
            {
                wheelOffset *= .78d;
                if (Math.Abs(wheelOffset) < .004d)
                {
                    wheelOffset = 0d;
                    motionTimer.Stop();
                }
                InvalidateVisual();
            };
            MouseWheel += OnWheel;
            MouseMove += OnPointerMove;
            MouseLeave += delegate { hover = string.Empty; Cursor = Cursors.Arrow; InvalidateVisual(); };
            MouseLeftButtonDown += OnPointerDown;
            MouseLeftButtonUp += OnPointerUp;
            LostMouseCapture += delegate
            {
                pressedHit = string.Empty;
                InvalidateVisual();
            };
            KeyDown += OnKeyPressed;
        }

        private static WpfBrush Freeze(SolidColorBrush brush)
        {
            brush.Freeze();
            return brush;
        }

        public void SetAppearance(AppearanceStore store, string key)
        {
            appearance = store;
            background = null;
            if (appearance != null) appearance.Settings.backgrounds.TryGetValue(key, out background);
            CloseMedia();
            image = null;
            string path = appearance == null || background == null
                ? null : appearance.ImagePath(background);
            if (AppearanceStore.IsVideo(path))
            {
                player = new System.Windows.Media.MediaPlayer
                { Volume = 0d, IsMuted = true, ScrubbingEnabled = true };
                player.MediaOpened += delegate
                {
                    mediaOpened = true;
                    InvalidateVisual();
                    if (playbackActive) player.Play();
                };
                player.MediaEnded += delegate
                {
                    if (player == null) return;
                    player.Position = TimeSpan.Zero;
                    if (playbackActive) player.Play();
                };
                player.MediaFailed += delegate { mediaOpened = false; InvalidateVisual(); };
                player.Open(new Uri(path));
            }
            else if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    BitmapImage loaded = new BitmapImage();
                    loaded.BeginInit();
                    loaded.CacheOption = BitmapCacheOption.OnLoad;
                    loaded.UriSource = new Uri(path);
                    loaded.EndInit();
                    loaded.Freeze();
                    image = loaded;
                }
                catch { image = null; }
            }
            InvalidateVisual();
        }

        public void SetPlaybackActive(bool active)
        {
            playbackActive = active;
            if (player == null || !mediaOpened) return;
            if (active) player.Play(); else player.Pause();
        }

        public void UpdateState(string[] values, string selectedKey,
            string eyebrow, string heading, string description, string status,
            string primaryLabel, bool primaryEnabled, string secondaryLabel,
            bool secondaryVisible)
        {
            if (values != null && values.Length >= 4) metrics = (string[])values.Clone();
            int index = Array.IndexOf(keys, selectedKey);
            if (index >= 0) selectedIndex = index;
            this.eyebrow = eyebrow ?? string.Empty;
            this.heading = heading ?? string.Empty;
            this.description = description ?? string.Empty;
            this.status = status ?? string.Empty;
            this.primaryLabel = primaryLabel ?? string.Empty;
            this.primaryEnabled = primaryEnabled;
            this.secondaryLabel = secondaryLabel ?? string.Empty;
            this.secondaryVisible = secondaryVisible;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double width = Math.Max(1d, ActualWidth);
            double height = Math.Max(1d, ActualHeight);
            DrawBackground(dc, new WpfRect(0, 0, width, height));

            // ElementHost reports WPF device-independent units, while the v1.3.2
            // WinForms home page was laid out in physical client pixels.  Rebuild
            // the old three-tier layout in physical pixels, then convert every
            // coordinate back to DIPs.  This prevents 125%/150% display scaling
            // from changing the established positions and proportions.
            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            double dpiX = Math.Max(.1d, dpi.DpiScaleX);
            double dpiY = Math.Max(.1d, dpi.DpiScaleY);
            double widthPx = width * dpiX;
            double heightPx = height * dpiY;
            int layoutClass = widthPx >= 1700d ? 2 : widthPx >= 1200d ? 1 : 0;
            double headerHeightPx = layoutClass == 2 ? 220d : layoutClass == 1 ? 188d : 165d;
            double metricsHeightPx = layoutClass == 2 ? 176d : layoutClass == 1 ? 158d : 146d;
            double outerXPx = layoutClass == 2 ? 36d : layoutClass == 1 ? 30d : 28d;
            double topPx = layoutClass == 2 ? 24d : layoutClass == 1 ? 16d : 14d;
            double bottomPx = layoutClass == 2 ? 30d : 26d;
            double shellWidthPx = Math.Max(100d, widthPx - outerXPx * 2d);

            DrawHeader(dc, outerXPx, topPx, shellWidthPx, headerHeightPx,
                widthPx, layoutClass, dpiX, dpiY);

            double metricsCellYPx = topPx + headerHeightPx;
            WpfRect metricCard = PixelRect(outerXPx + 5d, metricsCellYPx,
                Math.Max(100d, shellWidthPx - 10d),
                Math.Max(40d, metricsHeightPx - 10d), dpiX, dpiY);
            DrawPanel(dc, metricCard, PixelX(22d, dpiX));
            DrawMetrics(dc, outerXPx + 5d, metricsCellYPx,
                Math.Max(100d, shellWidthPx - 10d),
                Math.Max(40d, metricsHeightPx - 10d), layoutClass, dpiX, dpiY);

            double modeCellYPx = metricsCellYPx + metricsHeightPx;
            double modeCellHeightPx = Math.Max(220d,
                heightPx - topPx - bottomPx - headerHeightPx - metricsHeightPx);
            double modeCardXPx = outerXPx + 5d;
            double modeCardYPx = modeCellYPx + 6d;
            double modeCardWidthPx = Math.Max(100d, shellWidthPx - 10d);
            double modeCardHeightPx = Math.Max(214d, modeCellHeightPx - 6d);
            lastLayout = string.Format(CultureInfo.InvariantCulture,
                "class={0}; px={1:0}x{2:0}; dpi={3:0.##}; header=({4:0},{5:0},{6:0},{7:0}); metrics=({8:0},{9:0},{10:0},{11:0}); mode=({12:0},{13:0},{14:0},{15:0})",
                layoutClass, widthPx, heightPx, dpiX,
                outerXPx, topPx, shellWidthPx, headerHeightPx,
                outerXPx + 5d, metricsCellYPx, shellWidthPx - 10d,
                metricsHeightPx - 10d, modeCardXPx, modeCardYPx,
                modeCardWidthPx, modeCardHeightPx);
            WpfRect modeCard = PixelRect(modeCardXPx, modeCardYPx,
                modeCardWidthPx, modeCardHeightPx, dpiX, dpiY);
            DrawPanel(dc, modeCard, PixelX(22d, dpiX));
            DrawMode(dc, modeCardXPx, modeCardYPx, modeCardWidthPx,
                modeCardHeightPx, widthPx, layoutClass, dpiX, dpiY);
        }

        private void DrawBackground(DrawingContext dc, WpfRect bounds)
        {
            WpfColor color = WpfColor.FromRgb(11, 13, 16);
            if (background != null)
            {
                System.Drawing.Color source = System.Drawing.Color.FromArgb(background.color);
                color = WpfColor.FromRgb(source.R, source.G, source.B);
            }
            dc.DrawRectangle(new SolidColorBrush(color), null, bounds);
            if (player != null && mediaOpened)
                dc.DrawVideo(player, Fit(bounds, player.NaturalVideoWidth,
                    player.NaturalVideoHeight, background == null ? "cover" : background.fit));
            else if (image != null)
                dc.DrawImage(image, Fit(bounds, image.PixelWidth, image.PixelHeight,
                    background == null ? "cover" : background.fit));
            if ((player != null && mediaOpened) || image != null)
            {
                int shade = background == null ? 35 : background.shade;
                dc.DrawRectangle(new SolidColorBrush(WpfColor.FromArgb(
                    (byte)(shade * 255 / 100), 0, 0, 0)), null, bounds);
            }
        }

        private WpfRect Fit(WpfRect bounds, double sourceWidth,
            double sourceHeight, string mode)
        {
            sourceWidth = Math.Max(1d, sourceWidth);
            sourceHeight = Math.Max(1d, sourceHeight);
            if (mode == "stretch") return bounds;
            if (mode == "center") return new WpfRect(
                bounds.X + (bounds.Width - sourceWidth) / 2d,
                bounds.Y + (bounds.Height - sourceHeight) / 2d,
                sourceWidth, sourceHeight);
            double scale = mode == "contain"
                ? Math.Min(bounds.Width / sourceWidth, bounds.Height / sourceHeight)
                : Math.Max(bounds.Width / sourceWidth, bounds.Height / sourceHeight);
            double width = sourceWidth * scale;
            double height = sourceHeight * scale;
            return new WpfRect(bounds.X + (bounds.Width - width) / 2d,
                bounds.Y + (bounds.Height - height) / 2d, width, height);
        }

        private void DrawPanel(DrawingContext dc, WpfRect bounds, double radius)
        {
            int transparency = background == null ? 0 : background.panelTransparency;
            byte alpha = (byte)((100 - Math.Max(0, Math.Min(100, transparency))) * 238 / 100);
            byte bottomAlpha = (byte)Math.Max(0, Math.Min(255, alpha * 18 / 29));
            LinearGradientBrush glass = new LinearGradientBrush(
                WpfColor.FromArgb(alpha, 29, 33, 40),
                WpfColor.FromArgb(bottomAlpha, 18, 21, 26), 108d);
            glass.Freeze();
            dc.DrawRoundedRectangle(glass, new Pen(border, 1d), bounds, radius, radius);
            double sheenInset = Math.Min(bounds.Width / 3d, Math.Max(12d, radius));
            if (bounds.Width > sheenInset * 2d)
                dc.DrawLine(new Pen(new SolidColorBrush(
                    WpfColor.FromArgb(32, 255, 255, 255)), 1d),
                    new WpfPoint(bounds.X + sheenInset, bounds.Y + 1d),
                    new WpfPoint(bounds.Right - sheenInset, bounds.Y + 1d));
        }

        private void DrawHeader(DrawingContext dc, double xPx, double yPx,
            double widthPx, double heightPx, double clientWidthPx,
            int layoutClass, double dpiX, double dpiY)
        {
            double headerScale = Math.Max(1d, Math.Min(1.32d,
                Math.Min(widthPx / 1540d, heightPx / 150d)));
            double textWidthPx = Math.Max(100d, widthPx - 340d);
            double textTopPx = Math.Round(12d * headerScale);
            double eyebrowPoints = 8.5d * headerScale;
            double eyebrowHeightPx = Math.Ceiling(PointSize(eyebrowPoints) * dpiY * 1.15d) + 4d;
            DrawText(dc, "KRY ADVANCED   FOCUS ON YOUR WORDS",
                PixelX(xPx + 27d, dpiX), PixelY(yPx + textTopPx, dpiY),
                PointSize(eyebrowPoints), semibold, muted, false,
                PixelX(textWidthPx, dpiX));
            textTopPx += eyebrowHeightPx + Math.Round(8d * headerScale);
            double titlePoints = (textWidthPx < 820d ? 27d : 32d) * headerScale;
            double titleHeightPx = Math.Max(54d,
                Math.Ceiling(PointSize(titlePoints) * dpiY * 1.15d) + 4d);
            DrawTextInPixelRect(dc, "今天，从一个单词开始。",
                xPx + 26d, yPx + textTopPx, textWidthPx, titleHeightPx,
                titlePoints, bold, white, false, dpiX, dpiY);

            double buttonWidthPx = layoutClass == 2 ? 132d : layoutClass == 1 ? 118d : 108d;
            double buttonHeightPx = layoutClass == 2 ? 54d : layoutClass == 1 ? 50d : 46d;
            double utilityPoints = layoutClass == 2 ? 12d : layoutClass == 1 ? 10.5d : 9.5d;
            double utilityYPx = Math.Max(18d, (heightPx - buttonHeightPx) / 2d);
            double settingsXPx = xPx + widthPx - buttonWidthPx - 10d;
            double helpXPx = settingsXPx - buttonWidthPx - 14d;
            settingsHit = PixelRect(settingsXPx, yPx + utilityYPx,
                buttonWidthPx, buttonHeightPx, dpiX, dpiY);
            helpHit = PixelRect(helpXPx, yPx + utilityYPx,
                buttonWidthPx, buttonHeightPx, dpiX, dpiY);
            DrawSubtleButton(dc, helpHit, "帮助", "help", PointSize(utilityPoints),
                PixelX(14d, dpiX));
            DrawSubtleButton(dc, settingsHit, "设置", "settings", PointSize(utilityPoints),
                PixelX(14d, dpiX));
            double contactWidthPx = layoutClass == 2 ? 320d : layoutClass == 1 ? 300d : 280d;
            double contactHeightPx = layoutClass == 2 ? 28d : layoutClass == 1 ? 24d : 22d;
            double contactGapPx = layoutClass == 2 ? 6d : 4d;
            double contactXPx = settingsXPx + buttonWidthPx - contactWidthPx;
            double contactYPx = yPx + utilityYPx + buttonHeightPx + contactGapPx;
            githubHit = PixelRect(contactXPx, contactYPx,
                contactWidthPx, contactHeightPx, dpiX, dpiY);
            emailHit = PixelRect(contactXPx,
                contactYPx + contactHeightPx + contactGapPx,
                contactWidthPx, contactHeightPx, dpiX, dpiY);
            double contactPoints = layoutClass == 2 ? 10.5d : layoutClass == 1 ? 9.5d : 8.5d;
            DrawContactLink(dc, githubHit, "GitHub 项目仓库", "github",
                PointSize(contactPoints), PixelX(9d, dpiX));
            DrawContactLink(dc, emailHit, "QQ 邮箱 1242532684@qq.com", "email",
                PointSize(contactPoints), PixelX(9d, dpiX));
        }

        private void DrawMetrics(DrawingContext dc, double cardXPx, double cardYPx,
            double cardWidthPx, double cardHeightPx, int layoutClass,
            double dpiX, double dpiY)
        {
            string[] captions = { "今日新学", "列表复习", "错题复习", "今日新学列表" };
            double paddingXPx = layoutClass == 2 ? 18d : 12d;
            double paddingYPx = layoutClass == 2 ? 12d : 8d;
            double tableXPx = cardXPx + paddingXPx;
            double tableYPx = cardYPx + paddingYPx;
            double tableWidthPx = Math.Max(80d, cardWidthPx - paddingXPx * 2d);
            double tableHeightPx = Math.Max(24d, cardHeightPx - paddingYPx * 2d);
            double columnPx = tableWidthPx / 4d;
            for (int index = 0; index < 4; index++)
            {
                double xPx = tableXPx + columnPx * index;
                double metricScale = Math.Max(.92d, Math.Min(1.36d,
                    Math.Min(columnPx / 360d, tableHeightPx / 112d)));
                if (index < 3)
                    dc.DrawLine(new Pen(border, 1d),
                        new WpfPoint(PixelX(xPx + columnPx - 1d, dpiX),
                            PixelY(tableYPx + 18d * metricScale, dpiY)),
                        new WpfPoint(PixelX(xPx + columnPx - 1d, dpiX),
                            PixelY(tableYPx + tableHeightPx - 18d * metricScale, dpiY)));
                double insetPx = Math.Max(14d, Math.Round(18d * metricScale));
                double contentXPx = xPx + insetPx;
                double contentYPx = tableYPx + 4d;
                double contentWidthPx = Math.Max(20d, columnPx - insetPx * 2d);
                double contentHeightPx = Math.Max(20d, tableHeightPx - 8d);
                double captionHeightPx = Math.Max(24d, Math.Round(contentHeightPx * .38d));
                double valueHeightPx = Math.Max(42d, contentHeightPx - captionHeightPx);
                DrawTextInPixelRect(dc, captions[index], contentXPx, contentYPx,
                    contentWidthPx, captionHeightPx, 9.5d * metricScale,
                    regular, muted, false, dpiX, dpiY);
                DrawTextInPixelRect(dc, metrics[index] ?? "0", contentXPx,
                    contentYPx + captionHeightPx, contentWidthPx, valueHeightPx,
                    24d * metricScale, regular, white, false, dpiX, dpiY);
            }
        }

        private void DrawMode(DrawingContext dc, double cardXPx, double cardYPx,
            double cardWidthPx, double cardHeightPx, double clientWidthPx,
            int layoutClass, double dpiX, double dpiY)
        {
            double paddingPx = layoutClass == 2 ? 42d : layoutClass == 1 ? 34d : 28d;
            double innerXPx = cardXPx + paddingPx;
            double innerYPx = cardYPx + paddingPx;
            double innerWidthPx = Math.Max(80d, cardWidthPx - paddingPx * 2d);
            double innerHeightPx = Math.Max(80d, cardHeightPx - paddingPx * 2d);
            double leftWidthPx = innerWidthPx * .40d;
            double dividerXPx = innerXPx + leftWidthPx;
            guideWheelHit = PixelRect(innerXPx, innerYPx,
                Math.Max(40d, leftWidthPx - 18d), innerHeightPx, dpiX, dpiY);
            dc.DrawLine(new Pen(border, 1d),
                new WpfPoint(PixelX(dividerXPx, dpiX), PixelY(innerYPx, dpiY)),
                new WpfPoint(PixelX(dividerXPx, dpiX),
                    PixelY(innerYPx + innerHeightPx, dpiY)));
            DrawWheel(dc, innerXPx, innerYPx,
                Math.Max(40d, leftWidthPx - 34d), innerHeightPx, dpiX, dpiY);
            DrawDetail(dc, dividerXPx + 1d, innerYPx,
                Math.Max(40d, innerWidthPx - leftWidthPx - 1d), innerHeightPx,
                clientWidthPx, layoutClass, dpiX, dpiY);
        }

        private void DrawWheel(DrawingContext dc, double xPx, double yPx,
            double widthPx, double heightPx, double dpiX, double dpiY)
        {
            double scale = Math.Max(1d, Math.Min(1.34d,
                Math.Min(widthPx / 580d, heightPx / 540d)));
            DrawText(dc, (selectedIndex + 1).ToString("00") + " / 04",
                PixelX(xPx + 18d * scale, dpiX), PixelY(yPx + 10d * scale, dpiY),
                PointSize(8.5d * scale), semibold, soft, false,
                PixelX(120d * scale, dpiX));
            double centerYPx = yPx + heightPx / 2d;
            double spacingPx = 78d * scale;
            for (int index = 0; index < 4; index++)
            {
                int logicalDistance = index - selectedIndex;
                if (logicalDistance > 2) logicalDistance -= 4;
                if (logicalDistance < -2) logicalDistance += 4;
                double distance = logicalDistance + wheelOffset;
                if (Math.Abs(distance) > 1.6d)
                {
                    modeHits[index] = WpfRect.Empty;
                    continue;
                }
                double focusAmount = 1d - Math.Min(1d, Math.Abs(distance));
                double rowHeightPx = 72d * scale;
                WpfRect row = PixelRect(xPx + 14d * scale,
                    centerYPx + distance * spacingPx - rowHeightPx / 2d,
                    Math.Max(20d, widthPx - 28d * scale), rowHeightPx, dpiX, dpiY);
                modeHits[index] = row;
                if (focusAmount > .25d)
                    dc.DrawRoundedRectangle(new SolidColorBrush(WpfColor.FromArgb(
                        (byte)(36d + focusAmount * 22d), 37, 42, 50)), null, row,
                        PixelX(16d, dpiX), PixelY(16d, dpiY));
                byte alpha = (byte)(62d + 193d * focusAmount);
                WpfBrush ink = new SolidColorBrush(WpfColor.FromArgb(alpha, 244, 244, 245));
                DrawVisuallyCenteredWheelText(dc, names[index], row,
                    PointSize((14.2d + focusAmount * 15.3d) * scale),
                    regular, ink);
            }
            Pen arrow = new Pen(soft, PixelX(1.6d, dpiX));
            double arrowXPx = xPx + widthPx - 18d;
            dc.DrawLine(arrow, new WpfPoint(PixelX(arrowXPx - 5d, dpiX), PixelY(yPx + 18d, dpiY)),
                new WpfPoint(PixelX(arrowXPx, dpiX), PixelY(yPx + 13d, dpiY)));
            dc.DrawLine(arrow, new WpfPoint(PixelX(arrowXPx, dpiX), PixelY(yPx + 13d, dpiY)),
                new WpfPoint(PixelX(arrowXPx + 5d, dpiX), PixelY(yPx + 18d, dpiY)));
            dc.DrawLine(arrow, new WpfPoint(PixelX(arrowXPx - 5d, dpiX),
                    PixelY(yPx + heightPx - 18d, dpiY)),
                new WpfPoint(PixelX(arrowXPx, dpiX), PixelY(yPx + heightPx - 13d, dpiY)));
            dc.DrawLine(arrow, new WpfPoint(PixelX(arrowXPx, dpiX),
                    PixelY(yPx + heightPx - 13d, dpiY)),
                new WpfPoint(PixelX(arrowXPx + 5d, dpiX),
                    PixelY(yPx + heightPx - 18d, dpiY)));
        }

        private void DrawDetail(DrawingContext dc, double xPx, double yPx,
            double widthPx, double heightPx, double clientWidthPx,
            int layoutClass, double dpiX, double dpiY)
        {
            double paddingXPx = layoutClass == 2 ? 68d : layoutClass == 1 ? 50d : 32d;
            double paddingYPx = 14d;
            double contentXPx = xPx + paddingXPx;
            double contentYPx = yPx + paddingYPx;
            double contentWidthPx = Math.Max(40d, widthPx - paddingXPx * 2d);
            double contentHeightPx = Math.Max(40d, heightPx - paddingYPx * 2d);
            double[] rowHeights = layoutClass == 2
                ? new[] { 42d, 92d, 72d, 62d, 76d }
                : layoutClass == 1 ? new[] { 34d, 72d, 60d, 52d, 64d }
                : new[] { 30d, 64d, 52d, 48d, 58d };
            double fixedHeightPx = 0d;
            for (int index = 0; index < rowHeights.Length; index++)
                fixedHeightPx += rowHeights[index];
            double rowYPx = contentYPx + Math.Max(0d, (contentHeightPx - fixedHeightPx) / 2d);
            double eyebrowPoints = layoutClass == 2 ? 12d : layoutClass == 1 ? 10d : 8.5d;
            double titlePoints = layoutClass == 2 ? 34d : layoutClass == 1 ? 28d : 24d;
            double descriptionPoints = layoutClass == 2 ? 16d : layoutClass == 1 ? 13d : 11d;
            double statusPoints = layoutClass == 2 ? 13d : layoutClass == 1 ? 10d : 8.5d;
            DrawCenteredText(dc, eyebrow, PixelRect(contentXPx, rowYPx,
                contentWidthPx, rowHeights[0], dpiX, dpiY), PointSize(eyebrowPoints),
                semibold, muted);
            rowYPx += rowHeights[0];
            DrawCenteredText(dc, heading, PixelRect(contentXPx, rowYPx,
                contentWidthPx, rowHeights[1], dpiX, dpiY), PointSize(titlePoints),
                bold, white);
            rowYPx += rowHeights[1];
            DrawCenteredText(dc, description, PixelRect(contentXPx, rowYPx,
                contentWidthPx, rowHeights[2], dpiX, dpiY), PointSize(descriptionPoints),
                regular, muted);
            rowYPx += rowHeights[2];
            DrawCenteredText(dc, status, PixelRect(contentXPx, rowYPx,
                contentWidthPx, rowHeights[3], dpiX, dpiY), PointSize(statusPoints),
                regular, soft);
            rowYPx += rowHeights[3];

            double availableWidthPx = Math.Max(120d, contentWidthPx);
            double buttonWidthPx = Math.Min(clientWidthPx >= 1700d ? 820d : availableWidthPx,
                availableWidthPx);
            double actionXPx = contentXPx + Math.Max(0d,
                (availableWidthPx - buttonWidthPx) / 2d);
            double targetHeightPx = clientWidthPx >= 1700d ? 62d
                : clientWidthPx >= 1200d ? 52d : 46d;
            double buttonHeightPx = Math.Min(targetHeightPx,
                Math.Max(38d, rowHeights[4] - 2d));
            double actionYPx = rowYPx + Math.Max(0d,
                (rowHeights[4] - buttonHeightPx) / 2d);
            double actionPoints = layoutClass == 2 ? 13d : layoutClass == 1 ? 11d : 9.5d;
            if (secondaryVisible)
            {
                double gapPx = 9d;
                double secondaryWidthPx = Math.Min(clientWidthPx >= 1700d ? 170d : 132d,
                    Math.Max(104d, buttonWidthPx / 3d));
                primaryHit = PixelRect(actionXPx, actionYPx,
                    Math.Max(120d, buttonWidthPx - secondaryWidthPx - gapPx),
                    buttonHeightPx, dpiX, dpiY);
                secondaryHit = PixelRect(actionXPx + buttonWidthPx - secondaryWidthPx,
                    actionYPx, secondaryWidthPx, buttonHeightPx, dpiX, dpiY);
                DrawPrimaryButton(dc, primaryHit, primaryLabel, primaryEnabled,
                    hover == "primary", PointSize(actionPoints), PixelX(14d, dpiX));
                DrawSecondaryButton(dc, secondaryHit, secondaryLabel,
                    hover == "secondary", PointSize(actionPoints), PixelX(14d, dpiX));
            }
            else
            {
                secondaryHit = WpfRect.Empty;
                primaryHit = PixelRect(actionXPx, actionYPx, buttonWidthPx,
                    buttonHeightPx, dpiX, dpiY);
                DrawPrimaryButton(dc, primaryHit, primaryLabel, primaryEnabled,
                    hover == "primary", PointSize(actionPoints), PixelX(14d, dpiX));
            }
        }

        private void DrawSubtleButton(DrawingContext dc, WpfRect bounds,
            string label, string id, double fontSize, double radius)
        {
            WpfBrush fill = new SolidColorBrush(WpfColor.FromArgb(
                hover == id ? (byte)220 : (byte)190, 24, 29, 38));
            dc.DrawRoundedRectangle(fill, new Pen(border, 1d), bounds,
                radius, radius);
            DrawCenteredText(dc, label, bounds, fontSize, semibold, white);
        }

        private void DrawContactLink(DrawingContext dc, WpfRect bounds,
            string label, string id, double fontSize, double radius)
        {
            bool over = hover == id;
            WpfBrush fill = new SolidColorBrush(WpfColor.FromArgb(
                over ? (byte)92 : (byte)54, 245, 247, 250));
            WpfBrush outline = new SolidColorBrush(WpfColor.FromArgb(
                over ? (byte)120 : (byte)66, 225, 232, 244));
            dc.DrawRoundedRectangle(fill, new Pen(outline, 1d), bounds,
                radius, radius);
            DrawCenteredText(dc, label, bounds, fontSize, semibold,
                over ? WpfBrushes.White : white);
        }

        private void DrawPrimaryButton(DrawingContext dc, WpfRect bounds,
            string label, bool enabled, bool over, double fontSize, double radius)
        {
            WpfBrush fill = enabled
                ? (over ? WpfBrushes.White : primary)
                : new SolidColorBrush(WpfColor.FromRgb(121, 125, 132));
            dc.DrawRoundedRectangle(fill, null, bounds, radius, radius);
            DrawCenteredText(dc, label, bounds, fontSize, semibold,
                enabled ? primaryText : soft);
        }

        private void DrawSecondaryButton(DrawingContext dc, WpfRect bounds,
            string label, bool over, double fontSize, double radius)
        {
            WpfBrush fill = new SolidColorBrush(WpfColor.FromArgb(
                over ? (byte)70 : (byte)42, 245, 247, 250));
            dc.DrawRoundedRectangle(fill, new Pen(border, 1d), bounds,
                radius, radius);
            DrawCenteredText(dc, label, bounds, fontSize, semibold, white);
        }

        private static double PointSize(double points)
        {
            return Math.Max(1d, points * 96d / 72d);
        }

        private static double PixelX(double pixels, double dpiX)
        {
            return pixels / Math.Max(.1d, dpiX);
        }

        private static double PixelY(double pixels, double dpiY)
        {
            return pixels / Math.Max(.1d, dpiY);
        }

        private static WpfRect PixelRect(double x, double y, double width,
            double height, double dpiX, double dpiY)
        {
            return new WpfRect(PixelX(x, dpiX), PixelY(y, dpiY),
                PixelX(Math.Max(0d, width), dpiX),
                PixelY(Math.Max(0d, height), dpiY));
        }

        private void DrawTextInPixelRect(DrawingContext dc, string text,
            double xPx, double yPx, double widthPx, double heightPx,
            double points, Typeface face, WpfBrush brush, bool centered,
            double dpiX, double dpiY)
        {
            WpfRect bounds = PixelRect(xPx, yPx, widthPx, heightPx, dpiX, dpiY);
            FormattedText formatted = new FormattedText(text ?? string.Empty,
                CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                face, PointSize(points), brush, 1d);
            formatted.MaxTextWidth = Math.Max(1d, bounds.Width);
            formatted.Trimming = TextTrimming.CharacterEllipsis;
            double x = centered ? bounds.X + Math.Max(0d,
                (bounds.Width - formatted.Width) / 2d) : bounds.X;
            double y = bounds.Y + Math.Max(0d,
                (bounds.Height - formatted.Height) / 2d);
            dc.DrawText(formatted, new WpfPoint(x, y));
        }

        private void DrawText(DrawingContext dc, string text, double x, double y,
            double size, Typeface face, WpfBrush brush, bool centered, double maxWidth)
        {
            FormattedText formatted = new FormattedText(text ?? string.Empty,
                CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                face, size, brush, 1d);
            formatted.MaxTextWidth = Math.Max(1d, maxWidth);
            formatted.Trimming = TextTrimming.CharacterEllipsis;
            dc.DrawText(formatted, new WpfPoint(centered ? x - formatted.Width / 2d : x, y));
        }

        private void DrawCenteredText(DrawingContext dc, string text,
            WpfRect bounds, double size, Typeface face, WpfBrush brush)
        {
            FormattedText formatted = new FormattedText(text ?? string.Empty,
                CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                face, size, brush, 1d);
            formatted.MaxTextWidth = Math.Max(1d, bounds.Width - 8d);
            formatted.Trimming = TextTrimming.CharacterEllipsis;
            double x = bounds.X + Math.Max(0d, (bounds.Width - formatted.Width) / 2d);
            double y = bounds.Y + Math.Max(0d, (bounds.Height - formatted.Height) / 2d);
            dc.DrawText(formatted, new WpfPoint(x, y));
        }

        private void DrawVisuallyCenteredWheelText(DrawingContext dc, string text,
            WpfRect bounds, double size, Typeface face, WpfBrush brush)
        {
            FormattedText formatted = new FormattedText(text ?? string.Empty,
                CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                face, size, brush, 1d);
            formatted.MaxTextWidth = Math.Max(1d, bounds.Width - 8d);
            formatted.Trimming = TextTrimming.CharacterEllipsis;
            double x = bounds.X + Math.Max(0d, (bounds.Width - formatted.Width) / 2d);

            // FormattedText.Height includes the font's line box, whose upper and
            // lower whitespace are not symmetrical for CJK glyphs.  Center the
            // actual ink extent instead, and do not clamp a negative line-box
            // offset at high DPI; clamping was what made the wheel text sit on
            // the lower edge of its selection bar at 150% display scaling.
            double extent = formatted.Extent;
            double y;
            if (extent > 0d)
            {
                double inkTop = formatted.Height + formatted.OverhangAfter - extent;
                y = bounds.Y + bounds.Height / 2d - inkTop - extent / 2d;
            }
            else
            {
                y = bounds.Y + (bounds.Height - formatted.Height) / 2d;
            }
            dc.DrawText(formatted, new WpfPoint(x, y));
        }

        private void OnWheel(object sender, MouseWheelEventArgs e)
        {
            DateTime now = DateTime.UtcNow;
            if ((now - lastWheel).TotalMilliseconds < 145d) return;
            lastWheel = now;
            Step(e.Delta < 0 ? 1 : -1);
            if (WheelUsed != null) WheelUsed();
            e.Handled = true;
        }

        private void OnKeyPressed(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up) { Step(-1); e.Handled = true; }
            else if (e.Key == Key.Down) { Step(1); e.Handled = true; }
            else if (e.Key == Key.Enter && primaryEnabled)
            {
                if (PrimaryRequested != null) PrimaryRequested();
                e.Handled = true;
            }
        }

        private void Step(int direction)
        {
            int next = (selectedIndex + direction + keys.Length) % keys.Length;
            selectedIndex = next;
            wheelOffset = direction > 0 ? -1d : 1d;
            motionTimer.Start();
            InvalidateVisual();
            if (ModeRequested != null) ModeRequested(keys[next]);
        }

        private void OnPointerMove(object sender, MouseEventArgs e)
        {
            WpfPoint point = e.GetPosition(this);
            string next = Hit(point);
            if (next == hover) return;
            hover = next;
            Cursor = string.IsNullOrEmpty(hover) ? Cursors.Arrow : Cursors.Hand;
            InvalidateVisual();
        }

        private string Hit(WpfPoint point)
        {
            if (helpHit.Contains(point)) return "help";
            if (settingsHit.Contains(point)) return "settings";
            if (githubHit.Contains(point)) return "github";
            if (emailHit.Contains(point)) return "email";
            if (primaryHit.Contains(point)) return "primary";
            if (secondaryVisible && secondaryHit.Contains(point)) return "secondary";
            for (int index = 0; index < modeHits.Length; index++)
                if (!modeHits[index].IsEmpty && modeHits[index].Contains(point))
                    return "mode:" + index;
            return string.Empty;
        }

        private void OnPointerDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            pressedHit = Hit(e.GetPosition(this));
            if (!string.IsNullOrEmpty(pressedHit)) CaptureMouse();
            e.Handled = true;
        }

        private void OnPointerUp(object sender, MouseButtonEventArgs e)
        {
            string hit = Hit(e.GetPosition(this));
            string pressed = pressedHit;
            pressedHit = string.Empty;
            if (IsMouseCaptured) ReleaseMouseCapture();
            if (!string.IsNullOrEmpty(pressed) && pressed == hit)
                ActivateHit(hit);
            e.Handled = true;
        }

        private void ActivateHit(string hit)
        {
            if (hit == "help" && HelpRequested != null) HelpRequested();
            else if (hit == "settings" && SettingsRequested != null) SettingsRequested();
            else if (hit == "github" && GithubRequested != null) GithubRequested();
            else if (hit == "email" && EmailRequested != null) EmailRequested();
            else if (hit == "primary" && primaryEnabled && PrimaryRequested != null)
                PrimaryRequested();
            else if (hit == "secondary" && secondaryVisible && SecondaryRequested != null)
                SecondaryRequested();
            else if (hit.StartsWith("mode:", StringComparison.Ordinal))
            {
                int index;
                if (int.TryParse(hit.Substring(5), out index) && index != selectedIndex)
                {
                    int direction = index > selectedIndex ? 1 : -1;
                    selectedIndex = index;
                    wheelOffset = direction > 0 ? -1d : 1d;
                    motionTimer.Start();
                    if (ModeRequested != null) ModeRequested(keys[index]);
                }
            }
        }

        internal void DebugActivatePrimary()
        {
            ActivateHit("primary");
        }

        private void CloseMedia()
        {
            mediaOpened = false;
            if (player != null)
            {
                player.Close();
                player = null;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            motionTimer.Stop();
            CloseMedia();
            image = null;
        }
    }
}
