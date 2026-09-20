using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace EnglishDictationTool
{
    internal sealed class TextAppearance
    {
        public string font { get; set; }
        public float size { get; set; }
        public int color { get; set; }
        public bool bold { get; set; }
    }

    internal sealed class BackgroundAppearance
    {
        public int color { get; set; }
        public string image { get; set; }
        public string fit { get; set; }
        public int shade { get; set; }
    }

    internal sealed class AppearanceSettings
    {
        public Dictionary<string, TextAppearance> text { get; set; }
        public Dictionary<string, BackgroundAppearance> backgrounds { get; set; }
        public Dictionary<string, string> prompts { get; set; }

        public static AppearanceSettings Defaults()
        {
            AppearanceSettings settings = new AppearanceSettings();
            settings.text = new Dictionary<string, TextAppearance>();
            settings.text["normal"] = Style(14, Color.White, false);
            settings.text["word"] = Style(28, Color.White, true);
            settings.text["example"] = Style(18, Color.White, false);
            settings.text["meaning"] = Style(18, Color.White, false);
            settings.text["correct"] = Style(17, Color.FromArgb(70, 210, 105), true);
            settings.text["error"] = Style(17, Color.FromArgb(255, 82, 82), true);
            settings.text["mastered"] = Style(17, Color.FromArgb(115, 190, 255), true);
            settings.backgrounds = new Dictionary<string, BackgroundAppearance>();
            foreach (string key in new[] { "free", "new", "list_review", "problem_review", "calendar" })
                settings.backgrounds[key] = new BackgroundAppearance
                { color = Color.FromArgb(12, 12, 12).ToArgb(), image = "", fit = "cover", shade = 35 };
            settings.prompts = new Dictionary<string, string>();
            settings.prompts["correct"] = "✓ 正确：{answer}";
            settings.prompts["error"] = "✗ 错误；正确答案：{answer}";
            settings.prompts["mastered"] = "已斩并移入已掌握：{word}";
            return settings;
        }

        private static TextAppearance Style(float size, Color color, bool bold)
        {
            return new TextAppearance { font = "Microsoft YaHei UI", size = size,
                color = color.ToArgb(), bold = bold };
        }
    }

    internal sealed class AppearanceStore
    {
        private readonly string root;
        private readonly string path;
        public AppearanceSettings Settings { get; private set; }

        public AppearanceStore(string projectRoot)
        {
            root = Path.GetFullPath(projectRoot);
            path = Path.Combine(root, "appearance.json");
            Settings = File.Exists(path)
                ? new JavaScriptSerializer().Deserialize<AppearanceSettings>(File.ReadAllText(path, Encoding.UTF8))
                : AppearanceSettings.Defaults();
            Normalize(Settings);
        }

        public AppearanceSettings CopySettings()
        {
            return new JavaScriptSerializer().Deserialize<AppearanceSettings>(
                new JavaScriptSerializer().Serialize(Settings));
        }

        public void Save(AppearanceSettings settings)
        {
            Normalize(settings);
            string json = new JavaScriptSerializer().Serialize(settings);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, json, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
            Settings = settings;
        }

        public string ImportImage(string source)
        {
            string extension = Path.GetExtension(source).ToLowerInvariant();
            if (!new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif" }.Contains(extension))
                throw new InvalidDataException("请选择 PNG、JPG、BMP 或 GIF 图片。");
            FileInfo info = new FileInfo(source);
            if (info.Length > 30 * 1024 * 1024)
                throw new InvalidDataException("图片不能超过 30 MB。");
            using (Image image = Image.FromFile(source))
                if (image.Width < 16 || image.Height < 16)
                    throw new InvalidDataException("图片尺寸过小。");
            string folder = Path.Combine(root, "appearance_images");
            Directory.CreateDirectory(folder);
            string name = Guid.NewGuid().ToString("N") + extension;
            File.Copy(source, Path.Combine(folder, name));
            return Path.Combine("appearance_images", name);
        }

        public string ImagePath(BackgroundAppearance background)
        {
            if (background == null || string.IsNullOrWhiteSpace(background.image)) return null;
            string folder = Path.GetFullPath(Path.Combine(root, "appearance_images"))
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(Path.Combine(root, background.image));
            return candidate.StartsWith(folder, StringComparison.OrdinalIgnoreCase) && File.Exists(candidate)
                ? candidate : null;
        }

        public string Prompt(string kind, WordEntry word, string input)
        {
            return Prompt(kind, word, input, null);
        }

        public string Prompt(string kind, WordEntry word, string input, string answerOverride)
        {
            string template;
            if (!Settings.prompts.TryGetValue(kind, out template)) template = "{word}";
            string clean = word == null ? "" : GameEngine.CleanEnglish(word);
            string answer = string.IsNullOrWhiteSpace(answerOverride) ? clean : answerOverride;
            return (template ?? "").Replace("{word}", clean).Replace("{answer}", answer)
                .Replace("{input}", input ?? "");
        }

        public static Font CreateFont(TextAppearance style)
        {
            FontStyle weight = style.bold ? FontStyle.Bold : FontStyle.Regular;
            try { return new Font(style.font, style.size, weight); }
            catch { return new Font("Microsoft YaHei UI", style.size, weight); }
        }

        public static void Normalize(AppearanceSettings settings)
        {
            if (settings == null) throw new InvalidDataException("外观设置文件无效。");
            AppearanceSettings defaults = AppearanceSettings.Defaults();
            if (settings.text == null) settings.text = new Dictionary<string, TextAppearance>();
            if (settings.backgrounds == null) settings.backgrounds = new Dictionary<string, BackgroundAppearance>();
            if (settings.prompts == null) settings.prompts = new Dictionary<string, string>();
            // v1.1.7 之前的默认答对提示使用词条原形。仅迁移完全未修改的旧默认文案，
            // 用户自行编写的包含 {word} 的提示仍保持原样。
            string legacyCorrectPrompt;
            if (settings.prompts.TryGetValue("correct", out legacyCorrectPrompt)
                && string.Equals(legacyCorrectPrompt, "✓ 正确：{word}",
                    StringComparison.Ordinal))
                settings.prompts["correct"] = defaults.prompts["correct"];
            foreach (KeyValuePair<string, TextAppearance> entry in defaults.text)
            {
                if (!settings.text.ContainsKey(entry.Key) || settings.text[entry.Key] == null)
                    settings.text[entry.Key] = entry.Value;
                TextAppearance style = settings.text[entry.Key];
                if (string.IsNullOrWhiteSpace(style.font)) style.font = entry.Value.font;
                if (style.size < 8 || style.size > 72) style.size = entry.Value.size;
            }
            foreach (KeyValuePair<string, BackgroundAppearance> entry in defaults.backgrounds)
            {
                if (!settings.backgrounds.ContainsKey(entry.Key) || settings.backgrounds[entry.Key] == null)
                    settings.backgrounds[entry.Key] = entry.Value;
                BackgroundAppearance background = settings.backgrounds[entry.Key];
                if (background.fit != "contain" && background.fit != "stretch") background.fit = "cover";
                background.shade = Math.Max(0, Math.Min(90, background.shade));
            }
            foreach (KeyValuePair<string, string> entry in defaults.prompts)
                if (!settings.prompts.ContainsKey(entry.Key)) settings.prompts[entry.Key] = entry.Value;
        }
    }

    internal sealed class StyledCanvas : ScrollableControl
    {
        private sealed class Block { public string Text; public string Role; }
        private readonly List<Block> blocks = new List<Block>();
        private readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        private AppearanceStore appearance;
        private string backgroundKey;
        private Image backgroundImage;
        private int contentHeight;
        private bool scrollToBottomRequested;

        public StyledCanvas()
        {
            DoubleBuffered = true;
            AutoScroll = true;
            TabStop = true;
            BackColor = Theme.Background;
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("复制全部内容", null, delegate { Clipboard.SetText(ContentText); });
            ContextMenuStrip = menu;
        }

        public string ContentText { get { return string.Join(Environment.NewLine, blocks.Select(x => x.Text)); } }

        public void SetAppearance(AppearanceStore store, string key)
        {
            appearance = store;
            backgroundKey = key;
            foreach (Font font in fonts.Values) font.Dispose();
            fonts.Clear();
            if (backgroundImage != null) { backgroundImage.Dispose(); backgroundImage = null; }
            if (appearance != null)
            {
                string path = appearance.ImagePath(appearance.Settings.backgrounds[key]);
                if (path != null)
                {
                    try { using (Image loaded = Image.FromFile(path)) backgroundImage = new Bitmap(loaded); }
                    catch { backgroundImage = null; }
                }
            }
            Recalculate();
            Invalidate();
        }

        public void ClearContent()
        {
            blocks.Clear();
            scrollToBottomRequested = false;
            AutoScrollPosition = Point.Empty;
            Recalculate(); Invalidate();
        }

        public void Add(string text, string role)
        {
            blocks.Add(new Block { Text = text ?? "", Role = role ?? "normal" });
            Recalculate(); Invalidate();
        }

        public void ScrollToBottom()
        {
            scrollToBottomRequested = true;
            AutoScrollPosition = new Point(0, contentHeight);
            Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Recalculate();
            if (scrollToBottomRequested) AutoScrollPosition = new Point(0, contentHeight);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Recalculate(); }

        private Font FontFor(string role)
        {
            Font font;
            if (fonts.TryGetValue(role, out font)) return font;
            TextAppearance style = StyleFor(role);
            font = AppearanceStore.CreateFont(style);
            fonts[role] = font;
            return font;
        }

        private TextAppearance StyleFor(string role)
        {
            TextAppearance style;
            return appearance != null && appearance.Settings.text.TryGetValue(role, out style)
                ? style : AppearanceSettings.Defaults().text["normal"];
        }

        private void Recalculate()
        {
            if (!IsHandleCreated) return;
            using (Graphics graphics = CreateGraphics())
            {
                int width = Math.Max(80, ClientSize.Width - 48 - SystemInformation.VerticalScrollBarWidth);
                int height = 24;
                foreach (Block block in blocks)
                    height += (int)Math.Ceiling(graphics.MeasureString(block.Text + " ", FontFor(block.Role), width).Height) + 10;
                contentHeight = height + 24;
                if (AutoScrollMinSize.Height != contentHeight)
                    AutoScrollMinSize = new Size(0, contentHeight);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            BackgroundAppearance background = null;
            if (appearance != null && backgroundKey != null)
                appearance.Settings.backgrounds.TryGetValue(backgroundKey, out background);
            using (Brush fill = new SolidBrush(background == null ? Theme.Background : Color.FromArgb(background.color)))
                e.Graphics.FillRectangle(fill, ClientRectangle);
            if (backgroundImage == null) return;
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
            e.Graphics.DrawImage(backgroundImage, target);
            using (Brush shade = new SolidBrush(Color.FromArgb(background.shade * 255 / 100, 0, 0, 0)))
                e.Graphics.FillRectangle(shade, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            int width = Math.Max(80, ClientSize.Width - 48 - SystemInformation.VerticalScrollBarWidth);
            float y = 24 + AutoScrollPosition.Y;
            foreach (Block block in blocks)
            {
                Font font = FontFor(block.Role);
                float height = e.Graphics.MeasureString(block.Text + " ", font, width).Height;
                if (y + height >= 0 && y < ClientSize.Height)
                {
                    using (Brush ink = new SolidBrush(Color.FromArgb(StyleFor(block.Role).color)))
                        e.Graphics.DrawString(block.Text, font, ink, new RectangleF(24, y, width, height));
                }
                y += height + 10;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (Font font in fonts.Values) font.Dispose();
                if (backgroundImage != null) backgroundImage.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
