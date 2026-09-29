using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EnglishDictationTool
{
    internal static class ModernUI
    {
        public static readonly Color Accent = Color.FromArgb(244, 244, 245);
        public static readonly Color AccentWash = Color.FromArgb(37, 42, 50);
        public static readonly Color Card = Color.FromArgb(21, 24, 29);
        public static readonly Color CardRaised = Color.FromArgb(29, 33, 40);
        public static readonly Color MutedSurface = Color.FromArgb(37, 42, 50);
        public static readonly Color Muted = Color.FromArgb(161, 161, 170);
        public static readonly Color MutedSoft = Color.FromArgb(113, 113, 122);

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
            form.Load += delegate
            {
                // Keep the top-level window fully opaque.  Fading a video-backed
                // HWND forces the desktop compositor to rebuild layered surfaces
                // and was a visible source of flashing on maximize/restore.
                form.Opacity = 1d;
            };
        }

        public static Color Blend(Color from, Color to, float amount)
        {
            amount = Math.Max(0f, Math.Min(1f, amount));
            return Color.FromArgb(
                (int)Math.Round(from.A + (to.A - from.A) * amount),
                (int)Math.Round(from.R + (to.R - from.R) * amount),
                (int)Math.Round(from.G + (to.G - from.G) * amount),
                (int)Math.Round(from.B + (to.B - from.B) * amount));
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
        private bool pressed;
        private bool primary;
        private bool subtle;
        private readonly Timer motionTimer;
        private float hoverAmount;
        private float hoverTarget;
        public bool Primary { get { return primary; } set { primary = value; Invalidate(); } }
        public bool Subtle { get { return subtle; } set { subtle = value; Invalidate(); } }

        public ModernButton()
        {
            Height = 46;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            ForeColor = Theme.Text;
            BackColor = ModernUI.CardRaised;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            UseVisualStyleBackColor = false;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            Cursor = Cursors.Hand;
            motionTimer = new Timer { Interval = 15 };
            motionTimer.Tick += delegate
            {
                float distance = hoverTarget - hoverAmount;
                hoverAmount += distance * 0.32f;
                if (Math.Abs(distance) < 0.015f)
                {
                    hoverAmount = hoverTarget;
                    motionTimer.Stop();
                }
                Invalidate();
            };
            UpdateRoundedRegion();
        }

        public override void NotifyDefault(bool value)
        {
            base.NotifyDefault(false);
        }

        protected override void OnMouseEnter(EventArgs e) { hoverTarget = 1f; motionTimer.Start(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { pressed = false; hoverTarget = 0f; motionTimer.Start(); base.OnMouseLeave(e); }
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
            using (GraphicsPath path = ModernUI.Round(new Rectangle(0, 0, Width, Height), 14))
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
            int lift = pressed ? 1 : (int)Math.Round(hoverAmount);
            int inset = pressed ? 2 : 1;
            Rectangle rect = new Rectangle(inset, inset - lift,
                Math.Max(1, Width - inset * 2 - 1), Math.Max(1, Height - inset * 2 - 1));
            Color fill = primary ? ModernUI.Accent : subtle ? ModernUI.Card : ModernUI.CardRaised;
            Color hoverFill = primary ? Color.White : ModernUI.MutedSurface;
            fill = ModernUI.Blend(fill, hoverFill, hoverAmount);
            if (pressed) fill = primary ? Color.FromArgb(220, 220, 222) : Color.FromArgb(25, 29, 35);
            if (!Enabled) fill = Color.FromArgb(33, 37, 43);
            using (GraphicsPath path = ModernUI.Round(rect, 14))
            {
                using (Brush brush = new SolidBrush(fill)) e.Graphics.FillPath(brush, path);
                using (Pen border = new Pen(primary ? Color.FromArgb(235, 235, 238) : Theme.Border, 1f))
                    e.Graphics.DrawPath(border, path);
            }
            Color ink = !Enabled ? Theme.MutedText : primary ? Theme.Background : Theme.Text;
            Rectangle textRect = Rectangle.Inflate(rect, -10, -3);
            TextRenderer.DrawText(e.Graphics, Text, Font, textRect, ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && motionTimer != null) motionTimer.Dispose();
            base.Dispose(disposing);
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
            Height = 38;
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent == null ? Theme.Background : Parent.BackColor);
            Rectangle labelRect = new Rectangle(1, 0, Math.Max(1, Width - 3), 22);
            Rectangle track = new Rectangle(1, 27, Math.Max(1, Width - 3), 7);
            using (GraphicsPath path = ModernUI.Round(track, 4))
            {
                using (Brush brush = new SolidBrush(ModernUI.CardRaised))
                    e.Graphics.FillPath(brush, path);
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
                using (GraphicsPath path = ModernUI.Round(track, 4))
                using (Brush brush = new SolidBrush(ModernUI.Accent))
                {
                    GraphicsState state = e.Graphics.Save();
                    e.Graphics.SetClip(path);
                    e.Graphics.FillRectangle(brush, fill);
                    e.Graphics.Restore(state);
                }
            }
            TextRenderer.DrawText(e.Graphics, "学习进度", Font, labelRect, Theme.MutedText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(e.Graphics,
                safeCompleted + " / " + safeTotal + "    " + percent + "%", Font,
                labelRect, Theme.Text, TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
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
            Padding = new Padding(24);
            Resize += delegate { Invalidate(); };
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent == null ? Theme.Background : Parent.BackColor);
            if (Width < 4 || Height < 4) return;
            using (GraphicsPath path = ModernUI.Round(new Rectangle(1, 1, Width - 3, Height - 3), 22))
            {
                using (LinearGradientBrush glass = new LinearGradientBrush(
                    new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3)),
                    Color.FromArgb(29, 33, 40), Color.FromArgb(18, 21, 26), 108f))
                    e.Graphics.FillPath(glass, path);
                using (Pen border = new Pen(Color.FromArgb(58, 64, 75)))
                    e.Graphics.DrawPath(border, path);
                using (Pen sheen = new Pen(Color.FromArgb(32, 255, 255, 255)))
                    e.Graphics.DrawLine(sheen, 24, 2, Math.Max(24, Width - 25), 2);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (string.IsNullOrEmpty(Title) && string.IsNullOrEmpty(Detail)) return;
            int width = Math.Max(10, Width - 50);
            int top = 21;
            if (!string.IsNullOrEmpty(Eyebrow))
                using (Font small = new Font("Segoe UI", 8.5f, FontStyle.Bold))
                {
                    int height = Math.Max(30, TextRenderer.MeasureText(e.Graphics, Eyebrow,
                        small, new Size(width, int.MaxValue), TextFormatFlags.SingleLine).Height + 6);
                    TextRenderer.DrawText(e.Graphics, Eyebrow, small, new Rectangle(25, top, width, height),
                        ModernUI.Muted, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
                    top += height + 5;
                }
            using (Font titleFont = new Font("Segoe UI", 19, FontStyle.Regular))
            {
                int height = Math.Max(52, TextRenderer.MeasureText(e.Graphics, Title ?? "",
                    titleFont, new Size(width, int.MaxValue), TextFormatFlags.WordBreak).Height + 12);
                TextRenderer.DrawText(e.Graphics, Title ?? "", titleFont,
                    new Rectangle(25, top, width, height), Theme.Text,
                    TextFormatFlags.WordBreak | TextFormatFlags.VerticalCenter);
                top += height + 5;
            }
            using (Font detailFont = new Font("Segoe UI", 9.5f))
            {
                int height = Math.Max(42, TextRenderer.MeasureText(e.Graphics, Detail ?? "",
                    detailFont, new Size(width, int.MaxValue), TextFormatFlags.WordBreak).Height + 8);
                TextRenderer.DrawText(e.Graphics, Detail ?? "", detailFont,
                    new Rectangle(25, top, width, height), Theme.MutedText,
                    TextFormatFlags.WordBreak | TextFormatFlags.VerticalCenter);
            }
        }
    }

    internal sealed class ModernMetric : Control
    {
        private string caption = string.Empty;
        private string value = "0";
        private string detail = string.Empty;
        private bool showDivider;

        public string Caption { get { return caption; } set { caption = value ?? string.Empty; Invalidate(); } }
        public string Value { get { return value; } set { this.value = value ?? string.Empty; Invalidate(); } }
        public string Detail { get { return detail; } set { detail = value ?? string.Empty; Invalidate(); } }
        public bool ShowDivider { get { return showDivider; } set { showDivider = value; Invalidate(); } }

        public ModernMetric()
        {
            DoubleBuffered = true;
            BackColor = ModernUI.Card;
            ForeColor = Theme.Text;
            MinimumSize = new Size(150, 104);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(ModernUI.Card);
            float scale = Math.Max(.92f, Math.Min(1.36f,
                Math.Min(Width / 360f, Height / 112f)));
            if (showDivider)
                using (Pen line = new Pen(Color.FromArgb(47, 52, 62)))
                    e.Graphics.DrawLine(line, Width - 1, (int)(18 * scale), Width - 1,
                        Math.Max((int)(18 * scale), Height - (int)(18 * scale)));
            int inset = Math.Max(14, (int)Math.Round(18 * scale));
            Rectangle content = new Rectangle(inset, 4, Math.Max(20, Width - inset * 2),
                Math.Max(20, Height - 8));
            using (Font captionFont = new Font("Segoe UI", 9.5f * scale))
            using (Font valueFont = new Font("Segoe UI", 24f * scale, FontStyle.Regular))
            using (Font detailFont = new Font("Segoe UI", 8.5f * scale))
            {
                TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.SingleLine;
                bool hasDetail = !string.IsNullOrWhiteSpace(detail);
                int captionHeight = Math.Max(24, (int)Math.Round(content.Height *
                    (hasDetail ? .26f : .38f)));
                int valueHeight = hasDetail
                    ? Math.Max(42, (int)Math.Round(content.Height * .44f))
                    : Math.Max(42, content.Height - captionHeight);
                int detailHeight = hasDetail
                    ? Math.Max(22, content.Height - captionHeight - valueHeight) : 0;
                int top = content.Top;
                TextRenderer.DrawText(e.Graphics, caption, captionFont,
                    new Rectangle(content.Left, top, content.Width, captionHeight), ModernUI.Muted, flags);
                top += captionHeight;
                TextRenderer.DrawText(e.Graphics, value, valueFont,
                    new Rectangle(content.Left, top, content.Width, valueHeight), Theme.Text, flags);
                if (hasDetail)
                {
                    top += valueHeight;
                    TextRenderer.DrawText(e.Graphics, detail, detailFont,
                        new Rectangle(content.Left, top, content.Width, detailHeight), ModernUI.MutedSoft, flags);
                }
            }
        }
    }

    internal sealed class StudyModeWheel : Control
    {
        private readonly string[] keys;
        private readonly string[] titles;
        private readonly Timer motionTimer;
        private readonly System.Collections.Generic.Dictionary<int, Rectangle> hitAreas
            = new System.Collections.Generic.Dictionary<int, Rectangle>();
        private int selectedIndex;
        private double visualPosition;
        private double targetPosition;
        private double velocity;
        private DateTime lastWheelInput;

        public event EventHandler SelectionChanged;

        public string SelectedKey
        {
            get { return keys.Length == 0 ? string.Empty : keys[selectedIndex]; }
        }

        public int SelectedIndex { get { return selectedIndex; } }

        public StudyModeWheel(string[] itemKeys, string[] itemTitles)
        {
            keys = itemKeys ?? new string[0];
            titles = itemTitles ?? new string[0];
            if (keys.Length != titles.Length) throw new ArgumentException("学习模式键和值数量不一致。");
            DoubleBuffered = true;
            TabStop = true;
            Cursor = Cursors.Hand;
            BackColor = ModernUI.Card;
            ForeColor = Theme.Text;
            motionTimer = new Timer { Interval = 15 };
            motionTimer.Tick += delegate { AdvanceMotion(); };
        }

        public void SelectMode(string key)
        {
            int index = Array.IndexOf(keys, key);
            if (index >= 0) SelectIndex(index, false);
        }

        public void Step(int direction)
        {
            if (keys.Length == 0 || direction == 0) return;
            int next = (selectedIndex + (direction > 0 ? 1 : -1) + keys.Length) % keys.Length;
            SelectIndex(next, true);
        }

        private void SelectIndex(int next, bool animate)
        {
            if (keys.Length == 0) return;
            next = (next % keys.Length + keys.Length) % keys.Length;
            if (next == selectedIndex && !motionTimer.Enabled) return;
            double delta = next - selectedIndex;
            if (delta > keys.Length / 2d) delta -= keys.Length;
            if (delta < -keys.Length / 2d) delta += keys.Length;
            selectedIndex = next;
            if (!animate)
            {
                visualPosition = next;
                targetPosition = next;
                velocity = 0d;
                motionTimer.Stop();
            }
            else
            {
                // Advance from the existing logical destination, not from the
                // partially animated screen position. This guarantees that every
                // wheel notch attaches to exactly the next item in its direction,
                // even when several notches arrive before the previous motion ends.
                targetPosition += delta;
                // Retain only a small amount of momentum when several wheel inputs
                // arrive in the same direction. A direction reversal cancels the
                // old momentum so the wheel never drifts away from the requested row.
                if (Math.Sign(velocity) != 0 && Math.Sign(velocity) != Math.Sign(delta))
                    velocity = 0d;
                else
                    velocity *= 0.28d;
                motionTimer.Start();
            }
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
            Invalidate();
        }

        private void AdvanceMotion()
        {
            double displacement = targetPosition - visualPosition;
            // A damped ease-out is deliberately used instead of an overshooting
            // spring. It preserves continuous wheel motion while making the final
            // attachment soft and predictable.
            double desiredVelocity = displacement * 0.14d;
            velocity += (desiredVelocity - velocity) * 0.18d;
            double step = velocity;
            if (Math.Abs(step) > Math.Abs(displacement)) step = displacement;
            visualPosition += step;
            if (Math.Abs(displacement) < 0.0015d && Math.Abs(velocity) < 0.0015d)
            {
                visualPosition = selectedIndex;
                targetPosition = selectedIndex;
                velocity = 0d;
                motionTimer.Stop();
            }
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Up || keyData == Keys.Down || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up) { Step(-1); e.Handled = true; }
            else if (e.KeyCode == Keys.Down) { Step(1); e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            DateTime now = DateTime.UtcNow;
            int direction = e.Delta < 0 ? 1 : -1;
            // Ignore the dense burst of events emitted by high-resolution wheels.
            // One deliberate gesture should advance one row instead of skipping
            // through several modes before the user's hand has stopped moving.
            if ((now - lastWheelInput).TotalMilliseconds > 90d)
            {
                Step(direction);
                lastWheelInput = now;
            }
            base.OnMouseWheel(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            if (e.Y < 30) Step(-1);
            else if (e.Y > Height - 30) Step(1);
            else
            {
                foreach (System.Collections.Generic.KeyValuePair<int, Rectangle> item in hitAreas)
                    if (item.Value.Contains(e.Location))
                    {
                        SelectIndex(item.Key, true);
                        break;
                    }
            }
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            hitAreas.Clear();
            if (titles.Length == 0) return;

            float scale = Math.Max(1f, Math.Min(1.34f,
                Math.Min(Width / 580f, Height / 540f)));
            using (Font counterFont = new Font("Segoe UI", 8.5f * scale, FontStyle.Bold))
            using (Brush counterBrush = new SolidBrush(ModernUI.MutedSoft))
                e.Graphics.DrawString((selectedIndex + 1).ToString("00") + " / "
                    + titles.Length.ToString("00"), counterFont, counterBrush, 18f * scale, 10f * scale);

            float centerY = Height / 2f;
            float spacing = 78f * scale;
            for (int index = 0; index < titles.Length; index++)
            {
                double distance = index - visualPosition;
                while (distance > titles.Length / 2d) distance -= titles.Length;
                while (distance < -titles.Length / 2d) distance += titles.Length;
                // Keep the focused row and its immediate neighbours visible. The spring
                // position gives the wheel a short, physical settle without delaying input.
                if (Math.Abs(distance) > 1.6d) continue;
                float focus = 1f - Math.Min(1f, (float)Math.Abs(distance));
                float y = centerY + (float)distance * spacing;
                // The selected font can exceed 50 physical pixels on a wide screen.
                // Keep generous vertical padding so ascenders and descenders are
                // never clipped by the focus row.
                int rowHeight = (int)Math.Round(72f * scale);
                Rectangle row = new Rectangle((int)(14 * scale), (int)y - rowHeight / 2,
                    Math.Max(20, Width - (int)(42 * scale)), rowHeight);
                hitAreas[index] = row;
                if (focus > .25f)
                {
                    using (GraphicsPath pill = ModernUI.Round(row, 16))
                    using (Brush fill = new SolidBrush(Color.FromArgb((int)(36 + focus * 22),
                        ModernUI.MutedSurface)))
                        e.Graphics.FillPath(fill, pill);
                }
                float fontSize = (14.2f + focus * 15.3f) * scale;
                int alpha = 62 + (int)(193 * focus);
                using (Font font = new Font("Segoe UI", fontSize,
                    focus > .55f ? FontStyle.Regular : FontStyle.Regular))
                using (Brush ink = new SolidBrush(Color.FromArgb(alpha, Theme.Text)))
                using (StringFormat format = new StringFormat { Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
                    e.Graphics.DrawString(titles[index], font, ink, row, format);
            }

            using (Pen arrow = new Pen(ModernUI.MutedSoft, 1.6f))
            {
                float x = Width - 18f;
                e.Graphics.DrawLines(arrow, new[] { new PointF(x - 5, 18), new PointF(x, 13), new PointF(x + 5, 18) });
                e.Graphics.DrawLines(arrow, new[] { new PointF(x - 5, Height - 18),
                    new PointF(x, Height - 13), new PointF(x + 5, Height - 18) });
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) motionTimer.Dispose();
            base.Dispose(disposing);
        }
    }

    internal sealed class ModernGroupBox : GroupBox
    {
        public ModernGroupBox()
        {
            BackColor = ModernUI.Card;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
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
            using (GraphicsPath path = ModernUI.Round(frame, 14))
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
        public string Eyebrow { get; set; }

        public ModernHeader() { DoubleBuffered = true; BackColor = Theme.Background; }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int width = Math.Max(100, Width - 340);
            float scale = Math.Max(1f, Math.Min(1.32f,
                Math.Min(Width / 1540f, Height / 150f)));
            int top = (int)Math.Round(12 * scale);
            using (Font eyebrow = new Font("Segoe UI", 8.5f * scale, FontStyle.Bold))
            {
                int eyebrowHeight = TextRenderer.MeasureText(e.Graphics, Eyebrow ?? string.Empty,
                    eyebrow, new Size(width, int.MaxValue), TextFormatFlags.SingleLine).Height + 4;
                TextRenderer.DrawText(e.Graphics, Eyebrow ?? string.Empty, eyebrow,
                    new Rectangle(27, top, width, eyebrowHeight), ModernUI.Muted,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
                top += eyebrowHeight + (int)Math.Round(8 * scale);
            }
            float titleSize = (width < 820 ? 27f : 32f) * scale;
            using (Font title = new Font("Segoe UI", titleSize, FontStyle.Bold))
            {
                int height = Math.Max(54, TextRenderer.MeasureText(e.Graphics, Title ?? "",
                    title, new Size(width, int.MaxValue), TextFormatFlags.SingleLine).Height + 4);
                TextRenderer.DrawText(e.Graphics, Title ?? "", title,
                    new Rectangle(26, top, width, height), Theme.Text,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
                top += height + 8;
            }
            using (Font subtitle = new Font("Segoe UI", 10f))
            {
                int height = Math.Max(28, TextRenderer.MeasureText(e.Graphics, Subtitle ?? "",
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
            ItemSize = new Size(142, 44);
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
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
            using (GraphicsPath path = ModernUI.Round(rect, 14))
            {
                using (Brush fill = new SolidBrush(selected ? ModernUI.MutedSurface : Theme.Background))
                    graphics.FillPath(fill, path);
                using (Pen border = new Pen(selected ? Color.FromArgb(66, 72, 82) : Theme.Border))
                    graphics.DrawPath(border, path);
            }
            TextRenderer.DrawText(graphics, TabPages[index].Text, Font, rect,
                selected ? Theme.Text : Theme.MutedText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);
        }
    }

    internal sealed class ModernListView : ListView
    {
        public ModernListView()
        {
            OwnerDraw = true;
            FullRowSelect = true;
            HideSelection = false;
            BorderStyle = BorderStyle.None;
            BackColor = ModernUI.Card;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9.5f);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
        {
            using (Brush fill = new SolidBrush(ModernUI.CardRaised))
                e.Graphics.FillRectangle(fill, e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.Header.Text, Font,
                Rectangle.Inflate(e.Bounds, -10, 0), ModernUI.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            using (Pen edge = new Pen(Theme.Border))
                e.Graphics.DrawLine(edge, e.Bounds.Left, e.Bounds.Bottom - 1,
                    e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        protected override void OnDrawItem(DrawListViewItemEventArgs e)
        {
            if (View != View.Details) DrawRow(e.Graphics, e.Bounds, e.Item.Selected,
                e.Item.Text, e.ItemIndex);
        }

        protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
        {
            DrawRow(e.Graphics, e.Bounds, e.Item.Selected, e.SubItem.Text, e.ItemIndex);
        }

        private void DrawRow(Graphics graphics, Rectangle bounds, bool selected,
            string text, int index)
        {
            Color fillColor = selected ? ModernUI.MutedSurface
                : index % 2 == 0 ? ModernUI.Card : Color.FromArgb(18, 21, 26);
            using (Brush fill = new SolidBrush(fillColor)) graphics.FillRectangle(fill, bounds);
            TextRenderer.DrawText(graphics, text ?? string.Empty, Font,
                Rectangle.Inflate(bounds, -9, 0), selected ? Theme.Text : Theme.MutedText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }
    }
}
