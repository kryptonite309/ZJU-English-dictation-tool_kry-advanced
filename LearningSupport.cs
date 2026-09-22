using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace EnglishDictationTool
{
    internal static class SpellingDifference
    {
        private sealed class Step
        {
            public char Actual;
            public char Expected;
            public char Kind;
        }

        public static string ClosestExpected(string input, IEnumerable<string> candidates)
        {
            string actual = StudyStore.NormalizeAnswer(input);
            List<string> values = (candidates ?? Enumerable.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (values.Count == 0) return string.Empty;
            return values.OrderBy(x => Distance(actual, StudyStore.NormalizeAnswer(x)))
                .ThenBy(x => x.Length).First();
        }

        public static string Report(string input, IEnumerable<string> candidates)
        {
            string expectedDisplay = ClosestExpected(input, candidates);
            if (expectedDisplay.Length == 0) return string.Empty;
            string actual = StudyStore.NormalizeAnswer(input);
            string expected = StudyStore.NormalizeAnswer(expectedDisplay);
            List<Step> steps = Align(actual, expected);
            List<string> changes = new List<string>();
            int position = 0;
            foreach (Step step in steps)
            {
                if (step.Kind == '=') { position++; continue; }
                if (step.Kind == '~')
                {
                    changes.Add("第 " + (position + 1) + " 位“" + Printable(step.Actual)
                        + "”应为“" + Printable(step.Expected) + "”");
                    position++;
                }
                else if (step.Kind == '-')
                {
                    changes.Add("第 " + (position + 1) + " 位缺少“"
                        + Printable(step.Expected) + "”");
                    position++;
                }
                else changes.Add("第 " + Math.Max(1, position + 1) + " 位多出“"
                    + Printable(step.Actual) + "”");
            }
            if (changes.Count == 0) return string.Empty;
            return "拼写差异（按最接近答案比较）：\n你的答案：" + actual
                + "\n正确答案：" + expectedDisplay + "\n" + string.Join("；", changes);
        }

        private static string Printable(char value) { return value == ' ' ? "空格" : value.ToString(); }

        private static int Distance(string left, string right)
        {
            int[,] score = Matrix(left, right);
            return score[left.Length, right.Length];
        }

        private static int[,] Matrix(string actual, string expected)
        {
            int[,] score = new int[actual.Length + 1, expected.Length + 1];
            for (int i = 0; i <= actual.Length; i++) score[i, 0] = i;
            for (int j = 0; j <= expected.Length; j++) score[0, j] = j;
            for (int i = 1; i <= actual.Length; i++)
                for (int j = 1; j <= expected.Length; j++)
                    score[i, j] = Math.Min(score[i - 1, j] + 1,
                        Math.Min(score[i, j - 1] + 1, score[i - 1, j - 1]
                            + (actual[i - 1] == expected[j - 1] ? 0 : 1)));
            return score;
        }

        private static List<Step> Align(string actual, string expected)
        {
            int[,] score = Matrix(actual, expected);
            int i = actual.Length, j = expected.Length;
            List<Step> reversed = new List<Step>();
            while (i > 0 || j > 0)
            {
                if (i > 0 && j > 0 && score[i, j] == score[i - 1, j - 1]
                    + (actual[i - 1] == expected[j - 1] ? 0 : 1))
                {
                    reversed.Add(new Step { Actual = actual[i - 1], Expected = expected[j - 1],
                        Kind = actual[i - 1] == expected[j - 1] ? '=' : '~' });
                    i--; j--;
                }
                else if (j > 0 && score[i, j] == score[i, j - 1] + 1)
                {
                    reversed.Add(new Step { Expected = expected[j - 1], Kind = '-' });
                    j--;
                }
                else
                {
                    reversed.Add(new Step { Actual = actual[i - 1], Kind = '+' });
                    i--;
                }
            }
            reversed.Reverse();
            return reversed;
        }
    }

    internal sealed class LearningTimer : IDisposable
    {
        private readonly Control owner;
        private readonly Label label;
        private readonly Func<bool> mayRun;
        private readonly Action<long> commit;
        private readonly Timer timer;
        private long savedMilliseconds;
        private DateTime segmentStarted;
        private bool running;
        private bool enabled;
        private string precision;

        public LearningTimer(Control owner, Label label, long initialMilliseconds,
            bool enabled, string precision, Func<bool> mayRun, Action<long> commit)
        {
            this.owner = owner; this.label = label; this.mayRun = mayRun; this.commit = commit;
            savedMilliseconds = Math.Max(0, initialMilliseconds);
            this.enabled = enabled;
            this.precision = precision == "millisecond" ? "millisecond" : "minute";
            timer = new Timer { Interval = this.precision == "millisecond" ? 47 : 1000 };
            timer.Tick += delegate { Refresh(); };
            timer.Start();
            Refresh();
        }

        public long TotalMilliseconds
        {
            get
            {
                return savedMilliseconds + (running
                    ? Math.Max(0L, (long)(DateTime.UtcNow - segmentStarted).TotalMilliseconds) : 0L);
            }
        }

        public void Refresh()
        {
            bool shouldRun = enabled && mayRun != null && mayRun()
                && owner.Visible && owner.FindForm() != null && owner.FindForm().ContainsFocus;
            if (shouldRun && !running) { segmentStarted = DateTime.UtcNow; running = true; }
            else if (!shouldRun && running) CommitSegment();
            if (label != null)
            {
                label.Visible = enabled;
                label.Text = DisplayText(TotalMilliseconds, precision);
            }
        }

        public void StopAndCommit()
        {
            if (running) CommitSegment();
        }

        private void CommitSegment()
        {
            long delta = Math.Max(0L, (long)(DateTime.UtcNow - segmentStarted).TotalMilliseconds);
            running = false;
            if (delta <= 0) return;
            savedMilliseconds += delta;
            if (commit != null) commit(delta);
        }

        public static string Format(long milliseconds, string precision)
        {
            TimeSpan span = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
            if (precision != "millisecond")
                return Math.Floor(span.TotalMinutes) + " 分钟";
            int hours = (int)Math.Floor(span.TotalHours);
            return string.Format("{0:00}:{1:00}:{2:00}.{3:000}", hours,
                span.Minutes, span.Seconds, span.Milliseconds);
        }

        public static string DisplayText(long milliseconds, string precision)
        {
            return "本模块用时\r\n" + Format(milliseconds, precision);
        }

        public void Dispose()
        {
            StopAndCommit();
            timer.Stop(); timer.Dispose();
        }
    }

    internal sealed class PauseOverlay : UserControl
    {
        private Bitmap background;
        private readonly Button resume;
        private readonly Label title;
        public event EventHandler ResumeRequested;

        public PauseOverlay()
        {
            Dock = DockStyle.Fill;
            DoubleBuffered = true;
            BackColor = Color.Black;
            title = new Label { Text = "已暂停", AutoSize = false, Width = 420, Height = 70,
                TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White,
                BackColor = Color.Transparent, Font = new Font("Microsoft YaHei UI", 26, FontStyle.Bold) };
            title.Visible = false;
            resume = new ModernButton { Text = "继续学习", Width = 190, Height = 52, Primary = true };
            resume.Click += delegate { if (ResumeRequested != null) ResumeRequested(this, EventArgs.Empty); };
            Controls.Add(title); Controls.Add(resume);
            Resize += delegate { PositionChildren(); };
        }

        public void Prepare(Control target, AppearanceStore appearance, string moduleKey)
        {
            if (background != null) { background.Dispose(); background = null; }
            BackgroundAppearance chosen = null;
            string overrideKey = "pause_" + moduleKey;
            if (appearance != null && appearance.Settings.backgrounds.ContainsKey(overrideKey)
                && !appearance.Settings.backgrounds[overrideKey].inherit)
                chosen = appearance.Settings.backgrounds[overrideKey];
            if (chosen == null && appearance != null && appearance.Settings.backgrounds.ContainsKey("pause"))
                chosen = appearance.Settings.backgrounds["pause"];
            string imagePath = appearance == null || chosen == null ? null : appearance.ImagePath(chosen);
            if (imagePath != null)
            {
                using (Image loaded = Image.FromFile(imagePath))
                    background = RenderImage(loaded, target.ClientSize, chosen.fit);
            }
            else
            {
                Bitmap capture = new Bitmap(Math.Max(1, target.ClientSize.Width),
                    Math.Max(1, target.ClientSize.Height));
                target.DrawToBitmap(capture, target.ClientRectangle);
                background = Frost(capture);
                capture.Dispose();
            }
            int shade = chosen == null ? 42 : chosen.shade;
            if (background != null)
                using (Graphics graphics = Graphics.FromImage(background))
                using (Brush brush = new SolidBrush(Color.FromArgb(shade * 255 / 100, 0, 0, 0)))
                    graphics.FillRectangle(brush, new Rectangle(Point.Empty, background.Size));
            PositionChildren();
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (background == null) { e.Graphics.Clear(Color.FromArgb(24, 28, 36)); return; }
            e.Graphics.DrawImage(background, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            TextRenderer.DrawText(e.Graphics, title.Text, title.Font, title.Bounds, title.ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }

        private void PositionChildren()
        {
            title.Location = new Point(Math.Max(0, (Width - title.Width) / 2), Math.Max(30, Height / 2 - 85));
            resume.Location = new Point(Math.Max(0, (Width - resume.Width) / 2), title.Bottom + 18);
        }

        private static Bitmap Frost(Bitmap source)
        {
            int smallWidth = Math.Max(8, source.Width / 22);
            int smallHeight = Math.Max(8, source.Height / 22);
            using (Bitmap small = new Bitmap(smallWidth, smallHeight))
            using (Graphics down = Graphics.FromImage(small))
            {
                down.InterpolationMode = InterpolationMode.HighQualityBilinear;
                down.DrawImage(source, new Rectangle(0, 0, smallWidth, smallHeight));
                Bitmap result = new Bitmap(source.Width, source.Height);
                using (Graphics up = Graphics.FromImage(result))
                {
                    up.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    up.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    up.DrawImage(small, new Rectangle(0, 0, result.Width, result.Height));
                    using (Brush haze = new SolidBrush(Color.FromArgb(58, 220, 225, 232)))
                        up.FillRectangle(haze, new Rectangle(Point.Empty, result.Size));
                }
                return result;
            }
        }

        private static Bitmap RenderImage(Image image, Size size, string fit)
        {
            Bitmap result = new Bitmap(Math.Max(1, size.Width), Math.Max(1, size.Height));
            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.Clear(Color.Black);
                Rectangle bounds = new Rectangle(Point.Empty, result.Size);
                if (fit == "stretch") graphics.DrawImage(image, bounds);
                else if (fit == "center")
                    graphics.DrawImage(image, new Rectangle((bounds.Width - image.Width) / 2,
                        (bounds.Height - image.Height) / 2, image.Width, image.Height));
                else
                {
                    float scale = fit == "contain"
                        ? Math.Min((float)bounds.Width / image.Width, (float)bounds.Height / image.Height)
                        : Math.Max((float)bounds.Width / image.Width, (float)bounds.Height / image.Height);
                    Rectangle target = new Rectangle((bounds.Width - (int)(image.Width * scale)) / 2,
                        (bounds.Height - (int)(image.Height * scale)) / 2,
                        (int)(image.Width * scale), (int)(image.Height * scale));
                    graphics.DrawImage(image, target);
                }
            }
            return result;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && background != null) { background.Dispose(); background = null; }
            base.Dispose(disposing);
        }
    }
}
