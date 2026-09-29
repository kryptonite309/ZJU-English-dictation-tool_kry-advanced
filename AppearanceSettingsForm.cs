using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace EnglishDictationTool
{
    internal sealed class AppearanceSettingsForm : Form
    {
        private sealed class WallpaperItem
        {
            public string RelativePath;
            public string Display;
            public override string ToString() { return Display ?? RelativePath ?? string.Empty; }
        }

        private sealed class BackgroundTargetItem
        {
            public string Key;
            public string Display;
            public override string ToString() { return Display ?? Key ?? string.Empty; }
        }

        private sealed class StyleControls
        {
            public ComboBox Font;
            public NumericUpDown Size;
            public Button Color;
            public CheckBox Bold;
            public ComboBox Weight;
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
        private TrackBar panelTransparency;
        private Label transparencyLabel;
        private VideoBackground previewVideo;
        private ListBox wallpaperAssets;
        private Label wallpaperUsage;
        private FlowLayoutPanel wallpaperLibrary;
        private CheckedListBox wallpaperTargets;
        private Button applyWallpaper;
        private readonly Dictionary<string, Control> guideTargets =
            new Dictionary<string, Control>();

        internal event Action<string> GuideTabSelected;

        private static readonly KeyValuePair<string, string>[] BackgroundNames = {
            new KeyValuePair<string, string>("main", "主界面"),
            new KeyValuePair<string, string>("free", "自由练习"),
            new KeyValuePair<string, string>("new", "新学"),
            new KeyValuePair<string, string>("list_review", "历史列表复习"),
            new KeyValuePair<string, string>("problem_review", "错题与易错词复习"),
            new KeyValuePair<string, string>("calendar", "日历"),
            new KeyValuePair<string, string>("pause", "暂停遮罩（共用）"),
            new KeyValuePair<string, string>("pause_new", "暂停遮罩  新学"),
            new KeyValuePair<string, string>("pause_list_review", "暂停遮罩  历史列表复习"),
            new KeyValuePair<string, string>("pause_problem_review", "暂停遮罩  错题复习"),
            new KeyValuePair<string, string>("pause_free", "暂停遮罩  自由练习")
        };

        public AppearanceSettingsForm(AppearanceStore appearance)
        {
            ModernUI.ApplyAppIcon(this);
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
            tabs.SelectedIndexChanged += delegate
            {
                if (GuideTabSelected != null && tabs.SelectedTab != null)
                    GuideTabSelected(tabs.SelectedTab.Text);
            };
            FlowLayoutPanel bottom = new FlowLayoutPanel { Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            Button save = MakeButton("保存外观", delegate { SaveAll(); });
            ((ModernButton)save).Primary = true;
            Button cancel = MakeButton("取消", delegate { Close(); });
            Button restoreAll = MakeButton("全部恢复默认", delegate { RestoreAllDefaults(); });
            restoreAll.Width = 190;
            Button restorePage = MakeButton("本页恢复默认", delegate { RestoreCurrentDefaults(); });
            restorePage.Width = 190;
            bottom.Controls.Add(save); bottom.Controls.Add(cancel);
            bottom.Controls.Add(restoreAll); bottom.Controls.Add(restorePage);
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

        internal Rectangle GuideTabScreen(string title)
        {
            for (int index = 0; index < tabs.TabPages.Count; index++)
            {
                if (!tabs.TabPages[index].Text.StartsWith(title)) continue;
                Rectangle tab = tabs.GetTabRect(index);
                return tabs.RectangleToScreen(tab);
            }
            return Rectangle.Empty;
        }

        internal Rectangle SelectGuideTarget(string key)
        {
            SelectTab("背景设置");
            if (backgroundChoice != null && backgroundChoice.SelectedIndex != 0)
                backgroundChoice.SelectedIndex = 0;
            if (string.Equals(key, "appearance_controls", StringComparison.Ordinal)
                && wallpaperLibrary != null && panelTransparency != null)
            {
                ScrollableControl page = wallpaperLibrary.Parent as ScrollableControl;
                if (page != null) page.ScrollControlIntoView(wallpaperLibrary);
                Rectangle first = wallpaperLibrary.RectangleToScreen(wallpaperLibrary.ClientRectangle);
                Rectangle second = panelTransparency.RectangleToScreen(panelTransparency.ClientRectangle);
                return Rectangle.Union(first, second);
            }
            Control target;
            if (!guideTargets.TryGetValue(key, out target) || target == null)
                return Rectangle.Empty;
            Control scroll = target.Parent;
            while (scroll != null && !(scroll is ScrollableControl)) scroll = scroll.Parent;
            ScrollableControl scrollable = scroll as ScrollableControl;
            if (scrollable != null) scrollable.ScrollControlIntoView(target);
            target.Update();
            return GuideTargetScreen(key);
        }

        internal Rectangle GuideTargetScreen(string key)
        {
            if (string.Equals(key, "appearance_controls", StringComparison.Ordinal)
                && wallpaperLibrary != null && panelTransparency != null
                && wallpaperLibrary.Visible && panelTransparency.Visible)
            {
                Rectangle first = wallpaperLibrary.RectangleToScreen(wallpaperLibrary.ClientRectangle);
                Rectangle second = panelTransparency.RectangleToScreen(panelTransparency.ClientRectangle);
                return Rectangle.Union(first, second);
            }
            Control target;
            if (!guideTargets.TryGetValue(key, out target) || target == null
                || target.IsDisposed || !target.Visible)
                return Rectangle.Empty;
            return target.RectangleToScreen(target.ClientRectangle);
        }

        private static Button MakeButton(string title, EventHandler click)
        {
            Button button = title == "选择颜色"
                ? new Button { Text = title, Width = 125, Height = 42,
                    FlatStyle = FlatStyle.Flat, BackColor = Theme.SurfaceRaised, ForeColor = Theme.Text }
                : (Button)new ModernButton { Text = title, Width = 125, Height = 46 };
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
                FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Padding = new Padding(18) };
            AddPageContent(page, flow);
            Label intro = Label("各学习界面共用文字样式。字体、字号、颜色与粗细均可独立调整。", 1190);
            intro.ForeColor = Theme.MutedText;
            flow.Controls.Add(intro);
            KeyValuePair<string, string>[] names = {
                new KeyValuePair<string, string>("normal", "普通说明"),
                new KeyValuePair<string, string>("word", "英文单词"),
                new KeyValuePair<string, string>("example", "英文例句"),
                new KeyValuePair<string, string>("exampleTarget", "例句中的目标词"),
                new KeyValuePair<string, string>("meaning", "中文释义"),
                new KeyValuePair<string, string>("correct", "答对提示"),
                new KeyValuePair<string, string>("error", "答错提示"),
                new KeyValuePair<string, string>("mastered", "斩词提示")
            };
            string[] fontNames = FontFamily.Families.Select(x => x.Name).ToArray();
            foreach (KeyValuePair<string, string> name in names)
            {
                TextAppearance initial = draft.text[name.Key];
                ModernGroupBox group = new ModernGroupBox { Text = name.Value,
                    Width = 580, Height = 112, Margin = new Padding(8),
                    BackColor = ModernUI.Card, ForeColor = Theme.Text };
                FlowLayoutPanel row = new FlowLayoutPanel { Location = new Point(14, 38),
                    Width = 550, Height = 58, WrapContents = false,
                    FlowDirection = FlowDirection.LeftToRight, BackColor = ModernUI.Card };
                ComboBox font = new DarkComboBox {
                    Width = 235, BackColor = Theme.Surface, ForeColor = Theme.Text };
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
                color.Width = 105;
                row.Controls.Add(color);
                CheckBox bold = new CheckBox { Text = "加粗", Checked = initial.bold,
                    Width = 80, ForeColor = Theme.Text };
                ComboBox weight = null;
                if (name.Key == "exampleTarget")
                {
                    group.Height = 162;
                    weight = new DarkComboBox { Left = 110, Top = 112, Width = 210 };
                    weight.Items.AddRange(new object[] { "常规 400", "中等 500", "半粗 600", "加粗 700", "特粗 800", "极粗 900" });
                    weight.SelectedIndex = Math.Max(0, Math.Min(5, ((initial.weight == 0 ? 700 : initial.weight) - 400) / 100));
                    Label weightLabel = Label("加粗程度", 90); weightLabel.Location = new Point(14, 110);
                    group.Controls.Add(weightLabel); group.Controls.Add(weight);
                }
                else row.Controls.Add(bold);
                styles[name.Key] = new StyleControls { Font = font, Size = size, Color = color, Bold = bold, Weight = weight };
                group.Controls.Add(row);
                flow.Controls.Add(group);
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
                FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Padding = new Padding(18) };
            AddPageContent(page, flow);
            Label intro = Label("可用占位符：{word} 当前单词、{answer} 正确答案、{input} 你的作答。", 1190);
            intro.ForeColor = Theme.MutedText;
            flow.Controls.Add(intro);
            foreach (KeyValuePair<string, string> kind in new[] {
                new KeyValuePair<string, string>("correct", "答对提示"),
                new KeyValuePair<string, string>("error", "答错提示"),
                new KeyValuePair<string, string>("mastered", "斩词提示") })
            {
                ModernGroupBox group = new ModernGroupBox { Text = kind.Value,
                    Width = 580, Height = 164, Margin = new Padding(8),
                    BackColor = ModernUI.Card, ForeColor = Theme.Text };
                TextBox editor = new TextBox { Location = new Point(16, 42), Width = 545,
                    Height = 94, Multiline = true,
                    Text = draft.prompts[kind.Key], BackColor = Theme.Surface, ForeColor = Theme.Text };
                prompts[kind.Key] = editor;
                group.Controls.Add(editor);
                flow.Controls.Add(group);
            }
        }

        private void BuildBackgroundTab(TabControl tabs)
        {
            TabPage page = Page(tabs, "背景设置");
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true,
                FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(18) };
            AddPageContent(page, flow);
            Label backgroundIntro = Label("各界面独立设置。视频默认静音循环，只在最上层窗口播放；同一份壁纸可从文件库复用于多个界面。", 880);
            backgroundIntro.Height = 70;
            flow.Controls.Add(backgroundIntro);
            backgroundChoice = new DarkComboBox { Width = 360,
                BackColor = Theme.Surface, ForeColor = Theme.Text };
            foreach (KeyValuePair<string, string> item in BackgroundNames) backgroundChoice.Items.Add(item.Value);
            flow.Controls.Add(backgroundChoice);
            imageName = Label("", 860);
            flow.Controls.Add(imageName);
            FlowLayoutPanel actions = new FlowLayoutPanel { Width = 890, Height = 48, WrapContents = false };
            actions.Controls.Add(MakeButton("导入图片", delegate { ImportBackground(); }));
            actions.Controls.Add(MakeButton("导入视频", delegate { ImportVideoBackground(); }));
            actions.Controls.Add(MakeButton("改用纯色", delegate
            {
                draft.backgrounds[currentBackground].image = "";
                draft.backgrounds[currentBackground].inherit = false;
                RefreshBackground();
            }));
            Button inheritPause = MakeButton("继承共用遮罩", delegate
            {
                if (currentBackground == null || !currentBackground.StartsWith("pause_")) return;
                draft.backgrounds[currentBackground].inherit = true;
                RefreshBackground();
            });
            inheritPause.Width = 165;
            actions.Controls.Add(inheritPause);
            flow.Controls.Add(actions);

            wallpaperLibrary = new FlowLayoutPanel { Width = 890, Height = 434,
                FlowDirection = FlowDirection.TopDown, WrapContents = false,
                BackColor = ModernUI.Card, Padding = new Padding(0, 4, 0, 0) };
            wallpaperLibrary.Controls.Add(Label("壁纸文件库", 860));
            wallpaperAssets = new ListBox { Width = 860, Height = 96,
                BackColor = Theme.Surface, ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle, HorizontalScrollbar = true };
            wallpaperAssets.SelectedIndexChanged += delegate { RefreshWallpaperUsage(); };
            wallpaperLibrary.Controls.Add(wallpaperAssets);
            wallpaperUsage = Label("选择素材后可查看正在使用它的界面。", 860);
            wallpaperUsage.ForeColor = Theme.MutedText;
            wallpaperLibrary.Controls.Add(wallpaperUsage);
            Label targetsLabel = Label("选择要应用壁纸的窗口（可多选）", 860);
            wallpaperLibrary.Controls.Add(targetsLabel);
            wallpaperTargets = new CheckedListBox {
                Width = 860, Height = 160,
                BackColor = Theme.Surface, ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                CheckOnClick = true, MultiColumn = true, ColumnWidth = 270 };
            foreach (KeyValuePair<string, string> item in BackgroundNames)
                wallpaperTargets.Items.Add(new BackgroundTargetItem {
                    Key = item.Key, Display = item.Value });
            wallpaperLibrary.Controls.Add(wallpaperTargets);
            FlowLayoutPanel libraryActions = new FlowLayoutPanel { Width = 870, Height = 50,
                FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
                BackColor = ModernUI.Card };
            applyWallpaper = MakeButton("应用到所选窗口", delegate { ApplySelectedWallpaper(); });
            applyWallpaper.Width = 210;
            Button deleteAsset = MakeButton("删除所选文件", delegate { DeleteSelectedWallpaper(); });
            deleteAsset.Width = 190;
            Button deleteUnused = MakeButton("清理未使用文件", delegate { DeleteUnusedWallpapers(); });
            deleteUnused.Width = 210;
            libraryActions.Controls.Add(applyWallpaper);
            libraryActions.Controls.Add(deleteAsset);
            libraryActions.Controls.Add(deleteUnused);
            wallpaperLibrary.Controls.Add(libraryActions);
            flow.Controls.Add(wallpaperLibrary);
            guideTargets["wallpaper_library"] = wallpaperLibrary;

            FlowLayoutPanel colors = new FlowLayoutPanel { Width = 890, Height = 48, WrapContents = false };
            colors.Controls.Add(Label("底色", 70));
            solidColor = ColorButton(Color.FromArgb(12, 12, 12).ToArgb());
            colors.Controls.Add(solidColor);
            colors.Controls.Add(Label("背景暗化", 100));
            shade = new NumericUpDown { Width = 75, Minimum = 0, Maximum = 90,
                BackColor = Theme.Surface, ForeColor = Theme.Text };
            colors.Controls.Add(shade);
            colors.Controls.Add(Label("%", 30));
            flow.Controls.Add(colors);
            FlowLayoutPanel modes = new FlowLayoutPanel { Width = 890, Height = 48, WrapContents = false };
            modes.Controls.Add(Label("背景显示", 100));
            fit = new DarkComboBox { Width = 180,
                BackColor = Theme.Surface, ForeColor = Theme.Text };
            fit.Items.AddRange(new object[] { "填满并裁剪", "完整显示", "拉伸铺满", "原尺寸居中" });
            modes.Controls.Add(fit);
            flow.Controls.Add(modes);
            transparencyLabel = Label("主界面黑色面板透明度 0%（不会改变文字显示）", 860);
            flow.Controls.Add(transparencyLabel);
            panelTransparency = new TrackBar { Width = 850, Height = 48,
                Minimum = 0, Maximum = 100, TickFrequency = 10 };
            flow.Controls.Add(panelTransparency);
            guideTargets["panel_transparency"] = panelTransparency;
            panelTransparency.ValueChanged += delegate
            {
                if (updatingBackground || currentBackground == null) return;
                draft.backgrounds["main"].panelTransparency = panelTransparency.Value;
                transparencyLabel.Text = "主界面黑色面板透明度 " + panelTransparency.Value
                    + "%（不会改变文字显示）";
                if (preview != null) preview.Invalidate();
            };
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
            RefreshWallpaperLibrary();
        }

        private void ChangedBackground()
        {
            if (updatingBackground) return;
            if (currentBackground != null && currentBackground.StartsWith("pause_"))
                draft.backgrounds[currentBackground].inherit = false;
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
                if (AppearanceStore.IsVideo(path))
                {
                    if (previewVideo != null) previewVideo.Draw(graphics, bounds);
                }
                else try
                {
                    using (Image image = Image.FromFile(path))
                    {
                        float scale = background.fit == "contain"
                            ? Math.Min((float)bounds.Width / image.Width, (float)bounds.Height / image.Height)
                            : Math.Max((float)bounds.Width / image.Width, (float)bounds.Height / image.Height);
                        Rectangle target = background.fit == "stretch" ? bounds
                            : background.fit == "center" ? new Rectangle(
                                (bounds.Width - image.Width) / 2, (bounds.Height - image.Height) / 2,
                                image.Width, image.Height)
                            : new Rectangle((bounds.Width - (int)(image.Width * scale)) / 2,
                                (bounds.Height - (int)(image.Height * scale)) / 2,
                                (int)(image.Width * scale), (int)(image.Height * scale));
                        graphics.DrawImage(image, target);
                    }
                    using (Brush shadeBrush = new SolidBrush(Color.FromArgb(background.shade * 255 / 100, 0, 0, 0)))
                        graphics.FillRectangle(shadeBrush, bounds);
                }
                catch { }
            }
            if (currentBackground == "main")
            {
                int transparency = Math.Max(0, Math.Min(100,
                    draft.backgrounds["main"].panelTransparency));
                using (Brush panel = new SolidBrush(Color.FromArgb(
                    (100 - transparency) * 238 / 100, 20, 24, 31)))
                    graphics.FillRectangle(panel, new Rectangle(15, 20,
                        bounds.Width - 30, bounds.Height - 40));
            }
            using (Font title = new Font("Segoe UI", 20, FontStyle.Bold))
                graphics.DrawString("预览 Preview", title, Brushes.White, new PointF(25, 35));
        }

        private void SaveCurrentBackground()
        {
            if (currentBackground == null || solidColor == null) return;
            BackgroundAppearance background = draft.backgrounds[currentBackground];
            background.color = (int)solidColor.Tag;
            background.shade = (int)shade.Value;
            background.fit = fit.SelectedIndex == 1 ? "contain" : fit.SelectedIndex == 2
                ? "stretch" : fit.SelectedIndex == 3 ? "center" : "cover";
        }

        private void RefreshBackground()
        {
            if (currentBackground == null || solidColor == null) return;
            BackgroundAppearance background = draft.backgrounds[currentBackground];
            updatingBackground = true;
            panelTransparency.Value = Math.Max(0,
                Math.Min(100, draft.backgrounds["main"].panelTransparency));
            panelTransparency.Visible = transparencyLabel.Visible = currentBackground == "main";
            transparencyLabel.Text = "主界面黑色面板透明度 " + panelTransparency.Value
                + "%（不会改变文字显示）";
            solidColor.Tag = background.color;
            solidColor.BackColor = Color.FromArgb(background.color);
            solidColor.ForeColor = Contrast(solidColor.BackColor);
            shade.Value = Math.Max(shade.Minimum, Math.Min(shade.Maximum, background.shade));
            fit.SelectedIndex = background.fit == "contain" ? 1 : background.fit == "stretch"
                ? 2 : background.fit == "center" ? 3 : 0;
            imageName.Text = background.inherit && currentBackground.StartsWith("pause_")
                ? "当前继承共用暂停遮罩"
                : string.IsNullOrWhiteSpace(background.image)
                    ? (currentBackground.StartsWith("pause") ? "未设置素材，暂停时使用毛玻璃遮罩"
                        : "当前使用纯色背景")
                    : "已导入：" + System.IO.Path.GetFileName(background.image);
            updatingBackground = false;
            if (previewVideo != null) { previewVideo.Dispose(); previewVideo = null; }
            string mediaPath = store.ImagePath(background);
            if (AppearanceStore.IsVideo(mediaPath))
                previewVideo = new VideoBackground(preview, mediaPath, background, 100);
            preview.Invalidate();
            RefreshWallpaperUsage();
        }

        private static string BackgroundDisplayName(string key)
        {
            KeyValuePair<string, string> match = BackgroundNames.FirstOrDefault(
                entry => string.Equals(entry.Key, key, StringComparison.Ordinal));
            return string.IsNullOrEmpty(match.Value) ? key : match.Value;
        }

        private WallpaperItem SelectedWallpaper()
        {
            return wallpaperAssets == null ? null : wallpaperAssets.SelectedItem as WallpaperItem;
        }

        private void RefreshWallpaperLibrary()
        {
            if (wallpaperAssets == null) return;
            string selected = SelectedWallpaper() == null ? null
                : SelectedWallpaper().RelativePath;
            wallpaperAssets.BeginUpdate();
            wallpaperAssets.Items.Clear();
            foreach (string relative in store.WallpaperAssets())
            {
                string full = store.ImagePath(new BackgroundAppearance { image = relative });
                long bytes = full != null && File.Exists(full) ? new FileInfo(full).Length : 0L;
                string size = bytes >= 1024L * 1024L
                    ? (bytes / 1024d / 1024d).ToString("0.0") + " MB"
                    : Math.Max(1d, bytes / 1024d).ToString("0") + " KB";
                WallpaperItem item = new WallpaperItem
                {
                    RelativePath = relative,
                    Display = Path.GetFileName(relative) + "    " + size
                };
                wallpaperAssets.Items.Add(item);
                if (string.Equals(relative, selected, StringComparison.OrdinalIgnoreCase))
                    wallpaperAssets.SelectedItem = item;
            }
            wallpaperAssets.EndUpdate();
            RefreshWallpaperUsage();
        }

        private void RefreshWallpaperUsage()
        {
            if (wallpaperUsage == null) return;
            WallpaperItem selected = SelectedWallpaper();
            if (selected == null)
            {
                wallpaperUsage.Text = wallpaperAssets != null && wallpaperAssets.Items.Count == 0
                    ? "文件库为空。导入图片或视频后会显示在这里。"
                    : "选择素材后可查看正在使用它的界面。";
                RefreshWallpaperTargets(null);
                return;
            }
            List<string> usage = store.WallpaperUsage(draft, selected.RelativePath);
            wallpaperUsage.Text = usage.Count == 0 ? "当前没有界面使用此素材。"
                : "正在使用：" + string.Join("、", usage.Select(BackgroundDisplayName));
            RefreshWallpaperTargets(selected);
        }

        private void RefreshWallpaperTargets(WallpaperItem selected)
        {
            if (wallpaperTargets == null) return;
            for (int index = 0; index < wallpaperTargets.Items.Count; index++)
            {
                BackgroundTargetItem target = wallpaperTargets.Items[index]
                    as BackgroundTargetItem;
                BackgroundAppearance background;
                bool isUsed = selected != null && target != null
                    && draft.backgrounds.TryGetValue(target.Key, out background)
                    && background != null
                    && string.Equals(background.image, selected.RelativePath,
                        StringComparison.OrdinalIgnoreCase)
                    && !background.inherit;
                wallpaperTargets.SetItemChecked(index, isUsed);
            }
            if (applyWallpaper != null) applyWallpaper.Enabled = selected != null;
        }

        private void ApplySelectedWallpaper()
        {
            WallpaperItem selected = SelectedWallpaper();
            if (selected == null) return;
            List<string> targets = wallpaperTargets.CheckedItems
                .Cast<BackgroundTargetItem>().Select(item => item.Key).ToList();
            if (targets.Count == 0)
            {
                MessageBox.Show(this, "请至少勾选一个要应用壁纸的窗口。", "尚未选择窗口");
                return;
            }
            SaveCurrentBackground();
            SaveWallpaperTargetSelection(selected.RelativePath, targets);
            RefreshBackground();
        }

        private void SaveWallpaperTargetSelection(string relativePath,
            IEnumerable<string> targets)
        {
            HashSet<string> selectedTargets = new HashSet<string>(targets
                ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> entry in BackgroundNames)
            {
                BackgroundAppearance background;
                if (!draft.backgrounds.TryGetValue(entry.Key, out background)
                    || background == null) continue;
                if (selectedTargets.Contains(entry.Key))
                {
                    background.image = relativePath;
                    background.inherit = false;
                }
                else if (!background.inherit && string.Equals(background.image,
                    relativePath, StringComparison.OrdinalIgnoreCase))
                {
                    background.image = string.Empty;
                }
            }
        }

        private void ApplyWallpaperToTargets(string relativePath, IEnumerable<string> targets)
        {
            foreach (string key in targets.Distinct(StringComparer.Ordinal))
            {
                BackgroundAppearance background;
                if (!draft.backgrounds.TryGetValue(key, out background)
                    || background == null) continue;
                background.image = relativePath;
                background.inherit = false;
            }
        }

        internal void ApplyWallpaperForTests(string relativePath, params string[] targets)
        {
            ApplyWallpaperToTargets(relativePath, targets ?? new string[0]);
        }

        internal void SelectWallpaperTargetsForTests(string relativePath,
            params string[] targets)
        {
            WallpaperItem item = wallpaperAssets.Items.Cast<WallpaperItem>()
                .FirstOrDefault(x => string.Equals(x.RelativePath, relativePath,
                    StringComparison.OrdinalIgnoreCase));
            wallpaperAssets.SelectedItem = item;
            HashSet<string> selected = new HashSet<string>(targets ?? new string[0],
                StringComparer.Ordinal);
            for (int index = 0; index < wallpaperTargets.Items.Count; index++)
            {
                BackgroundTargetItem target = wallpaperTargets.Items[index]
                    as BackgroundTargetItem;
                wallpaperTargets.SetItemChecked(index, target != null
                    && selected.Contains(target.Key));
            }
        }

        internal void SaveForTests()
        {
            SaveAll();
        }

        internal string WallpaperForTests(string key)
        {
            BackgroundAppearance background;
            return draft.backgrounds.TryGetValue(key, out background) && background != null
                ? background.image : null;
        }

        internal int WallpaperTargetCountForTests
        {
            get { return wallpaperTargets == null ? 0 : wallpaperTargets.Items.Count; }
        }

        private void DeleteSelectedWallpaper()
        {
            WallpaperItem selected = SelectedWallpaper();
            if (selected == null) return;
            List<string> usage = store.WallpaperUsage(draft, selected.RelativePath);
            if (usage.Count > 0)
            {
                MessageBox.Show(this, "该壁纸仍被以下界面使用：\n"
                    + string.Join("、", usage.Select(BackgroundDisplayName))
                    + "\n\n请先为这些界面改用其他背景。", "无法删除");
                return;
            }
            if (MessageBox.Show(this, "将永久删除所选壁纸文件，不进入回收站。继续吗？",
                "删除壁纸", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                store.DeleteWallpaperAsset(draft, selected.RelativePath);
                RefreshWallpaperLibrary();
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, "删除失败"); }
        }

        private void DeleteUnusedWallpapers()
        {
            if (MessageBox.Show(this, "将永久删除文件库中所有未被任何界面使用的壁纸，不进入回收站。继续吗？",
                "清理壁纸", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                int count = store.DeleteUnusedWallpaperAssets(draft);
                RefreshWallpaperLibrary();
                MessageBox.Show(this, "已清理 " + count + " 个未使用的壁纸文件。", "清理完成");
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, "清理失败"); }
        }

        private void ImportVideoBackground()
        {
            using (OpenFileDialog picker = new OpenFileDialog
            {
                Filter = "视频|*.mp4;*.m4v;*.mov;*.wmv;*.avi",
                Title = "选择背景视频（推荐 H.264 MP4）"
            })
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    SaveCurrentBackground();
                    draft.backgrounds[currentBackground].image = store.ImportVideo(picker.FileName);
                    draft.backgrounds[currentBackground].inherit = false;
                    RefreshWallpaperLibrary();
                    RefreshBackground();
                }
                catch (Exception error)
                {
                    MessageBox.Show(this, error.Message, "视频导入失败");
                }
            }
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
                    draft.backgrounds[currentBackground].inherit = false;
                    RefreshWallpaperLibrary();
                    RefreshBackground();
                }
                catch (Exception error) { MessageBox.Show(this, error.Message, "图片导入失败"); }
            }
        }

        private static AppearanceSettings FreshDefaults()
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            return serializer.Deserialize<AppearanceSettings>(
                serializer.Serialize(AppearanceSettings.Defaults()));
        }

        private void RestoreCurrentDefaults()
        {
            AppearanceSettings defaults = FreshDefaults();
            string tab = tabs.SelectedTab == null ? string.Empty : tabs.SelectedTab.Text;
            if (tab.StartsWith("文字", StringComparison.Ordinal))
            {
                draft.text = defaults.text;
                RefreshTextEditors();
            }
            else if (tab.StartsWith("提示", StringComparison.Ordinal))
            {
                draft.prompts = defaults.prompts;
                RefreshPromptEditors();
            }
            else
            {
                draft.backgrounds = defaults.backgrounds;
                RefreshBackground();
            }
        }

        private void RestoreAllDefaults()
        {
            if (MessageBox.Show(this,
                "将把全部字体、字号、颜色、提示文案、背景和透明度恢复为内置默认值。\n"
                + "已导入的壁纸文件会保留在文件库中，保存前仍可取消。继续吗？",
                "全部恢复默认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            AppearanceSettings defaults = FreshDefaults();
            draft.text = defaults.text;
            draft.prompts = defaults.prompts;
            draft.backgrounds = defaults.backgrounds;
            RefreshTextEditors();
            RefreshPromptEditors();
            RefreshBackground();
            RefreshWallpaperLibrary();
        }

        private void RefreshTextEditors()
        {
            foreach (KeyValuePair<string, StyleControls> entry in styles)
            {
                TextAppearance style = draft.text[entry.Key];
                entry.Value.Font.SelectedItem = style.font;
                if (entry.Value.Font.SelectedIndex < 0)
                {
                    entry.Value.Font.Items.Add(style.font);
                    entry.Value.Font.SelectedItem = style.font;
                }
                decimal size = (decimal)Math.Max((float)entry.Value.Size.Minimum,
                    Math.Min((float)entry.Value.Size.Maximum, style.size));
                entry.Value.Size.Value = size;
                entry.Value.Color.Tag = style.color;
                entry.Value.Color.BackColor = Color.FromArgb(style.color);
                entry.Value.Color.ForeColor = Contrast(entry.Value.Color.BackColor);
                if (entry.Value.Bold != null) entry.Value.Bold.Checked = style.bold;
                if (entry.Value.Weight != null)
                    entry.Value.Weight.SelectedIndex = Math.Max(0, Math.Min(5,
                        ((style.weight == 0 ? 700 : style.weight) - 400) / 100));
            }
        }

        private void RefreshPromptEditors()
        {
            foreach (KeyValuePair<string, TextBox> entry in prompts)
                entry.Value.Text = draft.prompts.ContainsKey(entry.Key)
                    ? draft.prompts[entry.Key] : string.Empty;
        }

        private void SaveAll()
        {
            try
            {
                SaveCurrentBackground();
                WallpaperItem selectedWallpaper = SelectedWallpaper();
                if (selectedWallpaper != null)
                {
                    List<string> selectedTargets = wallpaperTargets.CheckedItems
                        .Cast<BackgroundTargetItem>().Select(item => item.Key).ToList();
                    SaveWallpaperTargetSelection(selectedWallpaper.RelativePath,
                        selectedTargets);
                }
                foreach (KeyValuePair<string, StyleControls> entry in styles)
                {
                    TextAppearance style = draft.text[entry.Key];
                    style.font = entry.Value.Font.Text;
                    style.size = (float)entry.Value.Size.Value;
                    style.color = (int)entry.Value.Color.Tag;
                    style.bold = entry.Value.Bold.Checked;
                    if (entry.Value.Weight != null)
                    {
                        style.weight = 400 + entry.Value.Weight.SelectedIndex * 100;
                        style.bold = style.weight >= 600;
                    }
                }
                foreach (KeyValuePair<string, TextBox> entry in prompts)
                    draft.prompts[entry.Key] = entry.Value.Text;
                store.Save(draft);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, "外观保存失败"); }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && previewVideo != null)
            {
                previewVideo.Dispose();
                previewVideo = null;
            }
            base.Dispose(disposing);
        }
    }
}
