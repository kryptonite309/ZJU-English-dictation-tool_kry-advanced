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
        public int dictationCorrectCount { get; set; }
        public int spellingCorrectCount { get; set; }
        public int errorCount { get; set; }
        public DateTime lastAnsweredAt { get; set; }
        public DateTime lastEditedAt { get; set; }
        public bool favorite { get; set; }
        public List<string> favoriteFolderIds { get; set; }
        public int problemDrawCount { get; set; }
        public int problemLastDrawRound { get; set; }
        public DateTime lastProblemAttemptAt { get; set; }
        public DateTime lastProblemCompletedAt { get; set; }
        public string lastProblemResult { get; set; }
    }

    internal sealed class FavoriteFolder
    {
        public string id { get; set; }
        public string name { get; set; }
        public override string ToString() { return name; }
    }

    internal sealed class NotebookState
    {
        public int version { get; set; }
        public bool reviewFirstLetter { get; set; }
        public int reviewCorrectTarget { get; set; }
        public int masteryShortcut { get; set; }
        public int problemFairRound { get; set; }
        public List<NotebookRecord> records { get; set; }
        public List<WordEntry> recent { get; set; }
        public List<FavoriteFolder> favoriteFolders { get; set; }
        public string defaultFavoriteFolderId { get; set; }
    }

    internal sealed class AnswerOutcome
    {
        public bool Correct { get; set; }
        public bool MovedToErrorProne { get; set; }
        public int CorrectCount { get; set; }
        public string CorrectAnswer { get; set; }
    }

    internal sealed class NotebookStore
    {
        private readonly string statePath;
        private readonly string legacyWrongPath;
        private readonly string migrationBackupPath;
        private readonly string projectRoot;
        private NotebookState state;

        public event EventHandler Changed;

        public NotebookStore(string projectRoot)
        {
            this.projectRoot = projectRoot;
            statePath = Path.Combine(projectRoot, "notebook_state.json");
            legacyWrongPath = Path.Combine(projectRoot, "wrong_words.json");
            migrationBackupPath = Path.Combine(projectRoot, "wrong_words.pre-migration.json");
            if (File.Exists(statePath)) Load();
            else MigrateLegacyWrongWords();
            bool changed = NormalizeFavorites();
            if (ConsolidateDuplicateWords()) changed = true;
            if (state.problemFairRound < 1) { state.problemFairRound = 1; changed = true; }
            if (EnrichPartsOfSpeech()) changed = true;
            if (changed) Save();
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

        public List<WordEntry> GetProblemWords()
        {
            return state.records.Where(x => x.notebook == Notebooks.Wrong
                    || x.notebook == Notebooks.ErrorProne)
                .Select(x => x.word).ToList();
        }

        public bool IsFavorite(WordEntry word)
        {
            NotebookRecord record = FindRecord(word);
            return record != null && record.favoriteFolderIds != null && record.favoriteFolderIds.Count > 0;
        }

        public IList<FavoriteFolder> FavoriteFolders { get { return state.favoriteFolders.AsReadOnly(); } }
        public string DefaultFavoriteFolderId { get { return state.defaultFavoriteFolderId; } }

        public FavoriteFolder GetFavoriteFolder(string id)
        { return state.favoriteFolders.FirstOrDefault(x => x.id == id); }

        public List<string> FavoriteFolderIds(WordEntry word)
        {
            NotebookRecord record = FindRecord(word);
            return record == null || record.favoriteFolderIds == null ? new List<string>()
                : new List<string>(record.favoriteFolderIds);
        }

        public string FavoriteFolderNames(WordEntry word)
        {
            List<string> ids = FavoriteFolderIds(word);
            return string.Join("、", state.favoriteFolders.Where(x => ids.Contains(x.id)).Select(x => x.name));
        }

        public List<WordEntry> GetFavoriteWords(string folderId = null)
        {
            return state.records.Where(x => x.favoriteFolderIds != null && (string.IsNullOrEmpty(folderId)
                ? x.favoriteFolderIds.Count > 0 : x.favoriteFolderIds.Contains(folderId)))
                .Select(x => x.word).ToList();
        }

        public bool ToggleFavorite(WordEntry word)
        {
            if (word == null || DataLoader.WordKey(word).Length == 0) return false;
            List<string> ids = FavoriteFolderIds(word);
            bool added = !ids.Contains(DefaultFavoriteFolderId);
            if (added) ids.Add(DefaultFavoriteFolderId); else ids.Remove(DefaultFavoriteFolderId);
            SetFavoriteFolders(word, ids);
            return added;
        }

        public void SetFavoriteFolders(WordEntry word, IEnumerable<string> folderIds)
        {
            if (word == null || DataLoader.WordKey(word).Length == 0) throw new ArgumentException("请选择一个单词。");
            List<string> ids = (folderIds ?? Enumerable.Empty<string>()).Distinct().ToList();
            if (ids.Any(x => GetFavoriteFolder(x) == null)) throw new ArgumentException("收藏夹已不存在，请重新选择。");
            NotebookRecord record = FindRecord(word);
            if (record == null)
            {
                record = new NotebookRecord { word = word, notebook = Notebooks.None };
                state.records.Add(record);
            }
            else record.word = DataLoader.MergeWordEntry(new[] { record.word, word });
            record.favoriteFolderIds = ids;
            record.favorite = ids.Count > 0;
            record.lastEditedAt = DateTime.Now;
            Save();
        }

        public void SetFavoriteMembership(WordEntry word, string folderId, bool included)
        {
            List<string> ids = FavoriteFolderIds(word);
            if (included && !ids.Contains(folderId)) ids.Add(folderId);
            if (!included) ids.Remove(folderId);
            SetFavoriteFolders(word, ids);
        }

        private string ValidateFavoriteName(string name, string exceptId = null)
        {
            name = (name ?? string.Empty).Trim();
            if (name.Length == 0 || name.Length > 60) throw new ArgumentException("收藏夹名称须为 1 至 60 个字符。");
            if (state.favoriteFolders.Any(x => x.id != exceptId
                && string.Equals(x.name, name, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("已存在同名收藏夹。");
            return name;
        }

        public FavoriteFolder CreateFavoriteFolder(string name)
        {
            name = ValidateFavoriteName(name);
            FavoriteFolder folder = new FavoriteFolder { id = Guid.NewGuid().ToString("N"), name = name };
            state.favoriteFolders.Add(folder); Save(); return folder;
        }

        public void RenameFavoriteFolder(string id, string name)
        {
            FavoriteFolder folder = GetFavoriteFolder(id);
            if (folder == null) throw new ArgumentException("收藏夹不存在。");
            folder.name = ValidateFavoriteName(name, id); Save();
        }

        public void SetDefaultFavoriteFolder(string id)
        {
            if (GetFavoriteFolder(id) == null) throw new ArgumentException("收藏夹不存在。");
            state.defaultFavoriteFolderId = id; Save();
        }

        public void DeleteFavoriteFolder(string id)
        {
            if (GetFavoriteFolder(id) == null) throw new ArgumentException("收藏夹不存在。");
            if (state.favoriteFolders.Count <= 1) throw new InvalidOperationException("请至少保留一个收藏夹。");
            state.favoriteFolders.RemoveAll(x => x.id == id);
            foreach (NotebookRecord record in state.records)
            {
                if (record.favoriteFolderIds == null || !record.favoriteFolderIds.Remove(id)) continue;
                record.favorite = record.favoriteFolderIds.Count > 0;
                record.lastEditedAt = DateTime.Now;
            }
            if (state.defaultFavoriteFolderId == id) state.defaultFavoriteFolderId = state.favoriteFolders[0].id;
            Save();
        }

        private bool NormalizeFavorites()
        {
            bool changed = false;
            if (state.favoriteFolders == null || state.favoriteFolders.Count == 0)
            {
                state.favoriteFolders = new List<FavoriteFolder> { new FavoriteFolder { id = "default", name = "默认收藏夹" } };
                changed = true;
            }
            if (GetFavoriteFolder(state.defaultFavoriteFolderId) == null)
            { state.defaultFavoriteFolderId = state.favoriteFolders[0].id; changed = true; }
            foreach (NotebookRecord record in state.records)
            {
                if (record.favoriteFolderIds == null)
                {
                    record.favoriteFolderIds = record.favorite ? new List<string> { state.defaultFavoriteFolderId }
                        : new List<string>();
                    changed = true;
                }
                List<string> valid = record.favoriteFolderIds.Where(id => GetFavoriteFolder(id) != null).Distinct().ToList();
                if (!valid.SequenceEqual(record.favoriteFolderIds)) changed = true;
                record.favoriteFolderIds = valid;
                if (record.favorite != (valid.Count > 0)) changed = true;
                record.favorite = valid.Count > 0;
            }
            return changed;
        }

        public List<WordEntry> SelectFairProblemWords(IEnumerable<WordEntry> eligible,
            int count, DateTime now)
        {
            List<NotebookRecord> records = (eligible ?? Enumerable.Empty<WordEntry>())
                .Select(FindRecord).Where(x => x != null && (x.notebook == Notebooks.Wrong
                    || x.notebook == Notebooks.ErrorProne))
                .GroupBy(x => DataLoader.WordKey(x.word), StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First()).ToList();
            if (records.Count == 0 || count <= 0) return new List<WordEntry>();
            if (state.problemFairRound < 1) state.problemFairRound = 1;

            List<NotebookRecord> selected = records
                .OrderBy(x => x.problemLastDrawRound >= state.problemFairRound ? 1 : 0)
                .ThenBy(x => x.problemDrawCount)
                .ThenBy(x => x.lastProblemCompletedAt != DateTime.MinValue
                    && x.lastProblemCompletedAt >= x.lastProblemAttemptAt ? 1 : 0)
                .ThenByDescending(x => x.errorCount)
                .ThenBy(x => x.lastProblemCompletedAt)
                .ThenBy(x => x.lastAnsweredAt)
                .ThenBy(x => DataLoader.WordKey(x.word), StringComparer.OrdinalIgnoreCase)
                .Take(count).ToList();
            foreach (NotebookRecord record in selected)
            {
                record.problemDrawCount++;
                record.problemLastDrawRound = state.problemFairRound;
                record.lastEditedAt = now;
            }
            if (records.All(x => x.problemLastDrawRound >= state.problemFairRound))
                state.problemFairRound++;
            Save();
            return selected.Select(x => x.word).ToList();
        }

        public void RecordProblemReviewActivity(WordEntry word, bool correct,
            bool completed, DateTime when)
        {
            NotebookRecord record = FindRecord(word);
            if (record == null) return;
            record.lastProblemAttemptAt = when;
            record.lastProblemResult = correct ? (completed ? "completed" : "correct") : "wrong";
            if (completed) record.lastProblemCompletedAt = when;
            record.lastEditedAt = when;
            Save();
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

        public DateTime GetLastEditedAt(WordEntry word)
        {
            NotebookRecord record = FindRecord(word);
            if (record == null) return DateTime.MinValue;
            return record.lastEditedAt == DateTime.MinValue
                ? record.lastAnsweredAt : record.lastEditedAt;
        }

        public string Snapshot()
        {
            return Serialize(state);
        }

        public void RestoreSnapshot(string json)
        {
            // Learning undo must not undo an independently edited favorite.
            List<NotebookRecord> favorites = state.records.Where(x => x.favorite).ToList();
            List<FavoriteFolder> folders = state.favoriteFolders;
            string defaultId = state.defaultFavoriteFolderId;
            NotebookState restored = new JavaScriptSerializer().Deserialize<NotebookState>(json);
            Validate(restored);
            state = restored;
            state.favoriteFolders = folders; state.defaultFavoriteFolderId = defaultId;
            ConsolidateDuplicateWords();
            foreach (NotebookRecord record in state.records)
            { record.favorite = false; record.favoriteFolderIds = new List<string>(); }
            foreach (NotebookRecord saved in favorites)
            {
                NotebookRecord record = FindRecord(saved.word);
                if (record == null)
                {
                    record = new NotebookRecord { word = saved.word, notebook = Notebooks.None };
                    state.records.Add(record);
                }
                record.favorite = true;
                record.favoriteFolderIds = new List<string>(saved.favoriteFolderIds);
            }
            EnrichPartsOfSpeech();
            Save();
        }

        public AnswerOutcome RecordStudyAnswer(WordEntry word, bool correct, string questionType,
            bool firstAttempt, int exampleTarget, int dictationTarget, int spellingTarget,
            DateTime answeredAt)
        {
            AnswerOutcome outcome = new AnswerOutcome { Correct = correct };
            NotebookRecord record = FindRecord(word);
            if (record == null)
            {
                record = new NotebookRecord { word = word, notebook = Notebooks.None };
                state.records.Add(record);
            }
            else record.word = DataLoader.MergeWordEntry(new[] { record.word, word });
            record.lastAnsweredAt = answeredAt;
            record.lastEditedAt = answeredAt;
            if (!correct)
            {
                record.errorCount++;
                record.notebook = Notebooks.Wrong;
                record.exampleCorrectCount = 0;
                record.dictationCorrectCount = 0;
                record.spellingCorrectCount = 0;
                record.correctCount = 0;
            }
            else if (firstAttempt && record.notebook == Notebooks.Wrong)
            {
                if (questionType == "example") record.exampleCorrectCount++;
                else if (questionType == "dictation") record.dictationCorrectCount++;
                else record.spellingCorrectCount++;
                record.correctCount = record.spellingCorrectCount;
                outcome.CorrectCount = record.spellingCorrectCount;
                if (record.exampleCorrectCount >= exampleTarget
                    && record.dictationCorrectCount >= dictationTarget
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
            if (record != null) record.word = DataLoader.MergeWordEntry(new[] { record.word, word });

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
                record.dictationCorrectCount = 0;
            }

            if (record != null)
            {
                record.lastAnsweredAt = DateTime.Now;
                record.lastEditedAt = record.lastAnsweredAt;
            }

            AddRecent(word);
            Save();
            return outcome;
        }

        public bool SkipWithoutPenalty(WordEntry word)
        {
            NotebookRecord record = FindRecord(word);
            bool removed = record != null && record.notebook == Notebooks.Wrong;
            if (removed)
            {
                record.notebook = Notebooks.None;
                record.correctCount = 0;
                record.exampleCorrectCount = 0;
                record.dictationCorrectCount = 0;
                record.spellingCorrectCount = 0;
                record.lastEditedAt = DateTime.Now;
            }
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
            if (record == null)
            {
                record = new NotebookRecord { word = word, notebook = Notebooks.None };
                state.records.Add(record);
            }
            else record.word = DataLoader.MergeWordEntry(new[] { record.word, word });
            if (record.notebook != notebook)
            {
                record.correctCount = 0;
                record.spellingCorrectCount = 0;
                record.exampleCorrectCount = 0;
                record.dictationCorrectCount = 0;
            }
            record.notebook = notebook;
            record.lastEditedAt = DateTime.Now;

            AddRecent(word);
            Save();
        }

        public void ClearWrongWords()
        {
            foreach (NotebookRecord record in state.records.Where(x =>
                x.notebook == Notebooks.Wrong && x.favorite))
            {
                record.notebook = Notebooks.None;
                record.correctCount = record.exampleCorrectCount = record.dictationCorrectCount
                    = record.spellingCorrectCount = 0;
                record.lastEditedAt = DateTime.Now;
            }
            state.records.RemoveAll(item => item.notebook == Notebooks.Wrong && !item.favorite);
            Save();
        }

        private NotebookRecord FindRecord(WordEntry word)
        {
            string key = DataLoader.WordKey(word);
            return key.Length == 0 ? null : state.records.FirstOrDefault(item =>
                DataLoader.WordKey(item.word) == key);
        }

        private void AddRecent(WordEntry word)
        {
            string key = DataLoader.WordKey(word);
            state.recent.RemoveAll(item => DataLoader.WordKey(item) == key);
            NotebookRecord record = FindRecord(word);
            state.recent.Insert(0, record == null ? word : record.word);
            if (state.recent.Count > 5) state.recent.RemoveRange(5, state.recent.Count - 5);
        }

        private bool ConsolidateDuplicateWords()
        {
            bool changed = false;
            List<NotebookRecord> merged = new List<NotebookRecord>();
            Dictionary<string, NotebookRecord> byEnglish = new Dictionary<string, NotebookRecord>(
                StringComparer.OrdinalIgnoreCase);
            foreach (NotebookRecord source in state.records)
            {
                if (source.lastEditedAt == DateTime.MinValue
                    && source.lastAnsweredAt != DateTime.MinValue) changed = true;
                string key = DataLoader.WordKey(source.word);
                NotebookRecord target;
                if (!byEnglish.TryGetValue(key, out target))
                {
                    target = new NotebookRecord
                    {
                        word = DataLoader.MergeWordEntry(new[] { source.word }),
                        favorite = source.favorite,
                        favoriteFolderIds = source.favoriteFolderIds == null ? null : new List<string>(source.favoriteFolderIds),
                        notebook = source.notebook,
                        correctCount = source.correctCount,
                        exampleCorrectCount = source.exampleCorrectCount,
                        dictationCorrectCount = source.dictationCorrectCount,
                        spellingCorrectCount = source.spellingCorrectCount,
                        errorCount = source.errorCount,
                        lastAnsweredAt = source.lastAnsweredAt,
                        problemDrawCount = source.problemDrawCount,
                        problemLastDrawRound = source.problemLastDrawRound,
                        lastProblemAttemptAt = source.lastProblemAttemptAt,
                        lastProblemCompletedAt = source.lastProblemCompletedAt,
                        lastProblemResult = source.lastProblemResult,
                        lastEditedAt = source.lastEditedAt == DateTime.MinValue
                            ? source.lastAnsweredAt : source.lastEditedAt
                    };
                    byEnglish[key] = target;
                    merged.Add(target);
                    continue;
                }
                changed = true;
                target.word = DataLoader.MergeWordEntry(new[] { target.word, source.word });
                target.favorite |= source.favorite;
                target.favoriteFolderIds = (target.favoriteFolderIds ?? new List<string>())
                    .Concat(source.favoriteFolderIds ?? new List<string>()).Distinct().ToList();
                if (NotebookPriority(source.notebook) > NotebookPriority(target.notebook))
                    target.notebook = source.notebook;
                target.correctCount = Math.Max(target.correctCount, source.correctCount);
                target.exampleCorrectCount = Math.Max(target.exampleCorrectCount, source.exampleCorrectCount);
                target.dictationCorrectCount = Math.Max(target.dictationCorrectCount, source.dictationCorrectCount);
                target.spellingCorrectCount = Math.Max(target.spellingCorrectCount, source.spellingCorrectCount);
                target.errorCount += source.errorCount;
                target.problemDrawCount = Math.Max(target.problemDrawCount, source.problemDrawCount);
                target.problemLastDrawRound = Math.Max(target.problemLastDrawRound,
                    source.problemLastDrawRound);
                if (source.lastProblemAttemptAt > target.lastProblemAttemptAt)
                {
                    target.lastProblemAttemptAt = source.lastProblemAttemptAt;
                    target.lastProblemResult = source.lastProblemResult;
                }
                if (source.lastProblemCompletedAt > target.lastProblemCompletedAt)
                    target.lastProblemCompletedAt = source.lastProblemCompletedAt;
                if (source.lastAnsweredAt > target.lastAnsweredAt) target.lastAnsweredAt = source.lastAnsweredAt;
                DateTime sourceEdited = source.lastEditedAt == DateTime.MinValue
                    ? source.lastAnsweredAt : source.lastEditedAt;
                if (sourceEdited > target.lastEditedAt) target.lastEditedAt = sourceEdited;
            }
            state.records = merged;

            List<WordEntry> recent = new List<WordEntry>();
            foreach (WordEntry word in state.recent.Where(x => x != null))
            {
                string key = DataLoader.WordKey(word);
                int existing = recent.FindIndex(x => DataLoader.WordKey(x) == key);
                if (existing < 0) recent.Add(word);
                else
                {
                    recent[existing] = DataLoader.MergeWordEntry(new[] { recent[existing], word });
                    changed = true;
                }
            }
            state.recent = recent.Take(5).ToList();
            return changed;
        }

        private bool EnrichPartsOfSpeech()
        {
            string data = Path.Combine(projectRoot, "data");
            if (!Directory.Exists(data)) return false;
            DataLoader loader = new DataLoader(data);
            Dictionary<string, string> parts = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (string book in loader.GetAvailableBooks())
                foreach (string unit in loader.GetUnitsForBook(book))
                    foreach (WordEntry word in loader.LoadWordList(book, new[] { unit }))
                    {
                        string key = DataLoader.WordKey(word);
                        string existing;
                        parts.TryGetValue(key, out existing);
                        parts[key] = PartOfSpeech.Merge(existing, word.partOfSpeech);
                    }

            bool changed = false;
            foreach (WordEntry word in state.records.Select(x => x.word)
                .Concat(state.recent).Where(x => x != null))
            {
                string incoming;
                if (!parts.TryGetValue(DataLoader.WordKey(word), out incoming)) continue;
                string merged = PartOfSpeech.IsPhrase(word.english)
                    ? string.Empty : PartOfSpeech.Merge(word.partOfSpeech, incoming);
                if (string.Equals(word.partOfSpeech ?? string.Empty, merged,
                    StringComparison.Ordinal)) continue;
                word.partOfSpeech = merged;
                changed = true;
            }
            return changed;
        }

        private static int NotebookPriority(string notebook)
        {
            if (notebook == Notebooks.Mastered) return 3;
            if (notebook == Notebooks.ErrorProne) return 2;
            if (notebook == Notebooks.Wrong) return 1;
            return 0;
        }

        private static NotebookState NewState()
        {
            return new NotebookState
            {
                version = 1,
                reviewFirstLetter = true,
                reviewCorrectTarget = 3,
                masteryShortcut = (int)(Keys.Alt | Keys.J),
                problemFairRound = 1,
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
            state.recent = state.recent.Where(item => item != null).Take(5).ToList();
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
