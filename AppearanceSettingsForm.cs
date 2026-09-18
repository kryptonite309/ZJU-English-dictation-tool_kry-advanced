using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EnglishDictationTool
{
    internal sealed class AppearanceSettingsForm : Form
    {
        private sealed class StyleControls
        {
            public ComboBox Font;
            public NumericUpDown Size;
            public Button Color;
            public CheckBox Bold;
        }

        private readonly AppearanceStore store;
        private readonly AppearanceSettings draft;
        private readonly Dictionary<string, StyleControls> styles = new Dictionary<string, StyleControls>();
        private readonly Dictionary<string, TextBox> prompts = new Dictionary<string, TextBox>();
        private ComboBox backgroundChoice;
        private Button solidColor;
        private NumericUpDown shade;
        private ComboBox fit;
        private Label imageName;
        private Panel preview;
        private string currentBackground;
        private TabControl tabs;
        private bool updatingBackground;

        private static readonly KeyValuePair<string, string>[] BackgroundNames = {
            new KeyValuePair<string, string>("free", "自由练习"),
            new KeyValuePair<string, string>("new", "新学"),
            new KeyValuePair<string, string>("list_review", "历史列表复习"),
            new KeyValuePair<string, string>("problem_review", "错题与易错词复习"),
            new KeyValuePair<string, string>("calendar", "日历")
        };

        public AppearanceSettingsForm(AppearanceStore appearance)
        {
            store = appearance;
            draft = store.CopySettings();
            Text = "外观定制";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = ModernUI.FitWindow(1320, 820);
            MinimumSize = new Size(960, 650);
            BackColor = Theme.Background; ForeColor = Theme.Text; Font = Theme.UiFont;
            TableLayoutPanel shell = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2,
                ColumnCount = 1, Padding = new Padding(14), BackColor = Theme.Background };
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            Controls.Add(shell);
            tabs = new ModernTabControl { Dock = DockStyle.Fill };
            shell.Controls.Add(tabs, 0, 0);
            BuildTextTab(tabs);
            BuildPromptTab(tabs);
            BuildBackgroundTab(tabs);
            FlowLayoutPanel bottom = new FlowLayoutPanel { Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            Button save = MakeButton("保存外观", delegate { SaveAll(); });
            Button cancel = MakeButton("取消", delegate { Close(); });
            bottom.Controls.Add(save); bottom.Controls.Add(cancel);
            shell.Controls.Add(bottom, 0, 1);
            Theme.Apply(this);
            foreach (StyleControls control in styles.Values)
            {
                control.Color.BackColor = Color.FromArgb((int)control.Color.Tag);
                control.Color.ForeColor = Contrast(control.Color.BackColor);
            }
            RefreshBackground();
        }

        internal void SelectTab(string title)
        {
            TabPage page = tabs.TabPages.Cast<TabPage>().FirstOrDefault(x => x.Text.StartsWith(title));
            if (page != null) tabs.SelectedTab = page;
        }

        private static Button MakeButton(string title, EventHandler click)
        {
            Button button = title == "选择颜色"
                ? new Button { Text = title, Width = 125, Height = 40,
                    FlatStyle = FlatStyle.Flat, BackColor = Theme.SurfaceRaised, ForeColor = Theme.Text }
                : (Button)new ModernButton { Text = title, Width = 125, Height = 40 };
            button.Click += click;
            return button;
        }

        private static void AddPageContent(TabPage page, Control content)
        {
            ModernCard card = new ModernCard { Dock = DockStyle.Fill, Padding = new Padding(10) };
            page.Controls.Add(card);
            card.Controls.Add(content);
        }

        private static TabPage Page(TabControl tabs, string title)
        {
            TabPage page = new TabPage(title) { BackColor = Theme.Background, ForeColor = Theme.Text };
            tabs.TabPages.Add(page);
            return page;
        }

        private static Label Label(string title, int width)
        {
            int measured = TextRenderer.MeasureText(title ?? string.Empty, Theme.UiFont,
                new Size(width, int.MaxValue), TextFormatFlags.WordBreak).Height;
            return new Label { Text = title, Width = width, Height = Math.Max(36, measured + 8),
                TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.Text };
        }

        private void BuildTextTab(TabControl tabs)
        {
            TabPage page = Page(tabs, "文字样式");
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true,
                FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16) };
            AddPageContent(page, flow);
            flow.Controls.Add(Label("各学习界面共用文字样式；字体和颜色互不覆盖。", 870));
            KeyValuePair<string, string>[] names = {
                new KeyValuePair<string, string>("normal", "普通说明"),
                new KeyValuePair<string, string>("word", "英文单词"),
                new KeyValuePair<string, string>("example", "英文例句"),
                new KeyValuePair<string, string>("meaning", "中文释义"),
                new KeyValuePair<string, string>("correct", "答对提示"),
                new KeyValuePair<string, string>("error", "答错提示"),
                new KeyValuePair<string, string>("mastered", "斩词提示")
            };
            string[] fontNames = FontFamily.Families.Select(x => x.Name).ToArray();
            foreach (KeyValuePair<string, string> name in names)
            {
                TextAppearance initial = draft.text[name.Key];
                FlowLayoutPanel row = new FlowLayoutPanel { Width = 900, Height = 50,
                    WrapContents = false, FlowDirection = FlowDirection.LeftToRight };
                row.Controls.Add(Label(name.Value, 125));
                ComboBox font = new DarkComboBox {
                    Width = 300, BackColor = Theme.Surface, ForeColor = Theme.Text };
                font.Items.AddRange(fontNames);
                font.SelectedItem = initial.font;
                if (font.SelectedIndex < 0) { font.Items.Add(initial.font); font.SelectedItem = initial.font; }
                row.Controls.Add(font);
                NumericUpDown size = new NumericUpDown { Minimum = 8, Maximum = 72, DecimalPlaces = 1,
                    Increment = .5M, Value = (decimal)initial.size, Width = 76,
                    BackColor = Theme.Surface, ForeColor = Theme.Text };
                row.Controls.Add(size);
                row.Controls.Add(Label("号", 25));
                Button color = ColorButton(initial.color);
                row.Controls.Add(color);
                CheckBox bold = new CheckBox { Text = "加粗", Checked = initial.bold,
                    Width = 80, ForeColor = Theme.Text };
                row.Controls.Add(bold);
                styles[name.Key] = new StyleControls { Font = font, Size = size, Color = color, Bold = bold };
                flow.Controls.Add(row);
            }
        }

        private static Button ColorButton(int argb)
        {
            Button button = MakeButton("选择颜色", null);
            button.Width = 120;
            button.Tag = argb;
            button.BackColor = Color.FromArgb(argb);
            button.ForeColor = Contrast(Color.FromArgb(argb));
            button.Click += delegate
            {
                using (ColorDialog picker = new ColorDialog { Color = Color.FromArgb((int)button.Tag), FullOpen = true })
                {
                    if (picker.ShowDialog(button.FindForm()) != DialogResult.OK) return;
                    button.Tag = picker.Color.ToArgb();
                    button.BackColor = picker.Color;
                    button.ForeColor = Contrast(picker.Color);
                }
            };
            return button;
        }

        private static Color Contrast(Color color)
        {
            return color.R * .299 + color.G * .587 + color.B * .114 > 140 ? Color.Black : Color.White;
        }

        private void BuildPromptTab(TabControl tabs)
        {
            TabPage page = Page(tabs, "提示文案");
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true,
                FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(18) };
            AddPageContent(page, flow);
            flow.Controls.Add(Label("可用占位符：{word} 当前单词、{answer} 正确答案、{input} 你的作答。", 900));
            foreach (KeyValuePair<string, string> kind in new[] {
                new KeyValuePair<string, string>("correct", "答对提示"),
                new KeyValuePair<string, string>("error", "答错提示"),
                new KeyValuePair<string, string>("mastered", "斩词提示") })
            {
                flow.Controls.Add(Label(kind.Value, 880));
                TextBox editor = new TextBox { Width = 850, Height = 75, Multiline = true,
                    Text = draft.prompts[kind.Key], BackColor = Theme.Surface, ForeColor = Theme.Text };
                prompts[kind.Key] = editor;
                flow.Controls.Add(editor);
            }
        }

        private void BuildBackgroundTab(TabControl tabs)
        {
            TabPage page = Page(tabs, "背景设置");
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true,
                FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(18) };
            AddPageContent(page, flow);
            flow.Controls.Add(Label("各练习界面与日历的背景独立设置；图片会复制进项目并纳入备份。", 880));
            backgroundChoice = new DarkComboBox { Width = 360,
                BackColor = Theme.Surface, ForeColor = Theme.Text };
            foreach (KeyValuePair<string, string> item in BackgroundNames) backgroundChoice.Items.Add(item.Value);
            flow.Controls.Add(backgroundChoice);
            imageName = Label("", 860);
            flow.Controls.Add(imageName);
            FlowLayoutPanel actions = new FlowLayoutPanel { Width = 890, Height = 48, WrapContents = false };
            actions.Controls.Add(MakeButton("导入图片", delegate { ImportBackground(); }));
            actions.Controls.Add(MakeButton("改用纯色", delegate
            {
                draft.backgrounds[currentBackground].image = "";
                RefreshBackground();
            }));
            flow.Controls.Add(actions);
            FlowLayoutPanel colors = new FlowLayoutPanel { Width = 890, Height = 48, WrapContents = false };
            colors.Controls.Add(Label("底色", 70));
            solidColor = ColorButton(Color.FromArgb(12, 12, 12).ToArgb());
            colors.Controls.Add(solidColor);
            colors.Controls.Add(Label("图片暗化", 100));
            shade = new NumericUpDown { Width = 75, Minimum = 0, Maximum = 90,
                BackColor = Theme.Surface, ForeColor = Theme.Text };
            colors.Controls.Add(shade);
            colors.Controls.Add(Label("%", 30));
            flow.Controls.Add(colors);
            FlowLayoutPanel modes = new FlowLayoutPanel { Width = 890, Height = 48, WrapContents = false };
            modes.Controls.Add(Label("图片显示", 100));
            fit = new DarkComboBox { Width = 180,
                BackColor = Theme.Surface, ForeColor = Theme.Text };
            fit.Items.AddRange(new object[] { "填满并裁剪", "完整显示", "拉伸铺满" });
            modes.Controls.Add(fit);
            flow.Controls.Add(modes);
            preview = new Panel { Width = 850, Height = 240, BorderStyle = BorderStyle.FixedSingle };
            preview.Paint += delegate(object sender, PaintEventArgs e) { PaintPreview(e.Graphics, preview.ClientRectangle); };
            flow.Controls.Add(preview);
            currentBackground = null;
            backgroundChoice.SelectedIndexChanged += delegate
            {
                SaveCurrentBackground();
                currentBackground = BackgroundNames[backgroundChoice.SelectedIndex].Key;
                RefreshBackground();
            };
            backgroundChoice.SelectedIndex = 0;
            solidColor.Click += delegate { ChangedBackground(); };
            shade.ValueChanged += delegate { ChangedBackground(); };
            fit.SelectedIndexChanged += delegate { ChangedBackground(); };
        }

        private void ChangedBackground()
        {
            if (updatingBackground) return;
            SaveCurrentBackground();
            preview.Invalidate();
        }

        private void PaintPreview(Graphics graphics, Rectangle bounds)
        {
            if (currentBackground == null) return;
            BackgroundAppearance background = draft.backgrounds[currentBackground];
            using (Brush brush = new SolidBrush(Color.FromArgb(background.color))) graphics.FillRectangle(brush, bounds);
            string path = store.ImagePath(background);
            if (path != null)
            {
                try
                {
                    using (Image image = Image.FromFile(path))
                    {
                        float scale = background.fit == "contain"
                            ? Math.Min((float)bounds.Width / image.Width, (float)bounds.Height / image.Height)
                            : Math.Max((float)bounds.Width / image.Width, (float)bounds.Height / image.Height);
                        Rectangle target = background.fit == "stretch" ? bounds : new Rectangle(
                            (bounds.Width - (int)(image.Width * scale)) / 2,
                            (bounds.Height - (int)(image.Height * scale)) / 2,
                            (int)(image.Width * scale), (int)(image.Height * scale));
                        graphics.DrawImage(image, target);
                    }
                    using (Brush shadeBrush = new SolidBrush(Color.FromArgb(background.shade * 255 / 100, 0, 0, 0)))
                        graphics.FillRectangle(shadeBrush, bounds);
                }
                catch { }
            }
            using (Font title = new Font("Microsoft YaHei UI", 20, FontStyle.Bold))
                graphics.DrawString("预览 Preview", title, Brushes.White, new PointF(25, 35));
        }

        private void SaveCurrentBackground()
        {
            if (currentBackground == null || solidColor == null) return;
            BackgroundAppearance background = draft.backgrounds[currentBackground];
            background.color = (int)solidColor.Tag;
            background.shade = (int)shade.Value;
            background.fit = fit.SelectedIndex == 1 ? "contain" : fit.SelectedIndex == 2 ? "stretch" : "cover";
        }

        private void RefreshBackground()
        {
            if (currentBackground == null || solidColor == null) return;
            BackgroundAppearance background = draft.backgrounds[currentBackground];
            updatingBackground = true;
            solidColor.Tag = background.color;
            solidColor.BackColor = Color.FromArgb(background.color);
            solidColor.ForeColor = Contrast(solidColor.BackColor);
            shade.Value = Math.Max(shade.Minimum, Math.Min(shade.Maximum, background.shade));
            fit.SelectedIndex = background.fit == "contain" ? 1 : background.fit == "stretch" ? 2 : 0;
            imageName.Text = string.IsNullOrWhiteSpace(background.image)
                ? "当前使用纯色背景" : "已导入：" + System.IO.Path.GetFileName(background.image);
            updatingBackground = false;
            preview.Invalidate();
        }

        private void ImportBackground()
        {
            using (OpenFileDialog picker = new OpenFileDialog {
                Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif", Title = "选择背景图片" })
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    SaveCurrentBackground();
                    draft.backgrounds[currentBackground].image = store.ImportImage(picker.FileName);
                    RefreshBackground();
                }
                catch (Exception error) { MessageBox.Show(this, error.Message, "图片导入失败"); }
            }
        }

        private void SaveAll()
        {
            try
            {
                SaveCurrentBackground();
                foreach (KeyValuePair<string, StyleControls> entry in styles)
                {
                    TextAppearance style = draft.text[entry.Key];
                    style.font = entry.Value.Font.Text;
                    style.size = (float)entry.Value.Size.Value;
                    style.color = (int)entry.Value.Color.Tag;
                    style.bold = entry.Value.Bold.Checked;
                }
                foreach (KeyValuePair<string, TextBox> entry in prompts)
                    draft.prompts[entry.Key] = entry.Value.Text;
                store.Save(draft);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, "外观保存失败"); }
        }
    }
}
