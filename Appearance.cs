using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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
        public int weight { get; set; }
    }

    internal sealed class BackgroundAppearance
    {
        public int color { get; set; }
        public string image { get; set; }
        public string fit { get; set; }
        public int shade { get; set; }
        public bool inherit { get; set; }
        // 0 = completely opaque panel, 100 = completely transparent panel.
        // This value is deliberately separate from text appearance so changing
        // the glass strength can never fade labels, numbers or buttons.
        public int panelTransparency { get; set; }
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
            settings.text["exampleTarget"] = Style(18, Color.White, true);
            settings.text["exampleTarget"].weight = 700;
            settings.text["meaning"] = Style(18, Color.White, false);
            settings.text["correct"] = Style(17, Color.FromArgb(70, 210, 105), true);
            settings.text["error"] = Style(17, Color.FromArgb(255, 82, 82), true);
            settings.text["mastered"] = Style(17, Color.FromArgb(115, 190, 255), true);
            foreach (TextAppearance style in settings.text.Values)
                style.font = "Microsoft YaHei UI";
            settings.text["exampleTarget"].font = "Segoe UI";
            settings.backgrounds = new Dictionary<string, BackgroundAppearance>();
            foreach (string key in new[] { "free", "new", "list_review", "problem_review", "calendar" })
                settings.backgrounds[key] = new BackgroundAppearance
                { color = Color.FromArgb(-15987700).ToArgb(),
                    image = "",
                    fit = "cover", shade = 35, panelTransparency = 0 };
            settings.backgrounds["main"] = new BackgroundAppearance
                { color = Color.FromArgb(-16052976).ToArgb(),
                    image = "",
                    fit = "cover", shade = 15, panelTransparency = 85 };
            settings.backgrounds["pause"] = new BackgroundAppearance
                { color = Color.FromArgb(18, 22, 30).ToArgb(), image = "", fit = "cover", shade = 42 };
            foreach (string key in new[] { "pause_new", "pause_list_review",
                "pause_problem_review", "pause_free" })
                settings.backgrounds[key] = new BackgroundAppearance
                { color = Color.FromArgb(18, 22, 30).ToArgb(), image = "", fit = "cover",
                    shade = 42, inherit = true };
            settings.prompts = new Dictionary<string, string>();
            settings.prompts["correct"] = "✓ 正确：{answer}";
            settings.prompts["error"] = "✗ 错误；正确答案：{answer}";
            settings.prompts["mastered"] = "已斩并移入已掌握：{word}";
            return settings;
        }

        private static TextAppearance Style(float size, Color color, bool bold)
        {
            return new TextAppearance { font = "Segoe UI", size = size,
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
            ClearBundledDefaultBackgrounds();
        }

        private void ClearBundledDefaultBackgrounds()
        {
            string main = Path.Combine(root, "appearance_images", "default.mp4");
            string study = Path.Combine(root, "appearance_images", "default-study.mp4");
            foreach (BackgroundAppearance background in Settings.backgrounds.Values)
            {
                if (background == null || string.IsNullOrWhiteSpace(background.image)) continue;
                string image;
                try { image = Path.GetFullPath(Path.IsPathRooted(background.image)
                    ? background.image : Path.Combine(root, background.image)); }
                catch (ArgumentException) { continue; }
                catch (NotSupportedException) { continue; }
                catch (PathTooLongException) { continue; }
                if (string.Equals(image, main, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(image, study, StringComparison.OrdinalIgnoreCase))
                    background.image = "";
            }
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
            return ImportAsset(source, extension);
        }

        public static bool IsVideo(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && new[]
                { ".mp4", ".m4v", ".mov", ".wmv", ".avi" }
                .Contains(Path.GetExtension(path).ToLowerInvariant());
        }

        public string ImportVideo(string source)
        {
            if (!IsVideo(source))
                throw new InvalidDataException("请选择 MP4、M4V、MOV、WMV 或 AVI 视频。");
            FileInfo info = new FileInfo(source);
            if (info.Length == 0 || info.Length > 500L * 1024 * 1024)
                throw new InvalidDataException("视频须大于 0 字节且不超过 500 MB。");
            return ImportAsset(source, Path.GetExtension(source).ToLowerInvariant());
        }

        private string ImportAsset(string source, string extension)
        {
            string folder = Path.Combine(root, "appearance_images");
            Directory.CreateDirectory(folder);
            FileInfo incoming = new FileInfo(source);
            foreach (string existing in Directory.GetFiles(folder))
            {
                FileInfo candidate = new FileInfo(existing);
                if (candidate.Length != incoming.Length) continue;
                if (!SameFileContent(source, existing)) continue;
                return Path.Combine("appearance_images", candidate.Name);
            }
            string name = Guid.NewGuid().ToString("N") + extension;
            File.Copy(source, Path.Combine(folder, name));
            return Path.Combine("appearance_images", name);
        }

        private static bool SameFileContent(string left, string right)
        {
            using (SHA256 hash = SHA256.Create())
            using (FileStream stream = File.OpenRead(left))
            {
                byte[] leftHash = hash.ComputeHash(stream);
                using (SHA256 otherHash = SHA256.Create())
                using (FileStream other = File.OpenRead(right))
                    return leftHash.SequenceEqual(otherHash.ComputeHash(other));
            }
        }

        public List<string> WallpaperAssets()
        {
            string folder = Path.Combine(root, "appearance_images");
            if (!Directory.Exists(folder)) return new List<string>();
            return Directory.GetFiles(folder)
                .Where(path => IsVideo(path) || new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif" }
                    .Contains(Path.GetExtension(path).ToLowerInvariant()))
                .OrderByDescending(path => File.GetLastWriteTimeUtc(path))
                .Select(path => Path.Combine("appearance_images", Path.GetFileName(path)))
                .ToList();
        }

        public List<string> WallpaperUsage(AppearanceSettings settings, string relativePath)
        {
            if (settings == null || settings.backgrounds == null
                || string.IsNullOrWhiteSpace(relativePath)) return new List<string>();
            return settings.backgrounds
                .Where(entry => entry.Value != null && string.Equals(
                    entry.Value.image ?? string.Empty, relativePath,
                    StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.Key).OrderBy(value => value).ToList();
        }

        public void DeleteWallpaperAsset(AppearanceSettings settings, string relativePath)
        {
            List<string> usage = WallpaperUsage(settings, relativePath);
            if (usage.Count > 0)
                throw new InvalidOperationException("该壁纸仍被界面使用，请先解除关联。");
            string path = ResolveAssetPath(relativePath);
            if (path != null && File.Exists(path)) File.Delete(path);
        }

        public int DeleteUnusedWallpaperAssets(AppearanceSettings settings)
        {
            int deleted = 0;
            foreach (string asset in WallpaperAssets())
            {
                if (WallpaperUsage(settings, asset).Count > 0) continue;
                string path = ResolveAssetPath(asset);
                if (path == null || !File.Exists(path)) continue;
                File.Delete(path);
                deleted++;
            }
            return deleted;
        }

        private string ResolveAssetPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return null;
            string folder = Path.GetFullPath(Path.Combine(root, "appearance_images"))
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(Path.Combine(root, relativePath));
            return candidate.StartsWith(folder, StringComparison.OrdinalIgnoreCase)
                ? candidate : null;
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
            FontStyle weight = (style.weight > 0 ? style.weight >= 600 : style.bold) ? FontStyle.Bold : FontStyle.Regular;
            try { return new Font(style.font, style.size, weight); }
            catch { return new Font("Segoe UI", style.size, weight); }
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
                if (background.fit != "contain" && background.fit != "stretch"
                    && background.fit != "center") background.fit = "cover";
                background.shade = Math.Max(0, Math.Min(90, background.shade));
                background.panelTransparency = Math.Max(0,
                    Math.Min(100, background.panelTransparency));
            }
            foreach (KeyValuePair<string, string> entry in defaults.prompts)
                if (!settings.prompts.ContainsKey(entry.Key)) settings.prompts[entry.Key] = entry.Value;
        }
    }

    internal class AppearanceBackgroundPanel : Panel
    {
        private AppearanceStore appearance;
        private string backgroundKey;
        private Image backgroundImage;
        private VideoBackground video;

        public AppearanceBackgroundPanel()
        {
            DoubleBuffered = true;
            BackColor = Theme.Background;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
        }

        public void SetAppearance(AppearanceStore store, string key)
        {
            if (video != null) { video.Dispose(); video = null; }
            appearance = store;
            backgroundKey = key;
            if (backgroundImage != null)
            {
                backgroundImage.Dispose();
                backgroundImage = null;
            }
            BackgroundAppearance background;
            if (appearance != null && appearance.Settings.backgrounds.TryGetValue(key, out background))
            {
                string path = appearance.ImagePath(background);
                if (AppearanceStore.IsVideo(path))
                    video = new VideoBackground(this, path, background, 10);
                else if (path != null)
                {
                    try
                    {
                        using (Image loaded = Image.FromFile(path))
                            backgroundImage = new Bitmap(loaded);
                    }
                    catch { backgroundImage = null; }
                }
            }
            Invalidate(true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            BackgroundAppearance background = null;
            if (appearance != null && backgroundKey != null)
                appearance.Settings.backgrounds.TryGetValue(backgroundKey, out background);
            Color color = background == null ? Theme.Background : Color.FromArgb(background.color);
            using (Brush fill = new SolidBrush(color))
                e.Graphics.FillRectangle(fill, ClientRectangle);
            if (video != null)
            {
                video.Draw(e.Graphics, ClientRectangle);
                return;
            }
            if (backgroundImage == null || background == null || Width < 1 || Height < 1) return;

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
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(backgroundImage, target);
            using (Brush shade = new SolidBrush(Color.FromArgb(
                background.shade * 255 / 100, 0, 0, 0)))
                e.Graphics.FillRectangle(shade, ClientRectangle);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && video != null) video.Dispose();
            if (disposing && backgroundImage != null) backgroundImage.Dispose();
            base.Dispose(disposing);
        }
    }

    internal sealed class StyledCanvas : ScrollableControl
    {
        private sealed class Block { public string Text; public string Role; public bool Marked; }
        private readonly List<Block> blocks = new List<Block>();
        private readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        private AppearanceStore appearance;
        private string backgroundKey;
        private Image backgroundImage;
        private VideoBackground video;
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

        public string ContentText { get { return string.Join(Environment.NewLine, blocks.Select(x => x.Marked ? ExampleCloze.PlainText(x.Text) : x.Text)); } }

        public void AddExample(WordEntry word)
        {
            AddMarkedExample(ExampleCloze.HighlightedExamples(word));
        }

        public void AddMarkedExample(string text)
        {
            blocks.Add(new Block { Text = text ?? "", Role = "example", Marked = true });
            Recalculate(); Invalidate();
        }

        public void SetAppearance(AppearanceStore store, string key)
        {
            if (video != null) { video.Dispose(); video = null; }
            appearance = store;
            backgroundKey = key;
            foreach (Font font in fonts.Values) font.Dispose();
            fonts.Clear();
            if (backgroundImage != null) { backgroundImage.Dispose(); backgroundImage = null; }
            if (appearance != null)
            {
                string path = appearance.ImagePath(appearance.Settings.backgrounds[key]);
                if (AppearanceStore.IsVideo(path))
                    video = new VideoBackground(this, path,
                        appearance.Settings.backgrounds[key], 10);
                else if (path != null)
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
                    height += (int)Math.Ceiling(block.Marked ? LayoutExample(graphics, block.Text, width, 0, false)
                        : LayoutPlain(graphics, block, width, 0, false)) + 28;
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
            if (video != null) { video.Draw(e.Graphics, ClientRectangle); return; }
            if (backgroundImage == null) return;
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
                if (block.Marked)
                {
                    y += LayoutExample(e.Graphics, block.Text, width, y, true) + 28;
                    continue;
                }
                y += LayoutPlain(e.Graphics, block, width, y, true) + 28;
            }
        }

        private float LayoutPlain(Graphics graphics, Block block, int width, float top, bool draw)
        {
            Font font = FontFor(block.Role);
            float line = font.GetHeight(graphics), y = 0;
            using (StringFormat format = new StringFormat(StringFormat.GenericDefault))
            using (Brush ink = new SolidBrush(Color.FromArgb(StyleFor(block.Role).color)))
            {
                format.FormatFlags |= StringFormatFlags.LineLimit;
                foreach (string paragraph in (block.Text ?? "").Replace("\r", "").Split('\n'))
                {
                    string remaining = paragraph;
                    if (remaining.Length == 0) { y += line * 1.45f; continue; }
                    while (remaining.Length > 0)
                    {
                        int chars, lines;
                        graphics.MeasureString(remaining, font, new SizeF(width, line * 1.25f), format, out chars, out lines);
                        chars = Math.Max(1, chars);
                        string part = remaining.Substring(0, Math.Min(chars, remaining.Length));
                        if (draw) graphics.DrawString(part, font, ink, new RectangleF(24, top + y, width, line * 1.45f), format);
                        remaining = remaining.Substring(part.Length);
                        y += line * 1.45f;
                    }
                }
            }
            return y;
        }

        private float LayoutExample(Graphics graphics, string text, int width, float top, bool draw)
        {
            float x = 0, y = 0, lineHeight = FontFor("example").GetHeight(graphics) * 1.3f;
            using (StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone())
            {
                format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
                foreach (Match segment in Regex.Matches(text ?? "", @"\[\[(.*?)\]\]|([^\[]+|\[)", RegexOptions.Singleline))
                {
                    bool target = segment.Value.StartsWith("[[");
                    string value = target ? segment.Groups[1].Value : segment.Value;
                    string role = target ? "exampleTarget" : "example";
                    Font font = FontFor(role);
                    TextAppearance style = StyleFor(role);
                    foreach (Match token in Regex.Matches(value.Replace("\r", ""), @"\n|[^\S\n]+|[^\s]+"))
                    {
                        string piece = token.Value;
                        if (piece == "\n") { x = 0; y += lineHeight; lineHeight = font.GetHeight(graphics) * 1.3f; continue; }
                        float measured = graphics.MeasureString(piece, font, int.MaxValue, format).Width;
                        // Split only oversized tokens so large custom fonts cannot clip at the right edge.
                        IEnumerable<string> pieces = measured > width ? piece.Select(c => c.ToString()) : new[] { piece };
                        foreach (string part in pieces)
                        {
                            float w = graphics.MeasureString(part, font, int.MaxValue, format).Width;
                            if (x > 0 && x + w > width) { x = 0; y += lineHeight; lineHeight = font.GetHeight(graphics) * 1.3f; }
                            lineHeight = Math.Max(lineHeight, font.GetHeight(graphics) * 1.3f);
                            if (draw)
                            {
                                using (Brush ink = new SolidBrush(Color.FromArgb(style.color)))
                                {
                                    graphics.DrawString(part, font, ink, new PointF(24 + x, top + y), format);
                                    if (target && style.weight > 700)
                                        graphics.DrawString(part, font, ink,
                                            new PointF(24 + x + (style.weight - 700) / 200f, top + y), format);
                                }
                            }
                            x += w;
                        }
                    }
                }
            }
            return y + lineHeight;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (video != null) video.Dispose();
                foreach (Font font in fonts.Values) font.Dispose();
                if (backgroundImage != null) backgroundImage.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
