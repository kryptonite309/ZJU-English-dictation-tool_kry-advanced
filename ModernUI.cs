using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EnglishDictationTool
{
    internal static class ModernUI
    {
        public static readonly Color Accent = Color.FromArgb(174, 150, 255);
        public static readonly Color AccentWash = Color.FromArgb(44, 39, 67);
        public static readonly Color Card = Color.FromArgb(25, 30, 37);
        public static readonly Color CardRaised = Color.FromArgb(32, 39, 49);

        public static void ApplyAppIcon(Form form)
        {
            if (form == null) return;
            try
            {
                using (Icon executableIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath))
                {
                    if (executableIcon != null) form.Icon = (Icon)executableIcon.Clone();
                }
            }
            catch
            {
                // A missing shell icon must never prevent a study window from opening.
            }
        }

        public static Size FitWindow(int width, int height)
        {
            Rectangle available = Screen.FromPoint(Cursor.Position).WorkingArea;
            return new Size(Math.Max(760, Math.Min(width, available.Width - 72)),
                Math.Max(520, Math.Min(height, available.Height - 96)));
        }

        public static GraphicsPath Round(Rectangle bounds, int radius)
        {
            int r = Math.Max(2, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2));
            GraphicsPath path = new GraphicsPath();
            int diameter = r * 2;
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class ModernButton : Button
    {
        private bool hovered;
        private bool pressed;
        private bool primary;
        private bool subtle;
        public bool Primary { get { return primary; } set { primary = value; Invalidate(); } }
        public bool Subtle { get { return subtle; } set { subtle = value; Invalidate(); } }

        public ModernButton()
        {
            Height = 42;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            ForeColor = Theme.Text;
            BackColor = ModernUI.CardRaised;
            Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold);
            UseVisualStyleBackColor = false;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            Cursor = Cursors.Hand;
            UpdateRoundedRegion();
        }

        public override void NotifyDefault(bool value)
        {
            base.NotifyDefault(false);
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateRoundedRegion();
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            Invalidate();
        }

        private void UpdateRoundedRegion()
        {
            if (Width < 2 || Height < 2) return;
            Region previous = Region;
            using (GraphicsPath path = ModernUI.Round(new Rectangle(0, 0, Width, Height), 11))
                Region = new Region(path);
            if (previous != null) previous.Dispose();
        }

        private Color BackdropColor()
        {
            for (Control ancestor = Parent; ancestor != null; ancestor = ancestor.Parent)
            {
                if (ancestor is ModernCard || ancestor is ModernGroupBox) return ModernUI.Card;
                if (ancestor.BackColor.A == 255) return ancestor.BackColor;
            }
            return Theme.Background;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(BackdropColor());
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // ButtonBase can leave its rectangular native surface visible around a custom
            // rounded path. Clear it here as well as in OnPaintBackground, then constrain
            // the actual control window with Region so the corners always belong to the parent.
            e.Graphics.Clear(BackdropColor());
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
            Color fill = primary ? ModernUI.Accent : subtle ? ModernUI.Card : ModernUI.CardRaised;
            if (hovered) fill = primary ? Color.FromArgb(192, 175, 255) : Color.FromArgb(44, 52, 63);
            if (pressed) fill = primary ? Color.FromArgb(150, 126, 231) : Color.FromArgb(35, 41, 50);
            if (!Enabled) fill = Color.FromArgb(33, 37, 43);
            using (GraphicsPath path = ModernUI.Round(rect, 11))
            {
                using (Brush brush = new SolidBrush(fill)) e.Graphics.FillPath(brush, path);
                using (Pen border = new Pen(primary ? ModernUI.Accent : Theme.Border, 1f))
                    e.Graphics.DrawPath(border, path);
            }
            Color ink = !Enabled ? Theme.MutedText : primary ? Color.FromArgb(19, 17, 30) : Theme.Text;
            Rectangle textRect = Rectangle.Inflate(rect, -10, -3);
            TextRenderer.DrawText(e.Graphics, Text, Font, textRect, ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);
        }
    }

    internal sealed class ModernProgressBar : Control
    {
        private int completed;
        private int total;

        public int Completed
        {
            get { return completed; }
            set { completed = Math.Max(0, value); Invalidate(); }
        }

        public int Total
        {
            get { return total; }
            set { total = Math.Max(0, value); Invalidate(); }
        }

        public ModernProgressBar()
        {
            Height = 34;
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent == null ? Theme.Background : Parent.BackColor);
            Rectangle track = new Rectangle(1, 3, Math.Max(1, Width - 3), Math.Max(1, Height - 7));
            using (GraphicsPath path = ModernUI.Round(track, 10))
            {
                using (Brush brush = new SolidBrush(ModernUI.CardRaised))
                    e.Graphics.FillPath(brush, path);
                using (Pen border = new Pen(Theme.Border)) e.Graphics.DrawPath(border, path);
            }
            int safeTotal = Math.Max(0, total);
            int safeCompleted = Math.Min(Math.Max(0, completed), safeTotal);
            int percent = safeTotal == 0 ? 100 : safeCompleted * 100 / safeTotal;
            int fillWidth = safeTotal == 0 ? track.Width
                : (int)Math.Round(track.Width * (safeCompleted / (double)safeTotal));
            if (fillWidth > 0)
            {
                Rectangle fill = new Rectangle(track.Left, track.Top,
                    Math.Min(track.Width, Math.Max(1, fillWidth)), track.Height);
                using (GraphicsPath path = ModernUI.Round(track, 10))
                using (Brush brush = new SolidBrush(ModernUI.Accent))
                {
                    GraphicsState state = e.Graphics.Save();
                    e.Graphics.SetClip(path);
                    e.Graphics.FillRectangle(brush, fill);
                    e.Graphics.Restore(state);
                }
            }
            string label = safeCompleted + " / " + safeTotal + "  ·  " + percent + "%";
            TextRenderer.DrawText(e.Graphics, label, Font, track, Theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);
        }
    }

    internal sealed class ModernCard : Panel
    {
        public string Title { get; set; }
        public string Detail { get; set; }
        public string Eyebrow { get; set; }

        public ModernCard()
        {
            DoubleBuffered = true;
            BackColor = ModernUI.Card;
            ForeColor = Theme.Text;
            Padding = new Padding(20);
            Resize += delegate { Invalidate(); };
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent == null ? Theme.Background : Parent.BackColor);
            if (Width < 4 || Height < 4) return;
            using (GraphicsPath path = ModernUI.Round(new Rectangle(1, 1, Width - 3, Height - 3), 18))
            {
                using (Brush brush = new SolidBrush(ModernUI.Card)) e.Graphics.FillPath(brush, path);
                using (Pen border = new Pen(Theme.Border)) e.Graphics.DrawPath(border, path);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (string.IsNullOrEmpty(Title) && string.IsNullOrEmpty(Detail)) return;
            int width = Math.Max(10, Width - 42);
            int top = 18;
            if (!string.IsNullOrEmpty(Eyebrow))
                using (Font small = new Font("Microsoft YaHei UI", 9, FontStyle.Bold))
                {
                    int height = Math.Max(30, TextRenderer.MeasureText(e.Graphics, Eyebrow,
                        small, new Size(width, int.MaxValue), TextFormatFlags.SingleLine).Height + 6);
                    TextRenderer.DrawText(e.Graphics, Eyebrow, small, new Rectangle(21, top, width, height),
                        ModernUI.Accent, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
                    top += height + 7;
                }
            using (Font titleFont = new Font("Microsoft YaHei UI", 16, FontStyle.Bold))
            {
                int height = Math.Max(52, TextRenderer.MeasureText(e.Graphics, Title ?? "",
                    titleFont, new Size(width, int.MaxValue), TextFormatFlags.WordBreak).Height + 12);
                TextRenderer.DrawText(e.Graphics, Title ?? "", titleFont,
                    new Rectangle(21, top, width, height), Theme.Text,
                    TextFormatFlags.WordBreak | TextFormatFlags.VerticalCenter);
                top += height + 5;
            }
            using (Font detailFont = new Font("Microsoft YaHei UI", 9))
            {
                int height = Math.Max(42, TextRenderer.MeasureText(e.Graphics, Detail ?? "",
                    detailFont, new Size(width, int.MaxValue), TextFormatFlags.WordBreak).Height + 8);
                TextRenderer.DrawText(e.Graphics, Detail ?? "", detailFont,
                    new Rectangle(21, top, width, height), Theme.MutedText,
                    TextFormatFlags.WordBreak | TextFormatFlags.VerticalCenter);
            }
        }
    }

    internal sealed class ModernGroupBox : GroupBox
    {
        public ModernGroupBox()
        {
            BackColor = ModernUI.Card;
            ForeColor = Theme.Text;
            Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold);
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent == null ? Theme.Background : Parent.BackColor);
            if (Width < 4 || Height < 18) return;
            int captionHeight = Math.Max(30, TextRenderer.MeasureText(e.Graphics, Text,
                Font, new Size(Math.Max(10, Width - 28), int.MaxValue),
                TextFormatFlags.SingleLine).Height + 8);
            Rectangle frame = new Rectangle(1, captionHeight / 2, Width - 3,
                Height - captionHeight / 2 - 2);
            using (GraphicsPath path = ModernUI.Round(frame, 12))
            {
                using (Brush fill = new SolidBrush(ModernUI.Card)) e.Graphics.FillPath(fill, path);
                using (Pen edge = new Pen(Theme.Border)) e.Graphics.DrawPath(edge, path);
            }
            Rectangle caption = new Rectangle(15, 0, Math.Max(10, Width - 28), captionHeight);
            Size size = TextRenderer.MeasureText(e.Graphics, Text, Font,
                new Size(caption.Width, caption.Height), TextFormatFlags.SingleLine);
            using (Brush fill = new SolidBrush(ModernUI.Card))
                e.Graphics.FillRectangle(fill, caption.Left, caption.Top,
                    Math.Min(caption.Width, size.Width + 12), caption.Height);
            TextRenderer.DrawText(e.Graphics, Text, Font, caption, Theme.MutedText,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    internal sealed class ModernHeader : Panel
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }

        public ModernHeader() { DoubleBuffered = true; BackColor = Theme.Background; }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int width = Math.Max(100, Width - 310);
            int top = 12;
            using (Font title = new Font("Microsoft YaHei UI", 26, FontStyle.Bold))
            {
                int height = Math.Max(64, TextRenderer.MeasureText(e.Graphics, Title ?? "",
                    title, new Size(width, int.MaxValue), TextFormatFlags.SingleLine).Height + 8);
                TextRenderer.DrawText(e.Graphics, Title ?? "", title,
                    new Rectangle(26, top, width, height), Theme.Text,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
                top += height + 4;
            }
            using (Font subtitle = new Font("Microsoft YaHei UI", 10.5f))
            {
                int height = Math.Max(34, TextRenderer.MeasureText(e.Graphics, Subtitle ?? "",
                    subtitle, new Size(width, int.MaxValue), TextFormatFlags.SingleLine).Height + 6);
                TextRenderer.DrawText(e.Graphics, Subtitle ?? "", subtitle,
                    new Rectangle(28, top, width, height), Theme.MutedText,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
            }
        }
    }

    internal sealed class ModernTabControl : TabControl
    {
        public ModernTabControl()
        {
            DrawMode = TabDrawMode.OwnerDrawFixed;
            SizeMode = TabSizeMode.Fixed;
            ItemSize = new Size(132, 42);
            Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold);
            BackColor = Theme.Background;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Background);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Theme.Background);
            for (int index = 0; index < TabPages.Count; index++)
                DrawTab(e.Graphics, index);
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            base.OnSelectedIndexChanged(e);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            for (int index = 0; index < TabPages.Count; index++)
                if (GetTabRect(index).Contains(e.Location))
                {
                    SelectedIndex = index;
                    break;
                }
            base.OnMouseDown(e);
        }

        private void DrawTab(Graphics graphics, int index)
        {
            bool selected = index == SelectedIndex;
            Rectangle rect = Rectangle.Inflate(GetTabRect(index), -3, -3);
            if (rect.Width < 4 || rect.Height < 4) return;
            using (GraphicsPath path = ModernUI.Round(rect, 10))
            {
                using (Brush fill = new SolidBrush(selected ? ModernUI.AccentWash : Theme.Surface))
                    graphics.FillPath(fill, path);
                using (Pen border = new Pen(selected ? ModernUI.Accent : Theme.Border))
                    graphics.DrawPath(border, path);
            }
            TextRenderer.DrawText(graphics, TabPages[index].Text, Font, rect,
                selected ? Theme.Text : Theme.MutedText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);
        }
    }
}
