using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace EnglishDictationTool
{
    internal sealed class StudyCalendarView : Control
    {
        private struct CalendarMetrics
        {
            public Rectangle Title;
            public Rectangle Previous;
            public Rectangle Next;
            public int WeekdayTop;
            public int WeekdayHeight;
            public int GridTop;
            public int CellWidth;
            public int CellHeight;
        }

        private readonly StudyStore store;
        private AppearanceStore appearance;
        private Image backgroundImage;
        private VideoBackground video;
        private DateTime month;
        private DateTime selected;
        private DateTime hovered;
        public event EventHandler SelectedDateChanged;

        public DateTime SelectedDate { get { return selected; } }

        public StudyCalendarView(StudyStore study, AppearanceStore appearanceStore)
        {
            store = study;
            month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            selected = DateTime.Today;
            Width = 1060; Height = 740;
            MinimumSize = Size.Empty;
            DoubleBuffered = true;
            Font = Theme.UiFont;
            Cursor = Cursors.Hand;
            SetAppearance(appearanceStore);
        }

        public void SetAppearance(AppearanceStore appearanceStore)
        {
            appearance = appearanceStore;
            if (video != null) { video.Dispose(); video = null; }
            if (backgroundImage != null) { backgroundImage.Dispose(); backgroundImage = null; }
            string path = appearance.ImagePath(appearance.Settings.backgrounds["calendar"]);
            if (AppearanceStore.IsVideo(path))
                video = new VideoBackground(this, path,
                    appearance.Settings.backgrounds["calendar"], 10);
            else if (path != null)
            {
                try { using (Image loaded = Image.FromFile(path)) backgroundImage = new Bitmap(loaded); }
                catch { backgroundImage = null; }
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            BackgroundAppearance background = appearance.Settings.backgrounds["calendar"];
            using (Brush baseBrush = new SolidBrush(Color.FromArgb(background.color)))
                graphics.FillRectangle(baseBrush, ClientRectangle);
            if (video != null)
                video.Draw(graphics, ClientRectangle);
            else if (backgroundImage != null)
            {
                Rectangle target = ClientRectangle;
                if (background.fit == "center")
                {
                    target = new Rectangle((Width - backgroundImage.Width) / 2,
                        (Height - backgroundImage.Height) / 2,
                        backgroundImage.Width, backgroundImage.Height);
                }
                else if (background.fit != "stretch")
                {
                    float scale = background.fit == "contain"
                        ? Math.Min((float)Width / backgroundImage.Width, (float)Height / backgroundImage.Height)
                        : Math.Max((float)Width / backgroundImage.Width, (float)Height / backgroundImage.Height);
                    int width = (int)Math.Ceiling(backgroundImage.Width * scale);
                    int height = (int)Math.Ceiling(backgroundImage.Height * scale);
                    target = new Rectangle((Width - width) / 2, (Height - height) / 2, width, height);
                }
                graphics.DrawImage(backgroundImage, target);
                using (Brush shade = new SolidBrush(Color.FromArgb(background.shade * 255 / 100, 0, 0, 0)))
                    graphics.FillRectangle(shade, ClientRectangle);
            }
            CalendarMetrics metrics = MeasureLayout();
            using (Font title = new Font("Segoe UI", 17, FontStyle.Bold))
                TextRenderer.DrawText(graphics, month.ToString("yyyy 年 M 月"), title,
                    metrics.Title, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            DrawNav(graphics, metrics.Previous, "←");
            DrawNav(graphics, metrics.Next, "→");
            string[] weekdays = { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };
            int left = 12;
            for (int col = 0; col < 7; col++)
                TextRenderer.DrawText(graphics, weekdays[col], Font,
                    new Rectangle(left + col * metrics.CellWidth, metrics.WeekdayTop,
                        metrics.CellWidth, metrics.WeekdayHeight),
                    Color.FromArgb(190, 205, 218),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            DateTime first = month.AddDays(-((int)month.DayOfWeek + 6) % 7);
            for (int index = 0; index < 42; index++)
            {
                DateTime day = first.AddDays(index);
                int row = index / 7, col = index % 7;
                Rectangle rect = new Rectangle(left + col * metrics.CellWidth + 3,
                    metrics.GridTop + row * metrics.CellHeight + 3,
                    Math.Max(1, metrics.CellWidth - 7),
                    Math.Max(1, metrics.CellHeight - 7));
                PaintDay(graphics, day, rect);
            }
        }

        private CalendarMetrics MeasureLayout()
        {
            int titleHeight;
            int arrowHeight;
            using (Font title = new Font("Segoe UI", 17, FontStyle.Bold))
                titleHeight = TextRenderer.MeasureText(month.ToString("yyyy 年 M 月"), title,
                    new Size(Math.Max(1, Width - 180), int.MaxValue),
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Height;
            using (Font arrow = new Font("Segoe UI Symbol", 13, FontStyle.Bold))
                arrowHeight = TextRenderer.MeasureText("→", arrow, Size.Empty,
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Height;
            int headerHeight = Math.Max(66, Math.Max(titleHeight + 22, arrowHeight + 24));
            int navHeight = Math.Max(38, arrowHeight + 14);
            int navWidth = Math.Max(50, navHeight + 10);
            int navTop = Math.Max(8, (headerHeight - navHeight) / 2);
            int weekdayHeight = Math.Max(32, TextRenderer.MeasureText("周一", Font,
                Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Height + 10);
            int gridTop = headerHeight + weekdayHeight;
            int gridHeight = Math.Max(6, Height - gridTop - 6);
            int cellWidth = Math.Max(1, (Width - 24) / 7);
            return new CalendarMetrics
            {
                Title = new Rectangle(82, 6, Math.Max(1, Width - 164), headerHeight - 8),
                Previous = new Rectangle(15, navTop, navWidth, navHeight),
                Next = new Rectangle(Math.Max(15, Width - 15 - navWidth), navTop,
                    navWidth, navHeight),
                WeekdayTop = headerHeight,
                WeekdayHeight = weekdayHeight,
                GridTop = gridTop,
                CellWidth = cellWidth,
                CellHeight = Math.Max(1, gridHeight / 6)
            };
        }

        private static void DrawNav(Graphics graphics, Rectangle rect, string text)
        {
            using (GraphicsPath path = ModernUI.Round(rect, 10))
            {
                using (Brush fill = new SolidBrush(Color.FromArgb(43, 49, 61)))
                    graphics.FillPath(fill, path);
                using (Pen edge = new Pen(Color.FromArgb(108, 114, 128)))
                    graphics.DrawPath(edge, path);
            }
            using (Font arrow = new Font("Segoe UI Symbol", 13, FontStyle.Bold))
                TextRenderer.DrawText(graphics, text, arrow,
                    rect, Color.White, TextFormatFlags.HorizontalCenter
                    | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding
                    | TextFormatFlags.SingleLine);
        }

        private void PaintDay(Graphics graphics, DateTime day, Rectangle rect)
        {
            string key = day.ToString("yyyy-MM-dd");
            var lists = store.Lists.Where(x => x.studyDate == key).ToList();
            Func<string, int> count = kind => lists.Where(x => x.kind == kind)
                .Sum(x => x.items.Count(y => y.firstAnsweredAt != DateTime.MinValue
                    && StudyStore.StudyDayKey(y.firstAnsweredAt) == key));
            int learned = count("new"), reviewed = count("list_review"), problems = count("problem_review");
            bool inMonth = day.Month == month.Month && day.Year == month.Year;
            bool chosen = day.Date == selected.Date;
            bool hoveredNow = day.Date == hovered.Date;
            bool hasRecord = learned + reviewed + problems > 0 || lists.Any(x =>
                (x.history != null && x.history.Any(h => h.answeredAt != DateTime.MinValue)) || x.activeMilliseconds > 0);
            int alpha = !inMonth ? 95 : hoveredNow ? 232 : learned + reviewed + problems > 0 ? 212 : 182;
            using (GraphicsPath path = ModernUI.Round(rect, 9))
            {
                using (Brush fill = new SolidBrush(hasRecord
                    ? (hoveredNow ? Color.FromArgb(246, 247, 249) : Color.FromArgb(222, 226, 232))
                    : Color.FromArgb(alpha, 29, 36, 46)))
                    graphics.FillPath(fill, path);
                using (Pen border = new Pen(chosen ? ModernUI.Accent
                    : hoveredNow ? Color.FromArgb(150, 149, 161)
                    : Color.FromArgb(87, 97, 111), chosen ? 2f : 1f))
                    graphics.DrawPath(border, path);
            }
            Color ink = hasRecord ? Color.FromArgb(38, 45, 55) : inMonth ? Color.White : Color.FromArgb(160, 170, 180);
            int dateHeight;
            if (rect.Height < 15 || rect.Width < 18) return;
            float dateSize = rect.Height < 28 ? 7.5f : rect.Height < 36 ? 9f : 10f;
            using (Font dateFont = new Font("Segoe UI", dateSize, FontStyle.Bold))
            {
                dateHeight = Math.Min(Math.Max(1, rect.Height - 4),
                    TextRenderer.MeasureText(graphics, "31", dateFont,
                        new Size(Math.Max(1, rect.Width - 14), int.MaxValue),
                        TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Height + 3);
                TextRenderer.DrawText(graphics, day.Day.ToString(), dateFont,
                    new Rectangle(rect.X + 7, rect.Y + 3, rect.Width - 14, dateHeight), ink,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.NoPadding);
            }
            if (day.Date == DateTime.Today)
            {
                using (Brush today = new SolidBrush(ModernUI.Accent))
                    graphics.FillEllipse(today, rect.Right - 18, rect.Y + 9, 7, 7);
            }
            using (Font metric = new Font("Segoe UI", 8.5f))
            {
                int lineHeight = Math.Max(20, TextRenderer.MeasureText(graphics, "新表 999", metric,
                    new Size(rect.Width - 12, int.MaxValue), TextFormatFlags.SingleLine).Height + 2);
                int firstLine = rect.Y + dateHeight + 4;
                if (firstLine + lineHeight * 2 + 3 > rect.Bottom) return;
                TextRenderer.DrawText(graphics, "新 " + learned + "   列 " + reviewed,
                    metric, new Rectangle(rect.X + 7, firstLine, rect.Width - 12, lineHeight),
                    hasRecord ? ink : inMonth ? Color.FromArgb(170, 225, 255) : ink,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(graphics, "错 " + problems + "  新表 " + lists.Count(x => x.kind == "new"),
                    metric, new Rectangle(rect.X + 7, firstLine + lineHeight, rect.Width - 12, lineHeight),
                    hasRecord ? ink : inMonth ? Color.FromArgb(197, 226, 182) : ink,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            CalendarMetrics metrics = MeasureLayout();
            if (e.Y < metrics.GridTop || e.X < 12 || e.X >= Width - 12) return;
            int col = (e.X - 12) / metrics.CellWidth;
            int row = (e.Y - metrics.GridTop) / metrics.CellHeight;
            if (col < 0 || col >= 7 || row < 0 || row >= 6) return;
            DateTime first = month.AddDays(-((int)month.DayOfWeek + 6) % 7);
            DateTime next = first.AddDays(row * 7 + col);
            if (next.Date == hovered.Date) return;
            hovered = next;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hovered = DateTime.MinValue;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            CalendarMetrics metrics = MeasureLayout();
            if (metrics.Previous.Contains(e.Location))
            { month = month.AddMonths(-1); Invalidate(); return; }
            if (metrics.Next.Contains(e.Location))
            { month = month.AddMonths(1); Invalidate(); return; }
            if (e.Y < metrics.GridTop) return;
            int col = (e.X - 12) / metrics.CellWidth;
            int row = (e.Y - metrics.GridTop) / metrics.CellHeight;
            if (col < 0 || col >= 7 || row < 0 || row >= 6) return;
            DateTime first = month.AddDays(-((int)month.DayOfWeek + 6) % 7);
            selected = first.AddDays(row * 7 + col);
            month = new DateTime(selected.Year, selected.Month, 1);
            Invalidate();
            if (SelectedDateChanged != null) SelectedDateChanged(this, EventArgs.Empty);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && video != null) video.Dispose();
            if (disposing && backgroundImage != null) backgroundImage.Dispose();
            base.Dispose(disposing);
        }
    }
}
