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
        public static readonly Color Background = Color.FromArgb(11, 13, 16);
        public static readonly Color Surface = Color.FromArgb(21, 24, 29);
        public static readonly Color SurfaceRaised = Color.FromArgb(29, 33, 40);
        public static readonly Color Border = Color.FromArgb(42, 47, 54);
        public static readonly Color Text = Color.FromArgb(244, 244, 245);
        public static readonly Color MutedText = Color.FromArgb(161, 161, 170);
        public static readonly Color Error = Color.FromArgb(255, 82, 82);
        public static readonly Color Correct = Color.FromArgb(70, 210, 105);
        public static readonly Font UiFont = new Font("Segoe UI", 10.0f, FontStyle.Regular);
        public static readonly Font LogFont = new Font("Segoe UI", 12.0f, FontStyle.Regular);

        public static void Apply(Control root)
        {
            bool modern = root is ModernButton || root is ModernCard
                || root is ModernHeader || root is ModernTabControl
                || root is ModernGroupBox || root is ModernMetric
                || root is ModernListView || root is StudyModeWheel;
            if (!modern)
            {
                bool inCard = false;
                for (Control parent = root.Parent; parent != null; parent = parent.Parent)
                    if (parent is ModernCard || parent is ModernGroupBox)
                    { inCard = true; break; }
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
                button.BackColor = Surface;
                button.ForeColor = Text;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Border;
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(37, 42, 50);
                button.FlatAppearance.MouseDownBackColor = SurfaceRaised;
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

        public static string HighlightedExamples(WordEntry word)
        {
            if (word == null || string.IsNullOrWhiteSpace(word.examples)) return "该单词暂时没有例句";
            return string.Join("\n", word.examples.Split(new[] { '；' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(raw => {
                    MatchCollection marks = Marker.Matches(raw);
                    if (marks.Count > 0)
                    {
                        List<string> tokens = Tokens(GameEngine.CleanEnglish(word));
                        return Marker.Replace(raw.Trim(), match => marks.Count == 1 || IsRelated(match.Groups[1].Value, tokens)
                            ? "[[" + match.Groups[1].Value + "]]" : match.Groups[1].Value);
                    }
                    string plain = PlainText(raw.Trim());
                    string head = GameEngine.CleanEnglish(word);
                    return string.IsNullOrWhiteSpace(head) ? plain : Regex.Replace(plain,
                        @"(?<![A-Za-z])" + Regex.Escape(head) + @"(?![A-Za-z])", "[[$0]]", RegexOptions.IgnoreCase);
                }));
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
            string isolated = AppDomain.CurrentDomain.BaseDirectory;
            if (File.Exists(Path.Combine(isolated, "isolated-build.txt"))) return isolated;
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
        public bool favoriteMode { get; set; }
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
        public bool IsFavoriteMode { get; private set; }
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
            StartDeck(deck, example, dictation, spelling, taskOrder, showFirstLetter, retryOnWrong, false);
        }

        public void StartFavorites(string filterMode, string orderMode, bool example,
            bool dictation, bool spelling, IEnumerable<string> taskOrder,
            bool showFirstLetter, bool retryOnWrong, string folderId = null)
        {
            List<WordEntry> favorites = notebooks.GetFavoriteWords(folderId);
            HashSet<string> favoriteKeys = new HashSet<string>(favorites.Select(DataLoader.WordKey));
            List<WordEntry> vocabulary = new List<WordEntry>(favorites);
            foreach (string book in loader.GetAvailableBooks())
                vocabulary.AddRange(loader.LoadWordList(book, loader.GetUnitsForBook(book))
                    .Where(x => favoriteKeys.Contains(DataLoader.WordKey(x))));
            Dictionary<string, WordEntry> byKey = DataLoader.MergeWordEntries(vocabulary)
                .ToDictionary(DataLoader.WordKey);
            HashSet<string> seen = new HashSet<string>();
            List<WordEntry> deck = new List<WordEntry>();
            foreach (string book in loader.GetAvailableBooks())
                foreach (string unit in loader.GetUnitsForBook(book))
                {
                    List<WordEntry> group = loader.LoadWordList(book, new[] { unit })
                        .Where(x => byKey.ContainsKey(DataLoader.WordKey(x)))
                        .Select(x => byKey[DataLoader.WordKey(x)])
                        .Where(x => seen.Add(DataLoader.WordKey(x))).ToList();
                    group = Filter(group, filterMode, true);
                    if (orderMode == "unit_random") Shuffle(group);
                    deck.AddRange(group);
                }
            deck.AddRange(Filter(favorites.Where(x => seen.Add(DataLoader.WordKey(x))), filterMode, true));
            if (orderMode == "book_random" || orderMode == "random") Shuffle(deck);
            StartDeck(deck, example, dictation, spelling, taskOrder, showFirstLetter, retryOnWrong, true);
        }

        private void StartDeck(List<WordEntry> deck, bool example, bool dictation,
            bool spelling, IEnumerable<string> taskOrder, bool showFirstLetter,
            bool retryOnWrong, bool favoriteMode)
        {
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
            IsFavoriteMode = favoriteMode;
            ClearExampleCache();
        }

        private List<WordEntry> Filter(IEnumerable<WordEntry> words, string filterMode,
            bool includeMastered = false)
        {
            IEnumerable<WordEntry> query = words.Where(item =>
                includeMastered || notebooks.GetNotebook(item) != Notebooks.Mastered);
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
            IsFavoriteMode = false;
            exampleDeck = new List<ExampleQuestion>();
            modeDeck = CurrentDeck.Select(x => "word").ToList();
            ClearExampleCache();
            return true;
        }

        public bool StartProblemFreeMode(IEnumerable<WordEntry> words, bool example,
            bool dictation, bool spelling, IEnumerable<string> taskOrder,
            bool showFirstLetter, bool retryOnWrong)
        {
            List<WordEntry> source = (words ?? Enumerable.Empty<WordEntry>())
                .Where(x => x != null && notebooks.GetNotebook(x) != Notebooks.Mastered)
                .GroupBy(DataLoader.WordKey, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First()).ToList();
            if (source.Count == 0) return false;
            Shuffle(source);
            List<WordEntry> expanded = new List<WordEntry>();
            exampleDeck = new List<ExampleQuestion>();
            modeDeck = new List<string>();
            foreach (WordEntry word in source)
            {
                foreach (string mode in taskOrder ?? new[] { "example", "dictation", "spelling" })
                {
                    if (mode == "example" && example)
                    {
                        foreach (ExampleQuestion question in ExampleCloze.Questions(word))
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
            IsReviewMode = true;
            IsFavoriteMode = false;
            ClearExampleCache();
            return CurrentDeck.Count > 0;
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
                if (IsReviewMode && notebook != Notebooks.Wrong
                    && notebook != Notebooks.ErrorProne) CurrentIndex++;
                else if (!IsReviewMode && !IsFavoriteMode && notebook == Notebooks.Mastered) CurrentIndex++;
                else break;
            }
            if (CurrentIndex >= CurrentDeck.Count) return null;
            if (CurrentIndex < modeDeck.Count) QuestionMode = modeDeck[CurrentIndex];
            return CurrentDeck[CurrentIndex];
        }

        public AnswerOutcome CheckAnswer(string userInput)
        {
            return CheckAnswer(userInput, null, true);
        }

        public AnswerOutcome CheckAnswer(string userInput,
            Func<WordEntry, bool, string, bool, AnswerOutcome> recorder,
            bool firstAttempt)
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

            string recordedMode = QuestionMode == "word" ? "spelling" : QuestionMode;
            AnswerOutcome outcome = recorder == null
                ? notebooks.RecordAnswer(current, correct, IsReviewMode)
                : recorder(current, correct, recordedMode, firstAttempt);
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
                reviewMode = IsReviewMode, favoriteMode = IsFavoriteMode
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
            IsFavoriteMode = snapshot.favoriteMode;
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

    internal sealed class FreePracticeForm : Form
    {
        public readonly Panel ToolbarHost;
        public readonly Panel ContentHost;
        public Func<Keys, bool> ShortcutHandler;
        public Action HiddenRequested;
        public bool AllowClose;

        public FreePracticeForm()
        {
            ModernUI.ApplyAppIcon(this);
            Text = "自由练习  KRY 增强版 v1.4.0";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = ModernUI.FitWindow(1320, 820);
            MinimumSize = new Size(920, 620);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = Theme.UiFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;

            TableLayoutPanel shell = new TableLayoutPanel { Dock = DockStyle.Fill,
                ColumnCount = 1, RowCount = 3, BackColor = Theme.Background,
                Padding = new Padding(24, 18, 24, 22) };
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 124));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Controls.Add(shell);

            TableLayoutPanel heading = new TableLayoutPanel { Dock = DockStyle.Fill,
                ColumnCount = 2, RowCount = 1, BackColor = Theme.Background };
            heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
            Label title = new Label { Dock = DockStyle.Fill, Text = "自由练习",
                ForeColor = Theme.Text, BackColor = Theme.Background,
                Font = new Font("Segoe UI", 24f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft };
            Label note = new Label { Dock = DockStyle.Fill,
                Text = "独立窗口  切换或失去焦点时自动停止计时",
                ForeColor = Theme.MutedText, BackColor = Theme.Background,
                Font = new Font("Segoe UI", 9f), TextAlign = ContentAlignment.MiddleRight };
            heading.Controls.Add(title, 0, 0);
            heading.Controls.Add(note, 1, 0);
            shell.Controls.Add(heading, 0, 0);

            ToolbarHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background,
                Padding = new Padding(0, 5, 0, 7) };
            shell.Controls.Add(ToolbarHost, 0, 1);

            ModernCard card = new ModernCard { Dock = DockStyle.Fill, Margin = Padding.Empty,
                Padding = new Padding(18) };
            ContentHost = new Panel { Dock = DockStyle.Fill, BackColor = ModernUI.Card };
            card.Controls.Add(ContentHost);
            shell.Controls.Add(card, 0, 2);
            FormClosing += OnPracticeClosing;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (ShortcutHandler != null && ShortcutHandler(keyData)) return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void OnPracticeClosing(object sender, FormClosingEventArgs e)
        {
            if (AllowClose) return;
            e.Cancel = true;
            Hide();
            if (HiddenRequested != null) HiddenRequested();
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
        private AppearanceBackgroundPanel mainBackground;
        private HomeDashboardHost homeDashboard;
        private TableLayoutPanel homeShell;
        private ModernHeader homeHeader;
        private TableLayoutPanel mainCenter;
        private StudyModeWheel studyModeWheel;
        private FreePracticeForm freePracticeWindow;
        private Button freeModeButton;
        private int homeLayoutClass = -1;
        private Panel modeActionHost;
        private Label modeEyebrow;
        private Label modeTitle;
        private Label modeDescription;
        private Label modeStatus;
        private StyledCanvas logArea;
        private TextBox inputLine;
        private DarkComboBox bookCombo;
        private CheckedListBox unitList;
        private DarkComboBox freeSource;
        private DarkComboBox freeFavoriteFolder;
        private Button unitToggleButton;
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
        private ModernMetric dailyNewMetric;
        private ModernMetric dailyListMetric;
        private ModernMetric dailyProblemMetric;
        private ModernMetric dailyListsMetric;
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
        private AnswerFeedback freeFeedback;
        private SessionSummaryView freeSummary;
        private Button freeReplayButton, freeNextButton, freeEndButton;
        private Button freePreviousButton, freeFavoriteButton;
        private readonly ToolTip freeFavoriteTip = new ToolTip();
        private FavoriteShortcutFilter freeFavoriteShortcuts;
        private int freeHistoryIndex = -1;
        private LinkLabel githubLink, emailLink;
        private OnboardingController onboarding;

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

            Text = "大英默写器  KRY 增强版 v1.4.0 收藏夹修正版";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = ModernUI.FitWindow(1540, 920);
            MinimumSize = new Size(1100, 720);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = Theme.UiFont;
            AutoScaleMode = AutoScaleMode.Dpi;

            BuildInterface();
            onboarding = new OnboardingController(this, homeDashboard, projectRoot,
                delegate { return notebooks.MasteryShortcut; },
                delegate
                {
                    return new StudySettingsForm(study, notebooks, loader,
                        backups, appearance, settingsPanel, CollectFreeSettingsControls);
                },
                delegate { return new AppearanceSettingsForm(appearance); },
                delegate { return new AcceptedAnswersForm(study, loader); },
                delegate { return new NotebookManagerForm(loader, notebooks, (Keys)study.Settings.favoriteKey); },
                delegate { return new StatisticsForm(study, practice, appearance); },
                RefreshAfterSettings);
            notebooks.Changed += delegate
            {
                UpdateReviewButtonCount();
                RefreshRecentQueue();
                UpdateDailyButtons();
                UpdateFreeFavoriteButton();
                RefreshFavoriteFolderChoices(false);
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
                bool guideStarted = Environment.GetCommandLineArgs().Length == 1
                    && onboarding != null && onboarding.ShouldStart();
                if (guideStarted) onboarding.Begin(false);
                if (!guideStarted && study.Settings.checkUpdatesOnStartup
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
            if (homeDashboard != null) homeDashboard.SetPlaybackActive(false);
            base.OnDeactivate(e);
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if (freeLearningTimer != null) freeLearningTimer.Refresh();
            if (homeDashboard != null) homeDashboard.SetPlaybackActive(true);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (onboarding != null && onboarding.Active)
                onboarding.NotifyApplicationClosing();
            if (freePracticeWindow != null && !freePracticeWindow.IsDisposed)
                freePracticeWindow.AllowClose = true;
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (practice.Current != null && practice.Current.active)
            {
                practice.SavePosition(engine, freeHistoryIndex >= 0 ? practice.Current.input
                    : inputLine == null ? string.Empty : inputLine.Text,
                    exampleRevealStage, freeDictationMeaningShown, true);
                ArchiveStaleProblemFreeSession();
            }
            if (freeLearningTimer != null) freeLearningTimer.Dispose();
            if (freePronouncer != null) freePronouncer.Dispose();
            if (freeFavoriteShortcuts != null) freeFavoriteShortcuts.Dispose();
            freeFavoriteTip.Dispose();
            if (freePracticeWindow != null && !freePracticeWindow.IsDisposed)
            {
                freePracticeWindow.AllowClose = true;
                freePracticeWindow.Close();
            }
            if (onboarding != null)
            {
                onboarding.Dispose();
                onboarding = null;
            }
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

        internal string HomeLayoutReport()
        {
            string child = homeDashboard == null || homeDashboard.Child == null ? "null"
                : homeDashboard.Child.RenderSize.Width + "x"
                    + homeDashboard.Child.RenderSize.Height;
            return "form=" + ClientSize + "; background="
                + (mainBackground == null ? "null" : mainBackground.Bounds.ToString())
                + "; host=" + (homeDashboard == null ? "null" : homeDashboard.Bounds.ToString())
                + "; child=" + child + "; media="
                + (homeDashboard == null ? "null" : homeDashboard.DebugState);
        }

        internal void SaveSettingsPreview(string outputFile)
        {
            using (StudySettingsForm form = new StudySettingsForm(study, notebooks, loader,
                backups, appearance, settingsPanel, CollectFreeSettingsControls))
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

        internal void SaveFreePracticePreview(string outputFile)
        {
            freePracticeWindow.StartPosition = FormStartPosition.Manual;
            freePracticeWindow.Location = new Point(-20000, -20000);
            freePracticeWindow.Show();
            Application.DoEvents();
            using (Bitmap image = new Bitmap(freePracticeWindow.Width, freePracticeWindow.Height))
            {
                freePracticeWindow.DrawToBitmap(image,
                    new Rectangle(Point.Empty, freePracticeWindow.Size));
                image.Save(outputFile, ImageFormat.Png);
            }
            freePracticeWindow.Hide();
        }

        private void BuildInterface()
        {
            mainBackground = new AppearanceBackgroundPanel { Dock = DockStyle.Fill };
            TableLayoutPanel shell = new TableLayoutPanel { Dock = DockStyle.Fill,
                ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent,
                Padding = new Padding(28, 14, 28, 26) };
            homeShell = shell;
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 165));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 148));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            mainBackground.Controls.Add(shell);
            Controls.Add(mainBackground);
            ModernHeader header = new ModernHeader { Dock = DockStyle.Fill,
                Eyebrow = "KRY ADVANCED   FOCUS ON YOUR WORDS",
                Title = "今天，从一个单词开始。", Subtitle = string.Empty };
            homeHeader = header;
            header.BackColor = Color.Transparent;
            ModernButton settingsEntry = new ModernButton { Text = "设置", Width = 108, Height = 46,
                Subtle = true, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            settingsEntry.Click += delegate { OpenStudySettings(); };
            ModernButton help = new ModernButton { Text = "帮助", Width = 108, Height = 46,
                Subtle = true, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            help.Click += delegate { ShowHelp(); };
            header.Controls.Add(settingsEntry);
            header.Controls.Add(help);
            githubLink = new LinkLabel { Text = "GitHub 项目仓库", AutoSize = false,
                Size = new Size(300, 24), BackColor = Theme.SurfaceRaised,
                ForeColor = Theme.Text, LinkColor = Theme.Text, ActiveLinkColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                LinkBehavior = LinkBehavior.HoverUnderline };
            emailLink = new LinkLabel { Text = "QQ 邮箱 1242532684@qq.com", AutoSize = false,
                Size = new Size(300, 24), BackColor = Theme.SurfaceRaised,
                ForeColor = Theme.Text, LinkColor = Theme.Text, ActiveLinkColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                LinkBehavior = LinkBehavior.HoverUnderline };
            githubLink.LinkClicked += delegate { OpenContact("https://github.com/kryptonite309/ZJU-English-dictation-tool_kry-advanced"); };
            emailLink.LinkClicked += delegate { OpenContact("mailto:1242532684@qq.com"); };
            header.Controls.Add(githubLink); header.Controls.Add(emailLink);
            header.Resize += delegate
            {
                int y = Math.Max(18, (header.Height - settingsEntry.Height) / 2);
                settingsEntry.Location = new Point(header.Width - settingsEntry.Width - 10, y);
                help.Location = new Point(settingsEntry.Left - help.Width - 14, y);
                LayoutContactLinks(settingsEntry);
            };
            shell.Controls.Add(header, 0, 0);

            ModernCard metricsCard = new ModernCard { Dock = DockStyle.Fill,
                Margin = new Padding(5, 0, 5, 10), Padding = new Padding(12, 8, 12, 8) };
            TableLayoutPanel metrics = new TableLayoutPanel { Dock = DockStyle.Fill,
                ColumnCount = 4, RowCount = 1, BackColor = ModernUI.Card,
                Padding = Padding.Empty };
            for (int column = 0; column < 4; column++)
                metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            dailyNewMetric = new ModernMetric { Dock = DockStyle.Fill, Margin = Padding.Empty,
                Caption = "今日新学", Detail = string.Empty, ShowDivider = true };
            dailyListMetric = new ModernMetric { Dock = DockStyle.Fill, Margin = Padding.Empty,
                Caption = "列表复习", Detail = string.Empty, ShowDivider = true };
            dailyProblemMetric = new ModernMetric { Dock = DockStyle.Fill, Margin = Padding.Empty,
                Caption = "错题复习", Detail = string.Empty, ShowDivider = true };
            dailyListsMetric = new ModernMetric { Dock = DockStyle.Fill, Margin = Padding.Empty,
                Caption = "今日新学列表", Detail = string.Empty };
            metrics.Controls.Add(dailyNewMetric, 0, 0);
            metrics.Controls.Add(dailyListMetric, 1, 0);
            metrics.Controls.Add(dailyProblemMetric, 2, 0);
            metrics.Controls.Add(dailyListsMetric, 3, 0);
            metricsCard.Controls.Add(metrics);
            shell.Controls.Add(metricsCard, 0, 1);

            ModernCard modeCard = new ModernCard { Dock = DockStyle.Fill,
                Margin = new Padding(5, 6, 5, 0), Padding = new Padding(28) };
            TableLayoutPanel modeLayout = new TableLayoutPanel { Dock = DockStyle.Fill,
                ColumnCount = 2, RowCount = 1, BackColor = ModernUI.Card };
            modeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));
            modeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60f));
            studyModeWheel = new StudyModeWheel(
                new[] { "new", "list_review", "problem_review", "free" },
                new[] { "新学", "历史列表复习", "错题与易错词复习", "自由练习" })
                { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 34, 0) };
            modeLayout.Controls.Add(studyModeWheel, 0, 0);

            TableLayoutPanel detailFrame = new TableLayoutPanel { Dock = DockStyle.Fill,
                ColumnCount = 2, RowCount = 1, BackColor = ModernUI.Card };
            detailFrame.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 1));
            detailFrame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            Panel divider = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Border,
                Margin = Padding.Empty };
            detailFrame.Controls.Add(divider, 0, 0);
            TableLayoutPanel modeDetail = new TableLayoutPanel { Dock = DockStyle.Fill,
                ColumnCount = 1, RowCount = 7, BackColor = ModernUI.Card,
                Padding = new Padding(42, 14, 18, 14) };
            modeDetail.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            modeDetail.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            modeDetail.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            modeDetail.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            modeDetail.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            modeDetail.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            modeDetail.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            modeEyebrow = new Label { Dock = DockStyle.Fill, BackColor = ModernUI.Card,
                ForeColor = ModernUI.Muted, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter };
            modeTitle = new Label { Dock = DockStyle.Fill, BackColor = ModernUI.Card,
                ForeColor = Theme.Text, Font = new Font("Segoe UI", 27f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter };
            modeDescription = new Label { Dock = DockStyle.Fill, BackColor = ModernUI.Card,
                ForeColor = Theme.MutedText, Font = new Font("Segoe UI", 11f),
                TextAlign = ContentAlignment.MiddleCenter };
            modeStatus = new Label { Dock = DockStyle.Fill, BackColor = ModernUI.Card,
                ForeColor = ModernUI.MutedSoft, Font = new Font("Segoe UI", 8.5f),
                TextAlign = ContentAlignment.MiddleCenter };
            modeActionHost = new Panel { Dock = DockStyle.Fill, BackColor = ModernUI.Card,
                Margin = Padding.Empty };
            modeDetail.Controls.Add(modeEyebrow, 0, 1);
            modeDetail.Controls.Add(modeTitle, 0, 2);
            modeDetail.Controls.Add(modeDescription, 0, 3);
            modeDetail.Controls.Add(modeStatus, 0, 4);
            modeDetail.Controls.Add(modeActionHost, 0, 5);
            detailFrame.Controls.Add(modeDetail, 1, 0);
            modeLayout.Controls.Add(detailFrame, 1, 0);
            modeCard.Controls.Add(modeLayout);
            shell.Controls.Add(modeCard, 0, 2);
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
            dailyListButton = MakeButton("历史列表复习");
            dailyListButton.Click += delegate { StartDailyList(); };
            ((ModernButton)dailyListButton).Primary = true;
            dailyListEndButton = MakeButton("结束列表");
            ((ModernButton)dailyListEndButton).Subtle = true;
            dailyListEndButton.Click += delegate { EndDaily("list_review"); };
            dailyProblemButton = MakeButton("错题与易错词复习");
            dailyProblemButton.Click += delegate { StartDailyProblems(); };
            ((ModernButton)dailyProblemButton).Primary = true;
            dailyProblemEndButton = MakeButton("结束列表");
            ((ModernButton)dailyProblemEndButton).Subtle = true;
            dailyProblemEndButton.Click += delegate { EndDaily("problem_review"); };
            selectionColumn.Controls.Add(MakeLabel("自由练习  词书与范围"));
            answerColumn.Controls.Add(MakeLabel("题型与作答"));
            notebookColumn.Controls.Add(MakeLabel("词本与快捷键"));

            selectionColumn.Controls.Add(MakeLabel("练习来源"));
            freeSource = new DarkComboBox { Width = 340 };
            freeSource.Items.AddRange(new object[] { "词书与单元", "收藏夹" });
            freeSource.SelectedIndex = study.Settings.freeSource == "favorites" ? 1 : 0;
            freeSource.SelectedIndexChanged += delegate { UpdateFreeSourceControls(); };
            selectionColumn.Controls.Add(freeSource);
            freeFavoriteFolder = new DarkComboBox { Width = 340 };
            selectionColumn.Controls.Add(freeFavoriteFolder);
            RefreshFavoriteFolderChoices(true);
            Button manageFavorites = MakeButton("管理收藏夹");
            manageFavorites.Width = 340;
            manageFavorites.Click += delegate
            {
                using (FavoriteManagerForm manager = new FavoriteManagerForm(loader, notebooks, (Keys)study.Settings.favoriteKey))
                    manager.ShowDialog(settingsPanel.FindForm() ?? this);
                RefreshFavoriteFolderChoices(false);
            };
            selectionColumn.Controls.Add(manageFavorites);

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
            unitToggleButton = MakeButton("全部取消");
            unitToggleButton.Width = 340;
            unitToggleButton.Click += delegate { ToggleAllFreeUnits(); };
            unitList.ItemCheck += delegate
            {
                if (IsHandleCreated) BeginInvoke((Action)UpdateUnitToggleText);
            };
            selectionColumn.Controls.Add(unitToggleButton);
            UpdateFreeSourceControls();

            contentAll = MakeRadio("全部（单词 + 短语）",
                study.Settings.freeContentMode == "all");
            contentWords = MakeRadio("仅单词",
                study.Settings.freeContentMode == "words_only");
            contentPhrases = MakeRadio("仅短语",
                study.Settings.freeContentMode == "phrases_only");
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

            showFirstLetter = MakeCheckBox("普通拼写直接显示首字母",
                study.Settings.freeShowFirstLetter);
            retryOnWrong = MakeCheckBox("答错后重试当前词",
                study.Settings.freeRetryOnWrong);
            answerColumn.Controls.Add(MakeGroup("普通拼写提示", showFirstLetter));
            answerColumn.Controls.Add(MakeGroup("答题选项", retryOnWrong));

            freeModeButton = MakeButton("打开自由练习窗口");
            ((ModernButton)freeModeButton).Primary = true;
            freeModeButton.Click += delegate { OpenFreePracticeWindow(); };
            freeStartButton = MakeButton("开始自由练习");
            ((ModernButton)freeStartButton).Primary = true;
            freeStartButton.Click += delegate { StartGame(); };
            freePauseButton = MakeButton("暂停自由练习");
            ((ModernButton)freePauseButton).Subtle = true;
            freePauseButton.Visible = false;
            freePauseButton.Click += delegate { ToggleFreePause(); };
            foreach (Button action in new[] { dailyNewButton, dailyNewEndButton,
                dailyListButton, dailyListEndButton, dailyProblemButton,
                dailyProblemEndButton, freeModeButton })
            {
                action.Margin = Padding.Empty;
                action.Height = 46;
                modeActionHost.Controls.Add(action);
            }
            homeDashboard = new HomeDashboardHost { Dock = DockStyle.Fill };
            homeDashboard.ModeRequested += delegate(string key)
            {
                studyModeWheel.SelectMode(key);
            };
            homeDashboard.PrimaryRequested += delegate
            {
                ExecuteHomeAction(false);
            };
            homeDashboard.SecondaryRequested += delegate
            {
                ExecuteHomeAction(true);
            };
            homeDashboard.SettingsRequested += delegate
            {
                if (onboarding != null && onboarding.HandleHomeSettingsRequested()) return;
                OpenStudySettings();
            };
            homeDashboard.HelpPageRequested += delegate { ShowHelp(); };
            homeDashboard.GithubRequested += delegate
            {
                OpenContact("https://github.com/kryptonite309/ZJU-English-dictation-tool_kry-advanced");
            };
            homeDashboard.EmailRequested += delegate
            {
                OpenContact("mailto:1242532684@qq.com");
            };
            mainBackground.Controls.Add(homeDashboard);
            homeDashboard.BringToFront();
            modeActionHost.Resize += delegate { LayoutModeActions(); };
            studyModeWheel.SelectionChanged += delegate { RefreshModeSelection(); };
            shell.Resize += delegate
            {
                ApplyResponsiveHomeLayout(shell, header, metricsCard, modeCard,
                    modeDetail, help, settingsEntry);
            };
            ApplyResponsiveHomeLayout(shell, header, metricsCard, modeCard,
                modeDetail, help, settingsEntry);
            RefreshModeSelection();
            freeTimerLabel = MakeLabel(string.Empty);
            freeTimerLabel.Width = 260; freeTimerLabel.Height = 58;
            freeTimerLabel.TextAlign = ContentAlignment.MiddleCenter;
            freeTimerLabel.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            freeTimerLabel.ForeColor = Theme.Text;
            Button statisticsButton = MakeButton("学习统计");
            statisticsButton.Click += delegate
            {
                using (StatisticsForm form = new StatisticsForm(study, practice, appearance))
                    form.ShowDialog(this);
            };
            notebookColumn.Controls.Add(statisticsButton);

            reviewButton = MakeButton("错题自由模式 (0)");
            reviewButton.Click += delegate { StartReview(); };
            answerColumn.Controls.Add(reviewButton);

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

            Button manageButton = MakeButton("管理全部单词本");
            manageButton.Click += delegate { OpenNotebookManager(); };
            notebookColumn.Controls.Add(manageButton);

            GroupBox recentGroup = new ModernGroupBox();
            recentGroup.Text = "最近 5 次操作  待定队列";
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

            TableLayoutPanel center = new TableLayoutPanel();
            center.Dock = DockStyle.Fill;
            center.ColumnCount = 1;
            center.RowCount = 3;
            center.Padding = Padding.Empty;
            center.BackColor = ModernUI.Card;
            center.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            center.RowStyles.Add(new RowStyle(SizeType.Absolute, 57f));
            center.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
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

            freePracticeWindow = new FreePracticeForm();
            freePracticeWindow.ShortcutHandler = HandleFreePracticeShortcut;
            freePracticeWindow.HiddenRequested = SaveFreePracticeWindowState;
            freePracticeWindow.ContentHost.Controls.Add(center);

            FlowLayoutPanel freeToolbar = new FlowLayoutPanel { Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight, WrapContents = true,
                AutoScroll = true, BackColor = Theme.Background, Padding = Padding.Empty };
            freeStartButton.Width = 190;
            freePauseButton.Width = 190;
            masterButton.Width = 150;
            ModernButton freeSettings = new ModernButton { Text = "练习设置", Width = 150,
                Height = 46, Subtle = true, Margin = new Padding(8, 0, 0, 0) };
            freeSettings.Click += delegate { OpenStudySettings(); };
            foreach (Control action in new Control[] { freeStartButton, freePauseButton,
                masterButton, freeSettings, freeTimerLabel })
            {
                action.Height = action == freeTimerLabel ? Math.Max(58,
                    TextRenderer.MeasureText("本模块用时\r\n00:00:00.000", freeTimerLabel.Font).Height + 8) : 46;
                action.Margin = new Padding(action == freeStartButton || action == freePauseButton ? 0 : 8, 0, 0, 0);
                freeToolbar.Controls.Add(action);
            }
            freePracticeWindow.ToolbarHost.Controls.Add(freeToolbar);
            freeReplayButton = MakeButton("重听单词"); freeReplayButton.Width = 112;
            freeReplayButton.Click += delegate { ReplayFreeWord(); };
            freeNextButton = MakeButton("下一题"); freeNextButton.Width = 112;
            freeNextButton.Click += delegate { ContinueFreeFeedback(); };
            freeEndButton = MakeButton("结束列表"); freeEndButton.Width = 112;
            freeEndButton.Click += delegate { EndFreeList(); };
            foreach (Button extra in new[] { freeReplayButton, freeNextButton, freeEndButton })
            { extra.Width = 150; extra.Enabled = false; freeToolbar.Controls.Add(extra); }
            freePreviousButton = MakeButton("上一页"); freePreviousButton.Width = 140;
            freePreviousButton.Enabled = false;
            freePreviousButton.Click += delegate { PreviousFreePage(); };
            freeToolbar.Controls.Add(freePreviousButton);
            freeFavoriteButton = MakeButton("收藏单词"); freeFavoriteButton.Width = 150;
            freeFavoriteButton.Enabled = false;
            freeFavoriteButton.Click += delegate { EditFreeFavoriteFolders(); };
            freeToolbar.Controls.Add(freeFavoriteButton);
            freeFavoriteShortcuts = new FavoriteShortcutFilter(freePracticeWindow,
                delegate { return (Keys)study.Settings.favoriteKey; }, ToggleFreeFavorite);
            freePauseOverlay = new PauseOverlay { Visible = false };
            freePauseOverlay.ResumeRequested += delegate { ResumeFreePractice(); };
            freePracticeWindow.Controls.Add(freePauseOverlay);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (HandleFreePracticeShortcut(keyData)) return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private bool HandleFreePracticeShortcut(Keys keyData)
        {
            if (freePracticeWindow == null || !freePracticeWindow.ContainsFocus) return false;
            if (freeSummary != null)
            {
                if (keyData == Keys.Escape) { freePracticeWindow.Hide(); ClearFreeSummary(); }
                return true;
            }
            if (shortcutCapture != null && shortcutCapture.Focused) return false;
            if (practice.Current != null
                && keyData == (Keys)study.Settings.pauseKey)
            {
                ToggleFreePause();
                return true;
            }
            if (freePaused) return true;
            if (keyData == (Keys)study.Settings.favoriteKey) { ToggleFreeFavorite(); return true; }
            if (keyData == (Keys)study.Settings.previousPageKey) { PreviousFreePage(); return true; }
            if (keyData == (Keys)study.Settings.nextPageKey) { NextFreePage(); return true; }
            if (freeHistoryIndex >= 0)
            {
                if (keyData == Keys.Enter) { NextFreePage(); return true; }
                if (keyData == (Keys)freePronunciationSettings.replayKey) ReplayFreeWord();
                return true;
            }
            if (freeFeedback != null)
            {
                if (keyData == Keys.Enter) { ContinueFreeFeedback(); return true; }
                if (keyData == (Keys)freePronunciationSettings.replayKey) { ReplayFreeWord(); return true; }
                return true;
            }
            if (currentWord == null && practice.Current != null && !practice.Current.active && keyData == Keys.Enter)
            { ShowFreeSummary(false); return true; }
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
            if (currentWord != null && keyData == (Keys)freePronunciationSettings.replayKey)
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
            return false;
        }

        private void OpenFreePracticeWindow()
        {
            if (freePracticeWindow == null || freePracticeWindow.IsDisposed) return;
            if (!freePracticeWindow.Visible) freePracticeWindow.Show(this);
            else freePracticeWindow.Activate();
            freePracticeWindow.BringToFront();
            if (practice.Current != null && practice.Current.active && currentWord == null)
                RestoreFreePracticeIfNeeded();
            if (freePaused && practice.Current != null && practice.Current.active)
                BeginFreePauseOverlay(false);
            else if (inputLine != null && inputLine.Enabled) inputLine.Focus();
            if (freeLearningTimer != null) freeLearningTimer.Refresh();
        }

        private void SaveFreePracticeWindowState()
        {
            if (practice.Current != null && practice.Current.active && freeHistoryIndex < 0)
                practice.SavePosition(engine, inputLine == null ? string.Empty : inputLine.Text,
                    exampleRevealStage, freeDictationMeaningShown, freePaused);
            if (freePronouncer != null) freePronouncer.Stop();
            if (freeLearningTimer != null) freeLearningTimer.Refresh();
            ArchiveStaleProblemFreeSession();
        }

        private bool ArchiveStaleProblemFreeSession()
        {
            PracticeSessionState session = practice.Current;
            if (session == null || !session.active || session.kind != "problem_free"
                || string.Equals(session.studyDate, StudyStore.StudyDayKey(DateTime.Now),
                    StringComparison.Ordinal)) return false;
            if (freeLearningTimer != null) freeLearningTimer.StopAndCommit();
            practice.FinishCurrent();
            practice.SaveFeedback(null);
            freeFeedback = null;
            currentWord = null;
            freePaused = false;
            if (freePauseButton != null) freePauseButton.Visible = false;
            if (freeStartButton != null) freeStartButton.Visible = true;
            if (freeEndButton != null) freeEndButton.Enabled = false;
            if (freeProgress != null) freeProgress.Visible = false;
            if (logArea != null)
            {
                logArea.ClearContent();
                AppendToLog("上一学习日的错题自由模式已按实际作答记录自动归档。", "normal");
            }
            RefreshModeSelection();
            return true;
        }

        private void UpdateDailyButtons()
        {
            if (dailyNewButton == null) return;
            StudyList newActive = study.ActiveFor("new");
            StudyList listActive = study.ActiveFor("list_review");
            StudyList problemActive = study.ActiveFor("problem_review");
            dailyNewButton.Text = newActive == null ? "开始新学（" + study.Settings.newCount + " 词）"
                : "继续新学";
            dailyListButton.Text = listActive == null ? "开始复习（" + study.Settings.listCount + " 列表）"
                : "继续列表复习";
            dailyProblemButton.Text = problemActive == null ? "开始复习（" + study.Settings.problemCount + " 词）"
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
            if (dailyNewMetric != null) dailyNewMetric.Value = answered("new").ToString();
            if (dailyListMetric != null) dailyListMetric.Value = answered("list_review").ToString();
            if (dailyProblemMetric != null) dailyProblemMetric.Value = answered("problem_review").ToString();
            if (dailyListsMetric != null)
                dailyListsMetric.Value = todayLists.Count(x => x.kind == "new").ToString();
            RefreshModeSelection();
        }

        private void OpenStudySettings()
        {
            using (StudySettingsForm form = new StudySettingsForm(study, notebooks, loader,
                backups, appearance, settingsPanel, CollectFreeSettingsControls))
            {
                try { form.ShowDialog(this); }
                finally { form.DetachFreeSettings(); }
            }
            RefreshAfterSettings();
        }

        private void RefreshAfterSettings()
        {
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
            contentAll.Checked = study.Settings.freeContentMode == "all";
            contentWords.Checked = study.Settings.freeContentMode == "words_only";
            contentPhrases.Checked = study.Settings.freeContentMode == "phrases_only";
            showFirstLetter.Checked = study.Settings.freeShowFirstLetter;
            retryOnWrong.Checked = study.Settings.freeRetryOnWrong;
            freeSource.SelectedIndex = study.Settings.freeSource == "favorites" ? 1 : 0;
            RefreshFavoriteFolderChoices(true);
            UpdateFreeSourceControls();
        }

        private void CollectFreeSettingsControls(StudySettings target, bool includePracticeChoices)
        {
            if (target == null || bookCombo == null || unitList == null) return;
            target.freeBook = bookCombo.Text ?? string.Empty;
            target.freeSource = freeSource.SelectedIndex == 1 ? "favorites" : "units";
            FavoriteFolder favoriteFolder = freeFavoriteFolder.SelectedItem as FavoriteFolder;
            target.freeFavoriteFolderId = favoriteFolder == null ? string.Empty : favoriteFolder.id;
            target.freeUnitsConfigured = true;
            target.freeUnits = unitList.CheckedItems.Cast<object>()
                .Select(item => item.ToString()).ToList();
            target.freeContentMode = contentWords.Checked ? "words_only"
                : contentPhrases.Checked ? "phrases_only" : "all";
            target.freeShowFirstLetter = showFirstLetter.Checked;
            target.freeRetryOnWrong = retryOnWrong.Checked;
            if (!includePracticeChoices) return;
            target.freeExample = questionExample.Checked;
            target.freeDictation = questionDictation.Checked;
            target.freeSpelling = questionWord.Checked;
            target.freeQuestionOrder = orderUnitRandom.Checked ? "unit_random"
                : orderRandom.Checked ? "book_random" : "sequential";
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
                if (!ResolveCrossDay("new")) return;
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
            if (!ResolveCrossDay("list_review")) return;
            if (!EnsureDictationAvailable("list_review", false)) return;
            if (study.ActiveFor("list_review") != null) { OpenStudySession("list_review"); return; }
            try
            {
                using (ListReviewSelectionForm form = new ListReviewSelectionForm(study))
                {
                    if (form.ShowDialog(this) != DialogResult.OK) return;
                    if (form.UseAutomatic) study.StartListReview(DateTime.Now);
                    else study.StartSelectedListReview(form.SelectedIds, DateTime.Now);
                }
                OpenStudySession("list_review");
            }
            catch (Exception error) { ShowDarkDialog("列表复习无法开始", error.Message, false); }
        }

        private void StartDailyProblems()
        {
            if (!ResolveCrossDay("problem_review")) return;
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
                + (kind == "new"
                    ? "已经完成本部分全部要求的单词会保留；未完成单词回到普通未抽取池，不额外提高优先级。"
                    : kind == "list_review"
                    ? "本次作答记录会保留，未完成部分回归原列表。以后手动选择该列表时，完整列表会一起出现。"
                    : "本次临时抽取列表会结束；单词仍按当前归属留在错题本或易错本中。")
                + (study.Settings.backupBeforeManualEnd
                    ? "\n\n执行前会自动创建一份手动备份；可在设置中关闭。"
                    : "\n\n当前已关闭结束前自动备份；可在设置中重新开启。" );
            if (MessageBox.Show(this, message, "结束当前列表", MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes) return;
            ExecuteEndDaily(kind);
        }

        private bool ResolveCrossDay(string kind)
        {
            StudyList active = study.ActiveFor(kind);
            if (active == null || !study.IsCrossDay(active, DateTime.Now)) return true;
            string title = kind == "new" ? "新学" : kind == "list_review"
                ? "历史列表复习" : "错题复习";
            string detail = kind == "new"
                ? "结束后，未完成单词回到普通未抽取池。"
                : kind == "list_review"
                ? "结束后，未完成部分回归原列表；以后选择该列表时仍按完整列表复习。"
                : "结束后，本次临时列表关闭；单词仍保留在错题本或易错本。";
            DialogResult choice = MessageBox.Show(this,
                "检测到跨学习日未完成的“" + title + "”列表。\n\n"
                + "选择“是”：继续原列表\n选择“否”：结束当前列表\n选择“取消”：稍后处理\n\n"
                + detail,
                "跨日未完成列表", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (choice == DialogResult.Yes)
            {
                study.ContinueCrossDay(kind, DateTime.Now);
                return true;
            }
            if (choice == DialogResult.No) ExecuteEndDaily(kind);
            return false;
        }

        private void ExecuteEndDaily(string kind)
        {
            if (study.Settings.backupBeforeManualEnd && !ManualBackup()) return;
            try
            {
                StudyEndResult result = study.EndActive(kind, DateTime.Now);
                string resultText = kind == "new"
                    ? "保留已完成单词 " + result.kept + " 个；未完成单词 " + result.released
                        + " 个已回到普通未抽取池。"
                    : kind == "list_review"
                    ? "本次记录已保存。完整列表共 " + (result.kept + result.released)
                        + " 个单词，其中 " + result.kept + " 个已完成、" + result.released
                        + " 个未完成；以后可重新选择整份列表。"
                    : "本次错题复习已结束。已完成 " + result.kept + " 个，未完成 "
                        + result.released + " 个；单词本归属保持不变。";
                ShowDarkDialog("列表已结束", resultText, false);
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
            if (freeSummary != null || freePaused) return;
            bool wasFeedback = freeFeedback != null;
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
            if (wasFeedback && word != null) { practice.SaveFeedback(null); AskNextQuestion(); }
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
                || shortcut == (Keys)study.Settings.nextPageKey
                || shortcut == (Keys)study.Settings.favoriteKey)
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
            using (NotebookManagerForm manager = new NotebookManagerForm(loader, notebooks, (Keys)study.Settings.favoriteKey))
            {
                manager.ShowDialog(owner);
            }
            RefreshRecentQueue();
            if (currentWord != null && !currentWord.Equals(engine.GetNextQuestion()))
                AskNextQuestion();
        }

        private void MasterCurrentWord()
        {
            if (freeHistoryIndex >= 0) return;
            if (freeFeedback != null || freePaused || freeSummary != null) return;
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
            button.Height = 46;
            button.Margin = new Padding(3, 6, 3, 0);
            return button;
        }

        private void RefreshModeSelection()
        {
            if (studyModeWheel == null || modeActionHost == null || dailyNewButton == null) return;
            string key = studyModeWheel.SelectedKey;
            StudyList newActive = study.ActiveFor("new");
            StudyList listActive = study.ActiveFor("list_review");
            StudyList problemActive = study.ActiveFor("problem_review");
            bool freeActive = practice.Current != null && practice.Current.active;

            foreach (Control control in modeActionHost.Controls) control.Visible = false;
            if (key == "list_review")
            {
                modeEyebrow.Text = "02  REVIEW";
                modeTitle.Text = "回顾列表";
                modeDescription.Text = "按时间回看最近学过的单词。";
                modeStatus.Text = listActive == null ? "从历史列表中按设定数量开始复习"
                    : "检测到未完成的列表，可继续或手动结束";
                dailyListButton.Visible = true;
                dailyListEndButton.Visible = listActive != null;
            }
            else if (key == "problem_review")
            {
                modeEyebrow.Text = "03  FOCUS";
                modeTitle.Text = "攻克错词";
                modeDescription.Text = "集中练习错题与易错词。";
                modeStatus.Text = problemActive == null ? "按优先级混合抽取错题本与易错本"
                    : "检测到未完成的错词复习，可继续或手动结束";
                dailyProblemButton.Visible = true;
                dailyProblemEndButton.Visible = problemActive != null;
            }
            else if (key == "free")
            {
                modeEyebrow.Text = "04  FREE PRACTICE";
                modeTitle.Text = "自由练习";
                modeDescription.Text = "自行选择词书、单元、顺序和题型。";
                modeStatus.Text = freeActive ? (freePaused
                    ? "检测到已暂停的练习"
                    : "自由练习进行中")
                    : "练习范围与题型可在设置中调整";
                freeModeButton.Text = freeActive ? "打开并继续自由练习" : "打开自由练习窗口";
                freeModeButton.Visible = true;
            }
            else
            {
                modeEyebrow.Text = "01  NEW WORDS";
                modeTitle.Text = "学习新词";
                modeDescription.Text = "按词书抽取，先看后练。";
                modeStatus.Text = newActive == null ? "完成展示后进入所选题型练习"
                    : "检测到未完成的新学列表，可继续或手动结束";
                dailyNewButton.Visible = true;
                dailyNewEndButton.Visible = newActive != null;
            }
            LayoutModeActions();
            SyncHomeDashboard();
        }

        private Button CurrentHomeAction(bool secondary)
        {
            if (studyModeWheel == null) return null;
            string key = studyModeWheel.SelectedKey;
            if (key == "list_review") return secondary ? dailyListEndButton : dailyListButton;
            if (key == "problem_review") return secondary ? dailyProblemEndButton : dailyProblemButton;
            if (key == "free") return secondary ? null : freeModeButton;
            return secondary ? dailyNewEndButton : dailyNewButton;
        }

        private void ExecuteHomeAction(bool secondary)
        {
            if (studyModeWheel == null) return;
            string key = studyModeWheel.SelectedKey;
            if (secondary)
            {
                StudyList active = study.ActiveFor(key);
                if (active != null && key != "free") EndDaily(key);
                return;
            }
            if (key == "list_review") StartDailyList();
            else if (key == "problem_review") StartDailyProblems();
            else if (key == "free") OpenFreePracticeWindow();
            else StartDailyNew();
        }

        private void SyncHomeDashboard()
        {
            if (homeDashboard == null || studyModeWheel == null || modeEyebrow == null) return;
            Button primary = CurrentHomeAction(false);
            Button secondary = CurrentHomeAction(true);
            homeDashboard.UpdateState(new[]
            {
                dailyNewMetric == null ? "0" : dailyNewMetric.Value,
                dailyListMetric == null ? "0" : dailyListMetric.Value,
                dailyProblemMetric == null ? "0" : dailyProblemMetric.Value,
                dailyListsMetric == null ? "0" : dailyListsMetric.Value
            }, studyModeWheel.SelectedKey, modeEyebrow.Text, modeTitle.Text,
                modeDescription.Text, modeStatus.Text,
                primary == null ? string.Empty : primary.Text,
                primary != null && primary.Enabled,
                secondary == null ? string.Empty : secondary.Text,
                secondary != null && secondary.Visible);
        }

        private void ApplyResponsiveHomeLayout(TableLayoutPanel shell, ModernHeader header,
            ModernCard metricsCard, ModernCard modeCard, TableLayoutPanel modeDetail,
            ModernButton help, ModernButton settingsEntry)
        {
            int width = Math.Max(ClientSize.Width, shell.ClientSize.Width);
            int layoutClass = width >= 1700 ? 2 : width >= 1200 ? 1 : 0;
            if (layoutClass == homeLayoutClass)
            {
                LayoutModeActions();
                return;
            }
            homeLayoutClass = layoutClass;

            int headerHeight = layoutClass == 2 ? 220 : layoutClass == 1 ? 188 : 165;
            int metricsHeight = layoutClass == 2 ? 176 : layoutClass == 1 ? 158 : 146;
            int outerX = layoutClass == 2 ? 36 : layoutClass == 1 ? 30 : 28;
            shell.Padding = new Padding(outerX, layoutClass == 2 ? 24 : layoutClass == 1 ? 16 : 14,
                outerX, layoutClass == 2 ? 30 : 26);
            shell.RowStyles[0].Height = headerHeight;
            shell.RowStyles[1].Height = metricsHeight;
            metricsCard.Padding = layoutClass == 2 ? new Padding(18, 12, 18, 12)
                : new Padding(12, 8, 12, 8);
            modeCard.Padding = layoutClass == 2 ? new Padding(42)
                : layoutClass == 1 ? new Padding(34) : new Padding(28);

            float eyebrowSize = layoutClass == 2 ? 12f : layoutClass == 1 ? 10f : 8.5f;
            float titleSize = layoutClass == 2 ? 34f : layoutClass == 1 ? 28f : 24f;
            float descriptionSize = layoutClass == 2 ? 16f : layoutClass == 1 ? 13f : 11f;
            float statusSize = layoutClass == 2 ? 13f : layoutClass == 1 ? 10f : 8.5f;
            modeEyebrow.Font = new Font("Segoe UI", eyebrowSize, FontStyle.Bold);
            modeTitle.Font = new Font("Segoe UI", titleSize, FontStyle.Bold);
            modeDescription.Font = new Font("Segoe UI", descriptionSize, FontStyle.Regular);
            modeStatus.Font = new Font("Segoe UI", statusSize, FontStyle.Regular);

            int detailX = layoutClass == 2 ? 68 : layoutClass == 1 ? 50 : 32;
            modeDetail.Padding = new Padding(detailX, 14, detailX, 14);
            int[] rowHeights = layoutClass == 2
                ? new[] { 42, 92, 72, 62, 76 }
                : layoutClass == 1 ? new[] { 34, 72, 60, 52, 64 }
                : new[] { 30, 64, 52, 48, 58 };
            for (int index = 0; index < rowHeights.Length; index++)
                modeDetail.RowStyles[index + 1].Height = rowHeights[index];

            int utilityWidth = layoutClass == 2 ? 132 : layoutClass == 1 ? 118 : 108;
            int utilityHeight = layoutClass == 2 ? 54 : layoutClass == 1 ? 50 : 46;
            float utilityFont = layoutClass == 2 ? 12f : layoutClass == 1 ? 10.5f : 9.5f;
            foreach (ModernButton button in new[] { help, settingsEntry })
            {
                button.Size = new Size(utilityWidth, utilityHeight);
                button.Font = new Font("Segoe UI", utilityFont, FontStyle.Bold);
            }
            int utilityY = Math.Max(18, (header.Height - utilityHeight) / 2);
            settingsEntry.Location = new Point(header.Width - settingsEntry.Width - 10, utilityY);
            help.Location = new Point(settingsEntry.Left - help.Width - 14, utilityY);
            LayoutContactLinks(settingsEntry);

            float actionFont = layoutClass == 2 ? 13f : layoutClass == 1 ? 11f : 9.5f;
            foreach (Control control in modeActionHost.Controls)
                control.Font = new Font("Segoe UI", actionFont, FontStyle.Bold);
            dailyNewMetric.Invalidate();
            dailyListMetric.Invalidate();
            dailyProblemMetric.Invalidate();
            dailyListsMetric.Invalidate();
            studyModeWheel.Invalidate();
            header.Invalidate();
            LayoutModeActions();
        }

        private void LayoutModeActions()
        {
            if (modeActionHost == null || studyModeWheel == null || dailyNewButton == null) return;
            Control primary;
            Control secondary = null;
            string key = studyModeWheel.SelectedKey;
            if (key == "list_review")
            {
                primary = dailyListButton;
                if (study.ActiveFor("list_review") != null) secondary = dailyListEndButton;
            }
            else if (key == "problem_review")
            {
                primary = dailyProblemButton;
                if (study.ActiveFor("problem_review") != null) secondary = dailyProblemEndButton;
            }
            else if (key == "free")
                primary = freeModeButton;
            else
            {
                primary = dailyNewButton;
                if (study.ActiveFor("new") != null) secondary = dailyNewEndButton;
            }
            int availableWidth = Math.Max(120, modeActionHost.ClientSize.Width);
            int width = Math.Min(ClientSize.Width >= 1700 ? 820 : availableWidth, availableWidth);
            int x = Math.Max(0, (availableWidth - width) / 2);
            int targetHeight = ClientSize.Width >= 1700 ? 62 : ClientSize.Width >= 1200 ? 52 : 46;
            int height = Math.Min(targetHeight, Math.Max(38, modeActionHost.ClientSize.Height - 2));
            int y = Math.Max(0, (modeActionHost.ClientSize.Height - height) / 2);
            if (secondary == null)
            {
                primary.Bounds = new Rectangle(x, y, width, height);
                return;
            }
            int gap = 9;
            int endWidth = Math.Min(ClientSize.Width >= 1700 ? 170 : 132,
                Math.Max(104, width / 3));
            primary.Bounds = new Rectangle(x, y, Math.Max(120, width - endWidth - gap), height);
            secondary.Bounds = new Rectangle(x + width - endWidth, y, endWidth, height);
        }

        private void PopulateBooks()
        {
            List<string> books = loader.GetAvailableBooks();
            bookCombo.Items.Clear();
            bookCombo.Items.AddRange(books.Cast<object>().ToArray());
            if (bookCombo.Items.Count > 0)
            {
                int saved = books.FindIndex(x => string.Equals(x, study.Settings.freeBook,
                    StringComparison.OrdinalIgnoreCase));
                bookCombo.SelectedIndex = saved >= 0 ? saved : 0;
            }
            else ShowDarkDialog("警告", "在 data 目录中未找到任何包含 CSV 的词书。", false);
        }

        private void PopulateUnits()
        {
            unitList.Items.Clear();
            bool useSaved = string.Equals(bookCombo.Text, study.Settings.freeBook,
                StringComparison.OrdinalIgnoreCase)
                && study.Settings.freeUnits != null && study.Settings.freeUnitsConfigured;
            int selected = 0;
            foreach (string unit in loader.GetUnitsForBook(bookCombo.Text))
            {
                bool isChecked = !useSaved || study.Settings.freeUnits.Any(x =>
                    string.Equals(x, unit, StringComparison.OrdinalIgnoreCase));
                unitList.Items.Add(unit, isChecked);
                if (isChecked) selected++;
            }
            UpdateUnitToggleText();
        }

        private void UpdateFreeSourceControls()
        {
            bool byUnits = freeSource == null || freeSource.SelectedIndex != 1;
            if (freeFavoriteFolder != null) freeFavoriteFolder.Enabled = !byUnits;
            if (bookCombo != null) bookCombo.Enabled = byUnits;
            if (unitList != null) unitList.Enabled = byUnits;
            if (unitToggleButton != null) unitToggleButton.Enabled = byUnits;
        }

        private void RefreshFavoriteFolderChoices(bool fromSettings)
        {
            if (freeFavoriteFolder == null) return;
            FavoriteFolder previous = freeFavoriteFolder.SelectedItem as FavoriteFolder;
            string selected = fromSettings || previous == null ? study.Settings.freeFavoriteFolderId : previous.id;
            freeFavoriteFolder.BeginUpdate();
            try
            {
                freeFavoriteFolder.Items.Clear();
                freeFavoriteFolder.Items.Add(new FavoriteFolder { id = string.Empty, name = "全部收藏（去重）" });
                foreach (FavoriteFolder folder in notebooks.FavoriteFolders) freeFavoriteFolder.Items.Add(folder);
                int index = freeFavoriteFolder.Items.Cast<FavoriteFolder>().ToList().FindIndex(x => x.id == selected);
                freeFavoriteFolder.SelectedIndex = index < 0 ? 0 : index;
            }
            finally { freeFavoriteFolder.EndUpdate(); }
        }

        private void UpdateUnitToggleText()
        {
            if (unitToggleButton != null) unitToggleButton.Text = unitList.Items.Count > 0
                && unitList.CheckedItems.Count == unitList.Items.Count ? "全部取消" : "一键全选";
        }

        private void ToggleAllFreeUnits()
        {
            bool select = unitList.CheckedItems.Count != unitList.Items.Count;
            unitList.BeginUpdate();
            try { for (int index = 0; index < unitList.Items.Count; index++) unitList.SetItemChecked(index, select); }
            finally { unitList.EndUpdate(); }
            UpdateUnitToggleText();
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
            ClearFreeSummary();
            if (practice.Current != null && practice.Current.active)
            {
                if (!ArchiveStaleProblemFreeSession())
                {
                    RestoreFreePracticeIfNeeded();
                    if (freePaused) ResumeFreePractice();
                    return;
                }
            }
            List<string> selectedUnits = unitList.CheckedItems.Cast<object>()
                .Select(item => item.ToString()).ToList();
            bool favorites = freeSource.SelectedIndex == 1;
            if (!favorites && selectedUnits.Count == 0)
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
            CollectFreeSettingsControls(study.Settings, true);
            study.SaveSettings();
            if (favorites)
                engine.StartFavorites(filterMode, orderMode, questionExample.Checked,
                    questionDictation.Checked && !freeDictationTemporarilyDisabled, questionWord.Checked,
                    study.Settings.freeTaskOrder, showFirstLetter.Checked, retryOnWrong.Checked,
                    study.Settings.freeFavoriteFolderId);
            else
                engine.StartGame(bookCombo.Text, selectedUnits, filterMode, orderMode,
                    questionExample.Checked, questionDictation.Checked && !freeDictationTemporarilyDisabled,
                    questionWord.Checked,
                    study.Settings.freeTaskOrder, showFirstLetter.Checked, retryOnWrong.Checked);

            if (engine.CurrentDeck.Count == 0)
            {
                ShowDarkDialog("无法开始", "所选范围内没有符合条件的题目。", false);
                return;
            }
            practice.Begin(engine, favorites ? "favorites" : "free");
            freeHistoryIndex = -1;
            freeFeedback = null; freeEndButton.Enabled = true;
            freePaused = false;
            freePauseButton.Visible = true;
            freeStartButton.Visible = false;
            studyModeWheel.SelectMode("free");
            RefreshModeSelection();
            StartFreeTimer(0);

            logArea.ClearContent();
            AppendToLog("--- 游戏开始 ---", "normal");
            AppendToLog(string.Format("书本: {0}, 单元: {1}", bookCombo.Text,
                string.Join(", ", selectedUnits)), "normal");
            inputLine.Enabled = true;
            AskNextQuestion();
            OpenFreePracticeWindow();
        }

        private void StartReview()
        {
            ClearFreeSummary();
            if (practice.Current != null && practice.Current.active)
            {
                if (!ArchiveStaleProblemFreeSession())
                {
                    RestoreFreePracticeIfNeeded();
                    return;
                }
            }
            bool problemDictation = study.Settings.problemFreeDictation;
            if (problemDictation && !EnsureProblemFreeDictationAvailable(ref problemDictation)) return;
            if (!engine.StartProblemFreeMode(notebooks.GetProblemWords(),
                study.Settings.problemFreeExample, problemDictation,
                study.Settings.problemFreeSpelling, study.Settings.problemFreeTaskOrder,
                study.Settings.problemFreeShowFirstLetter,
                study.Settings.problemFreeRetryOnWrong))
            {
                ShowDarkDialog("提示", "错题本和易错本中没有可练习的单词。", false);
                return;
            }
            practice.Begin(engine, "problem_free");
            freeHistoryIndex = -1;
            freeFeedback = null; freeEndButton.Enabled = true;
            freePaused = false;
            freePauseButton.Visible = true;
            freeStartButton.Visible = false;
            studyModeWheel.SelectMode("free");
            RefreshModeSelection();
            StartFreeTimer(0);

            logArea.ClearContent();
            AppendToLog("--- 错题自由模式开始 ---", "normal");
            AppendToLog("本轮已将错题本与易错本中的单词去重并随机排列；作答多少记录多少。",
                "normal");
            inputLine.Enabled = true;
            AskNextQuestion();
            OpenFreePracticeWindow();
        }

        private bool EnsureProblemFreeDictationAvailable(ref bool includeDictation)
        {
            if (!includeDictation) return true;
            PronunciationSettings settings = new PronunciationStore(projectRoot).Settings;
            if (settings.enabled && WordPronouncer.GetEnglishVoices().Count > 0) return true;
            bool hasAlternative = study.Settings.problemFreeExample
                || study.Settings.problemFreeSpelling;
            DialogResult choice = MessageBox.Show(this,
                "当前没有可用的英语语音，错题自由模式的听写无法播放。\n\n"
                + "选择“是”打开 Windows 语音设置；选择“否”仅在本次练习中关闭听写；选择“取消”返回。",
                "听写语音不可用", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (choice == DialogResult.Yes)
            {
                try { Process.Start(new ProcessStartInfo("ms-settings:speech") { UseShellExecute = true }); }
                catch { ShowDarkDialog("语音设置", "无法打开系统设置，请手动安装 Windows 英语语音。", false); }
                return false;
            }
            if (choice != DialogResult.No) return false;
            if (!hasAlternative)
            {
                ShowDarkDialog("无法临时关闭听写", "错题自由模式只启用了听写。请先在设置中启用另一种题型。", false);
                return false;
            }
            includeDictation = false;
            return true;
        }

        private void AskNextQuestion()
        {
            freeHistoryIndex = -1;
            freeFeedback = null; freeNextButton.Enabled = false; freeReplayButton.Enabled = false;
            inputLine.Enabled = true;
            logArea.ClearContent();
            currentWord = engine.GetNextQuestion();
            UpdateFreeFavoriteButton();
            UpdateFreeHistoryButtons();
            exampleRevealStage = 0;
            freeDictationMeaningShown = false;
            if (currentWord == null)
            {
                if (freeLearningTimer != null) freeLearningTimer.StopAndCommit();
                if (practice.Current != null && practice.Current.active) practice.FinishCurrent();
                AppendToLog("本轮已完成，按 Enter 查看结算。", "correct");
                freeNextButton.Enabled = true; freeNextButton.Text = "查看结算";
                freeEndButton.Enabled = false;
                inputLine.Clear();
                inputLine.Enabled = false;
                freePauseButton.Visible = false;
                freeStartButton.Visible = true;
                freeStartButton.Text = "自由练习";
                RefreshModeSelection();
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

            freeReplayButton.Enabled = currentWord != null && freePronunciationSettings.enabled;
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
            return freeHistoryIndex < 0 && freeFeedback == null && currentWord != null && engine.QuestionMode == "example"
                && engine.GetExampleQuestion() != null;
        }

        private bool IsFreeDictationActive()
        {
            return freeHistoryIndex < 0 && freeFeedback == null && currentWord != null && engine.QuestionMode == "dictation";
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
            if (freeHistoryIndex >= 0 || freeFeedback != null || freePaused || freeSummary != null) return;
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
            bool problemFree = practice.Current != null
                && practice.Current.kind == "problem_free";
            bool firstAttempt = practice.Current == null || !practice.Attempts.Any(x =>
                x.sessionId == practice.Current.id && x.taskIndex == answeredIndex);
            DateTime answeredAt = DateTime.Now;
            AnswerOutcome outcome = problemFree
                ? engine.CheckAnswer(userInput, delegate(WordEntry word, bool correct,
                    string mode, bool first)
                {
                    return notebooks.RecordStudyAnswer(word, correct, mode, first,
                        study.Settings.exampleCorrectTarget,
                        study.Settings.dictationCorrectTarget,
                        study.Settings.spellingCorrectTarget, answeredAt);
                }, firstAttempt)
                : engine.CheckAnswer(userInput);
            AnswerFeedback answeredFeedback = new AnswerFeedback { word = answeredWord,
                answer = outcome.CorrectAnswer, input = userInput, mode = answeredMode,
                correct = outcome.Correct, difference = outcome.Correct ? string.Empty
                    : SpellingDifference.Report(userInput, accepted) };
            if (practice.Current != null && practice.Current.active)
                practice.RecordAttempt(answeredWord, answeredMode, answeredIndex,
                    outcome.Correct, answeredAt, answeredFeedback);
            if (problemFree)
                notebooks.RecordProblemReviewActivity(answeredWord, outcome.Correct,
                    outcome.Correct && ProblemFreeWordCompleted(answeredWord, answeredIndex),
                    answeredAt);
            if (!outcome.Correct && (engine.RetryOnWrong || engine.GetNextQuestion() == null)) engine.CurrentIndex--;
            freeFeedback = answeredFeedback;
            practice.SavePosition(engine, string.Empty, 0, false, false);
            practice.SaveFeedback(freeFeedback);
            if (engine.GetNextQuestion() == null)
            {
                if (freeLearningTimer != null) freeLearningTimer.StopAndCommit();
                practice.FinishCurrent();
            }
            UpdateReviewButtonCount();
            if (outcome.Correct)
            {
                freePronouncer.Stop(); freeFeedback = null; practice.SaveFeedback(null);
                AskNextQuestion();
                return;
            }
            ShowFreeFeedback();
            freeProgress.Completed = practice.Attempts.Where(x => x.sessionId == practice.Current.id && x.correct)
                .Select(x => x.taskIndex).Distinct().Count();
            if (!outcome.Correct) ReplayFreeWord();
        }

        private bool ProblemFreeWordCompleted(WordEntry word, int currentTaskIndex)
        {
            if (practice.Current == null || practice.Current.kind != "problem_free") return false;
            string key = DataLoader.WordKey(word);
            List<int> planned = engine.CurrentDeck.Select((entry, index) => new { entry, index })
                .Where(x => x.entry != null && DataLoader.WordKey(x.entry) == key)
                .Select(x => x.index).Distinct().ToList();
            HashSet<int> correct = new HashSet<int>(practice.Attempts.Where(x =>
                    x.sessionId == practice.Current.id && x.correct)
                .Select(x => x.taskIndex));
            correct.Add(currentTaskIndex);
            return planned.Count > 0 && planned.All(correct.Contains);
        }

        private void LayoutContactLinks(Control settings)
        {
            if (githubLink == null) return;
            githubLink.Location = new Point(settings.Right - githubLink.Width, settings.Bottom + 5);
            emailLink.Location = new Point(settings.Right - emailLink.Width, githubLink.Bottom + 3);
        }

        private void OpenContact(string address)
        {
            try { Process.Start(new ProcessStartInfo(address) { UseShellExecute = true }); }
            catch (Exception error) { ShowDarkDialog("无法打开链接", error.Message, false); }
        }

        private List<PracticeAttempt> FreeHistory()
        {
            return practice.Current == null ? new List<PracticeAttempt>() : practice.Attempts
                .Where(x => x.sessionId == practice.Current.id).ToList();
        }

        private WordEntry FreeDisplayedWord()
        {
            if (freeHistoryIndex < 0) return freeFeedback == null ? currentWord : freeFeedback.word;
            List<PracticeAttempt> history = FreeHistory();
            if (freeHistoryIndex >= history.Count) return null;
            PracticeAttempt attempt = history[freeHistoryIndex];
            if (attempt.feedback != null) return attempt.feedback.word;
            WordEntry word = engine.CurrentDeck.FirstOrDefault(x => GameEngine.CleanEnglish(x) == attempt.word);
            return word ?? notebooks.Records.Select(x => x.word)
                .FirstOrDefault(x => GameEngine.CleanEnglish(x) == attempt.word)
                ?? new WordEntry { english = attempt.word, chinese = string.Empty, examples = string.Empty };
        }

        private void UpdateFreeFavoriteButton()
        {
            if (freeFavoriteButton == null) return;
            WordEntry word = FreeDisplayedWord();
            freeFavoriteButton.Enabled = word != null && !freePaused && freeSummary == null;
            freeFavoriteButton.Text = FavoriteUi.ButtonText(notebooks, word);
            freeFavoriteTip.SetToolTip(freeFavoriteButton, "点击多选收藏归属；"
                + ShortcutText((Keys)study.Settings.favoriteKey) + " 快捷操作“"
                + notebooks.GetFavoriteFolder(notebooks.DefaultFavoriteFolderId).name
                + "”\n当前归属：" + notebooks.FavoriteFolderNames(word));
        }

        private void ToggleFreeFavorite()
        {
            if (freePaused || freeSummary != null) return;
            WordEntry word = FreeDisplayedWord();
            if (word == null) return;
            bool added = notebooks.ToggleFavorite(word);
            UpdateFreeFavoriteButton();
            if (freePracticeWindow.Visible) freeFavoriteTip.Show((added ? "已加入“" : "已移出“")
                + notebooks.GetFavoriteFolder(notebooks.DefaultFavoriteFolderId).name + "”", freeFavoriteButton, 1800);
        }

        private void EditFreeFavoriteFolders()
        {
            if (freePaused || freeSummary != null) return;
            WordEntry word = FreeDisplayedWord(); if (word == null) return;
            FavoriteUi.EditWord(freePracticeWindow, notebooks, word); UpdateFreeFavoriteButton();
            if (inputLine.Enabled) inputLine.Focus();
        }

        private void UpdateFreeHistoryButtons()
        {
            if (freePreviousButton == null) return;
            freePreviousButton.Enabled = !freePaused && (freeHistoryIndex < 0
                ? FreeHistory().Count > 0 : freeHistoryIndex > 0);
            if (freeHistoryIndex >= 0)
            { freeNextButton.Enabled = true; freeNextButton.Text = "下一页"; }
        }

        private void PreviousFreePage()
        {
            if (freePaused || freeSummary != null) return;
            List<PracticeAttempt> history = FreeHistory();
            if (history.Count == 0 || freeHistoryIndex == 0) return;
            if (freeHistoryIndex < 0)
            {
                if (practice.Current.active) practice.SavePosition(engine, inputLine.Text,
                    exampleRevealStage, freeDictationMeaningShown, freePaused);
                freeHistoryIndex = history.Count - 1;
            }
            else freeHistoryIndex--;
            ShowFreeHistory();
        }

        private void NextFreePage()
        {
            if (freePaused || freeHistoryIndex < 0) return;
            if (freeHistoryIndex + 1 < FreeHistory().Count)
            { freeHistoryIndex++; ShowFreeHistory(); }
            else ReturnToLiveFreePage();
        }

        private void ShowFreeHistory()
        {
            List<PracticeAttempt> history = FreeHistory();
            if (freeHistoryIndex < 0 || freeHistoryIndex >= history.Count)
            { ReturnToLiveFreePage(); return; }
            freePronouncer.Stop();
            PracticeAttempt attempt = history[freeHistoryIndex];
            WordEntry word = FreeDisplayedWord();
            if (attempt.feedback != null) attempt.feedback.Render(logArea, appearance);
            else
            {
                logArea.ClearContent();
                logArea.Add(GameEngine.CleanEnglish(word), "word");
                logArea.AddExample(word);
                logArea.Add(PartOfSpeech.DisplayChinese(word), "meaning");
                logArea.Add(attempt.correct ? "本题答对" : "本题答错", attempt.correct ? "correct" : "error");
            }
            logArea.Add("只读回看 " + (freeHistoryIndex + 1) + " / " + history.Count
                + "\n返回当前题继续作答，不修改进度或记录。", "normal");
            inputLine.Clear(); inputLine.Enabled = false;
            freeReplayButton.Enabled = freePronunciationSettings.enabled;
            practice.SetViewedAttempt(freeHistoryIndex);
            UpdateFreeHistoryButtons(); UpdateFreeFavoriteButton();
        }

        private void ReturnToLiveFreePage()
        {
            freeHistoryIndex = -1; practice.SetViewedAttempt(-1);
            freeNextButton.Text = "下一题";
            if (freeFeedback != null) ShowFreeFeedback();
            else if (currentWord == null) AskNextQuestion();
            else
            {
                logArea.ClearContent(); ReprintCurrentQuestion();
                inputLine.Enabled = !freePaused;
                inputLine.Text = practice.Current.input ?? string.Empty;
                inputLine.SelectionStart = inputLine.TextLength;
                freeNextButton.Enabled = false;
                freeReplayButton.Enabled = freePronunciationSettings.enabled;
                if (inputLine.Enabled) inputLine.Focus();
            }
            UpdateFreeHistoryButtons(); UpdateFreeFavoriteButton();
        }

        private void ReplayFreeWord()
        {
            if (freePaused || !freePronunciationSettings.enabled) return;
            WordEntry word = FreeDisplayedWord();
            if (word == null) return;
            string error;
            if (!freePronouncer.Speak(GameEngine.CleanEnglish(word), out error) && !string.IsNullOrEmpty(error))
                AppendToLog(error, "normal");
        }

        private void ShowFreeFeedback()
        {
            if (freeFeedback == null) return;
            currentWord = freeFeedback.word;
            freeFeedback.Render(logArea, appearance);
            inputLine.Clear(); inputLine.Enabled = false;
            freeReplayButton.Enabled = freePronunciationSettings.enabled;
            freeNextButton.Enabled = true;
            freeNextButton.Text = engine.GetNextQuestion() == null ? "查看结算" : "下一题";
            freeEndButton.Enabled = practice.Current != null && practice.Current.active;
            freeNextButton.Focus();
            UpdateFreeFavoriteButton(); UpdateFreeHistoryButtons();
        }

        private void ContinueFreeFeedback()
        {
            if (freePaused) return;
            if (freeHistoryIndex >= 0) { NextFreePage(); return; }
            freePronouncer.Stop(); freeFeedback = null; practice.SaveFeedback(null);
            if (engine.GetNextQuestion() == null) ShowFreeSummary(false);
            else AskNextQuestion();
        }

        private void EndFreeList()
        {
            if (freePaused || practice.Current == null || !practice.Current.active) return;
            if (freeHistoryIndex >= 0) ReturnToLiveFreePage();
            string modeName = practice.Current.kind == "problem_free"
                ? "错题自由模式" : "自由练习";
            string message = "确定结束当前“" + modeName + "”列表并查看结算吗？"
                + (study.Settings.backupBeforeManualEnd ? "\n结束前将创建手动备份，可在设置中关闭。" : "");
            if (MessageBox.Show(freePracticeWindow, message, "结束列表", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            if (study.Settings.backupBeforeManualEnd && !ManualBackup()) return;
            ShowFreeSummary(true);
        }

        private void ShowFreeSummary(bool manual)
        {
            if (practice.Current == null || freeSummary != null) return;
            if (freeLearningTimer != null) freeLearningTimer.StopAndCommit();
            practice.SavePosition(engine, string.Empty, 0, false, false);
            practice.FinishCurrent(); practice.SaveFeedback(null);
            freeFeedback = null; freePronouncer.Stop();
            freeHistoryIndex = -1; practice.SetViewedAttempt(-1);
            freeSummary = new SessionSummaryView(SessionSummary.FromPractice(practice.Current, practice.Attempts, manual),
                study.Settings.timerEnabled, study.Settings.timerPrecision);
            freeSummary.SetAppearance(appearance, "free");
            foreach (Control control in freePracticeWindow.Controls) control.Visible = false;
            freePracticeWindow.Controls.Add(freeSummary); freeSummary.BringToFront(); freeSummary.Focus();
            freeSummary.Visible = true;
            RefreshModeSelection();
        }

        private void ClearFreeSummary()
        {
            if (freeSummary == null) return;
            freePracticeWindow.Controls.Remove(freeSummary); freeSummary.Dispose(); freeSummary = null;
            foreach (Control control in freePracticeWindow.Controls)
                if (control != freePauseOverlay) control.Visible = true;
            freeStartButton.Visible = true; freePauseButton.Visible = false;
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
            if (session.kind == "problem_free"
                && !string.Equals(session.studyDate, StudyStore.StudyDayKey(DateTime.Now),
                    StringComparison.Ordinal))
            {
                practice.FinishCurrent();
                return;
            }
            practice.Resume(engine);
            freeFeedback = session.feedback;
            freeEndButton.Enabled = true;
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
            AppendToLog(session.kind == "problem_free"
                ? "--- 继续错题自由模式 ---" : "--- 继续自由练习 ---", "normal");
            ReprintCurrentQuestion();
            inputLine.Enabled = !freePaused;
            inputLine.Text = session.input ?? string.Empty;
            inputLine.SelectionStart = inputLine.TextLength;
            freeStartButton.Visible = false;
            freePauseButton.Visible = true;
            freePauseButton.Text = freePaused ? "继续自由练习" : "暂停自由练习";
            studyModeWheel.SelectMode("free");
            RefreshModeSelection();
            int[] current = engine.GetProgress();
            freeProgress.Visible = true;
            freeProgress.Total = Math.Max(1, current[1]);
            freeProgress.Completed = Math.Min(engine.CurrentIndex, current[1]);
            StartFreeTimer(session.activeMilliseconds);
            if (freeFeedback != null) ShowFreeFeedback();
            if (!freePaused && freePracticeWindow != null && freePracticeWindow.Visible)
                inputLine.Focus();
            freeHistoryIndex = session.viewedAttemptIndex ?? -1;
            if (freeHistoryIndex >= 0) ShowFreeHistory();
            UpdateFreeFavoriteButton(); UpdateFreeHistoryButtons();
        }

        private void StartFreeTimer(long initialMilliseconds)
        {
            if (freeLearningTimer != null) freeLearningTimer.Dispose();
            freeLearningTimer = new LearningTimer(freePracticeWindow ?? (Control)this,
                freeTimerLabel, initialMilliseconds,
                study.Settings.timerEnabled, study.Settings.timerPrecision,
                delegate { return !freePaused && practice.Current != null
                    && practice.Current.active && currentWord != null; },
                delegate(long delta) { practice.AddActiveMilliseconds(delta); });
        }

        private void ToggleFreePause()
        {
            if (practice.Current == null || freeSummary != null) return;
            if (freePaused) ResumeFreePractice();
            else BeginFreePauseOverlay(true);
        }

        private void BeginFreePauseOverlay(bool persist)
        {
            if (practice.Current == null || freeSummary != null) return;
            if (persist) practice.SavePosition(engine, freeHistoryIndex < 0 ? inputLine.Text : practice.Current.input, exampleRevealStage,
                freeDictationMeaningShown, true);
            if (freeLearningTimer != null) freeLearningTimer.StopAndCommit();
            freePronouncer.Stop();
            freePaused = true;
            inputLine.Enabled = false;
            freePauseOverlay.Visible = false;
            freePauseOverlay.Prepare(freePracticeWindow ?? (Form)this, appearance, "free");
            freePauseOverlay.Visible = true;
            freePauseOverlay.BringToFront();
            freePauseButton.Text = "继续自由练习";
            RefreshModeSelection();
            if (freeLearningTimer != null) freeLearningTimer.Refresh();
        }

        private void ResumeFreePractice()
        {
            if (practice.Current == null || freeSummary != null) return;
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
            RefreshModeSelection();
            inputLine.Enabled = freeHistoryIndex < 0;
            practice.SavePosition(engine, freeHistoryIndex < 0 ? inputLine.Text : practice.Current.input, exampleRevealStage,
                freeDictationMeaningShown, false);
            if (freeHistoryIndex >= 0) ShowFreeHistory();
            else if (freeFeedback != null) ShowFreeFeedback();
            else { logArea.ClearContent(); ReprintCurrentQuestion(); }
            if (freeLearningTimer != null) freeLearningTimer.Refresh();
            if (inputLine.Enabled) inputLine.Focus();
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
            if (reviewButton != null) reviewButton.Text = "错题自由模式 ("
                + notebooks.GetProblemWords().Count + ")";
        }

        private void AppendToLog(string message, string kind)
        {
            logArea.Add(message, kind);
            logArea.ScrollToBottom();
        }

        private void ApplyAppearance()
        {
            if (mainBackground != null)
                mainBackground.SetAppearance(homeDashboard == null ? appearance : null, "main");
            if (homeDashboard != null)
            {
                homeDashboard.SetAppearance(appearance);
                homeDashboard.SetPlaybackActive(ContainsFocus && WindowState != FormWindowState.Minimized);
            }
            if (homeShell != null) homeShell.BackColor = Color.Transparent;
            if (homeHeader != null) homeHeader.BackColor = Color.Transparent;
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
                + "如需重新查看功能位置，请点击本窗口底部的“新手引导与功能导览”。\n"
                + "学习窗口的“上一页”只用于回看；展示和答题阶段不能互相回退。\n"
                + "默认采用深色背景，字体、提示文案、主界面和练习背景可在外观设置中修改。\n\n"
                + "项目与联系\n"
                + "GitHub：https://github.com/kryptonite309/ZJU-English-dictation-tool_kry-advanced\n"
                + "QQ 邮箱：1242532684@qq.com";
            using (HelpCenterForm dialog = new HelpCenterForm(help))
            {
                dialog.ShowDialog(this);
                if (dialog.ReplayRequested && onboarding != null)
                    onboarding.Begin(true);
            }
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

                if (args.Length >= 2 && args[0] == "--test-v131")
                    return V131Tests.Run(args[1]);

                if (args.Length >= 2 && args[0] == "--test-v134")
                    return V134Tests.Run(args[1]);

                if (args.Length >= 2 && args[0] == "--test-v135")
                    return V135Tests.Run(args[1]);

                if (args.Length >= 2 && args[0] == "--test-v136")
                    return V136Tests.Run(args[1]);

                if (args.Length >= 2 && args[0] == "--test-v137")
                    return V137Tests.Run(args[1]);

                if (args.Length >= 2 && args[0] == "--test-v140")
                    return V140Tests.Run(args[1]);

                if (args.Length >= 2 && args[0] == "--test-favorite-folders")
                    return FavoriteFolderTests.Run(args[1]);

                if (args.Length >= 2 && args[0] == "--check-v140-state")
                    return V140Tests.CheckCopiedState(args[1]);

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

                if (args.Length >= 3 && args[0] == "--render-onboarding")
                {
                    return RenderOnboarding(args[1], args[2]);
                }

                if (args.Length >= 2 && args[0] == "--layout-report")
                {
                    using (MainForm form = new MainForm())
                    {
                        form.StartPosition = FormStartPosition.Manual;
                        form.Location = new Point(-30000, -30000);
                        form.ClientSize = new Size(1280, 820);
                        form.Show();
                        Application.DoEvents();
                        File.WriteAllText(args[1], form.HomeLayoutReport());
                        form.Close();
                    }
                    return 0;
                }

                if (args.Length >= 2 && args[0] == "--render-main-settings")
                {
                    using (MainForm form = new MainForm()) form.SaveSettingsPreview(args[1]);
                    return 0;
                }

                if (args.Length >= 2 && args[0] == "--render-free-practice")
                {
                    using (MainForm form = new MainForm()) form.SaveFreePracticePreview(args[1]);
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
                    return RenderStatistics(args[1], args.Length >= 3 ? args[2] : null);
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
                if (mode == "compact") form.ClientSize = new Size(1100, 720);
                else if (mode == "wide") form.Size = new Size(2560, 1600);
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

        private static int RenderOnboarding(string outputFile, string kind)
        {
            Form form;
            if (string.Equals(kind, "welcome", StringComparison.OrdinalIgnoreCase))
                form = new OnboardingWelcomeForm(false, 0, OnboardingController.StepCount);
            else if (string.Equals(kind, "replay-skip", StringComparison.OrdinalIgnoreCase))
                form = new OnboardingSkipForm(Keys.Alt | Keys.J, true);
            else if (string.Equals(kind, "skip-success", StringComparison.OrdinalIgnoreCase))
                form = new OnboardingSkipSuccessForm(false);
            else
            {
                OnboardingStep[] steps = OnboardingController.StepsForTests();
                int index = string.Equals(kind, "action", StringComparison.OrdinalIgnoreCase)
                    ? 6 : steps.Length - 1;
                form = new GuideCalloutForm(steps[index], index, steps.Length,
                    OnboardingController.ChapterCount, false,
                    string.IsNullOrEmpty(steps[index].RequiredAction));
            }
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

        private static int RenderStatistics(string outputFile, string mode)
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
                if (mode == "calendar" || mode == "calendar-small")
                    form.SelectCalendarForTests();
                if (mode == "calendar-small")
                {
                    form.MinimumSize = Size.Empty;
                    form.ClientSize = new Size(1280, 640);
                }
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
