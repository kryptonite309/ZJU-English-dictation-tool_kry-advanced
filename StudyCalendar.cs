using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace EnglishDictationTool
{
    internal sealed class StudyCalendarView : Control
    {
        private readonly StudyStore store;
        private AppearanceStore appearance;
        private Image backgroundImage;
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
            MinimumSize = new Size(850, 550);
            DoubleBuffered = true;
            Font = Theme.UiFont;
            Cursor = Cursors.Hand;
            SetAppearance(appearanceStore);
        }

        public void SetAppearance(AppearanceStore appearanceStore)
        {
            appearance = appearanceStore;
            if (backgroundImage != null) { backgroundImage.Dispose(); backgroundImage = null; }
            string path = appearance.ImagePath(appearance.Settings.backgrounds["calendar"]);
            if (path != null)
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
            if (backgroundImage != null)
            {
                Rectangle target = ClientRectangle;
                if (background.fit != "stretch")
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
            using (Font title = new Font("Microsoft YaHei UI", 17, FontStyle.Bold))
                TextRenderer.DrawText(graphics, month.ToString("yyyy 年 M 月"), title,
                    new Rectangle(90, 14, Width - 180, 38), Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            DrawNav(graphics, new Rectangle(18, 18, 42, 34), "‹");
            DrawNav(graphics, new Rectangle(Width - 60, 18, 42, 34), "›");
            string[] weekdays = { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };
            int left = 12;
            int cellWidth = (Width - 24) / 7;
            for (int col = 0; col < 7; col++)
                TextRenderer.DrawText(graphics, weekdays[col], Font,
                    new Rectangle(left + col * cellWidth, 62, cellWidth, 29),
                    Color.FromArgb(190, 205, 218),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            DateTime first = month.AddDays(-((int)month.DayOfWeek + 6) % 7);
            int cellHeight = (Height - 103) / 6;
            for (int index = 0; index < 42; index++)
            {
                DateTime day = first.AddDays(index);
                int row = index / 7, col = index % 7;
                Rectangle rect = new Rectangle(left + col * cellWidth + 3,
                    94 + row * cellHeight + 3, cellWidth - 7, cellHeight - 7);
                PaintDay(graphics, day, rect);
            }
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
            using (Font arrow = new Font("Segoe UI", 16, FontStyle.Bold))
                TextRenderer.DrawText(graphics, text, arrow,
                    rect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
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
            int alpha = !inMonth ? 95 : hoveredNow ? 232 : learned + reviewed + problems > 0 ? 212 : 182;
            using (GraphicsPath path = ModernUI.Round(rect, 9))
            {
                using (Brush fill = new SolidBrush(Color.FromArgb(alpha, 29, 36, 46)))
                    graphics.FillPath(fill, path);
                using (Pen border = new Pen(chosen ? ModernUI.Accent
                    : hoveredNow ? Color.FromArgb(150, 149, 161)
                    : Color.FromArgb(87, 97, 111), chosen ? 2f : 1f))
                    graphics.DrawPath(border, path);
            }
            Color ink = inMonth ? Color.White : Color.FromArgb(160, 170, 180);
            int dateHeight;
            using (Font dateFont = new Font("Microsoft YaHei UI", 10, FontStyle.Bold))
            {
                dateHeight = Math.Max(27, TextRenderer.MeasureText(graphics, "31", dateFont,
                    new Size(rect.Width - 14, int.MaxValue), TextFormatFlags.SingleLine).Height + 3);
                TextRenderer.DrawText(graphics, day.Day.ToString(), dateFont,
                    new Rectangle(rect.X + 7, rect.Y + 3, rect.Width - 14, dateHeight), ink,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
            }
            if (day.Date == DateTime.Today)
            {
                using (Brush today = new SolidBrush(ModernUI.Accent))
                    graphics.FillEllipse(today, rect.Right - 18, rect.Y + 9, 7, 7);
            }
            if (rect.Height < 70) return;
            using (Font metric = new Font("Microsoft YaHei UI", 8.5f))
            {
                int lineHeight = Math.Max(20, TextRenderer.MeasureText(graphics, "新表 999", metric,
                    new Size(rect.Width - 12, int.MaxValue), TextFormatFlags.SingleLine).Height + 2);
                int firstLine = rect.Y + dateHeight + 4;
                TextRenderer.DrawText(graphics, "新 " + learned + "   列 " + reviewed,
                    metric, new Rectangle(rect.X + 7, firstLine, rect.Width - 12, lineHeight),
                    inMonth ? Color.FromArgb(170, 225, 255) : ink,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(graphics, "错 " + problems + "  新表 " + lists.Count(x => x.kind == "new"),
                    metric, new Rectangle(rect.X + 7, firstLine + lineHeight, rect.Width - 12, lineHeight),
                    inMonth ? Color.FromArgb(197, 226, 182) : ink,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.Y < 94 || e.X < 12 || e.X >= Width - 12) return;
            int cellWidth = (Width - 24) / 7;
            int cellHeight = (Height - 103) / 6;
            int col = (e.X - 12) / cellWidth;
            int row = (e.Y - 94) / cellHeight;
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
            if (e.Y >= 14 && e.Y <= 55 && e.X < 65)
            { month = month.AddMonths(-1); Invalidate(); return; }
            if (e.Y >= 14 && e.Y <= 55 && e.X > Width - 65)
            { month = month.AddMonths(1); Invalidate(); return; }
            if (e.Y < 94) return;
            int cellWidth = (Width - 24) / 7;
            int cellHeight = (Height - 103) / 6;
            int col = (e.X - 12) / cellWidth;
            int row = (e.Y - 94) / cellHeight;
            if (col < 0 || col >= 7 || row < 0 || row >= 6) return;
            DateTime first = month.AddDays(-((int)month.DayOfWeek + 6) % 7);
            selected = first.AddDays(row * 7 + col);
            month = new DateTime(selected.Year, selected.Month, 1);
            Invalidate();
            if (SelectedDateChanged != null) SelectedDateChanged(this, EventArgs.Empty);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && backgroundImage != null) backgroundImage.Dispose();
            base.Dispose(disposing);
        }
    }
}
