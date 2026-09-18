using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace EnglishDictationTool
{
    internal static class Notebooks
    {
        public const string None = "none";
        public const string Wrong = "wrong";
        public const string ErrorProne = "error_prone";
        public const string Mastered = "mastered";

        public static readonly string[] All = { None, Wrong, ErrorProne, Mastered };

        public static string DisplayName(string notebook)
        {
            switch (notebook)
            {
                case Wrong: return "错题本";
                case ErrorProne: return "易错本";
                case Mastered: return "已掌握";
                default: return "未归档";
            }
        }
    }

    internal sealed class NotebookRecord
    {
        public WordEntry word { get; set; }
        public string notebook { get; set; }
        public int correctCount { get; set; }
        public int exampleCorrectCount { get; set; }
        public int spellingCorrectCount { get; set; }
        public int errorCount { get; set; }
        public DateTime lastAnsweredAt { get; set; }
    }

    internal sealed class NotebookState
    {
        public int version { get; set; }
        public bool reviewFirstLetter { get; set; }
        public int reviewCorrectTarget { get; set; }
        public int masteryShortcut { get; set; }
        public List<NotebookRecord> records { get; set; }
        public List<WordEntry> recent { get; set; }
    }

    internal sealed class AnswerOutcome
    {
        public bool Correct { get; set; }
        public bool MovedToErrorProne { get; set; }
        public int CorrectCount { get; set; }
    }

    internal sealed class NotebookStore
    {
        private readonly string statePath;
        private readonly string legacyWrongPath;
        private readonly string migrationBackupPath;
        private NotebookState state;

        public event EventHandler Changed;

        public NotebookStore(string projectRoot)
        {
            statePath = Path.Combine(projectRoot, "notebook_state.json");
            legacyWrongPath = Path.Combine(projectRoot, "wrong_words.json");
            migrationBackupPath = Path.Combine(projectRoot, "wrong_words.pre-migration.json");
            if (File.Exists(statePath)) Load();
            else MigrateLegacyWrongWords();
        }

        public bool ReviewFirstLetter
        {
            get { return state.reviewFirstLetter; }
            set
            {
                if (state.reviewFirstLetter == value) return;
                state.reviewFirstLetter = value;
                Save();
            }
        }

        public int ReviewCorrectTarget
        {
            get { return state.reviewCorrectTarget; }
            set
            {
                if (value < 1 || value > 99) throw new ArgumentOutOfRangeException("value");
                if (state.reviewCorrectTarget == value) return;
                state.reviewCorrectTarget = value;
                Save();
            }
        }

        public Keys MasteryShortcut
        {
            get { return (Keys)state.masteryShortcut; }
            set
            {
                if (state.masteryShortcut == (int)value) return;
                state.masteryShortcut = (int)value;
                Save();
            }
        }

        public IList<WordEntry> RecentWords
        {
            get { return state.recent.AsReadOnly(); }
        }

        public IList<NotebookRecord> Records
        {
            get { return state.records.AsReadOnly(); }
        }

        public List<WordEntry> GetWords(string notebook)
        {
            return state.records.Where(item => item.notebook == notebook)
                .Select(item => item.word).ToList();
        }

        public int Count(string notebook)
        {
            return state.records.Count(item => item.notebook == notebook);
        }

        public string GetNotebook(WordEntry word)
        {
            NotebookRecord record = FindRecord(word);
            return record == null ? Notebooks.None : record.notebook;
        }

        public int GetCorrectCount(WordEntry word)
        {
            NotebookRecord record = FindRecord(word);
            return record == null ? 0 : record.correctCount;
        }

        public NotebookRecord GetRecord(WordEntry word)
        {
            return FindRecord(word);
        }

        public string Snapshot()
        {
            return Serialize(state);
        }

        public void RestoreSnapshot(string json)
        {
            NotebookState restored = new JavaScriptSerializer().Deserialize<NotebookState>(json);
            Validate(restored);
            state = restored;
            Save();
        }

        public AnswerOutcome RecordStudyAnswer(WordEntry word, bool correct, string questionType,
            bool firstAttempt, int exampleTarget, int spellingTarget, DateTime answeredAt)
        {
            AnswerOutcome outcome = new AnswerOutcome { Correct = correct };
            NotebookRecord record = FindRecord(word);
            if (record == null)
            {
                record = new NotebookRecord { word = word, notebook = Notebooks.None };
                state.records.Add(record);
            }
            record.lastAnsweredAt = answeredAt;
            if (!correct)
            {
                record.errorCount++;
                record.notebook = Notebooks.Wrong;
                record.exampleCorrectCount = 0;
                record.spellingCorrectCount = 0;
                record.correctCount = 0;
            }
            else if (firstAttempt && record.notebook == Notebooks.Wrong)
            {
                if (questionType == "example") record.exampleCorrectCount++;
                else record.spellingCorrectCount++;
                record.correctCount = record.spellingCorrectCount;
                outcome.CorrectCount = record.spellingCorrectCount;
                if (record.exampleCorrectCount >= exampleTarget
                    && record.spellingCorrectCount >= spellingTarget)
                {
                    record.notebook = Notebooks.ErrorProne;
                    outcome.MovedToErrorProne = true;
                }
            }
            AddRecent(word);
            Save();
            return outcome;
        }

        public AnswerOutcome RecordAnswer(WordEntry word, bool correct, bool isReview)
        {
            AnswerOutcome outcome = new AnswerOutcome { Correct = correct };
            NotebookRecord record = FindRecord(word);

            if (correct)
            {
                if (isReview && record != null && record.notebook == Notebooks.Wrong)
                {
                    record.correctCount++;
                    record.spellingCorrectCount = record.correctCount;
                    outcome.CorrectCount = record.correctCount;
                    if (record.correctCount >= state.reviewCorrectTarget)
                    {
                        record.notebook = Notebooks.ErrorProne;
                        record.correctCount = 0;
                        outcome.MovedToErrorProne = true;
                    }
                }
            }
            else
            {
                if (record == null)
                {
                    record = new NotebookRecord { word = word };
                    state.records.Add(record);
                }
                record.notebook = Notebooks.Wrong;
                record.correctCount = 0;
                record.errorCount++;
                record.spellingCorrectCount = 0;
                record.exampleCorrectCount = 0;
            }

            if (record != null) record.lastAnsweredAt = DateTime.Now;

            AddRecent(word);
            Save();
            return outcome;
        }

        public bool SkipWithoutPenalty(WordEntry word)
        {
            NotebookRecord record = FindRecord(word);
            bool removed = record != null && record.notebook == Notebooks.Wrong;
            if (removed) state.records.Remove(record);
            AddRecent(word);
            Save();
            return removed;
        }

        public void Master(WordEntry word)
        {
            MoveToNotebook(word, Notebooks.Mastered);
        }

        public void MoveToNotebook(WordEntry word, string notebook)
        {
            if (word == null) throw new ArgumentNullException("word");
            if (!Notebooks.All.Contains(notebook)) throw new ArgumentException("未知单词本", "notebook");

            NotebookRecord record = FindRecord(word);
            if (notebook == Notebooks.None)
            {
                if (record != null) state.records.Remove(record);
            }
            else
            {
                if (record == null)
                {
                    record = new NotebookRecord { word = word };
                    state.records.Add(record);
                }
                if (record.notebook != notebook) record.correctCount = 0;
                if (record.notebook != notebook)
                {
                    record.spellingCorrectCount = 0;
                    record.exampleCorrectCount = 0;
                }
                record.notebook = notebook;
            }

            AddRecent(word);
            Save();
        }

        public void ClearWrongWords()
        {
            state.records.RemoveAll(item => item.notebook == Notebooks.Wrong);
            Save();
        }

        private NotebookRecord FindRecord(WordEntry word)
        {
            return word == null ? null : state.records.FirstOrDefault(item => item.word.Equals(word));
        }

        private void AddRecent(WordEntry word)
        {
            state.recent.RemoveAll(item => item.Equals(word));
            state.recent.Insert(0, word);
            if (state.recent.Count > 5) state.recent.RemoveRange(5, state.recent.Count - 5);
        }

        private static NotebookState NewState()
        {
            return new NotebookState
            {
                version = 1,
                reviewFirstLetter = true,
                reviewCorrectTarget = 3,
                masteryShortcut = (int)(Keys.Control | Keys.Shift | Keys.M),
                records = new List<NotebookRecord>(),
                recent = new List<WordEntry>()
            };
        }

        private void MigrateLegacyWrongWords()
        {
            NotebookState migrated = NewState();
            if (File.Exists(legacyWrongPath))
            {
                string json = File.ReadAllText(legacyWrongPath, Encoding.UTF8);
                List<WordEntry> oldWords = new JavaScriptSerializer().Deserialize<List<WordEntry>>(json);
                if (oldWords == null) throw new InvalidDataException("旧错题本不是单词数组，迁移已停止。");
                foreach (WordEntry word in oldWords.Where(item => item != null).Distinct())
                {
                    migrated.records.Add(new NotebookRecord
                    {
                        word = word,
                        notebook = Notebooks.Wrong,
                        correctCount = 0
                    });
                }

                if (!File.Exists(migrationBackupPath)) File.Copy(legacyWrongPath, migrationBackupPath);
            }

            state = migrated;
            WriteAtomically(statePath, Serialize(state));
        }

        private void Load()
        {
            string json = File.ReadAllText(statePath, Encoding.UTF8);
            NotebookState loaded = new JavaScriptSerializer().Deserialize<NotebookState>(json);
            Validate(loaded);
            state = loaded;
            state.recent = state.recent.Where(item => item != null).Distinct().Take(5).ToList();
        }

        private static void Validate(NotebookState loaded)
        {
            if (loaded == null || loaded.version != 1 || loaded.records == null || loaded.recent == null)
                throw new InvalidDataException("单词本状态文件无效，未覆盖任何数据。");
            if (loaded.reviewCorrectTarget < 1 || loaded.reviewCorrectTarget > 99)
                throw new InvalidDataException("复习答对次数设置无效。");
            foreach (NotebookRecord record in loaded.records)
            {
                if (record == null || record.word == null || !Notebooks.All.Contains(record.notebook))
                    throw new InvalidDataException("单词本记录无效。");
            }
        }

        private void Save()
        {
            WriteAtomically(statePath, Serialize(state));
            // Keep the old format available to older builds and to external editors.
            WriteAtomically(legacyWrongPath, Serialize(GetWords(Notebooks.Wrong)));
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        private static string Serialize(object value)
        {
            return new JavaScriptSerializer().Serialize(value);
        }

        private static void WriteAtomically(string path, string content)
        {
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
    }
}
