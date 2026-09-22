using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace EnglishDictationTool
{
    internal static class Theme
    {
        public static readonly Color Background = Color.FromArgb(12, 12, 12);
        public static readonly Color Surface = Color.FromArgb(24, 24, 24);
        public static readonly Color SurfaceRaised = Color.FromArgb(42, 42, 42);
        public static readonly Color Border = Color.FromArgb(82, 82, 82);
        public static readonly Color Text = Color.White;
        public static readonly Color MutedText = Color.FromArgb(205, 205, 205);
        public static readonly Color Error = Color.FromArgb(255, 82, 82);
        public static readonly Color Correct = Color.FromArgb(70, 210, 105);
        public static readonly Font UiFont = new Font("Microsoft YaHei UI", 10.0f, FontStyle.Regular);
        public static readonly Font LogFont = new Font("Microsoft YaHei UI", 12.0f, FontStyle.Regular);

        public static void Apply(Control root)
        {
            bool modern = root is ModernButton || root is ModernCard
                || root is ModernHeader || root is ModernTabControl
                || root is ModernGroupBox;
            if (!modern)
            {
                bool inCard = false;
                for (Control parent = root.Parent; parent != null; parent = parent.Parent)
                    if (parent is ModernCard) { inCard = true; break; }
                root.BackColor = inCard ? ModernUI.Card : Background;
                root.ForeColor = Text;
                root.Font = UiFont;
            }
            else if (root is ModernGroupBox) root.BackColor = ModernUI.Card;

            foreach (Control child in root.Controls)
            {
                Apply(child);
            }

            if (modern) return;

            Button button = root as Button;
            if (button != null)
            {
                button.BackColor = SurfaceRaised;
                button.ForeColor = Text;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Border;
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(58, 58, 58);
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(70, 70, 70);
            }

            TextBox textBox = root as TextBox;
            if (textBox != null)
            {
                textBox.BackColor = Surface;
                textBox.ForeColor = Text;
                textBox.BorderStyle = BorderStyle.FixedSingle;
            }

            RichTextBox richTextBox = root as RichTextBox;
            if (richTextBox != null)
            {
                richTextBox.BackColor = Background;
                richTextBox.ForeColor = Text;
                richTextBox.BorderStyle = BorderStyle.FixedSingle;
            }

            CheckedListBox checkedList = root as CheckedListBox;
            if (checkedList != null)
            {
                checkedList.BackColor = Surface;
                checkedList.ForeColor = Text;
                checkedList.BorderStyle = BorderStyle.FixedSingle;
            }

            GroupBox groupBox = root as GroupBox;
            if (groupBox != null)
            {
                groupBox.BackColor = Background;
                groupBox.ForeColor = Text;
            }

            ComboBox comboBox = root as ComboBox;
            if (comboBox != null)
            {
                comboBox.BackColor = Surface;
                comboBox.ForeColor = Text;
                comboBox.FlatStyle = FlatStyle.Flat;
            }

            NumericUpDown number = root as NumericUpDown;
            if (number != null)
            {
                number.BackColor = Surface;
                number.ForeColor = Text;
            }
        }
    }

    internal sealed class WordEntry : IEquatable<WordEntry>
    {
        public string english { get; set; }
        public string chinese { get; set; }
        public string examples { get; set; }
        public string partOfSpeech { get; set; }

        public WordEntry()
        {
            english = string.Empty;
            chinese = string.Empty;
            examples = string.Empty;
            partOfSpeech = string.Empty;
        }

        public bool Equals(WordEntry other)
        {
            if (ReferenceEquals(other, null)) return false;
            return string.Equals(english, other.english, StringComparison.Ordinal)
                && string.Equals(chinese, other.chinese, StringComparison.Ordinal)
                && string.Equals(examples, other.examples, StringComparison.Ordinal)
                && string.Equals(partOfSpeech, other.partOfSpeech, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as WordEntry);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (english ?? string.Empty).GetHashCode();
                hash = hash * 31 + (chinese ?? string.Empty).GetHashCode();
                hash = hash * 31 + (examples ?? string.Empty).GetHashCode();
                hash = hash * 31 + (partOfSpeech ?? string.Empty).GetHashCode();
                return hash;
            }
        }
    }

    internal static class PartOfSpeech
    {
        private static readonly string[] DisplayOrder =
        {
            "n.", "v.", "adj.", "adv.", "pron.", "prep.", "conj.",
            "interj.", "det.", "num.", "abbr."
        };

        private static readonly Regex LeadingTag = new Regex(
            @"^\s*(?:\d+\)\s*)?(?<tag>noun|verb|adjective|adverb|pronoun|preposition|conjunction|interjection|determiner|number|numeral|abbreviation|n\.|v\.|vt\.|vi\.|adj\.|a\.|adv\.|pron\.|prep\.|conj\.|interj\.|det\.|num\.|abbr\.)\s*",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        public static bool IsPhrase(string english)
        {
            return Regex.IsMatch(DataLoader.Sanitize(english), @"\s");
        }

        public static string Normalize(string raw)
        {
            HashSet<string> tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string value in Regex.Split((raw ?? string.Empty).ToLowerInvariant(), @"[/,;|\s]+"))
            {
                string token = Regex.Replace(value, @":\d+(?:\.\d+)?$", string.Empty)
                    .Trim().TrimEnd('.');
                string normalized = NormalizeToken(token);
                if (normalized.Length > 0) tags.Add(normalized);
            }
            return string.Join("/", DisplayOrder.Where(tags.Contains));
        }

        public static string Merge(params string[] values)
        {
            return Normalize(string.Join("/", values == null ? new string[0] : values));
        }

        public static string SelectForDefinition(string english, string chinese, string fallback)
        {
            if (IsPhrase(english)) return string.Empty;
            string explicitTags = ExtractLeading(chinese);
            if (explicitTags.Length > 0) return explicitTags;

            string source = chinese ?? string.Empty;
            if (Regex.IsMatch(source, @"(?i)\[(?:C|U)(?:\s*[,\]]|\s)")) return "n.";
            if (Regex.IsMatch(source, @"(?i)\((?:only|usu\.|usually|not)\s+before\s+noun\)"))
                return "adj.";

            string normalizedFallback = Normalize(fallback);
            if (Regex.IsMatch(DataLoader.Sanitize(english), @"(?i)ly$")
                && Contains(normalizedFallback, "adv.")) return "adv.";
            if (normalizedFallback.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).Length <= 1)
                return normalizedFallback;

            string gloss = Regex.Replace(source, @"^\s*\d+\)\s*", string.Empty).Trim();
            if (Contains(normalizedFallback, "adv.") && Regex.IsMatch(gloss,
                @"(?i)^(?:in\s+(?:a|an|the)\s+.+?way\b|used\s+when\b|according\s+to\b|wrongly\b)"))
                return "adv.";
            if (Contains(normalizedFallback, "adj.") && Regex.IsMatch(gloss,
                @"(?i)^(?:able\b|based\b|belonging\b|connected\b|consisting\b|containing\b|done\b|existing\b|expected\b|feeling\b|full\b|having\b|involving\b|lasting\b|likely\b|made\b|making\b|not\b|relating\b|shared\b|showing\b|similar\b|suitable\b|used\b|wrong\b)"))
                return "adj.";
            if (Contains(normalizedFallback, "n.") && Regex.IsMatch(gloss,
                @"(?i)^(?:a|an|the|one|someone|somebody|something)\b")) return "n.";
            return normalizedFallback;
        }

        public static string DisplayChinese(WordEntry word)
        {
            if (word == null) return string.Empty;
            string chinese = word.chinese ?? string.Empty;
            if (IsPhrase(word.english)) return chinese;
            string part = Normalize(word.partOfSpeech);
            if (part.Length == 0) part = ExtractLeading(chinese);
            Match existing = LeadingTag.Match(chinese);
            bool beginsWithTag = existing.Success
                && string.IsNullOrWhiteSpace(chinese.Substring(0, existing.Index));
            if (part.Length == 0 || beginsWithTag) return chinese;
            return part + " " + chinese;
        }

        public static string DisplayPrefix(WordEntry word)
        {
            if (word == null || IsPhrase(word.english)) return string.Empty;
            string part = Normalize(word.partOfSpeech);
            return part.Length > 0 ? part : ExtractLeading(word.chinese);
        }

        public static bool HasLeadingTag(string text)
        {
            string value = text ?? string.Empty;
            Match existing = LeadingTag.Match(value);
            return existing.Success && string.IsNullOrWhiteSpace(value.Substring(0, existing.Index));
        }

        private static string ExtractLeading(string text)
        {
            List<string> tags = new List<string>();
            foreach (Match match in LeadingTag.Matches(text ?? string.Empty))
            {
                string normalized = NormalizeToken(match.Groups["tag"].Value);
                if (normalized.Length > 0) tags.Add(normalized);
            }
            return Normalize(string.Join("/", tags));
        }

        private static bool Contains(string normalized, string tag)
        {
            return (normalized ?? string.Empty).Split('/').Contains(tag);
        }

        private static string NormalizeToken(string token)
        {
            switch ((token ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant())
            {
                case "n": case "noun": return "n.";
                case "v": case "verb": case "vt": case "vi": return "v.";
                case "a": case "s": case "adj": case "adjective": return "adj.";
                case "r": case "ad": case "adv": case "adverb": return "adv.";
                case "pron": case "pronoun": return "pron.";
                case "prep": case "preposition": return "prep.";
                case "conj": case "conjunction": return "conj.";
                case "interj": case "interjection": return "interj.";
                case "det": case "determiner": case "article": case "art": return "det.";
                case "num": case "number": case "numeral": return "num.";
                case "abbr": case "abbreviation": return "abbr.";
                default: return string.Empty;
            }
        }
    }

    internal sealed class ExampleQuestion
    {
        public string prompt { get; set; }
        public string answer { get; set; }
        public List<string> answers { get; set; }

        public string Render(bool showFirstLetter)
        {
            string blank = "________";
            if (showFirstLetter && !string.IsNullOrWhiteSpace(answer))
            {
                string first = answer.Trim().Substring(0, 1);
                blank = first + "_______";
            }
            return (prompt ?? string.Empty).Replace("________", blank);
        }
    }

    internal static class ExampleRevealFlow
    {
        public static int LastRevealStage(bool firstLetterEnabled)
        {
            return firstLetterEnabled ? 2 : 1;
        }

        public static int NextStage(int current, bool firstLetterEnabled)
        {
            return Math.Min(LastRevealStage(firstLetterEnabled), Math.Max(0, current) + 1);
        }

        public static bool ShowsFirstLetter(int stage, bool firstLetterEnabled)
        {
            return firstLetterEnabled && stage >= 1;
        }

        public static bool ShowsMeaning(int stage, bool firstLetterEnabled)
        {
            return stage >= LastRevealStage(firstLetterEnabled);
        }

        public static bool ReadyToSubmit(int stage, bool firstLetterEnabled)
        {
            return stage >= LastRevealStage(firstLetterEnabled);
        }

        public static bool HasTypedAnswer(string input)
        {
            return !string.IsNullOrWhiteSpace(input);
        }
    }

    internal static class ExampleCloze
    {
        private static readonly Regex Marker = new Regex(@"\[\[(.*?)\]\]",
            RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex EnglishToken = new Regex(@"[A-Za-z]+(?:'[A-Za-z]+)?",
            RegexOptions.Compiled);

        public static List<ExampleQuestion> Questions(WordEntry word)
        {
            List<ExampleQuestion> result = new List<ExampleQuestion>();
            if (word == null || string.IsNullOrWhiteSpace(word.examples)) return result;
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in word.examples.Split(new[] { '；' }, StringSplitOptions.RemoveEmptyEntries))
            {
                ExampleQuestion question = Create(word, raw.Trim());
                if (question == null) continue;
                string key = DataLoader.Sanitize(question.prompt) + "\u001f"
                    + DataLoader.Sanitize(question.answer);
                if (seen.Add(key)) result.Add(question);
            }
            return result;
        }

        public static ExampleQuestion First(WordEntry word)
        {
            return Questions(word).FirstOrDefault();
        }

        public static string PlainText(string sentence)
        {
            return Marker.Replace(sentence ?? string.Empty, "$1");
        }

        private static ExampleQuestion Create(WordEntry word, string sentence)
        {
            MatchCollection all = Marker.Matches(sentence ?? string.Empty);
            if (all.Count == 0) return null;
            List<Match> selected = new List<Match>();
            if (all.Count == 1)
            {
                selected.Add(all[0]);
            }
            else
            {
                List<string> headTokens = Tokens(GameEngine.CleanEnglish(word));
                List<Match> related = all.Cast<Match>()
                    .Where(match => IsRelated(match.Groups[1].Value, headTokens)).ToList();
                if (related.Count == 0) return null;
                if (headTokens.Count <= 1)
                {
                    selected.Add(related[0]);
                }
                else
                {
                    Match wholePhrase = related.FirstOrDefault(match =>
                        Tokens(match.Groups[1].Value).Count > 1);
                    if (wholePhrase != null) selected.Add(wholePhrase);
                    else selected.AddRange(related);
                }
            }

            Match first = selected.First();
            Match last = selected.Last();
            int start = first.Index;
            int end = last.Index + last.Length;
            string answer = PlainText(sentence.Substring(start, end - start)).Trim();
            if (answer.Length == 0) return null;
            string prompt = PlainText(sentence.Substring(0, start)) + "________"
                + PlainText(sentence.Substring(end));
            return new ExampleQuestion
            {
                prompt = prompt,
                answer = answer,
                answers = new List<string> { answer }
            };
        }

        private static bool IsRelated(string marked, List<string> headTokens)
        {
            if (headTokens.Count == 0) return false;
            foreach (string markedToken in Tokens(marked))
                if (headTokens.Any(head => SameWordFamily(head, markedToken))) return true;
            return false;
        }

        private static List<string> Tokens(string text)
        {
            return EnglishToken.Matches(text ?? string.Empty).Cast<Match>()
                .Select(match => match.Value.ToLowerInvariant()).ToList();
        }

        private static bool SameWordFamily(string lemma, string form)
        {
            if (lemma == form) return true;
            Dictionary<string, string[]> irregular = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "be", new[] { "am", "is", "are", "was", "were", "been", "being" } },
                { "do", new[] { "does", "did", "done", "doing" } },
                { "go", new[] { "goes", "went", "gone", "going" } },
                { "come", new[] { "comes", "came", "coming" } },
                { "get", new[] { "gets", "got", "gotten", "getting" } },
                { "have", new[] { "has", "had", "having" } },
                { "make", new[] { "makes", "made", "making" } },
                { "run", new[] { "runs", "ran", "running" } },
                { "speak", new[] { "speaks", "spoke", "spoken", "speaking" } },
                { "take", new[] { "takes", "took", "taken", "taking" } },
                { "teach", new[] { "teaches", "taught", "teaching" } },
                { "write", new[] { "writes", "wrote", "written", "writing" } }
            };
            string[] forms;
            if (irregular.TryGetValue(lemma, out forms) && forms.Contains(form)) return true;
            HashSet<string> regular = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                lemma + "s", lemma + "es", lemma + "ed", lemma + "ing"
            };
            if (lemma.EndsWith("e", StringComparison.OrdinalIgnoreCase) && lemma.Length > 1)
            {
                regular.Add(lemma + "d");
                regular.Add(lemma.Substring(0, lemma.Length - 1) + "ing");
            }
            if (lemma.EndsWith("y", StringComparison.OrdinalIgnoreCase) && lemma.Length > 1)
            {
                regular.Add(lemma.Substring(0, lemma.Length - 1) + "ies");
                regular.Add(lemma.Substring(0, lemma.Length - 1) + "ied");
            }
            if (lemma.Length >= 3 && !"aeiou".Contains(lemma[lemma.Length - 1])
                && "aeiou".Contains(lemma[lemma.Length - 2]))
            {
                regular.Add(lemma + lemma[lemma.Length - 1] + "ing");
                regular.Add(lemma + lemma[lemma.Length - 1] + "ed");
            }
            return regular.Contains(form);
        }
    }

    internal static class AppPaths
    {
        public static string FindProjectRoot()
        {
            string configured = Environment.GetEnvironmentVariable("ENGLISH_DICTATION_PROJECT_ROOT");
            if (!string.IsNullOrWhiteSpace(configured)
                && Directory.Exists(Path.Combine(configured, "data")))
            {
                return Path.GetFullPath(configured);
            }
            DirectoryInfo current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            for (int i = 0; i < 8 && current != null; i++, current = current.Parent)
            {
                string data = Path.Combine(current.FullName, "data");
                if (IsWordDataDirectory(data)) return current.FullName;
            }

            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static bool IsWordDataDirectory(string directory)
        {
            if (!Directory.Exists(directory)) return false;
            try
            {
                return Directory.GetDirectories(directory).Any(
                    subdirectory => Directory.GetFiles(subdirectory, "*.csv").Length > 0);
            }
            catch
            {
                return false;
            }
        }
    }

    internal sealed class NaturalStringComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            string[] left = Regex.Split(x ?? string.Empty, "([0-9]+)");
            string[] right = Regex.Split(y ?? string.Empty, "([0-9]+)");
            int length = Math.Min(left.Length, right.Length);

            for (int i = 0; i < length; i++)
            {
                int leftNumber;
                int rightNumber;
                if (int.TryParse(left[i], out leftNumber) && int.TryParse(right[i], out rightNumber))
                {
                    int numeric = leftNumber.CompareTo(rightNumber);
                    if (numeric != 0) return numeric;
                }
                else
                {
                    int textual = string.Compare(left[i], right[i], StringComparison.CurrentCultureIgnoreCase);
                    if (textual != 0) return textual;
                }
            }

            return left.Length.CompareTo(right.Length);
        }
    }

    internal sealed class DataLoader
    {
        private readonly string dataDirectory;
        private readonly NaturalStringComparer comparer = new NaturalStringComparer();
        private readonly Dictionary<string, string> defaultPartsOfSpeech;

        public string DataDirectory { get { return dataDirectory; } }

        public DataLoader(string directory)
        {
            dataDirectory = directory;
            if (!Directory.Exists(dataDirectory))
            {
                throw new DirectoryNotFoundException("未找到 data 目录：" + dataDirectory);
            }
            defaultPartsOfSpeech = LoadPartOfSpeechMap();
        }

        public List<string> GetAvailableBooks()
        {
            List<string> books = new List<string>();
            foreach (string directory in Directory.GetDirectories(dataDirectory))
            {
                if (Directory.GetFiles(directory, "*.csv").Length > 0)
                {
                    books.Add(Path.GetFileName(directory));
                }
            }
            books.Sort(comparer);
            return books;
        }

        public List<string> GetUnitsForBook(string book)
        {
            string bookPath = Path.Combine(dataDirectory, book ?? string.Empty);
            if (!Directory.Exists(bookPath)) return new List<string>();

            List<string> units = Directory.GetFiles(bookPath, "*.csv")
                .Select(Path.GetFileNameWithoutExtension)
                .ToList();
            units.Sort(comparer);
            return units;
        }

        public List<WordEntry> LoadWordList(string book, IEnumerable<string> units)
        {
            List<WordEntry> result = new List<WordEntry>();
            foreach (string unit in units)
            {
                string file = Path.Combine(dataDirectory, book, unit + ".csv");
                if (!File.Exists(file)) continue;

                string csv = ReadCsvText(file);
                List<List<string>> rows = ParseCsv(csv);
                if (rows.Count == 0) continue;

                Dictionary<string, int> headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < rows[0].Count; i++)
                {
                    headers[Sanitize(rows[0][i])] = i;
                }

                int englishIndex;
                int chineseIndex;
                int examplesIndex;
                int partOfSpeechIndex;
                if (!headers.TryGetValue("english", out englishIndex)
                    || !headers.TryGetValue("chinese", out chineseIndex))
                {
                    continue;
                }
                bool hasExamples = headers.TryGetValue("examples", out examplesIndex);
                bool hasPartOfSpeech = headers.TryGetValue("part_of_speech", out partOfSpeechIndex)
                    || headers.TryGetValue("pos", out partOfSpeechIndex);

                for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
                {
                    List<string> row = rows[rowIndex];
                    string english = Sanitize(GetCell(row, englishIndex));
                    if (string.IsNullOrWhiteSpace(english)) continue;
                    string chinese = Sanitize(GetCell(row, chineseIndex));
                    string explicitPart = hasPartOfSpeech
                        ? PartOfSpeech.Normalize(GetCell(row, partOfSpeechIndex)) : string.Empty;
                    string mappedPart;
                    defaultPartsOfSpeech.TryGetValue(Regex.Replace(english, @"\s+", " ").Trim(),
                        out mappedPart);

                    result.Add(new WordEntry
                    {
                        english = english,
                        chinese = chinese,
                        examples = hasExamples ? Sanitize(GetCell(row, examplesIndex)) : string.Empty,
                        partOfSpeech = explicitPart.Length > 0
                            ? (PartOfSpeech.IsPhrase(english) ? string.Empty : explicitPart)
                            : PartOfSpeech.SelectForDefinition(english, chinese, mappedPart)
                    });
                }
            }
            return MergeWordEntries(result);
        }

        public static string WordKey(WordEntry word)
        {
            string english = word == null ? string.Empty : Sanitize(word.english);
            return Regex.Replace(english, @"\s+", " ").Trim().ToLowerInvariant();
        }

        public static List<WordEntry> MergeWordEntries(IEnumerable<WordEntry> words)
        {
            List<WordEntry> merged = new List<WordEntry>();
            Dictionary<string, WordEntry> byEnglish = new Dictionary<string, WordEntry>(
                StringComparer.OrdinalIgnoreCase);
            foreach (WordEntry source in words ?? Enumerable.Empty<WordEntry>())
            {
                if (source == null || WordKey(source).Length == 0) continue;
                string key = WordKey(source);
                WordEntry target;
                if (!byEnglish.TryGetValue(key, out target))
                {
                    target = new WordEntry
                    {
                        english = Sanitize(source.english),
                        chinese = Sanitize(source.chinese),
                        examples = Sanitize(source.examples),
                        partOfSpeech = PartOfSpeech.IsPhrase(source.english) ? string.Empty
                            : PartOfSpeech.Normalize(source.partOfSpeech)
                    };
                    byEnglish[key] = target;
                    merged.Add(target);
                    continue;
                }
                target.chinese = MergeField(target.chinese, source.chinese, "\n");
                target.examples = MergeExamples(target.examples, source.examples);
                target.partOfSpeech = PartOfSpeech.IsPhrase(target.english) ? string.Empty
                    : PartOfSpeech.Merge(target.partOfSpeech, source.partOfSpeech);
            }
            return merged;
        }

        public static WordEntry MergeWordEntry(IEnumerable<WordEntry> words)
        {
            return MergeWordEntries(words).FirstOrDefault();
        }

        private static string MergeField(string current, string incoming, string separator)
        {
            current = Sanitize(current);
            incoming = Sanitize(incoming);
            if (incoming.Length == 0) return current;
            if (current.Length == 0) return incoming;
            if (string.Equals(current, incoming, StringComparison.Ordinal)) return current;
            return current + separator + incoming;
        }

        private static string MergeExamples(string current, string incoming)
        {
            List<string> result = new List<string>();
            foreach (string value in new[] { current, incoming })
                foreach (string example in Sanitize(value).Split(new[] { '；' },
                    StringSplitOptions.RemoveEmptyEntries))
                {
                    string cleaned = example.Trim();
                    if (cleaned.Length > 0 && !result.Contains(cleaned)) result.Add(cleaned);
                }
            return string.Join("；", result);
        }

        private Dictionary<string, string> LoadPartOfSpeechMap()
        {
            Dictionary<string, string> result = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            string file = Path.Combine(dataDirectory, "parts_of_speech.csv");
            if (!File.Exists(file)) return result;
            List<List<string>> rows = ParseCsv(ReadCsvText(file));
            if (rows.Count == 0) return result;
            Dictionary<string, int> headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < rows[0].Count; index++)
                headers[Sanitize(rows[0][index])] = index;
            int englishIndex;
            int partIndex;
            if (!headers.TryGetValue("english", out englishIndex)
                || (!headers.TryGetValue("part_of_speech", out partIndex)
                    && !headers.TryGetValue("pos", out partIndex))) return result;
            foreach (List<string> row in rows.Skip(1))
            {
                string english = Regex.Replace(Sanitize(GetCell(row, englishIndex)), @"\s+", " ").Trim();
                string part = PartOfSpeech.Normalize(GetCell(row, partIndex));
                if (english.Length == 0 || part.Length == 0 || PartOfSpeech.IsPhrase(english)) continue;
                string existing;
                result.TryGetValue(english, out existing);
                result[english] = PartOfSpeech.Merge(existing, part);
            }
            return result;
        }

        public int ValidateCsvFile(string file)
        {
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
                throw new FileNotFoundException("找不到所选 CSV 文件。", file);
            if (!string.Equals(Path.GetExtension(file), ".csv", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("只支持导入 .csv 文件。");
            List<List<string>> rows = ParseCsv(ReadCsvText(file));
            if (rows.Count == 0) throw new InvalidDataException("CSV 文件为空。");
            Dictionary<string, int> headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rows[0].Count; i++) headers[Sanitize(rows[0][i])] = i;
            int englishIndex;
            int chineseIndex;
            if (!headers.TryGetValue("english", out englishIndex)
                || !headers.TryGetValue("chinese", out chineseIndex))
                throw new InvalidDataException("CSV 必须包含 english 和 chinese 两列，可另加 examples 和 part_of_speech（或 pos）列。");
            int count = rows.Skip(1).Count(row => !string.IsNullOrWhiteSpace(GetCell(row, englishIndex)));
            if (count == 0) throw new InvalidDataException("CSV 中没有可导入的英文词条。");
            return count;
        }

        public string ImportCsv(string sourceFile, string book, string unit, bool overwrite)
        {
            ValidateCsvFile(sourceFile);
            book = ValidateDataName(book, "词书名称");
            unit = ValidateDataName(unit, "单元名称");
            string bookPath = Path.Combine(dataDirectory, book);
            string destination = Path.Combine(bookPath, unit + ".csv");
            if (File.Exists(destination) && !overwrite)
                throw new IOException("目标单元已经存在：" + book + " / " + unit);
            Directory.CreateDirectory(bookPath);
            File.WriteAllText(destination, ReadCsvText(sourceFile), new UTF8Encoding(true));
            return destination;
        }

        public void RenameBook(string oldName, string newName)
        {
            oldName = ValidateDataName(oldName, "原词书名称");
            newName = ValidateDataName(newName, "新词书名称");
            if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase)) return;
            string source = Path.Combine(dataDirectory, oldName);
            string destination = Path.Combine(dataDirectory, newName);
            if (!Directory.Exists(source)) throw new DirectoryNotFoundException("找不到词书：" + oldName);
            if (Directory.Exists(destination)) throw new IOException("已存在同名词书：" + newName);
            Directory.Move(source, destination);
        }

        public static string ValidateDataName(string value, string label)
        {
            string name = (value ?? string.Empty).Trim();
            if (name.Length == 0) throw new ArgumentException(label + "不能为空。");
            if (name == "." || name == ".." || name.Length > 80
                || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || name.Contains(Path.DirectorySeparatorChar.ToString())
                || name.Contains(Path.AltDirectorySeparatorChar.ToString()))
                throw new ArgumentException(label + "包含无效字符或长度超过 80。");
            return name;
        }

        private static string ReadCsvText(string file)
        {
            byte[] bytes = File.ReadAllBytes(file);
            try
            {
                return new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
            }
            catch (DecoderFallbackException)
            {
                return Encoding.Default.GetString(bytes).TrimStart('\uFEFF');
            }
        }

        private static string GetCell(List<string> row, int index)
        {
            return index >= 0 && index < row.Count ? row[index] : string.Empty;
        }

        public static string Sanitize(string text)
        {
            return (text ?? string.Empty)
                .Replace("\uFEFF", string.Empty)
                .Replace("\u200B", string.Empty)
                .Replace("\u00A0", " ")
                .Trim();
        }

        private static List<List<string>> ParseCsv(string text)
        {
            List<List<string>> rows = new List<List<string>>();
            List<string> row = new List<string>();
            StringBuilder field = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < text.Length; i++)
            {
                char current = text[i];
                if (quoted)
                {
                    if (current == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            quoted = false;
                        }
                    }
                    else
                    {
                        field.Append(current);
                    }
                    continue;
                }

                if (current == '"')
                {
                    quoted = true;
                }
                else if (current == ',')
                {
                    row.Add(field.ToString());
                    field.Length = 0;
                }
                else if (current == '\r' || current == '\n')
                {
                    if (current == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    row.Add(field.ToString());
                    field.Length = 0;
                    if (row.Any(value => value.Length > 0)) rows.Add(row);
                    row = new List<string>();
                }
                else
                {
                    field.Append(current);
                }
            }

            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                if (row.Any(value => value.Length > 0)) rows.Add(row);
            }

            return rows;
        }
    }

    internal sealed class PracticeEngineSnapshot
    {
        public List<WordEntry> deck { get; set; }
        public List<ExampleQuestion> examples { get; set; }
        public List<string> modes { get; set; }
        public int currentIndex { get; set; }
        public bool showFirstLetter { get; set; }
        public bool retryOnWrong { get; set; }
        public bool reviewMode { get; set; }
    }

    internal sealed class GameEngine
    {
        private readonly DataLoader loader;
        private readonly NotebookStore notebooks;
        private readonly Random random = new Random();
        private List<ExampleQuestion> exampleDeck;
        private List<string> modeDeck;

        public List<WordEntry> CurrentDeck { get; private set; }
        public int CurrentIndex { get; set; }
        public string QuestionMode { get; set; }
        public Func<WordEntry, string, string, IEnumerable<string>, bool> AnswerValidator { get; set; }
        public bool ShowFirstLetter { get; set; }
        public bool RetryOnWrong { get; private set; }
        public bool IsReviewMode { get; private set; }
        public List<WordEntry> WrongWords { get { return notebooks.GetWords(Notebooks.Wrong); } }

        public GameEngine(DataLoader dataLoader, NotebookStore notebookStore)
        {
            loader = dataLoader;
            notebooks = notebookStore;
            CurrentDeck = new List<WordEntry>();
            exampleDeck = new List<ExampleQuestion>();
            modeDeck = new List<string>();
            QuestionMode = "word";
        }

        public void StartGame(string book, List<string> units, string filterMode,
            string orderMode, string questionMode, bool showFirstLetter, bool retryOnWrong)
        {
            StartGame(book, units, filterMode, orderMode,
                questionMode == "example", questionMode == "dictation", questionMode == "word",
                new List<string> { questionMode == "word" ? "spelling" : questionMode },
                showFirstLetter, retryOnWrong);
        }

        public void StartGame(string book, List<string> units, string filterMode,
            string orderMode, bool example, bool dictation, bool spelling,
            IEnumerable<string> taskOrder, bool showFirstLetter, bool retryOnWrong)
        {
            List<WordEntry> deck = new List<WordEntry>();
            if (orderMode == "unit_random")
            {
                foreach (string unit in units)
                {
                    List<WordEntry> group = Filter(loader.LoadWordList(book, new[] { unit }), filterMode);
                    Shuffle(group);
                    foreach (WordEntry word in group)
                        if (!deck.Any(x => DataLoader.WordKey(x) == DataLoader.WordKey(word))) deck.Add(word);
                }
            }
            else
            {
                deck = Filter(loader.LoadWordList(book, units), filterMode);
                if (orderMode == "book_random" || orderMode == "random") Shuffle(deck);
            }
            exampleDeck = new List<ExampleQuestion>();
            modeDeck = new List<string>();
            List<WordEntry> expanded = new List<WordEntry>();
            foreach (WordEntry word in deck)
            {
                foreach (string mode in taskOrder ?? new[] { "example", "dictation", "spelling" })
                {
                    if (mode == "example" && example)
                    {
                        List<ExampleQuestion> questions = ExampleCloze.Questions(word);
                        foreach (ExampleQuestion question in questions)
                        {
                            expanded.Add(word); exampleDeck.Add(question); modeDeck.Add("example");
                        }
                    }
                    else if (mode == "dictation" && dictation)
                    {
                        expanded.Add(word); exampleDeck.Add(null); modeDeck.Add("dictation");
                    }
                    else if (mode == "spelling" && spelling)
                    {
                        expanded.Add(word); exampleDeck.Add(null); modeDeck.Add("word");
                    }
                }
            }
            CurrentDeck = expanded;
            CurrentIndex = 0;
            QuestionMode = modeDeck.Count == 0 ? "word" : modeDeck[0];
            ShowFirstLetter = showFirstLetter;
            RetryOnWrong = retryOnWrong;
            IsReviewMode = false;
            ClearExampleCache();
        }

        private List<WordEntry> Filter(IEnumerable<WordEntry> words, string filterMode)
        {
            IEnumerable<WordEntry> query = words.Where(item =>
                notebooks.GetNotebook(item) != Notebooks.Mastered);
            if (filterMode == "words_only") query = query.Where(item =>
                !Regex.IsMatch(CleanEnglish(item), @"\s"));
            else if (filterMode == "phrases_only") query = query.Where(item =>
                Regex.IsMatch(CleanEnglish(item), @"\s"));
            return query.ToList();
        }

        public bool StartReviewMode()
        {
            if (WrongWords.Count == 0) return false;
            CurrentDeck = new List<WordEntry>(WrongWords);
            CurrentIndex = 0;
            QuestionMode = "word";
            ShowFirstLetter = notebooks.ReviewFirstLetter;
            RetryOnWrong = false;
            IsReviewMode = true;
            exampleDeck = new List<ExampleQuestion>();
            modeDeck = CurrentDeck.Select(x => "word").ToList();
            ClearExampleCache();
            return true;
        }

        public ExampleQuestion GetExampleQuestion()
        {
            if (QuestionMode != "example") return null;
            WordEntry current = GetNextQuestion();
            if (current == null) return null;
            if (CurrentIndex >= 0 && CurrentIndex < exampleDeck.Count)
                return exampleDeck[CurrentIndex];
            List<ExampleQuestion> questions = ExampleCloze.Questions(current);
            return questions.FirstOrDefault();
        }

        public WordEntry GetNextQuestion()
        {
            if (CurrentIndex < 0) CurrentIndex = 0;
            while (CurrentIndex < CurrentDeck.Count)
            {
                string notebook = notebooks.GetNotebook(CurrentDeck[CurrentIndex]);
                if (IsReviewMode && notebook != Notebooks.Wrong) CurrentIndex++;
                else if (!IsReviewMode && notebook == Notebooks.Mastered) CurrentIndex++;
                else break;
            }
            if (CurrentIndex >= CurrentDeck.Count) return null;
            if (CurrentIndex < modeDeck.Count) QuestionMode = modeDeck[CurrentIndex];
            return CurrentDeck[CurrentIndex];
        }

        public AnswerOutcome CheckAnswer(string userInput)
        {
            WordEntry current = GetNextQuestion();
            if (current == null) return new AnswerOutcome();

            ExampleQuestion example = QuestionMode == "example" ? GetExampleQuestion() : null;
            IEnumerable<string> expected = example == null ? null : example.answers;
            bool correct = AnswerValidator != null
                ? AnswerValidator(current, userInput, QuestionMode, expected)
                : (QuestionMode == "example" && example != null
                    ? example.answers.Any(value => string.Equals(
                        DataLoader.Sanitize(userInput).ToLowerInvariant(),
                        DataLoader.Sanitize(value).ToLowerInvariant(), StringComparison.Ordinal))
                    : string.Equals(DataLoader.Sanitize(userInput).ToLowerInvariant(),
                        CleanEnglish(current).ToLowerInvariant(), StringComparison.Ordinal));

            AnswerOutcome outcome = notebooks.RecordAnswer(current, correct, IsReviewMode);
            outcome.CorrectAnswer = example == null ? CleanEnglish(current) : example.answer;
            CurrentIndex++;
            return outcome;
        }

        public WordEntry SkipWithoutPenalty(out bool removedFromWrongWords)
        {
            WordEntry current = GetNextQuestion();
            removedFromWrongWords = false;
            if (current == null) return null;
            removedFromWrongWords = notebooks.SkipWithoutPenalty(current);
            CurrentIndex++;
            return current;
        }

        public WordEntry MasterCurrent()
        {
            WordEntry current = GetNextQuestion();
            if (current == null) return null;
            notebooks.Master(current);
            CurrentIndex++;
            return current;
        }

        public void ClearWrongWordsCache()
        {
            notebooks.ClearWrongWords();
        }

        public int[] GetProgress()
        {
            return new[] { Math.Min(CurrentIndex + 1, CurrentDeck.Count), CurrentDeck.Count };
        }

        public PracticeEngineSnapshot ExportState()
        {
            return new PracticeEngineSnapshot
            {
                deck = new List<WordEntry>(CurrentDeck),
                examples = new List<ExampleQuestion>(exampleDeck),
                modes = new List<string>(modeDeck), currentIndex = CurrentIndex,
                showFirstLetter = ShowFirstLetter, retryOnWrong = RetryOnWrong,
                reviewMode = IsReviewMode
            };
        }

        public void RestoreState(PracticeEngineSnapshot snapshot)
        {
            if (snapshot == null || snapshot.deck == null) throw new InvalidDataException("自由练习进度无效。");
            CurrentDeck = new List<WordEntry>(snapshot.deck);
            exampleDeck = snapshot.examples == null ? CurrentDeck.Select(x => (ExampleQuestion)null).ToList()
                : new List<ExampleQuestion>(snapshot.examples);
            modeDeck = snapshot.modes == null ? CurrentDeck.Select(x => "word").ToList()
                : new List<string>(snapshot.modes);
            while (exampleDeck.Count < CurrentDeck.Count) exampleDeck.Add(null);
            while (modeDeck.Count < CurrentDeck.Count) modeDeck.Add("word");
            CurrentIndex = Math.Max(0, Math.Min(snapshot.currentIndex, CurrentDeck.Count));
            ShowFirstLetter = snapshot.showFirstLetter;
            RetryOnWrong = snapshot.retryOnWrong;
            IsReviewMode = snapshot.reviewMode;
            GetNextQuestion();
        }

        public void RemoveRemainingMode(string mode)
        {
            for (int index = CurrentDeck.Count - 1; index >= CurrentIndex; index--)
            {
                string currentMode = index < modeDeck.Count ? modeDeck[index] : "word";
                if (!string.Equals(currentMode, mode, StringComparison.OrdinalIgnoreCase)) continue;
                CurrentDeck.RemoveAt(index);
                if (index < exampleDeck.Count) exampleDeck.RemoveAt(index);
                if (index < modeDeck.Count) modeDeck.RemoveAt(index);
            }
            GetNextQuestion();
        }

        public static string CleanEnglish(WordEntry word)
        {
            if (word == null) return string.Empty;
            return DataLoader.Sanitize((word.english ?? string.Empty).Split(',')[0]);
        }

        public static string ChineseHint(string text)
        {
            MatchCollection matches = Regex.Matches(text ?? string.Empty, @"[\u4e00-\u9fa5；，。（）]+");
            List<string> hints = matches.Cast<Match>()
                .Select(match => match.Value.Trim())
                .Where(value => value.Length > 0)
                .ToList();
            return hints.Count == 0 ? (text ?? string.Empty).Trim() : string.Join(" / ", hints);
        }

        public static string ChineseHint(WordEntry word)
        {
            if (word == null) return string.Empty;
            string hint = ChineseHint(word.chinese);
            string prefix = PartOfSpeech.DisplayPrefix(word);
            return prefix.Length == 0 || hint.Length == 0 ? hint : prefix + " " + hint;
        }

        private void Shuffle(List<WordEntry> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                WordEntry temp = list[i];
                list[i] = list[other];
                list[other] = temp;
            }
        }

        private void ClearExampleCache()
        {
        }

    }

    internal sealed class DarkToolStripRenderer : ToolStripProfessionalRenderer
    {
        public DarkToolStripRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }
    }

    internal sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripGradientBegin { get { return Theme.Surface; } }
        public override Color ToolStripGradientMiddle { get { return Theme.Surface; } }
        public override Color ToolStripGradientEnd { get { return Theme.Surface; } }
        public override Color ButtonSelectedGradientBegin { get { return Theme.SurfaceRaised; } }
        public override Color ButtonSelectedGradientMiddle { get { return Theme.SurfaceRaised; } }
        public override Color ButtonSelectedGradientEnd { get { return Theme.SurfaceRaised; } }
        public override Color ButtonPressedGradientBegin { get { return Theme.Border; } }
        public override Color ButtonPressedGradientMiddle { get { return Theme.Border; } }
        public override Color ButtonPressedGradientEnd { get { return Theme.Border; } }
        public override Color MenuItemBorder { get { return Theme.Border; } }
    }

    internal sealed class DarkComboBox : ComboBox
    {
        public DarkComboBox()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            // DropDownList ignores BackColor for its closed field on some
            // Windows themes. DropDown preserves the requested dark colors;
            // keyboard editing is suppressed below so it still behaves as a picker.
            DropDownStyle = ComboBoxStyle.DropDown;
            BackColor = Theme.Surface;
            ForeColor = Theme.Text;
            FlatStyle = FlatStyle.Flat;
            ItemHeight = Math.Max(30, TextRenderer.MeasureText("国Ag", Theme.UiFont).Height + 8);
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            e.Handled = true;
            base.OnKeyPress(e);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            e.DrawBackground();
            Color background = (e.State & DrawItemState.Selected) != 0 ? Theme.SurfaceRaised : Theme.Surface;
            using (SolidBrush brush = new SolidBrush(background)) e.Graphics.FillRectangle(brush, e.Bounds);
            if (e.Index >= 0)
            {
                TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, e.Bounds,
                    Theme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
            e.DrawFocusRectangle();
        }
    }

    internal sealed class DarkCheckedListBox : CheckedListBox
    {
        public DarkCheckedListBox()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = Math.Max(34, TextRenderer.MeasureText("国Ag", Theme.UiFont).Height + 10);
            BackColor = Theme.Surface;
            ForeColor = Theme.Text;
            BorderStyle = BorderStyle.FixedSingle;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            Color background = (e.State & DrawItemState.Selected) != 0
                ? Theme.SurfaceRaised : Theme.Surface;
            using (SolidBrush brush = new SolidBrush(background))
                e.Graphics.FillRectangle(brush, e.Bounds);
            Rectangle checkRect = new Rectangle(e.Bounds.Left + 6,
                e.Bounds.Top + (e.Bounds.Height - 16) / 2, 16, 16);
            ControlPaint.DrawCheckBox(e.Graphics, checkRect,
                GetItemChecked(e.Index) ? ButtonState.Checked : ButtonState.Normal);
            Rectangle textRect = new Rectangle(e.Bounds.Left + 27, e.Bounds.Top,
                e.Bounds.Width - 30, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font,
                textRect, Theme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            e.DrawFocusRectangle();
        }
    }

    internal sealed class MainForm : Form
    {
        private const int DwmUseImmersiveDarkMode = 20;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute,
            ref int value, int valueSize);

        private readonly string projectRoot;
        private readonly DataLoader loader;
        private readonly NotebookStore notebooks;
        private readonly GameEngine engine;
        private readonly StudyStore study;
        private readonly BackupService backups;
        private readonly AppearanceStore appearance;
        private readonly PracticeStore practice;
        private readonly UpdateService updater;
        private readonly Timer backupTimer;
        private DateTime nextAutoBackup;
        private readonly Random random = new Random();
        private Dictionary<string, string> keybindings;
        private WordEntry currentWord;
        private int exampleRevealStage;

        private Panel settingsPanel;
        private FlowLayoutPanel quickActions;
        private TableLayoutPanel mainCenter;
        private StyledCanvas logArea;
        private TextBox inputLine;
        private DarkComboBox bookCombo;
        private CheckedListBox unitList;
        private RadioButton contentAll;
        private RadioButton contentWords;
        private RadioButton contentPhrases;
        private RadioButton orderSequential;
        private RadioButton orderRandom;
        private RadioButton orderUnitRandom;
        private CheckBox questionWord;
        private CheckBox questionExample;
        private CheckBox questionDictation;
        private CheckBox showFirstLetter;
        private CheckBox retryOnWrong;
        private Button reviewButton;
        private Button dailyNewButton;
        private Button dailyListButton;
        private Button dailyProblemButton;
        private Button dailyNewEndButton;
        private Button dailyListEndButton;
        private Button dailyProblemEndButton;
        private Label dailyProgress;
        private CheckBox reviewFirstLetter;
        private NumericUpDown reviewCorrectTarget;
        private TextBox shortcutCapture;
        private FlowLayoutPanel recentQueue;
        private bool updatingRecentQueue;
        private Button freeStartButton;
        private Label freeTimerLabel;
        private Button freePauseButton;
        private PauseOverlay freePauseOverlay;
        private LearningTimer freeLearningTimer;
        private bool freePaused;
        private bool freeDictationMeaningShown;
        private bool freeDictationTemporarilyDisabled;
        private WordEntry lastFreeDictationSpoken;
        private WordPronouncer freePronouncer;
        private PronunciationSettings freePronunciationSettings;
        private ModernProgressBar freeProgress;

        public MainForm()
        {
            ModernUI.ApplyAppIcon(this);
            projectRoot = AppPaths.FindProjectRoot();
            loader = new DataLoader(Path.Combine(projectRoot, "data"));
            notebooks = new NotebookStore(projectRoot);
            engine = new GameEngine(loader, notebooks);
            study = new StudyStore(projectRoot, loader, notebooks);
            engine.AnswerValidator = study.IsCorrect;
            backups = new BackupService(projectRoot);
            appearance = new AppearanceStore(projectRoot);
            practice = new PracticeStore(projectRoot);
            updater = new UpdateService(projectRoot, backups);
            freePronunciationSettings = new PronunciationStore(projectRoot).Settings;
            freePronouncer = new WordPronouncer(freePronunciationSettings);
            keybindings = LoadKeybindings();

            Text = "大英默写器 · KRY 增强版 v1.2.0";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = ModernUI.FitWindow(1500, 900);
            MinimumSize = new Size(960, 650);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = Theme.UiFont;
            AutoScaleMode = AutoScaleMode.Dpi;

            BuildInterface();
            notebooks.Changed += delegate
            {
                UpdateReviewButtonCount();
                RefreshRecentQueue();
                UpdateDailyButtons();
            };
            study.Changed += delegate { UpdateDailyButtons(); };
            PopulateBooks();
            UpdateReviewButtonCount();
            UpdateDailyButtons();
            RefreshRecentQueue();
            Theme.Apply(this);
            Theme.Apply(settingsPanel);
            ApplyAppearance();
            logArea.ClearContent();
            AppendToLog("欢迎使用大英默写器！", "normal");
            AppendToLog("请先选择学习方式。自由练习选项可在顶部设置中调整。", "normal");
            RestoreFreePracticeIfNeeded();
            nextAutoBackup = DateTime.Now.AddMinutes(study.Settings.autoBackupMinutes);
            backupTimer = new Timer { Interval = 60000 };
            backupTimer.Tick += delegate
            {
                study.SettleCrossDay(DateTime.Now);
                if (DateTime.Now < nextAutoBackup) return;
                nextAutoBackup = DateTime.Now.AddMinutes(study.Settings.autoBackupMinutes);
                try
                {
                    if (backups.AutoDue(study.Settings.autoBackupMinutes))
                        backups.Create(false, study.Settings.autoBackupKeep);
                }
                catch (Exception error) { AppendToLog("自动备份失败：" + error.Message, "error"); }
            };
            backupTimer.Start();
            Shown += delegate
            {
                UpdateService.ShowPriorResult(this, projectRoot);
                if (study.Settings.checkUpdatesOnStartup
                    && Environment.GetCommandLineArgs().Length == 1)
                    updater.CheckAsync(this, true);
            };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int enabled = 1;
            try
            {
                int result = DwmSetWindowAttribute(Handle, DwmUseImmersiveDarkMode, ref enabled, sizeof(int));
                if (result != 0) DwmSetWindowAttribute(Handle, 19, ref enabled, sizeof(int));
            }
            catch { }
        }

        protected override void OnDeactivate(EventArgs e)
        {
            if (freeLearningTimer != null) freeLearningTimer.Refresh();
            base.OnDeactivate(e);
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if (freeLearningTimer != null) freeLearningTimer.Refresh();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (practice.Current != null && practice.Current.active)
                practice.SavePosition(engine, inputLine == null ? string.Empty : inputLine.Text,
                    exampleRevealStage, freeDictationMeaningShown, true);
            if (freeLearningTimer != null) freeLearningTimer.Dispose();
            if (freePronouncer != null) freePronouncer.Dispose();
            backupTimer.Stop(); backupTimer.Dispose();
            base.OnFormClosed(e);
        }

        internal void PrepareColorPreview()
        {
            logArea.ClearContent();
            AppendToLog("普通说明文字", "normal");
            AppendToLog("答错提示", "error");
            AppendToLog("答对提示", "correct");
            AppendToLog("已斩提示", "mastered");
        }

        internal void FocusEndButtonPreview()
        {
            if (dailyNewEndButton == null || !dailyNewEndButton.Visible) return;
            dailyNewEndButton.Focus();
            dailyNewEndButton.Invalidate();
        }

        internal void PrepareForProgramUpdate()
        {
            if (practice.Current != null && practice.Current.active)
                practice.SavePosition(engine, inputLine.Text, exampleRevealStage,
                    freeDictationMeaningShown, true);
            if (freeLearningTimer != null) freeLearningTimer.StopAndCommit();
        }

        internal void ForcePausePreview()
        {
            if (practice.Current != null && practice.Current.active)
                BeginFreePauseOverlay(false);
        }

        internal string PausePreviewState()
        {
            return freePauseOverlay == null ? "null" : string.Format(
                "visible={0}; bounds={1}; parent={2}; index={3}; paused={4}",
                freePauseOverlay.Visible, freePauseOverlay.Bounds,
                freePauseOverlay.Parent == null ? "null" : freePauseOverlay.Parent.GetType().Name,
                freePauseOverlay.Parent == null ? -1 : freePauseOverlay.Parent.Controls.GetChildIndex(freePauseOverlay),
                freePaused);
        }

        internal Control PausePreviewControl { get { return freePauseOverlay; } }

        internal void SaveSettingsPreview(string outputFile)
        {
            using (StudySettingsForm form = new StudySettingsForm(study, notebooks, loader,
                backups, appearance, settingsPanel))
            {
                try
                {
                    form.SelectTab("自由练习");
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new Point(-20000, -20000);
                    form.Show();
                    Application.DoEvents();
                    using (Bitmap image = new Bitmap(form.Width, form.Height))
                    {
                        form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
                        image.Save(outputFile, ImageFormat.Png);
                    }
                    form.Close();
                }
                finally { form.DetachFreeSettings(); }
            }
        }

        internal void PrepareSettingsBottomPreview()
        {
            settingsPanel.AutoScrollPosition = new Point(0, settingsPanel.DisplayRectangle.Height);
        }

        private void BuildInterface()
        {
            TableLayoutPanel shell = new TableLayoutPanel { Dock = DockStyle.Fill,
                ColumnCount = 1, RowCount = 4, BackColor = Theme.Background,
                Padding = new Padding(18, 8, 18, 18) };
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 260));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(shell);
            ModernHeader header = new ModernHeader { Dock = DockStyle.Fill,
                Title = "大英默写器", Subtitle = "每日计划与自由练习，都从这里开始" };
            ModernButton settingsEntry = new ModernButton { Text = "设置", Width = 104, Height = 44,
                Subtle = true, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            settingsEntry.Click += delegate { OpenStudySettings(); };
            ModernButton help = new ModernButton { Text = "帮助", Width = 104, Height = 44,
                Subtle = true, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            help.Click += delegate { ShowHelp(); };
            header.Controls.Add(settingsEntry);
            header.Controls.Add(help);
            header.Resize += delegate
            {
                settingsEntry.Location = new Point(header.Width - 118, 37);
                help.Location = new Point(header.Width - 234, 37);
            };
            shell.Controls.Add(header, 0, 0);

            TableLayoutPanel cardRow = new TableLayoutPanel { Dock = DockStyle.Fill,
                ColumnCount = 3, RowCount = 1, BackColor = Theme.Background };
            for (int column = 0; column < 3; column++)
                cardRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
            ModernCard newCard = new ModernCard { Dock = DockStyle.Fill, Margin = new Padding(5),
                Eyebrow = "01 / DAILY", Title = "学习新词", Detail = "按词书抽取，先看后练" };
            ModernCard listCard = new ModernCard { Dock = DockStyle.Fill, Margin = new Padding(5),
                Eyebrow = "02 / REVIEW", Title = "回顾列表", Detail = "回看最近学过的单词" };
            ModernCard problemCard = new ModernCard { Dock = DockStyle.Fill, Margin = new Padding(5),
                Eyebrow = "03 / FOCUS", Title = "攻克错词", Detail = "错题与易错词集中练习" };
            cardRow.Controls.Add(newCard, 0, 0);
            cardRow.Controls.Add(listCard, 1, 0);
            cardRow.Controls.Add(problemCard, 2, 0);
            shell.Controls.Add(cardRow, 0, 1);
            quickActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true,
                WrapContents = false, FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(6, 5, 0, 0), BackColor = Theme.Background };
            shell.Controls.Add(quickActions, 0, 2);
            settingsPanel = new Panel();
            settingsPanel.Dock = DockStyle.Fill;
            settingsPanel.Padding = new Padding(12);
            settingsPanel.AutoScroll = true;
            settingsPanel.BackColor = Theme.Background;

            TableLayoutPanel settings = new TableLayoutPanel { Dock = DockStyle.Fill,
                ColumnCount = 3, RowCount = 1, BackColor = Theme.Background };
            for (int column = 0; column < 3; column++)
                settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
            FlowLayoutPanel selectionColumn = SettingsColumn();
            FlowLayoutPanel answerColumn = SettingsColumn();
            FlowLayoutPanel notebookColumn = SettingsColumn();
            settings.Controls.Add(selectionColumn, 0, 0);
            settings.Controls.Add(answerColumn, 1, 0);
            settings.Controls.Add(notebookColumn, 2, 0);
            settingsPanel.Controls.Add(settings);

            dailyNewButton = MakeButton("新学");
            dailyNewButton.Click += delegate { StartDailyNew(); };
            ((ModernButton)dailyNewButton).Primary = true;
            dailyNewEndButton = MakeButton("结束列表");
            ((ModernButton)dailyNewEndButton).Subtle = true;
            dailyNewEndButton.Click += delegate { EndDaily("new"); };
            PlaceCardButtons(newCard, dailyNewButton, dailyNewEndButton);
            dailyListButton = MakeButton("历史列表复习");
            dailyListButton.Click += delegate { StartDailyList(); };
            dailyListEndButton = MakeButton("结束列表");
            ((ModernButton)dailyListEndButton).Subtle = true;
            dailyListEndButton.Click += delegate { EndDaily("list_review"); };
            PlaceCardButtons(listCard, dailyListButton, dailyListEndButton);
            dailyProblemButton = MakeButton("错题与易错词复习");
            dailyProblemButton.Click += delegate { StartDailyProblems(); };
            dailyProblemEndButton = MakeButton("结束列表");
            ((ModernButton)dailyProblemEndButton).Subtle = true;
            dailyProblemEndButton.Click += delegate { EndDaily("problem_review"); };
            PlaceCardButtons(problemCard, dailyProblemButton, dailyProblemEndButton);
            dailyProgress = MakeLabel("");
            dailyProgress.Width = 365;
            dailyProgress.Height = 60;
            dailyProgress.TextAlign = ContentAlignment.MiddleLeft;
            dailyProgress.BackColor = Theme.Background;
            quickActions.Controls.Add(dailyProgress);
            selectionColumn.Controls.Add(MakeLabel("自由练习 · 词书与范围"));
            answerColumn.Controls.Add(MakeLabel("题型与作答"));
            notebookColumn.Controls.Add(MakeLabel("词本与快捷键"));

            selectionColumn.Controls.Add(MakeLabel("选择书本"));
            bookCombo = new DarkComboBox();
            bookCombo.Width = 340;
            bookCombo.SelectedIndexChanged += delegate { PopulateUnits(); };
            selectionColumn.Controls.Add(bookCombo);

            selectionColumn.Controls.Add(MakeLabel("选择单元"));
            unitList = new DarkCheckedListBox();
            unitList.Width = 340;
            unitList.Height = 170;
            unitList.CheckOnClick = true;
            unitList.IntegralHeight = false;
            selectionColumn.Controls.Add(unitList);

            contentAll = MakeRadio("全部（单词 + 短语）", true);
            contentWords = MakeRadio("仅单词", false);
            contentPhrases = MakeRadio("仅短语", false);
            selectionColumn.Controls.Add(MakeRadioGroup("选择内容", contentAll, contentWords, contentPhrases));

            orderSequential = MakeRadio("顺序", study.Settings.freeQuestionOrder == "sequential");
            orderUnitRandom = MakeRadio("单元内随机", study.Settings.freeQuestionOrder == "unit_random");
            orderRandom = MakeRadio("词书内随机", study.Settings.freeQuestionOrder == "book_random");
            answerColumn.Controls.Add(MakeRadioGroup("选择顺序", orderSequential,
                orderUnitRandom, orderRandom));

            questionWord = MakeCheckBox("普通拼写（中文 → 英文）", study.Settings.freeSpelling);
            questionExample = MakeCheckBox("例句填空", study.Settings.freeExample);
            questionDictation = MakeCheckBox("听写", study.Settings.freeDictation);
            answerColumn.Controls.Add(MakeCheckGroup("选择题型（可多选；先后顺序在设置中调整）",
                questionExample, questionDictation, questionWord));

            showFirstLetter = MakeCheckBox("普通拼写直接显示首字母", false);
            retryOnWrong = MakeCheckBox("答错后重试当前词", true);
            answerColumn.Controls.Add(MakeGroup("普通拼写提示", showFirstLetter));
            answerColumn.Controls.Add(MakeGroup("答题选项", retryOnWrong));

            freeStartButton = MakeButton("自由练习");
            freeStartButton.Width = 170;
            freeStartButton.Click += delegate { StartGame(); };
            quickActions.Controls.Add(freeStartButton);
            freePauseButton = MakeButton("暂停自由练习");
            freePauseButton.Width = 170;
            freePauseButton.Visible = false;
            freePauseButton.Click += delegate { ToggleFreePause(); };
            quickActions.Controls.Add(freePauseButton);
            freeTimerLabel = MakeLabel(string.Empty);
            freeTimerLabel.Width = 220; freeTimerLabel.Height = 50;
            freeTimerLabel.TextAlign = ContentAlignment.MiddleCenter;
            quickActions.Controls.Add(freeTimerLabel);
            Button statisticsButton = MakeButton("学习统计");
            statisticsButton.Width = 150;
            statisticsButton.Click += delegate
            {
                using (StatisticsForm form = new StatisticsForm(study, practice, appearance))
                    form.ShowDialog(this);
            };
            quickActions.Controls.Add(statisticsButton);

            reviewButton = MakeButton("复习错题 (0)");
            reviewButton.Width = 170;
            reviewButton.Click += delegate { StartReview(); };
            quickActions.Controls.Add(reviewButton);

            Button clearWrongButton = MakeButton("清空错题本");
            clearWrongButton.Click += delegate { ClearWrongWords(); };
            notebookColumn.Controls.Add(clearWrongButton);

            reviewFirstLetter = MakeCheckBox("复习时显示首字母提示", notebooks.ReviewFirstLetter);
            reviewFirstLetter.CheckedChanged += delegate { notebooks.ReviewFirstLetter = reviewFirstLetter.Checked; };
            reviewCorrectTarget = new NumericUpDown();
            reviewCorrectTarget.Minimum = 1;
            reviewCorrectTarget.Maximum = 99;
            reviewCorrectTarget.Value = notebooks.ReviewCorrectTarget;
            reviewCorrectTarget.Width = 70;
            reviewCorrectTarget.BackColor = Theme.Surface;
            reviewCorrectTarget.ForeColor = Theme.Text;
            reviewCorrectTarget.ValueChanged += delegate
            {
                notebooks.ReviewCorrectTarget = (int)reviewCorrectTarget.Value;
            };
            GroupBox reviewSettings = new ModernGroupBox();
            reviewSettings.Text = "自由复习设置";
            reviewSettings.Width = 340;
            reviewSettings.Height = 138;
            reviewSettings.BackColor = Theme.Background;
            reviewSettings.ForeColor = Theme.Text;
            reviewFirstLetter.Location = new Point(10, 36);
            reviewSettings.Controls.Add(reviewFirstLetter);
            Label countLabel = MakeLabel("连续答对次数后转入易错本");
            countLabel.Location = new Point(10, 74);
            countLabel.Width = 246;
            reviewSettings.Controls.Add(countLabel);
            reviewCorrectTarget.Location = new Point(256, 77);
            reviewSettings.Controls.Add(reviewCorrectTarget);
            answerColumn.Controls.Add(reviewSettings);

            GroupBox masterySettings = new ModernGroupBox();
            masterySettings.Text = "斩单词快捷键";
            masterySettings.Width = 340;
            masterySettings.Height = 124;
            masterySettings.BackColor = Theme.Background;
            masterySettings.ForeColor = Theme.Text;
            shortcutCapture = new TextBox();
            shortcutCapture.ReadOnly = true;
            shortcutCapture.TabStop = true;
            shortcutCapture.Text = ShortcutText(notebooks.MasteryShortcut);
            shortcutCapture.Location = new Point(10, 37);
            shortcutCapture.Width = 315;
            shortcutCapture.KeyDown += ShortcutCaptureOnKeyDown;
            masterySettings.Controls.Add(shortcutCapture);
            Label shortcutHint = MakeLabel("点击上方并按下新的组合键");
            shortcutHint.Location = new Point(10, 76);
            masterySettings.Controls.Add(shortcutHint);
            notebookColumn.Controls.Add(masterySettings);

            Button masterButton = MakeButton("斩当前单词 → 已掌握");
            masterButton.Text = "斩当前词";
            masterButton.Width = 170;
            masterButton.Click += delegate { MasterCurrentWord(); };
            quickActions.Controls.Add(masterButton);

            Button manageButton = MakeButton("管理全部单词本");
            manageButton.Click += delegate { OpenNotebookManager(); };
            notebookColumn.Controls.Add(manageButton);

            GroupBox recentGroup = new ModernGroupBox();
            recentGroup.Text = "最近 5 次操作 · 待定队列";
            recentGroup.Width = 340;
            recentGroup.Height = 280;
            recentGroup.BackColor = Theme.Background;
            recentGroup.ForeColor = Theme.Text;
            recentQueue = new FlowLayoutPanel();
            recentQueue.Location = new Point(8, 36);
            recentQueue.Width = 320;
            recentQueue.Height = 225;
            recentQueue.FlowDirection = FlowDirection.TopDown;
            recentQueue.WrapContents = false;
            recentQueue.AutoScroll = true;
            recentQueue.BackColor = Theme.Background;
            recentGroup.Controls.Add(recentQueue);
            notebookColumn.Controls.Add(recentGroup);

            ModernCard contentCard = new ModernCard { Dock = DockStyle.Fill,
                Margin = new Padding(5), Padding = new Padding(14) };
            shell.Controls.Add(contentCard, 0, 3);
            TableLayoutPanel center = new TableLayoutPanel();
            center.Dock = DockStyle.Fill;
            center.ColumnCount = 1;
            center.RowCount = 3;
            center.Padding = Padding.Empty;
            center.BackColor = ModernUI.Card;
            center.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            center.RowStyles.Add(new RowStyle(SizeType.Absolute, 57f));
            center.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            contentCard.Controls.Add(center);
            mainCenter = center;

            logArea = new StyledCanvas();
            logArea.Dock = DockStyle.Fill;
            logArea.BackColor = Theme.Background;
            logArea.ForeColor = Theme.Text;
            center.Controls.Add(logArea, 0, 0);

            inputLine = new TextBox();
            inputLine.Dock = DockStyle.Fill;
            inputLine.AutoSize = false;
            inputLine.Enabled = false;
            inputLine.BackColor = ModernUI.CardRaised;
            inputLine.ForeColor = Theme.Text;
            inputLine.Margin = new Padding(0, 10, 0, 0);
            inputLine.KeyDown += InputLineOnKeyDown;
            center.Controls.Add(inputLine, 0, 1);
            freeProgress = new ModernProgressBar { Dock = DockStyle.Fill,
                Margin = new Padding(0, 6, 0, 0), Visible = false };
            center.Controls.Add(freeProgress, 0, 2);

            freePauseOverlay = new PauseOverlay { Visible = false };
            freePauseOverlay.ResumeRequested += delegate { ResumeFreePractice(); };
            Controls.Add(freePauseOverlay);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (shortcutCapture != null && shortcutCapture.Focused)
                return base.ProcessCmdKey(ref msg, keyData);
            if (practice.Current != null && practice.Current.active
                && keyData == (Keys)study.Settings.pauseKey)
            {
                ToggleFreePause();
                return true;
            }
            if (freePaused) return true;
            if (IsFreeExampleActive() && keyData == Keys.Enter
                && ExampleRevealFlow.HasTypedAnswer(inputLine.Text))
            {
                exampleRevealStage = ExampleRevealFlow.LastRevealStage(
                    study.Settings.exampleFirstLetterHints);
                SubmitAnswer();
                return true;
            }
            if (IsFreeExampleActive() && keyData == (Keys)study.Settings.exampleHintKey)
            {
                AdvanceFreeExampleOrSubmit();
                return true;
            }
            if (IsFreeDictationActive() && keyData == (Keys)study.Settings.exampleHintKey
                && !ExampleRevealFlow.HasTypedAnswer(inputLine.Text))
            {
                RevealFreeDictationMeaning();
                return true;
            }
            if (IsFreeDictationActive() && keyData == (Keys)freePronunciationSettings.replayKey)
            {
                string error;
                if (!freePronouncer.Speak(GameEngine.CleanEnglish(currentWord), out error)
                    && !string.IsNullOrWhiteSpace(error)) AppendToLog(error, "error");
                return true;
            }
            if (keyData == notebooks.MasteryShortcut)
            {
                MasterCurrentWord();
                return true;
            }
            if (keyData == (Keys)study.Settings.undoKey)
            {
                UndoStudy();
                return true;
            }
            if (keyData == (Keys)study.Settings.manualBackupKey)
            {
                ManualBackup();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void UpdateDailyButtons()
        {
            if (dailyNewButton == null) return;
            StudyList newActive = study.ActiveFor("new");
            StudyList listActive = study.ActiveFor("list_review");
            StudyList problemActive = study.ActiveFor("problem_review");
            dailyNewButton.Text = newActive == null ? "开始新学 · " + study.Settings.newCount + " 词"
                : "继续新学";
            dailyListButton.Text = listActive == null ? "开始复习 · " + study.Settings.listCount + " 列表"
                : "继续列表复习";
            dailyProblemButton.Text = problemActive == null ? "开始复习 · " + study.Settings.problemCount + " 词"
                : "继续错词复习";
            dailyNewEndButton.Visible = newActive != null;
            dailyListEndButton.Visible = listActive != null;
            dailyProblemEndButton.Visible = problemActive != null;
            dailyListButton.Enabled = true;
            dailyProblemButton.Enabled = true;
            string today = StudyStore.StudyDayKey(DateTime.Now);
            List<StudyList> todayLists = study.Lists.Where(x => x.studyDate == today).ToList();
            Func<string, int> answered = kind => todayLists.Where(x => x.kind == kind)
                .Sum(x => x.items.Count(y => y.firstAnsweredAt != DateTime.MinValue
                    && StudyStore.StudyDayKey(y.firstAnsweredAt) == today));
            dailyProgress.Text = "今日已作答  新学 " + answered("new") + "  ·  列表 "
                + answered("list_review") + "\n错题复习 " + answered("problem_review")
                + "  ·  新学列表 " + todayLists.Count(x => x.kind == "new");
        }

        private void OpenStudySettings()
        {
            using (StudySettingsForm form = new StudySettingsForm(study, notebooks, loader,
                backups, appearance, settingsPanel))
            {
                try { form.ShowDialog(this); }
                finally { form.DetachFreeSettings(); }
            }
            ApplyAppearance();
            PopulateBooks();
            ApplyFreeSettingsControls();
            if (freePronouncer != null) freePronouncer.Dispose();
            freePronunciationSettings = new PronunciationStore(projectRoot).Settings;
            freePronouncer = new WordPronouncer(freePronunciationSettings);
            UpdateDailyButtons();
            nextAutoBackup = DateTime.Now.AddMinutes(study.Settings.autoBackupMinutes);
        }

        private void OpenStudySession(string kind)
        {
            if (!EnsureDictationAvailable(kind, false)) return;
            if (study.Resume(kind) == null) { UpdateDailyButtons(); return; }
            using (StudySessionForm form = new StudySessionForm(study, notebooks, ManualBackup, appearance))
                form.ShowDialog(this);
            UpdateDailyButtons();
            UpdateReviewButtonCount();
        }

        private void ApplyFreeSettingsControls()
        {
            if (questionWord == null) return;
            questionWord.Checked = study.Settings.freeSpelling;
            questionExample.Checked = study.Settings.freeExample;
            questionDictation.Checked = study.Settings.freeDictation;
            orderSequential.Checked = study.Settings.freeQuestionOrder == "sequential";
            orderUnitRandom.Checked = study.Settings.freeQuestionOrder == "unit_random";
            orderRandom.Checked = study.Settings.freeQuestionOrder == "book_random";
        }

        private bool EnsureDictationAvailable(string kind, bool free)
        {
            bool enabled = free ? questionDictation != null && questionDictation.Checked
                : study.DictationEnabled(kind);
            if (!enabled) return true;
            PronunciationSettings settings = new PronunciationStore(projectRoot).Settings;
            if (settings.enabled && WordPronouncer.GetEnglishVoices().Count > 0) return true;
            bool hasAlternative = free
                ? questionExample.Checked || questionWord.Checked
                : kind == "new" ? study.Settings.newExample || study.Settings.newSpelling
                : kind == "list_review" ? study.Settings.listExample || study.Settings.listSpelling
                : study.Settings.problemExample || study.Settings.problemSpelling;
            string message = "当前没有可用的英语语音，听写无法正常播放。\n\n"
                + "选择“是”打开 Windows 语音设置；选择“否”仅在本次运行中关闭该模块的听写；选择“取消”返回。";
            DialogResult choice = MessageBox.Show(this, message, "听写语音不可用",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (choice == DialogResult.Yes)
            {
                try { Process.Start(new ProcessStartInfo("ms-settings:speech") { UseShellExecute = true }); }
                catch { ShowDarkDialog("语音设置", "无法打开系统设置，请手动安装 Windows 英语语音。", false); }
                return false;
            }
            if (choice != DialogResult.No) return false;
            if (!hasAlternative)
            {
                ShowDarkDialog("无法临时关闭听写", "该模块只启用了听写。请先在设置中启用另一种题型。", false);
                return false;
            }
            if (free) freeDictationTemporarilyDisabled = true;
            else study.TemporarilyDisableDictation(kind);
            return true;
        }

        private void StartDailyNew()
        {
            try
            {
                study.SettleCrossDay(DateTime.Now);
                if (!EnsureDictationAvailable("new", false)) return;
                if (study.ActiveFor("new") != null) { OpenStudySession("new"); return; }
                using (QuotaForm form = new QuotaForm(study, loader))
                {
                    if (form.ShowDialog(this) != DialogResult.OK) return;
                    study.ExtractNew(form.Quotas, DateTime.Now);
                }
                OpenStudySession("new");
            }
            catch (Exception error) { ShowDarkDialog("新学无法开始", error.Message, false); }
        }

        private void StartDailyList()
        {
            if (!EnsureDictationAvailable("list_review", false)) return;
            if (study.ActiveFor("list_review") != null) { OpenStudySession("list_review"); return; }
            try { study.StartListReview(DateTime.Now); OpenStudySession("list_review"); }
            catch (Exception error) { ShowDarkDialog("列表复习无法开始", error.Message, false); }
        }

        private void StartDailyProblems()
        {
            if (!EnsureDictationAvailable("problem_review", false)) return;
            if (study.ActiveFor("problem_review") != null) { OpenStudySession("problem_review"); return; }
            try { study.StartProblemReview(DateTime.Now); OpenStudySession("problem_review"); }
            catch (Exception error) { ShowDarkDialog("错题复习无法开始", error.Message, false); }
        }

        private void EndDaily(string kind)
        {
            StudyList active = study.ActiveFor(kind);
            if (active == null) return;
            string title = kind == "new" ? "新学" : kind == "list_review" ? "历史列表复习" : "错题复习";
            string message = "确定手动结束“" + title + "”当前列表吗？\n\n"
                + "已经完成本部分全部要求的单词会保留；其余单词会退出当前列表。"
                + (kind == "new" ? "未完成词会回到新词池顶端，供下次优先抽取。" : "未完成词以后仍可再次复习。")
                + (study.Settings.backupBeforeManualEnd
                    ? "\n\n执行前会自动创建一份手动备份；可在设置中关闭。"
                    : "\n\n当前已关闭结束前自动备份；可在设置中重新开启。" );
            if (MessageBox.Show(this, message, "结束当前列表", MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes) return;
            if (study.Settings.backupBeforeManualEnd && !ManualBackup()) return;
            try
            {
                StudyEndResult result = study.EndActive(kind, DateTime.Now);
                ShowDarkDialog("列表已结束", "保留 " + result.kept + " 个，释放 " + result.released + " 个。", false);
                UpdateDailyButtons();
            }
            catch (Exception error) { ShowDarkDialog("无法结束列表", error.Message, false); }
        }

        private bool ManualBackup()
        {
            try
            {
                BackupInfo info = backups.Create(true, study.Settings.autoBackupKeep);
                AppendToLog("手动备份完成：" + info.createdAt.ToString("yyyy-MM-dd HH:mm:ss"), "correct");
                return true;
            }
            catch (Exception error)
            {
                ShowDarkDialog("手动备份失败", error.Message, false);
                return false;
            }
        }

        private void UndoStudy()
        {
            WordEntry word = study.Undo();
            if (word != null && study.Active == null && engine.CurrentDeck != null
                && engine.CurrentDeck.Count > 0)
            {
                int next = Math.Max(0, Math.Min(engine.CurrentIndex + 1, engine.CurrentDeck.Count));
                if (engine.GetNextQuestion() == null) next = engine.CurrentDeck.Count;
                engine.CurrentDeck.Insert(next, word);
            }
            AppendToLog(word == null ? "没有可撤销的每日学习操作。" :
                "已撤销：" + GameEngine.CleanEnglish(word), "normal");
            UpdateDailyButtons();
        }

        private static string ShortcutText(Keys shortcut)
        {
            return new KeysConverter().ConvertToString(shortcut);
        }

        private void ShortcutCaptureOnKeyDown(object sender, KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
            Keys key = e.KeyCode;
            if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu) return;
            if (key == Keys.None || key == Keys.Escape) return;
            Keys shortcut = e.KeyData;
            if ((shortcut & Keys.Modifiers) == Keys.None)
            {
                ShowDarkDialog("提示", "请同时按住 Ctrl、Alt 或 Shift 中至少一个修饰键。", false);
                return;
            }
            if (shortcut == (Keys)study.Settings.undoKey
                || shortcut == (Keys)study.Settings.manualBackupKey
                || shortcut == (Keys)study.Settings.previewKey
                || shortcut == (Keys)study.Settings.exampleHintKey
                || shortcut == (Keys)study.Settings.previousPageKey
                || shortcut == (Keys)study.Settings.nextPageKey)
            {
                ShowDarkDialog("快捷键冲突", "该按键已分配给每日学习中的其他操作。", false);
                return;
            }
            notebooks.MasteryShortcut = shortcut;
            shortcutCapture.Text = ShortcutText(shortcut);
        }

        private void RefreshRecentQueue()
        {
            if (recentQueue == null || updatingRecentQueue) return;
            updatingRecentQueue = true;
            try
            {
                foreach (Control old in recentQueue.Controls.Cast<Control>().ToArray()) old.Dispose();
                if (notebooks.RecentWords.Count == 0)
                {
                    recentQueue.Controls.Add(MakeLabel("暂无最近操作"));
                    return;
                }

                foreach (WordEntry word in notebooks.RecentWords.ToList())
                {
                    Panel row = new Panel();
                    row.BackColor = Theme.Background;

                    Label name = new Label();
                    name.Text = GameEngine.CleanEnglish(word);
                    name.Width = Math.Max(180, TextRenderer.MeasureText(name.Text, Theme.UiFont).Width + 8);
                    name.Height = 30;
                    name.Location = new Point(0, 2);
                    name.ForeColor = Theme.Text;
                    name.BackColor = Theme.Background;
                    name.TextAlign = ContentAlignment.MiddleLeft;
                    row.Controls.Add(name);

                    DarkComboBox book = new DarkComboBox();
                    book.Width = 125;
                    book.Location = new Point(name.Right + 4, 2);
                    row.Height = Math.Max(46, book.Height + 8);
                    row.Width = book.Right + 4;
                    book.Items.AddRange(Notebooks.All.Select(Notebooks.DisplayName).Cast<object>().ToArray());
                    book.SelectedIndex = Array.IndexOf(Notebooks.All, notebooks.GetNotebook(word));
                    book.SelectedIndexChanged += delegate
                    {
                        if (!updatingRecentQueue && book.SelectedIndex >= 0)
                            notebooks.MoveToNotebook(word, Notebooks.All[book.SelectedIndex]);
                    };
                    row.Controls.Add(book);
                    recentQueue.Controls.Add(row);
                }
            }
            finally
            {
                updatingRecentQueue = false;
            }
        }

        private void OpenNotebookManager()
        {
            Form owner = settingsPanel.FindForm() ?? this;
            using (NotebookManagerForm manager = new NotebookManagerForm(loader, notebooks))
            {
                manager.ShowDialog(owner);
            }
            RefreshRecentQueue();
            if (currentWord != null && !currentWord.Equals(engine.GetNextQuestion()))
                AskNextQuestion();
        }

        private void MasterCurrentWord()
        {
            if (engine.GetNextQuestion() != null) study.CaptureExternalUndo(engine.GetNextQuestion());
            WordEntry mastered = engine.MasterCurrent();
            if (mastered == null)
            {
                AppendToLog("当前没有可斩的单词。", "normal");
                return;
            }
            AppendToLog(appearance.Prompt("mastered", mastered, null), "mastered");
            AskNextQuestion();
        }

        private static FlowLayoutPanel SettingsColumn()
        {
            return new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true,
                FlowDirection = FlowDirection.TopDown, WrapContents = false,
                Padding = new Padding(12), BackColor = Theme.Background };
        }

        private static Label MakeLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.Width = 340;
            label.Height = 34;
            label.TextAlign = ContentAlignment.BottomLeft;
            label.ForeColor = Theme.Text;
            label.BackColor = Theme.Background;
            return label;
        }

        private static RadioButton MakeRadio(string text, bool isChecked)
        {
            RadioButton radio = new RadioButton();
            radio.Text = text;
            radio.Checked = isChecked;
            radio.AutoSize = true;
            radio.ForeColor = Theme.Text;
            radio.BackColor = Theme.Background;
            return radio;
        }

        private static CheckBox MakeCheckBox(string text, bool isChecked)
        {
            CheckBox checkBox = new CheckBox();
            checkBox.Text = text;
            checkBox.Checked = isChecked;
            checkBox.AutoSize = true;
            checkBox.ForeColor = Theme.Text;
            checkBox.BackColor = Theme.Background;
            return checkBox;
        }

        private static GroupBox MakeRadioGroup(string title, params RadioButton[] radios)
        {
            GroupBox group = new ModernGroupBox();
            group.Text = title;
            group.Width = 340;
            group.Height = 62 + radios.Length * 34;
            group.ForeColor = Theme.Text;
            group.BackColor = Theme.Background;
            FlowLayoutPanel flow = new FlowLayoutPanel();
            flow.Dock = DockStyle.Fill;
            flow.FlowDirection = FlowDirection.TopDown;
            flow.WrapContents = false;
            flow.Padding = new Padding(0, 8, 0, 0);
            flow.BackColor = Theme.Background;
            foreach (RadioButton radio in radios) flow.Controls.Add(radio);
            group.Controls.Add(flow);
            return group;
        }

        private static GroupBox MakeCheckGroup(string title, params CheckBox[] checks)
        {
            GroupBox group = new ModernGroupBox { Text = title, Width = 340,
                Height = 62 + checks.Length * 34, ForeColor = Theme.Text,
                BackColor = Theme.Background };
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown, WrapContents = false,
                Padding = new Padding(0, 8, 0, 0), BackColor = Theme.Background };
            foreach (CheckBox check in checks) flow.Controls.Add(check);
            group.Controls.Add(flow);
            return group;
        }

        private static GroupBox MakeGroup(string title, Control control)
        {
            GroupBox group = new ModernGroupBox();
            group.Text = title;
            group.Width = 340;
            group.Height = 86;
            group.ForeColor = Theme.Text;
            group.BackColor = Theme.Background;
            control.Location = new Point(10, 36);
            group.Controls.Add(control);
            return group;
        }

        private static Button MakeButton(string text)
        {
            ModernButton button = new ModernButton();
            button.Text = text;
            button.Width = 340;
            button.Height = 42;
            button.Margin = new Padding(3, 6, 3, 0);
            return button;
        }

        private static void PlaceCardButtons(ModernCard card, Button primary, Button end)
        {
            primary.Height = 42;
            end.Height = 42;
            primary.Margin = Padding.Empty;
            end.Margin = Padding.Empty;
            card.Controls.Add(primary);
            card.Controls.Add(end);
            EventHandler position = delegate
            {
                int available = Math.Max(180, card.ClientSize.Width - 42);
                int y = Math.Max(142, card.ClientSize.Height - 65);
                if (!end.Visible)
                {
                    primary.Width = available;
                    primary.Location = new Point(21, y);
                    return;
                }
                int endWidth = Math.Min(118, Math.Max(94, available / 3));
                primary.Width = Math.Max(100, available - endWidth - 8);
                end.Width = endWidth;
                primary.Location = new Point(21, y);
                end.Location = new Point(21 + primary.Width + 8, y);
            };
            card.Resize += position;
            end.VisibleChanged += position;
            position(card, EventArgs.Empty);
        }

        private void PopulateBooks()
        {
            List<string> books = loader.GetAvailableBooks();
            bookCombo.Items.Clear();
            bookCombo.Items.AddRange(books.Cast<object>().ToArray());
            if (bookCombo.Items.Count > 0) bookCombo.SelectedIndex = 0;
            else ShowDarkDialog("警告", "在 data 目录中未找到任何包含 CSV 的词书。", false);
        }

        private void PopulateUnits()
        {
            unitList.Items.Clear();
            foreach (string unit in loader.GetUnitsForBook(bookCombo.Text))
            {
                unitList.Items.Add(unit, true);
            }
        }

        private Dictionary<string, string> LoadKeybindings()
        {
            Dictionary<string, string> defaults = new Dictionary<string, string>
            {
                { "a", "action_skip_no_penalty" },
                { "/skip", "action_skip_no_penalty" },
                { "/clc", "action_clear_cache" },
                { "/clear", "action_clear_screen" },
                { "/review", "action_start_review" },
                { "/master", "action_master_word" }
            };

            string file = Path.Combine(projectRoot, "keybindings.json");
            try
            {
                if (!File.Exists(file))
                {
                    File.WriteAllText(file, new JavaScriptSerializer().Serialize(defaults), new UTF8Encoding(false));
                    return defaults;
                }

                Dictionary<string, string> loaded = new JavaScriptSerializer()
                    .Deserialize<Dictionary<string, string>>(File.ReadAllText(file, Encoding.UTF8));
                if (loaded != null && !loaded.ContainsKey("/master"))
                    loaded["/master"] = "action_master_word";
                return loaded == null ? defaults : loaded;
            }
            catch
            {
                return defaults;
            }
        }

        private void StartGame()
        {
            if (practice.Current != null && practice.Current.active)
            {
                RestoreFreePracticeIfNeeded();
                if (freePaused) ResumeFreePractice();
                return;
            }
            List<string> selectedUnits = unitList.CheckedItems.Cast<object>()
                .Select(item => item.ToString()).ToList();
            if (selectedUnits.Count == 0)
            {
                ShowDarkDialog("提示", "请至少选择一个单元！", false);
                return;
            }
            if (!questionExample.Checked && !questionDictation.Checked && !questionWord.Checked)
            {
                ShowDarkDialog("提示", "请至少启用一种自由练习题型。", false);
                return;
            }
            freeDictationTemporarilyDisabled = false;
            if (!EnsureDictationAvailable("free", true)) return;

            string filterMode = contentWords.Checked ? "words_only" :
                contentPhrases.Checked ? "phrases_only" : "all";
            string orderMode = orderUnitRandom.Checked ? "unit_random"
                : orderRandom.Checked ? "book_random" : "sequential";
            study.Settings.freeExample = questionExample.Checked;
            study.Settings.freeDictation = questionDictation.Checked;
            study.Settings.freeSpelling = questionWord.Checked;
            study.Settings.freeQuestionOrder = orderMode;
            study.SaveSettings();
            engine.StartGame(bookCombo.Text, selectedUnits, filterMode, orderMode,
                questionExample.Checked, questionDictation.Checked && !freeDictationTemporarilyDisabled,
                questionWord.Checked,
                study.Settings.freeTaskOrder, showFirstLetter.Checked, retryOnWrong.Checked);

            if (engine.CurrentDeck.Count == 0)
            {
                ShowDarkDialog("无法开始", "所选范围内没有符合条件的题目。", false);
                return;
            }
            practice.Begin(engine);
            freePaused = false;
            freePauseButton.Visible = true;
            freeStartButton.Visible = false;
            StartFreeTimer(0);

            logArea.ClearContent();
            AppendToLog("--- 游戏开始 ---", "normal");
            AppendToLog(string.Format("书本: {0}, 单元: {1}", bookCombo.Text,
                string.Join(", ", selectedUnits)), "normal");
            inputLine.Enabled = true;
            AskNextQuestion();
        }

        private void StartReview()
        {
            if (practice.Current != null && practice.Current.active)
            {
                RestoreFreePracticeIfNeeded();
                return;
            }
            if (!engine.StartReviewMode())
            {
                ShowDarkDialog("提示", "错题本是空的，太棒了！", false);
                return;
            }
            practice.Begin(engine);
            freePaused = false;
            freePauseButton.Visible = true;
            freeStartButton.Visible = false;
            StartFreeTimer(0);

            logArea.ClearContent();
            AppendToLog(string.Format("--- 错题本复习开始（{0}首字母，连续答对 {1} 次后进入易错本）---",
                notebooks.ReviewFirstLetter ? "显示" : "不显示", notebooks.ReviewCorrectTarget), "normal");
            inputLine.Enabled = true;
            AskNextQuestion();
        }

        private void AskNextQuestion()
        {
            currentWord = engine.GetNextQuestion();
            exampleRevealStage = 0;
            freeDictationMeaningShown = false;
            if (currentWord == null)
            {
                if (freeLearningTimer != null) freeLearningTimer.StopAndCommit();
                if (practice.Current != null && practice.Current.active) practice.FinishCurrent();
                AppendToLog("\n--- 恭喜！本轮已全部完成！---", "correct");
                inputLine.Clear();
                inputLine.Enabled = false;
                freePauseButton.Visible = false;
                freeStartButton.Visible = true;
                freeStartButton.Text = "自由练习";
                freeProgress.Visible = true;
                freeProgress.Total = Math.Max(1, engine.CurrentDeck.Count);
                freeProgress.Completed = engine.CurrentDeck.Count;
                UpdateReviewButtonCount();
                return;
            }

            if (engine.QuestionMode == "example")
            {
                int scanned = 0;
                while (currentWord != null && engine.GetExampleQuestion() == null)
                {
                    engine.CurrentIndex++;
                    scanned++;
                    if (scanned >= engine.CurrentDeck.Count) break;
                    currentWord = engine.GetNextQuestion();
                }

                if (currentWord == null || scanned >= engine.CurrentDeck.Count)
                {
                    AppendToLog("\n--- 错误：当前牌组中没有有效的例句数据！---", "error");
                    AppendToLog("请检查 CSV 文件中的 examples 列，确保包含 [[ 标记。", "normal");
                    inputLine.Enabled = false;
                    return;
                }
            }

            int[] progress = engine.GetProgress();
            freeProgress.Visible = true;
            freeProgress.Total = Math.Max(1, progress[1]);
            freeProgress.Completed = Math.Min(engine.CurrentIndex, progress[1]);
            if (engine.QuestionMode == "example")
            {
                ExampleQuestion example = engine.GetExampleQuestion();
                string blanked = example == null ? string.Empty : example.Render(false);
                AppendToLog(string.Format("\n({0}/{1}) 例句填空:", progress[0], progress[1]), "normal");
                AppendToLog("  " + blanked, "example");
                AppendToLog("  按 " + ShortcutText((Keys)study.Settings.exampleHintKey)
                    + (engine.ShowFirstLetter ? " 显示首字母提示。" : " 显示中文释义。"), "normal");
            }
            else if (engine.QuestionMode == "dictation")
            {
                AppendToLog(string.Format("\n({0}/{1}) 听写：请听发音后输入英文。",
                    progress[0], progress[1]), "normal");
                if (study.Settings.dictationMeaningHint)
                    AppendToLog("  输入框为空时按 " + ShortcutText((Keys)study.Settings.exampleHintKey)
                        + " 显示中文释义。", "normal");
                string speechError;
                if (!freePronouncer.Speak(GameEngine.CleanEnglish(currentWord), out speechError)
                    && !string.IsNullOrWhiteSpace(speechError))
                    AppendToLog("  听写朗读失败：" + speechError, "error");
                lastFreeDictationSpoken = currentWord;
            }
            else
            {
                string hint = ExtractChineseHint(currentWord);
                AppendToLog(string.Format("\n({0}/{1}) 请输入:", progress[0], progress[1]), "normal");
                AppendToLog("  " + (string.IsNullOrEmpty(hint)
                    ? PartOfSpeech.DisplayChinese(currentWord) : hint), "meaning");
                if (engine.ShowFirstLetter)
                {
                    string answer = GameEngine.CleanEnglish(currentWord);
                    if (answer.Length > 0) AppendToLog("  提示: " + answer.Substring(0, 1), "normal");
                }
            }

            inputLine.Clear();
            inputLine.Focus();
            if (practice.Current != null && practice.Current.active)
                practice.SavePosition(engine, inputLine.Text, exampleRevealStage,
                    freeDictationMeaningShown, freePaused);
        }

        private static string ExtractChineseHint(WordEntry word)
        {
            return GameEngine.ChineseHint(word);
        }

        private bool IsFreeExampleActive()
        {
            return currentWord != null && engine.QuestionMode == "example"
                && engine.GetExampleQuestion() != null;
        }

        private bool IsFreeDictationActive()
        {
            return currentWord != null && engine.QuestionMode == "dictation";
        }

        private void RevealFreeDictationMeaning()
        {
            if (!IsFreeDictationActive() || !study.Settings.dictationMeaningHint
                || ExampleRevealFlow.HasTypedAnswer(inputLine.Text) || freeDictationMeaningShown) return;
            freeDictationMeaningShown = true;
            AppendToLog("  中文释义：" + PartOfSpeech.DisplayChinese(currentWord), "meaning");
            if (practice.Current != null && practice.Current.active)
                practice.SavePosition(engine, inputLine.Text, exampleRevealStage, true, freePaused);
            inputLine.Focus();
        }

        private void AdvanceFreeExampleOrSubmit()
        {
            if (!IsFreeExampleActive()) return;
            bool firstLetter = study.Settings.exampleFirstLetterHints;
            if (ExampleRevealFlow.ReadyToSubmit(exampleRevealStage, firstLetter))
            {
                SubmitAnswer();
                return;
            }

            exampleRevealStage = ExampleRevealFlow.NextStage(exampleRevealStage, firstLetter);
            ExampleQuestion example = engine.GetExampleQuestion();
            if (ExampleRevealFlow.ShowsFirstLetter(exampleRevealStage, firstLetter)
                && !ExampleRevealFlow.ShowsMeaning(exampleRevealStage, firstLetter))
            {
                string initial = string.IsNullOrWhiteSpace(example.answer)
                    ? string.Empty : example.answer.Trim().Substring(0, 1);
                AppendToLog("  首字母提示：" + initial, "normal");
                AppendToLog("  再按 " + ShortcutText((Keys)study.Settings.exampleHintKey)
                    + " 显示中文释义。", "normal");
            }
            else
            {
                AppendToLog("  中文释义：" + GameEngine.ChineseHint(currentWord), "meaning");
                AppendToLog("  再按 " + ShortcutText((Keys)study.Settings.exampleHintKey)
                    + " 提交答案。", "normal");
            }
            inputLine.Focus();
        }

        private void InputLineOnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            if (IsFreeExampleActive())
            {
                if (ExampleRevealFlow.HasTypedAnswer(inputLine.Text))
                {
                    exampleRevealStage = ExampleRevealFlow.LastRevealStage(
                        study.Settings.exampleFirstLetterHints);
                    SubmitAnswer();
                }
                return;
            }
            if (IsFreeDictationActive() && !ExampleRevealFlow.HasTypedAnswer(inputLine.Text))
            {
                RevealFreeDictationMeaning();
                return;
            }
            SubmitAnswer();
        }

        private void SubmitAnswer()
        {
            if (IsFreeExampleActive() && !ExampleRevealFlow.ReadyToSubmit(exampleRevealStage,
                study.Settings.exampleFirstLetterHints)) return;
            string userInput = inputLine.Text.Trim();
            string action;
            if (keybindings.TryGetValue(userInput, out action))
            {
                HandleAction(action);
                inputLine.Clear();
                return;
            }

            if (currentWord == null)
            {
                inputLine.Clear();
                return;
            }

            study.CaptureExternalUndo(currentWord);
            int answeredIndex = engine.CurrentIndex;
            string answeredMode = engine.QuestionMode;
            WordEntry answeredWord = currentWord;
            List<string> accepted = answeredMode == "example" && engine.GetExampleQuestion() != null
                ? new List<string>(engine.GetExampleQuestion().answers)
                : study.AcceptedAnswers(currentWord);
            AnswerOutcome outcome = engine.CheckAnswer(userInput);
            if (practice.Current != null && practice.Current.active)
                practice.RecordAttempt(answeredWord, answeredMode, answeredIndex,
                    outcome.Correct, DateTime.Now);
            if (outcome.Correct)
            {
                AppendToLog(appearance.Prompt("correct", answeredWord, userInput,
                    outcome.CorrectAnswer), "correct");
                if (outcome.MovedToErrorProne)
                    AppendToLog("  已达标，移入易错本。", "normal");
                else if (engine.IsReviewMode)
                    AppendToLog(string.Format("  连续答对 {0}/{1} 次。",
                        outcome.CorrectCount, notebooks.ReviewCorrectTarget), "normal");
                if (answeredMode == "dictation")
                {
                    AppendToLog("  正确英文：" + outcome.CorrectAnswer, "word");
                    AppendToLog("  中文释义：" + PartOfSpeech.DisplayChinese(answeredWord), "meaning");
                }
                UpdateReviewButtonCount();
                AskNextQuestion();
            }
            else
            {
                AppendToLog(appearance.Prompt("error", answeredWord, userInput,
                    outcome.CorrectAnswer), "error");
                string difference = SpellingDifference.Report(userInput, accepted);
                if (!string.IsNullOrWhiteSpace(difference)) AppendToLog(difference, "error");
                if (answeredMode == "dictation")
                {
                    AppendToLog("  正确英文：" + outcome.CorrectAnswer, "word");
                    AppendToLog("  中文释义：" + PartOfSpeech.DisplayChinese(answeredWord), "meaning");
                }
                UpdateReviewButtonCount();
                if (engine.RetryOnWrong)
                {
                    engine.CurrentIndex--;
                    AppendToLog("  请重试！", "normal");
                    inputLine.Clear();
                    inputLine.Focus();
                    freeProgress.Completed = Math.Min(engine.CurrentIndex, engine.CurrentDeck.Count);
                    if (practice.Current != null && practice.Current.active)
                        practice.SavePosition(engine, string.Empty, 0, false, freePaused);
                }
                else
                {
                    AskNextQuestion();
                }
            }
        }

        private void HandleAction(string action)
        {
            if (action == "action_skip_no_penalty")
            {
                if (engine.GetNextQuestion() != null) study.CaptureExternalUndo(engine.GetNextQuestion());
                bool removed;
                WordEntry skipped = engine.SkipWithoutPenalty(out removed);
                if (skipped == null)
                {
                    AppendToLog("游戏尚未开始。", "normal");
                    return;
                }
                AppendToLog((removed ? "  ⏩ 已跳过并从错题本移除: " : "  ⏩ 已跳过: ")
                    + GameEngine.CleanEnglish(skipped), "normal");
                UpdateReviewButtonCount();
                AskNextQuestion();
            }
            else if (action == "action_clear_cache")
            {
                engine.ClearWrongWordsCache();
                UpdateReviewButtonCount();
                AppendToLog("--- 错题本已清空 ---", "normal");
            }
            else if (action == "action_clear_screen")
            {
                logArea.ClearContent();
                ReprintCurrentQuestion();
            }
            else if (action == "action_start_review")
            {
                StartReview();
            }
            else if (action == "action_master_word")
            {
                MasterCurrentWord();
            }
            else
            {
                AppendToLog("未知的动作: '" + action + "'。请检查 keybindings.json。", "error");
            }
        }

        private void ReprintCurrentQuestion()
        {
            if (currentWord == null)
            {
                AppendToLog("已清屏。请在右侧开始新游戏。", "normal");
                return;
            }

            int[] progress = engine.GetProgress();
            if (engine.QuestionMode == "example")
            {
                ExampleQuestion example = engine.GetExampleQuestion();
                bool firstLetter = study.Settings.exampleFirstLetterHints;
                string blanked = example == null ? string.Empty : example.Render(
                    ExampleRevealFlow.ShowsFirstLetter(exampleRevealStage, firstLetter));
                AppendToLog(string.Format("({0}/{1}) 例句填空:", progress[0], progress[1]), "normal");
                AppendToLog("  " + blanked, "normal");
                if (ExampleRevealFlow.ShowsMeaning(exampleRevealStage, firstLetter))
                    AppendToLog("  中文释义：" + GameEngine.ChineseHint(currentWord), "meaning");
            }
            else if (engine.QuestionMode == "dictation")
            {
                AppendToLog(string.Format("({0}/{1}) 听写：请听发音后输入英文。",
                    progress[0], progress[1]), "normal");
                if (freeDictationMeaningShown)
                    AppendToLog("  中文释义：" + PartOfSpeech.DisplayChinese(currentWord), "meaning");
                else if (study.Settings.dictationMeaningHint)
                    AppendToLog("  输入框为空时按 " + ShortcutText((Keys)study.Settings.exampleHintKey)
                        + " 显示中文释义。", "normal");
            }
            else
            {
                AppendToLog(string.Format("({0}/{1}) 请输入:", progress[0], progress[1]), "normal");
                AppendToLog("  " + ExtractChineseHint(currentWord), "normal");
            }
        }

        private void RestoreFreePracticeIfNeeded()
        {
            PracticeSessionState session = practice.Current;
            if (session == null || !session.active || session.engine == null) return;
            practice.Resume(engine);
            currentWord = engine.GetNextQuestion();
            if (currentWord == null)
            {
                practice.FinishCurrent();
                return;
            }
            exampleRevealStage = session.revealStage;
            freeDictationMeaningShown = session.dictationMeaningShown;
            freePaused = session.paused;
            logArea.ClearContent();
            AppendToLog("--- 继续自由练习 ---", "normal");
            ReprintCurrentQuestion();
            inputLine.Enabled = !freePaused;
            inputLine.Text = session.input ?? string.Empty;
            inputLine.SelectionStart = inputLine.TextLength;
            freeStartButton.Visible = false;
            freePauseButton.Visible = true;
            freePauseButton.Text = freePaused ? "继续自由练习" : "暂停自由练习";
            int[] current = engine.GetProgress();
            freeProgress.Visible = true;
            freeProgress.Total = Math.Max(1, current[1]);
            freeProgress.Completed = Math.Min(engine.CurrentIndex, current[1]);
            StartFreeTimer(session.activeMilliseconds);
            if (freePaused)
            {
                if (Visible) BeginFreePauseOverlay(false);
                else Shown += delegate { if (freePaused) BeginFreePauseOverlay(false); };
            }
            else inputLine.Focus();
        }

        private void StartFreeTimer(long initialMilliseconds)
        {
            if (freeLearningTimer != null) freeLearningTimer.Dispose();
            freeLearningTimer = new LearningTimer(this, freeTimerLabel, initialMilliseconds,
                study.Settings.timerEnabled, study.Settings.timerPrecision,
                delegate { return !freePaused && practice.Current != null
                    && practice.Current.active && currentWord != null; },
                delegate(long delta) { practice.AddActiveMilliseconds(delta); });
        }

        private void ToggleFreePause()
        {
            if (practice.Current == null || !practice.Current.active) return;
            if (freePaused) ResumeFreePractice();
            else BeginFreePauseOverlay(true);
        }

        private void BeginFreePauseOverlay(bool persist)
        {
            if (practice.Current == null || !practice.Current.active) return;
            if (persist) practice.SavePosition(engine, inputLine.Text, exampleRevealStage,
                freeDictationMeaningShown, true);
            if (freeLearningTimer != null) freeLearningTimer.StopAndCommit();
            freePronouncer.Stop();
            freePaused = true;
            inputLine.Enabled = false;
            freePauseOverlay.Visible = false;
            freePauseOverlay.Prepare(this, appearance, "free");
            freePauseOverlay.Visible = true;
            freePauseOverlay.BringToFront();
            freePauseButton.Text = "继续自由练习";
            if (freeLearningTimer != null) freeLearningTimer.Refresh();
        }

        private void ResumeFreePractice()
        {
            if (practice.Current == null || !practice.Current.active) return;
            bool includesDictation = practice.Current.engine != null
                && practice.Current.engine.modes != null
                && practice.Current.engine.modes.Skip(engine.CurrentIndex).Any(x => x == "dictation");
            if (includesDictation && !EnsureDictationAvailable("free", true)) return;
            if (freeDictationTemporarilyDisabled)
            {
                engine.RemoveRemainingMode("dictation");
                currentWord = engine.GetNextQuestion();
            }
            freePaused = false;
            freePauseOverlay.Visible = false;
            freePauseButton.Text = "暂停自由练习";
            inputLine.Enabled = true;
            practice.SavePosition(engine, inputLine.Text, exampleRevealStage,
                freeDictationMeaningShown, false);
            ReprintCurrentQuestion();
            if (freeLearningTimer != null) freeLearningTimer.Refresh();
            inputLine.Focus();
        }

        private void ClearWrongWords()
        {
            Form owner = settingsPanel.FindForm() ?? this;
            if (MessageBox.Show(owner, "确定清空整个错题本吗？此操作会影响学习记录，建议先手动备份。",
                "确认清空错题本", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            engine.ClearWrongWordsCache();
            UpdateReviewButtonCount();
            ShowDarkDialog("提示", "错题本已清空！", false);
        }

        private void UpdateReviewButtonCount()
        {
            if (reviewButton != null) reviewButton.Text = "复习错题 (" + engine.WrongWords.Count + ")";
        }

        private void AppendToLog(string message, string kind)
        {
            logArea.Add(message, kind);
            logArea.ScrollToBottom();
        }

        private void ApplyAppearance()
        {
            logArea.SetAppearance(appearance, "free");
            TextAppearance input = appearance.Settings.text["normal"];
            inputLine.Font = AppearanceStore.CreateFont(input);
            inputLine.ForeColor = Color.FromArgb(input.color);
            if (mainCenter != null)
                mainCenter.RowStyles[1].Height = Math.Max(64,
                    TextRenderer.MeasureText("Ag", inputLine.Font).Height + 20);
        }

        private void ShowHelp()
        {
            string help = "欢迎使用大英默写器！\n\n"
                + "功能\n"
                + "• 支持个性化词书与单词、短语筛选\n"
                + "• 支持例句填空、听写、普通拼写，以及顺序、单元内随机、词书内随机\n"
                + "• 自动记录错词并提供错题复习\n"
                + "• 新学、历史列表复习、错题复习相互独立，可分别继续或结束\n"
                + "• 支持暂停精确恢复、有效计时、学习统计和安全自动更新\n\n"
                + "默认指令\n"
                + "• /skip 或 a：跳过且不计入错题\n"
                + "• /review：开始错题复习\n"
                + "• /clc：清空错题本\n"
                + "• /clear：清空日志\n\n"
                + "未完成列表旁可选择“继续”或“结束列表”。手动结束时，已完成内容保留，未完成词会回到原来的候选范围。\n"
                + "重新进入未完成列表时，会立即采用当前的题型设置。\n"
                + "顶部“设置”可调整每日计划、题型顺序、快捷键、外观、统计、备份和更新；“词书管理”可以导入外部 CSV 或重命名词书。\n"
                + "学习窗口的“上一页”只用于回看；展示和答题阶段不能互相回退。\n"
                + "默认采用深色背景，字体、提示文案和练习背景可在外观设置中修改。\n\n"
                + "项目与联系\n"
                + "GitHub：https://github.com/kryptonite309/ZJU-English-dictation-tool_kry-advanced\n"
                + "QQ 邮箱：1242532684@qq.com";
            ShowDarkDialog("帮助", help, true);
        }

        private void ShowDarkDialog(string title, string message, bool large)
        {
            using (Form dialog = new Form())
            {
                ModernUI.ApplyAppIcon(dialog);
                dialog.Text = title;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.BackColor = Theme.Background;
                dialog.ForeColor = Theme.Text;
                dialog.Font = Theme.UiFont;
                dialog.ClientSize = large ? new Size(620, 470) : new Size(460, 190);

                RichTextBox messageBox = new RichTextBox();
                messageBox.Multiline = true;
                messageBox.ReadOnly = true;
                messageBox.DetectUrls = true;
                messageBox.BorderStyle = BorderStyle.None;
                messageBox.BackColor = Theme.Background;
                messageBox.ForeColor = Theme.Text;
                messageBox.Text = message;
                messageBox.ScrollBars = large ? RichTextBoxScrollBars.Vertical : RichTextBoxScrollBars.None;
                messageBox.LinkClicked += delegate(object sender, LinkClickedEventArgs e)
                {
                    try { Process.Start(e.LinkText); }
                    catch { }
                };
                messageBox.Location = new Point(22, 22);
                messageBox.Size = new Size(dialog.ClientSize.Width - 44, dialog.ClientSize.Height - 82);
                dialog.Controls.Add(messageBox);

                Button ok = MakeButton("确定");
                ok.Width = 96;
                ok.Height = 34;
                ok.Location = new Point(dialog.ClientSize.Width - 118, dialog.ClientSize.Height - 50);
                ok.DialogResult = DialogResult.OK;
                dialog.Controls.Add(ok);
                dialog.AcceptButton = ok;
                Theme.Apply(dialog);
                dialog.ShowDialog(settingsPanel.FindForm() ?? this);
            }
        }
    }

    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length >= 4 && args[0] == "--restore-from" && args[2] == "--wait-pid")
                {
                    int waitPid;
                    if (!int.TryParse(args[3], out waitPid)) return 2;
                    new BackupService(AppPaths.FindProjectRoot()).CompleteRestore(args[1], waitPid);
                    return 0;
                }
                if (args.Length >= 2 && args[0] == "--backup-manual")
                {
                    BackupInfo backup = new BackupService(AppPaths.FindProjectRoot()).Create(true, int.MaxValue);
                    File.WriteAllText(args[1], new JavaScriptSerializer().Serialize(backup), new UTF8Encoding(false));
                    return 0;
                }
                if (args.Length >= 4 && args[0] == "--install-after-exit")
                {
                    return InstallAfterExit(args[1], args[2], args[3]);
                }
                if (args.Length >= 6 && args[0] == "--apply-update")
                {
                    int waitPid;
                    if (!int.TryParse(args[4], out waitPid)) return 2;
                    return UpdateService.ApplyUpdate(args[1], args[2], args[3], waitPid, args[5]);
                }
                if (args.Length >= 2 && args[0] == "--self-test")
                {
                    return RunSelfTest(args[1]);
                }

                if (args.Length >= 3 && args[0] == "--test-notebooks")
                {
                    return NotebookTests.Run(args[1], args[2]);
                }

                if (args.Length >= 3 && args[0] == "--test-study")
                {
                    return StudyTests.Run(args[1], args[2]);
                }

                if (args.Length >= 3 && args[0] == "--test-v120")
                {
                    return V120Tests.Run(args[1], args[2]);
                }

                if (args.Length >= 2 && args[0] == "--audit-examples")
                {
                    return AuditExamples(args[1]);
                }

                if (args.Length >= 2 && args[0] == "--audit-parts-of-speech")
                {
                    return AuditPartsOfSpeech(args[1]);
                }

                if (args.Length >= 2 && args[0] == "--migrate")
                {
                    return MigrateNotebooks(args[1]);
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                if (args.Length >= 2 && args[0] == "--render-preview")
                {
                    return RenderPreview(args[1], args.Length >= 3 ? args[2] : null);
                }

                if (args.Length >= 2 && args[0] == "--render-main-settings")
                {
                    using (MainForm form = new MainForm()) form.SaveSettingsPreview(args[1]);
                    return 0;
                }

                if (args.Length >= 2 && args[0] == "--render-manager-preview")
                {
                    return RenderManagerPreview(args[1]);
                }

                if (args.Length >= 3 && args[0] == "--render-utility")
                {
                    return RenderUtility(args[1], args[2]);
                }

                if (args.Length >= 2 && args[0] == "--render-study-settings")
                {
                    return RenderStudySettings(args[1], args.Length >= 3 ? args[2] : null);
                }

                if (args.Length >= 2 && args[0] == "--render-appearance")
                {
                    return RenderAppearance(args[1], args.Length >= 3 ? args[2] : null);
                }

                if (args.Length >= 2 && args[0] == "--render-study-session")
                {
                    int revealSteps = 0;
                    if (args.Length >= 3) int.TryParse(args[2], out revealSteps);
                    return RenderStudySession(args[1], revealSteps,
                        args.Length >= 4 ? args[3] : null);
                }

                if (args.Length >= 2 && args[0] == "--render-statistics")
                {
                    return RenderStatistics(args[1]);
                }

                Application.Run(new MainForm());
                return 0;
            }
            catch (Exception error)
            {
                try
                {
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "main-error.log"),
                        error.ToString(), Encoding.UTF8);
                }
                catch { }
                return 1;
            }
        }

        private static int RunSelfTest(string outputFile)
        {
            string root = AppPaths.FindProjectRoot();
            DataLoader loader = new DataLoader(Path.Combine(root, "data"));
            List<string> books = loader.GetAvailableBooks();
            int unitCount = 0;
            int wordCount = 0;
            foreach (string book in books)
            {
                List<string> units = loader.GetUnitsForBook(book);
                unitCount += units.Count;
                wordCount += loader.LoadWordList(book, units).Count;
            }

            Dictionary<string, object> report = new Dictionary<string, object>
            {
                { "ok", books.Count > 0 && unitCount > 0 && wordCount > 0 },
                { "projectRoot", root },
                { "books", books.Count },
                { "units", unitCount },
                { "words", wordCount },
                { "background", ColorTranslator.ToHtml(Theme.Background) },
                { "text", ColorTranslator.ToHtml(Theme.Text) },
                { "error", ColorTranslator.ToHtml(Theme.Error) },
                { "correct", ColorTranslator.ToHtml(Theme.Correct) }
            };
            File.WriteAllText(outputFile, new JavaScriptSerializer().Serialize(report), new UTF8Encoding(false));
            return (bool)report["ok"] ? 0 : 2;
        }

        private static int AuditExamples(string outputFile)
        {
            string root = AppPaths.FindProjectRoot();
            DataLoader loader = new DataLoader(Path.Combine(root, "data"));
            int words = 0;
            int rowsWithMarkers = 0;
            int questions = 0;
            int changedForms = 0;
            List<string> invalid = new List<string>();
            List<string> changedSamples = new List<string>();
            foreach (string book in loader.GetAvailableBooks())
            {
                foreach (string unit in loader.GetUnitsForBook(book))
                {
                    foreach (WordEntry word in loader.LoadWordList(book, new[] { unit }))
                    {
                        words++;
                        if (string.IsNullOrWhiteSpace(word.examples) || !word.examples.Contains("[[")) continue;
                        rowsWithMarkers++;
                        List<ExampleQuestion> found = ExampleCloze.Questions(word);
                        questions += found.Count;
                        if (found.Count == 0)
                        {
                            if (invalid.Count < 50) invalid.Add(book + "/" + unit + ": " + word.english);
                            continue;
                        }
                        foreach (ExampleQuestion question in found)
                        {
                            if (string.IsNullOrWhiteSpace(question.answer)
                                || string.IsNullOrWhiteSpace(question.prompt)
                                || !question.prompt.Contains("________"))
                            {
                                if (invalid.Count < 50) invalid.Add(book + "/" + unit + ": " + word.english);
                                continue;
                            }
                            if (!string.Equals(StudyStore.NormalizeAnswer(question.answer),
                                StudyStore.NormalizeAnswer(GameEngine.CleanEnglish(word)),
                                StringComparison.Ordinal))
                            {
                                changedForms++;
                                if (changedSamples.Count < 50)
                                    changedSamples.Add(word.english + " => " + question.answer);
                            }
                        }
                    }
                }
            }
            Dictionary<string, object> report = new Dictionary<string, object>
            {
                { "ok", invalid.Count == 0 }, { "words", words },
                { "rowsWithMarkers", rowsWithMarkers }, { "validQuestions", questions },
                { "changedForms", changedForms }, { "invalid", invalid },
                { "changedSamples", changedSamples }
            };
            File.WriteAllText(outputFile, new JavaScriptSerializer().Serialize(report),
                new UTF8Encoding(false));
            return invalid.Count == 0 ? 0 : 3;
        }

        private static int AuditPartsOfSpeech(string outputFile)
        {
            string root = AppPaths.FindProjectRoot();
            DataLoader loader = new DataLoader(Path.Combine(root, "data"));
            List<WordEntry> rows = new List<WordEntry>();
            foreach (string book in loader.GetAvailableBooks())
            {
                foreach (string unit in loader.GetUnitsForBook(book))
                    rows.AddRange(loader.LoadWordList(book, new[] { unit }));
            }

            List<WordEntry> words = DataLoader.MergeWordEntries(rows);
            List<string> missing = words
                .Where(word => !PartOfSpeech.IsPhrase(word.english)
                    && string.IsNullOrWhiteSpace(word.partOfSpeech))
                .Select(word => word.english)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();
            List<string> phraseWithPart = words
                .Where(word => PartOfSpeech.IsPhrase(word.english)
                    && !string.IsNullOrWhiteSpace(word.partOfSpeech))
                .Select(word => word.english + " => " + word.partOfSpeech)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();
            List<string> missingDisplayPrefix = words
                .Where(word => !PartOfSpeech.IsPhrase(word.english)
                    && !PartOfSpeech.HasLeadingTag(PartOfSpeech.DisplayChinese(word))
                    && !PartOfSpeech.DisplayChinese(word).TrimStart().StartsWith(
                        PartOfSpeech.DisplayPrefix(word), StringComparison.OrdinalIgnoreCase))
                .Select(word => word.english + " => " + PartOfSpeech.DisplayChinese(word))
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();
            List<string> samples = words
                .Where(word => !PartOfSpeech.IsPhrase(word.english))
                .Take(20)
                .Select(word => word.english + " => " + PartOfSpeech.DisplayChinese(word))
                .ToList();
            WordEntry apple = words.FirstOrDefault(word => string.Equals(word.english,
                "apple", StringComparison.OrdinalIgnoreCase));
            if (apple != null) samples.Insert(0, apple.english + " => "
                + PartOfSpeech.DisplayChinese(apple));

            int phraseCount = words.Count(word => PartOfSpeech.IsPhrase(word.english));
            int singleWordCount = words.Count - phraseCount;
            Dictionary<string, object> report = new Dictionary<string, object>
            {
                { "ok", missing.Count == 0 && phraseWithPart.Count == 0
                    && missingDisplayPrefix.Count == 0 },
                { "projectRoot", root },
                { "sourceRows", rows.Count },
                { "uniqueEntries", words.Count },
                { "singleWords", singleWordCount },
                { "singleWordsWithPartOfSpeech", singleWordCount - missing.Count },
                { "phrases", phraseCount },
                { "missing", missing },
                { "phrasesWithPartOfSpeech", phraseWithPart },
                { "missingDisplayPrefix", missingDisplayPrefix },
                { "samples", samples }
            };
            File.WriteAllText(outputFile, new JavaScriptSerializer().Serialize(report),
                new UTF8Encoding(false));
            return (bool)report["ok"] ? 0 : 4;
        }

        private static int InstallAfterExit(string targetFile, string processId, string reportFile)
        {
            try
            {
                string root = AppPaths.FindProjectRoot();
                string expected = Path.GetFullPath(Path.Combine(root, "main.exe"));
                string target = Path.GetFullPath(targetFile);
                if (!string.Equals(expected, target, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("安装目标不属于当前项目。");
                int pid;
                if (!int.TryParse(processId, out pid) || pid <= 0)
                    throw new ArgumentException("无效的运行程序编号。");
                try
                {
                    using (Process active = Process.GetProcessById(pid)) active.WaitForExit();
                }
                catch (ArgumentException) { }

                foreach (Process candidate in Process.GetProcessesByName("main"))
                {
                    using (candidate)
                    {
                        try
                        {
                            if (string.Equals(Path.GetFullPath(candidate.MainModule.FileName), target,
                                StringComparison.OrdinalIgnoreCase)) candidate.WaitForExit();
                        }
                        catch (InvalidOperationException) { }
                    }
                }

                BackupInfo backup = new BackupService(root).Create(true, int.MaxValue);
                if (backup.fileCount < 1 || !File.Exists(Path.Combine(backup.path, "main.exe")))
                    throw new InvalidOperationException("完整备份未通过检查，程序没有被替换。");
                string source = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "main.exe"));
                if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("安装来源不能是正在替换的程序。");
                File.Copy(source, target, true);
                string builtHash, installedHash;
                using (SHA256 hash = SHA256.Create())
                using (FileStream input = File.OpenRead(source)) builtHash = Convert.ToBase64String(hash.ComputeHash(input));
                using (SHA256 hash = SHA256.Create())
                using (FileStream input = File.OpenRead(target)) installedHash = Convert.ToBase64String(hash.ComputeHash(input));
                if (builtHash != installedHash)
                    throw new IOException("安装后文件校验失败，请从备份恢复原程序。");
                File.WriteAllText(reportFile, new JavaScriptSerializer().Serialize(new {
                    ok = true, backup = backup.path, target = target, hash = installedHash
                }), new UTF8Encoding(false));
                return 0;
            }
            catch (Exception error)
            {
                File.WriteAllText(reportFile, new JavaScriptSerializer().Serialize(new {
                    ok = false, error = error.ToString()
                }), new UTF8Encoding(false));
                return 1;
            }
        }

        private static int MigrateNotebooks(string outputFile)
        {
            string root = AppPaths.FindProjectRoot();
            NotebookStore notebooks = new NotebookStore(root);
            Dictionary<string, object> report = new Dictionary<string, object>
            {
                { "ok", true },
                { "projectRoot", root },
                { "wrong", notebooks.Count(Notebooks.Wrong) },
                { "errorProne", notebooks.Count(Notebooks.ErrorProne) },
                { "mastered", notebooks.Count(Notebooks.Mastered) },
                { "migrationBackupExists", File.Exists(Path.Combine(root, "wrong_words.pre-migration.json")) }
            };
            File.WriteAllText(outputFile, new JavaScriptSerializer().Serialize(report), new UTF8Encoding(false));
            return 0;
        }

        private static int RenderPreview(string outputFile, string mode)
        {
            using (MainForm form = new MainForm())
            {
                form.PrepareColorPreview();
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-20000, -20000);
                form.Show();
                Application.DoEvents();
                if (mode == "bottom")
                {
                    form.PrepareSettingsBottomPreview();
                    Application.DoEvents();
                }
                else if (mode == "focus-end")
                {
                    form.FocusEndButtonPreview();
                    Application.DoEvents();
                }
                else if (mode == "pause")
                {
                    form.ForcePausePreview();
                    Application.DoEvents();
                    File.WriteAllText(outputFile + ".debug.txt", form.PausePreviewState());
                }
                Control rendered = mode == "pause" ? form.PausePreviewControl : (Control)form;
                using (Bitmap image = new Bitmap(rendered.Width, rendered.Height))
                {
                    rendered.DrawToBitmap(image, new Rectangle(Point.Empty, rendered.Size));
                    image.Save(outputFile, ImageFormat.Png);
                }
                form.Close();
            }
            return 0;
        }

        private static int RenderManagerPreview(string outputFile)
        {
            string root = AppPaths.FindProjectRoot();
            DataLoader loader = new DataLoader(Path.Combine(root, "data"));
            NotebookStore notebooks = new NotebookStore(root);
            using (NotebookManagerForm manager = new NotebookManagerForm(loader, notebooks))
            {
                manager.StartPosition = FormStartPosition.Manual;
                manager.Location = new Point(-20000, -20000);
                manager.Show();
                Application.DoEvents();
                using (Bitmap image = new Bitmap(manager.Width, manager.Height))
                {
                    manager.DrawToBitmap(image, new Rectangle(Point.Empty, manager.Size));
                    image.Save(outputFile, ImageFormat.Png);
                }
                manager.Close();
            }
            return 0;
        }

        private static int RenderUtility(string outputFile, string kind)
        {
            string root = AppPaths.FindProjectRoot();
            DataLoader loader = new DataLoader(Path.Combine(root, "data"));
            NotebookStore notebooks = new NotebookStore(root);
            StudyStore study = new StudyStore(root, loader, notebooks);
            Form form;
            if (kind == "quota") form = new QuotaForm(study, loader);
            else if (kind == "answers") form = new AcceptedAnswersForm(study, loader);
            else if (kind == "list")
            {
                StudyList list = study.Lists.FirstOrDefault();
                if (list == null) throw new InvalidOperationException("没有可预览的学习列表。");
                form = new StudyListViewerForm(list);
            }
            else throw new ArgumentException("未知的界面类型：" + kind);
            using (form)
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-20000, -20000);
                form.Show();
                Application.DoEvents();
                using (Bitmap image = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
                    image.Save(outputFile, ImageFormat.Png);
                }
                form.Close();
            }
            return 0;
        }

        private static int RenderStudySettings(string outputFile, string tab)
        {
            string root = AppPaths.FindProjectRoot();
            DataLoader loader = new DataLoader(Path.Combine(root, "data"));
            NotebookStore notebooks = new NotebookStore(root);
            StudyStore study = new StudyStore(root, loader, notebooks);
            using (StudySettingsForm form = new StudySettingsForm(study, notebooks, loader,
                new BackupService(root), new AppearanceStore(root)))
            {
                if (!string.IsNullOrEmpty(tab)) form.SelectTab(tab);
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-20000, -20000);
                form.Show();
                Application.DoEvents();
                using (Bitmap image = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
                    image.Save(outputFile, ImageFormat.Png);
                }
                form.Close();
            }
            return 0;
        }

        private static int RenderAppearance(string outputFile, string tab)
        {
            using (AppearanceSettingsForm form = new AppearanceSettingsForm(
                new AppearanceStore(AppPaths.FindProjectRoot())))
            {
                if (!string.IsNullOrEmpty(tab)) form.SelectTab(tab);
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-20000, -20000);
                form.Show();
                Application.DoEvents();
                using (Bitmap image = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
                    image.Save(outputFile, ImageFormat.Png);
                }
                form.Close();
            }
            return 0;
        }

        private static int RenderStatistics(string outputFile)
        {
            string root = AppPaths.FindProjectRoot();
            DataLoader loader = new DataLoader(Path.Combine(root, "data"));
            NotebookStore notebooks = new NotebookStore(root);
            StudyStore study = new StudyStore(root, loader, notebooks);
            using (StatisticsForm form = new StatisticsForm(study, new PracticeStore(root),
                new AppearanceStore(root)))
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-20000, -20000);
                form.Show(); Application.DoEvents();
                using (Bitmap image = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
                    image.Save(outputFile, ImageFormat.Png);
                }
                form.Close();
            }
            return 0;
        }

        private static int RenderStudySession(string outputFile, int revealSteps, string timerPrecision)
        {
            string root = AppPaths.FindProjectRoot();
            DataLoader loader = new DataLoader(Path.Combine(root, "data"));
            NotebookStore notebooks = new NotebookStore(root);
            StudyStore study = new StudyStore(root, loader, notebooks);
            if (timerPrecision == "minute" || timerPrecision == "millisecond")
            {
                study.Settings.timerPrecision = timerPrecision;
                study.Settings.timerEnabled = true;
                study.SaveSettings();
            }
            if (study.Active == null)
            {
                string book = loader.GetAvailableBooks().First();
                study.ExtractNew(new Dictionary<string, int> { { book, 1 } }, DateTime.Now);
            }
            using (StudySessionForm form = new StudySessionForm(study, notebooks, null,
                new AppearanceStore(root)))
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-20000, -20000);
                form.Show();
                Application.DoEvents();
                for (int step = 0; step < Math.Max(0, revealSteps); step++)
                {
                    form.RevealExampleForPreview();
                    Application.DoEvents();
                }
                if (string.IsNullOrWhiteSpace(form.VisibleText))
                    throw new InvalidOperationException("每日学习窗口未呈现题目文字。");
                using (Bitmap image = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
                    image.Save(outputFile, ImageFormat.Png);
                }
                form.Close();
            }
            return 0;
        }
    }
}
