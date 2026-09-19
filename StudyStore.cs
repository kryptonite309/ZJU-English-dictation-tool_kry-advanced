using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace EnglishDictationTool
{
    internal sealed class StudySettings
    {
        public int newCount { get; set; }
        public int listCount { get; set; }
        public int problemCount { get; set; }
        public bool randomExtraction { get; set; }
        public bool fuzzyAnswers { get; set; }
        public bool newExample { get; set; }
        public bool newSpelling { get; set; }
        public bool listExample { get; set; }
        public bool listSpelling { get; set; }
        public bool problemExample { get; set; }
        public bool problemSpelling { get; set; }
        public bool allowOverlap { get; set; }
        public bool reviewDueOnly { get; set; }
        public bool carryOverCountsInNewCount { get; set; }
        public bool carryOverPreview { get; set; }
        public int exampleCorrectTarget { get; set; }
        public int spellingCorrectTarget { get; set; }
        public int undoLimit { get; set; }
        public int autoBackupMinutes { get; set; }
        public int autoBackupKeep { get; set; }
        public int[] reviewDays { get; set; }
        public int previewKey { get; set; }
        public int undoKey { get; set; }
        public int manualBackupKey { get; set; }
        public Dictionary<string, int> defaultBookCounts { get; set; }

        public static StudySettings Defaults()
        {
            return new StudySettings
            {
                newCount = 20, listCount = 4, problemCount = 15,
                randomExtraction = false, fuzzyAnswers = false,
                newExample = false, newSpelling = true,
                listExample = false, listSpelling = true,
                problemExample = false, problemSpelling = true,
                allowOverlap = true, reviewDueOnly = false, carryOverCountsInNewCount = true,
                carryOverPreview = true,
                exampleCorrectTarget = 0, spellingCorrectTarget = 3,
                undoLimit = 5, autoBackupMinutes = 10, autoBackupKeep = 6,
                reviewDays = new[] { 1, 3, 7, 14 },
                previewKey = (int)System.Windows.Forms.Keys.Enter,
                undoKey = (int)(System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Z),
                manualBackupKey = (int)(System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift | System.Windows.Forms.Keys.B),
                defaultBookCounts = new Dictionary<string, int>()
            };
        }
    }

    internal sealed class StudyWord
    {
        public WordEntry word { get; set; }
        public string book { get; set; }
        public string unit { get; set; }
        public DateTime firstExtractedAt { get; set; }
        public DateTime priorityAt { get; set; }
        public List<string> acceptedAnswers { get; set; }
    }

    internal sealed class StudyItem
    {
        public WordEntry word { get; set; }
        public bool released { get; set; }
        public bool mastered { get; set; }
        public bool exampleComplete { get; set; }
        public bool spellingComplete { get; set; }
        public DateTime firstAnsweredAt { get; set; }
        public bool carriedOver { get; set; }
    }

    internal sealed class StudyTask
    {
        public WordEntry word { get; set; }
        public string mode { get; set; }
        public bool replay { get; set; }
        public int attempt { get; set; }
        public DateTime answeredAt { get; set; }
        public bool correct { get; set; }
        public string submittedAnswer { get; set; }
        public bool skipped { get; set; }
        public bool mastered { get; set; }
    }

    internal sealed class StudyList
    {
        public string id { get; set; }
        public string kind { get; set; }
        public DateTime createdAt { get; set; }
        public string studyDate { get; set; }
        public string status { get; set; }
        public string phase { get; set; }
        public List<string> sourceListIds { get; set; }
        public List<StudyItem> items { get; set; }
        public int previewCursor { get; set; }
        public int previewStage { get; set; }
        public List<StudyTask> tasks { get; set; }
        public int taskCursor { get; set; }
        public List<StudyTask> retries { get; set; }
        public List<StudyTask> history { get; set; }
    }

    internal sealed class StudyUndo
    {
        public string stateBefore { get; set; }
        public string notebooksBefore { get; set; }
        public WordEntry word { get; set; }
        public string mode { get; set; }
        public string listId { get; set; }
    }

    internal sealed class StudyEndResult
    {
        public string kind { get; set; }
        public int kept { get; set; }
        public int released { get; set; }
    }

    internal sealed class StudyState
    {
        public int version { get; set; }
        public StudySettings settings { get; set; }
        public List<StudyWord> words { get; set; }
        public List<StudyList> lists { get; set; }
        public List<WordEntry> priorityWords { get; set; }
        public List<StudyUndo> undo { get; set; }
        public string activeListId { get; set; }
    }

    internal sealed class StudyStore
    {
        private readonly string root;
        private readonly string statePath;
        private readonly DataLoader loader;
        private readonly NotebookStore notebooks;
        private readonly Random random = new Random();
        private StudyState state;
        public event EventHandler Changed;

        public StudyStore(string projectRoot, DataLoader dataLoader, NotebookStore notebookStore)
        {
            root = projectRoot;
            statePath = Path.Combine(root, "study_state.json");
            loader = dataLoader;
            notebooks = notebookStore;
            if (File.Exists(statePath))
            {
                state = Deserialize<StudyState>(File.ReadAllText(statePath, Encoding.UTF8));
                if (state == null || state.version != 1 || state.settings == null || state.words == null
                    || state.lists == null || state.priorityWords == null || state.undo == null)
                    throw new InvalidDataException("学习状态文件无效，未覆盖数据。请从备份恢复。");
                foreach (StudyList list in state.lists)
                {
                    if (list.history != null) continue;
                    list.history = new List<StudyTask>();
                    if (list.tasks != null)
                        foreach (StudyTask task in list.tasks.Take(Math.Min(list.taskCursor, list.tasks.Count)))
                            if (task.answeredAt != DateTime.MinValue) list.history.Add(task);
                }
            }
            else
            {
                state = new StudyState
                {
                    version = 1, settings = StudySettings.Defaults(),
                    words = new List<StudyWord>(), lists = new List<StudyList>(),
                    priorityWords = new List<WordEntry>(), undo = new List<StudyUndo>()
                };
                Save();
            }
            SettleCrossDay(DateTime.Now);
        }

        public StudySettings Settings { get { return state.settings; } }
        public void UpdateSettings(StudySettings settings)
        {
            StudySettings previous = state.settings;
            state.settings = settings;
            try { SaveSettings(); }
            catch { state.settings = previous; throw; }
        }
        public IList<StudyList> Lists { get { return state.lists.AsReadOnly(); } }
        public StudyList Active { get { return state.lists.FirstOrDefault(x => x.id == state.activeListId
            && x.status == "active"); } }
        public StudyList ActiveFor(string kind) { return state.lists.FirstOrDefault(x => x.kind == kind
            && x.status == "active"); }
        public bool HasAnyActive { get { return state.lists.Any(x => x.status == "active"); } }
        public int UndoCount { get { return state.undo.Count; } }
        public static DateTime StudyDay(DateTime when)
        {
            return when.TimeOfDay < new TimeSpan(4, 1, 0) ? when.Date.AddDays(-1) : when.Date;
        }
        public static string StudyDayKey(DateTime when)
        {
            return StudyDay(when).ToString("yyyy-MM-dd");
        }

        public void SaveSettings()
        {
            if (state.settings.newCount < 0 || state.settings.listCount < 0 || state.settings.problemCount < 0
                || state.settings.undoLimit < 0 || state.settings.undoLimit > 5
                || state.settings.autoBackupMinutes < 1 || state.settings.autoBackupKeep < 1
                || state.settings.exampleCorrectTarget < 0 || state.settings.spellingCorrectTarget < 0)
                throw new ArgumentOutOfRangeException("settings", "学习设置包含超出范围的值。");
            if (state.settings.reviewDays == null || state.settings.reviewDays.Length == 0
                || state.settings.reviewDays.Any(x => x < 0))
                throw new ArgumentOutOfRangeException("settings", "复习间隔天数无效。");
            if ((!state.settings.newExample && !state.settings.newSpelling)
                || (!state.settings.listExample && !state.settings.listSpelling)
                || (!state.settings.problemExample && !state.settings.problemSpelling))
                throw new ArgumentException("三个答题部分各至少启用例句或拼写之一。");
            if (state.settings.defaultBookCounts == null) state.settings.defaultBookCounts = new Dictionary<string, int>();
            if (state.undo.Count > state.settings.undoLimit)
                state.undo.RemoveRange(0, state.undo.Count - state.settings.undoLimit);
            Save();
        }

        public List<StudyWord> AllWords()
        {
            List<StudyWord> result = new List<StudyWord>();
            foreach (string book in loader.GetAvailableBooks())
            {
                foreach (string unit in loader.GetUnitsForBook(book))
                {
                    foreach (WordEntry word in loader.LoadWordList(book, new[] { unit }))
                    {
                        StudyWord existing = FindWord(word);
                        if (existing == null)
                        {
                            existing = new StudyWord
                            {
                                word = word, book = book, unit = unit,
                                acceptedAnswers = new List<string>()
                            };
                        }
                        else if (string.IsNullOrEmpty(existing.book))
                        {
                            existing.book = book;
                            existing.unit = unit;
                        }
                        if (!result.Any(x => x.word.Equals(word))) result.Add(existing);
                    }
                }
            }
            return result;
        }

        public void RenameBookReferences(string oldName, string newName)
        {
            foreach (StudyWord word in state.words.Where(x => string.Equals(x.book, oldName,
                StringComparison.OrdinalIgnoreCase))) word.book = newName;
            if (state.settings.defaultBookCounts != null)
            {
                KeyValuePair<string, int> prior = state.settings.defaultBookCounts.FirstOrDefault(x =>
                    string.Equals(x.Key, oldName, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(prior.Key))
                {
                    state.settings.defaultBookCounts.Remove(prior.Key);
                    state.settings.defaultBookCounts[newName] = prior.Value;
                }
            }
            Save();
        }

        public Dictionary<string, int> AvailableCounts()
        {
            return AllWords().Where(x => x.firstExtractedAt == DateTime.MinValue
                    && notebooks.GetNotebook(x.word) != Notebooks.Mastered)
                .GroupBy(x => x.book).ToDictionary(x => x.Key, x => x.Count());
        }

        public StudyWord FindWord(WordEntry word)
        {
            return state.words.FirstOrDefault(x => x.word != null && x.word.Equals(word));
        }

        private StudyWord EnsureWord(StudyWord source)
        {
            StudyWord known = FindWord(source.word);
            if (known != null) return known;
            known = new StudyWord
            {
                word = source.word, book = source.book, unit = source.unit,
                acceptedAnswers = new List<string>()
            };
            state.words.Add(known);
            return known;
        }

        public void SetAcceptedAnswers(WordEntry word, IEnumerable<string> answers)
        {
            StudyWord record = FindWord(word) ?? EnsureWord(new StudyWord { word = word });
            record.acceptedAnswers = answers.Select(NormalizeAnswer).Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            Save();
        }

        public void CaptureExternalUndo(WordEntry word)
        {
            if (word == null) return;
            RecordUndo(word, "legacy");
            Save();
        }

        public static string NormalizeAnswer(string value)
        {
            return System.Text.RegularExpressions.Regex.Replace(
                DataLoader.Sanitize(value), @"\s+", " ").ToLowerInvariant();
        }

        public List<string> AcceptedAnswers(WordEntry word)
        {
            string original = DataLoader.Sanitize(word == null ? string.Empty : word.english);
            if (!state.settings.fuzzyAnswers) return new List<string> { NormalizeAnswer(original) };
            List<string> answers = new List<string> { NormalizeAnswer(original) };
            string[] choices = original.Split('/');
            if (choices.Length > 1)
            {
                string lead = choices[0].Trim();
                answers.Add(NormalizeAnswer(lead));
                for (int i = 1; i < choices.Length; i++)
                {
                    string variant = choices[i].Trim();
                    if (variant.Length == 0) continue;
                    answers.Add(NormalizeAnswer(variant));
                    string[] leadWords = lead.Split(' ');
                    if (leadWords.Length > 1 && !variant.Contains(" "))
                        answers.Add(NormalizeAnswer(string.Join(" ", leadWords.Take(leadWords.Length - 1)) + " " + variant));
                    string[] variantWords = variant.Split(' ');
                    if (leadWords.Length == 1 && variantWords.Length > 1)
                        answers.Add(NormalizeAnswer(lead + " " +
                            string.Join(" ", variantWords.Skip(1))));
                }
            }
            StudyWord record = FindWord(word);
            if (record != null && record.acceptedAnswers != null) answers.AddRange(record.acceptedAnswers);
            return answers.Where(x => x.Length > 0).Distinct().ToList();
        }

        public bool IsCorrect(WordEntry word, string input)
        {
            return AcceptedAnswers(word).Contains(NormalizeAnswer(input));
        }

        public StudyList ExtractNew(Dictionary<string, int> quotas, DateTime now)
        {
            SettleCrossDay(now);
            if (ActiveFor("new") != null)
                throw new InvalidOperationException("存在未完成列表，请先继续完成。");
            if (quotas == null || quotas.Count == 0 || quotas.Any(x => x.Value < 0))
                throw new ArgumentException("请选择至少一本词书及抽取数量。", "quotas");
            List<StudyWord> pool = AllWords();
            List<StudyWord> selected = new List<StudyWord>();
            foreach (KeyValuePair<string, int> quota in quotas)
            {
                List<StudyWord> available = pool.Where(x => x.book == quota.Key
                    && x.firstExtractedAt == DateTime.MinValue
                    && notebooks.GetNotebook(x.word) != Notebooks.Mastered
                    && !selected.Any(y => y.word.Equals(x.word))).ToList();
                List<StudyWord> priority = state.priorityWords.Select(word => available.FirstOrDefault(x => x.word.Equals(word)))
                    .Where(x => x != null).ToList();
                List<StudyWord> ordinary = available.Where(x => !priority.Any(y => y.word.Equals(x.word))).ToList();
                if (state.settings.randomExtraction) Shuffle(ordinary);
                selected.AddRange(state.settings.carryOverCountsInNewCount
                    ? priority.Concat(ordinary).Take(quota.Value)
                    : priority.Concat(ordinary.Take(quota.Value)));
            }
            if (selected.Count == 0) throw new InvalidOperationException("所选范围内没有待抽取单词。");
            StudyList list = NewList("new", now, selected.Select(x => x.word).ToList());
            list.phase = "preview";
            foreach (StudyItem item in list.items)
            {
                StudyWord record = EnsureWord(selected.First(x => x.word.Equals(item.word)));
                item.carriedOver = state.priorityWords.Any(x => x.Equals(item.word));
                record.firstExtractedAt = now;
                record.priorityAt = DateTime.MinValue;
                state.priorityWords.RemoveAll(x => x.Equals(item.word));
            }
            state.activeListId = list.id;
            Save();
            return list;
        }

        public StudyList StartListReview(DateTime now)
        {
            SettleCrossDay(now);
            RequireNoActive("list_review");
            string date = StudyDayKey(now);
            List<WordEntry> already = state.lists.Where(x => x.kind == "list_review"
                && x.studyDate == date).SelectMany(x => x.items.Where(y => !y.released)
                    .Select(y => y.word)).ToList();
            List<StudyList> sources = state.lists.Where(x => x.kind == "new"
                && string.CompareOrdinal(x.studyDate, date) < 0
                && x.status != "active"
                && x.items.Any(y => !y.released && !y.mastered
                    && notebooks.GetNotebook(y.word) != Notebooks.Mastered
                    && !already.Any(done => done.Equals(y.word))))
                .OrderByDescending(x => x.createdAt).Take(state.settings.listCount)
                .OrderBy(x => x.createdAt).ToList();
            if (sources.Count == 0) throw new InvalidOperationException("没有新的历史提取列表可复习。");
            List<WordEntry> words = sources.SelectMany(x => x.items.Where(y => !y.released && !y.mastered)
                .Select(y => y.word)).Where(x => notebooks.GetNotebook(x) != Notebooks.Mastered
                    && !already.Any(done => done.Equals(x))).ToList();
            StudyList list = NewList("list_review", now, words);
            list.sourceListIds = sources.Select(x => x.id).ToList();
            PrepareTasks(list);
            state.activeListId = list.id;
            Save();
            return list;
        }

        public StudyList StartProblemReview(DateTime now)
        {
            SettleCrossDay(now);
            RequireNoActive("problem_review");
            string date = StudyDayKey(now);
            List<WordEntry> already = state.lists.Where(x => x.kind == "problem_review" && x.studyDate == date)
                .SelectMany(x => x.items.Select(y => y.word)).ToList();
            List<WordEntry> overlap = state.settings.allowOverlap ? new List<WordEntry>() :
                state.lists.Where(x => x.kind == "list_review" && x.studyDate == date)
                .SelectMany(x => x.items.Select(y => y.word)).ToList();
            List<WordEntry> candidates = notebooks.Records.Where(x =>
                    x.notebook == Notebooks.Wrong || x.notebook == Notebooks.ErrorProne)
                .Where(x => !already.Any(y => y.Equals(x.word)) && !overlap.Any(y => y.Equals(x.word)))
                .Where(x => !state.settings.reviewDueOnly || DueDate(x) <= now)
                .OrderBy(x => x.lastAnsweredAt).ThenByDescending(x => x.errorCount)
                .Select(x => x.word)
                .Take(state.settings.problemCount).ToList();
            if (candidates.Count == 0) throw new InvalidOperationException("没有新的错题或易错词可复习。");
            StudyList list = NewList("problem_review", now, candidates);
            PrepareTasks(list);
            state.activeListId = list.id;
            Save();
            return list;
        }

        private DateTime DueDate(NotebookRecord record)
        {
            if (record.lastAnsweredAt == DateTime.MinValue) return DateTime.MinValue;
            int[] days = state.settings.reviewDays ?? new[] { 1 };
            int count = record.notebook == Notebooks.Wrong ? record.spellingCorrectCount : days.Length - 1;
            return record.lastAnsweredAt.Date.AddDays(days[Math.Min(Math.Max(count, 0), days.Length - 1)]);
        }

        private StudyList NewList(string kind, DateTime now, List<WordEntry> words)
        {
            string baseId = now.ToString("yyyyMMdd_HHmmss");
            string id = baseId;
            int suffix = 2;
            while (state.lists.Any(x => x.id == id)) id = baseId + "_" + suffix++;
            StudyList list = new StudyList
            {
                id = id, kind = kind, createdAt = now, studyDate = StudyDayKey(now),
                status = "active", phase = "quiz", sourceListIds = new List<string>(),
                items = words.Select(x => new StudyItem { word = x }).ToList(),
                tasks = new List<StudyTask>(), retries = new List<StudyTask>(), history = new List<StudyTask>()
            };
            state.lists.Add(list);
            return list;
        }

        private void RequireNoActive(string kind)
        {
            if (ActiveFor(kind) != null)
                throw new InvalidOperationException("存在未完成列表，请先继续完成。");
        }

        public StudyList Resume(string kind)
        {
            SettleCrossDay(DateTime.Now);
            StudyList list = ActiveFor(kind);
            if (list == null) return null;
            state.activeListId = list.id;
            // Undo snapshots contain the whole study state. Keep them scoped to the
            // selected module so switching between independent sessions cannot roll
            // another module back accidentally.
            state.undo.RemoveAll(x => x.listId != list.id);
            if (list.phase == "quiz") RebuildPendingTasks(list);
            Save();
            return Active;
        }

        public StudyItem CurrentPreview()
        {
            StudyList list = Active;
            if (list == null || list.phase != "preview") return null;
            while (list.previewCursor < list.items.Count &&
                (list.items[list.previewCursor].mastered || list.items[list.previewCursor].released))
                list.previewCursor++;
            return list.previewCursor < list.items.Count ? list.items[list.previewCursor] : null;
        }

        public void AdvancePreview()
        {
            SettleCrossDay(DateTime.Now);
            StudyList list = Active;
            if (list == null || list.phase != "preview") return;
            if (CurrentPreview() == null) { PrepareTasks(list); Save(); return; }
            if (list.items[list.previewCursor].carriedOver && !state.settings.carryOverPreview)
            { list.previewStage = 0; list.previewCursor++; }
            else if (list.previewStage < 2) list.previewStage++;
            else { list.previewStage = 0; list.previewCursor++; }
            if (CurrentPreview() == null) PrepareTasks(list);
            Save();
        }

        private void PrepareTasks(StudyList list)
        {
            list.phase = "quiz";
            RebuildPendingTasks(list);
        }

        private void RebuildPendingTasks(StudyList list)
        {
            list.tasks.Clear();
            list.retries.Clear();
            list.taskCursor = 0;
            bool example = list.kind == "new" ? state.settings.newExample :
                list.kind == "list_review" ? state.settings.listExample : state.settings.problemExample;
            bool spelling = list.kind == "new" ? state.settings.newSpelling :
                list.kind == "list_review" ? state.settings.listSpelling : state.settings.problemSpelling;
            foreach (StudyItem item in list.items)
            {
                if (item.mastered || item.released || notebooks.GetNotebook(item.word) == Notebooks.Mastered) continue;
                List<StudyTask> itemHistory = (list.history ?? new List<StudyTask>())
                    .Where(x => x.word.Equals(item.word)).ToList();
                bool exampleDone = itemHistory.Any(x => x.mode == "example"
                    && (x.correct || x.skipped || x.mastered));
                bool spellingDone = itemHistory.Any(x => x.mode == "spelling"
                    && (x.correct || x.mastered));
                item.exampleComplete = !example || exampleDone;
                item.spellingComplete = !spelling || spellingDone;
                if (example && !exampleDone)
                {
                    List<StudyTask> attempts = itemHistory.Where(x => x.mode == "example").ToList();
                    list.tasks.Add(new StudyTask { word = item.word, mode = "example",
                        replay = attempts.Any(x => !x.correct && !x.skipped), attempt = attempts.Count });
                }
                if (spelling && !spellingDone)
                {
                    List<StudyTask> attempts = itemHistory.Where(x => x.mode == "spelling").ToList();
                    list.tasks.Add(new StudyTask { word = item.word, mode = "spelling",
                        replay = attempts.Any(x => !x.correct), attempt = attempts.Count });
                }
            }
            if (list.tasks.Count == 0) Finish(list);
        }

        public StudyEndResult EndActive(string kind, DateTime now)
        {
            SettleCrossDay(now);
            StudyList list = ActiveFor(kind);
            if (list == null) throw new InvalidOperationException("当前部分没有未完成列表。");
            bool example = list.kind == "new" ? state.settings.newExample :
                list.kind == "list_review" ? state.settings.listExample : state.settings.problemExample;
            bool spelling = list.kind == "new" ? state.settings.newSpelling :
                list.kind == "list_review" ? state.settings.listSpelling : state.settings.problemSpelling;
            List<StudyItem> kept = new List<StudyItem>();
            List<StudyItem> released = new List<StudyItem>();
            for (int index = 0; index < list.items.Count; index++)
            {
                StudyItem item = list.items[index];
                bool mastered = item.mastered || notebooks.GetNotebook(item.word) == Notebooks.Mastered;
                bool previewed = list.kind != "new" || list.phase != "preview" || index < list.previewCursor;
                List<StudyTask> history = (list.history ?? new List<StudyTask>())
                    .Where(x => x.word.Equals(item.word)).ToList();
                bool exampleDone = !example || history.Any(x => x.mode == "example"
                    && (x.correct || x.skipped || x.mastered));
                bool spellingDone = !spelling || history.Any(x => x.mode == "spelling"
                    && (x.correct || x.mastered));
                if (mastered || (previewed && exampleDone && spellingDone)) kept.Add(item);
                else released.Add(item);
            }
            if (list.kind == "new")
            {
                List<WordEntry> priority = new List<WordEntry>();
                foreach (StudyItem item in released)
                {
                    StudyWord word = FindWord(item.word);
                    if (word == null) word = EnsureWord(new StudyWord { word = item.word });
                    word.firstExtractedAt = DateTime.MinValue;
                    word.priorityAt = now;
                    if (!priority.Any(x => x.Equals(item.word))) priority.Add(item.word);
                }
                state.priorityWords.RemoveAll(x => priority.Any(y => y.Equals(x)));
                state.priorityWords.InsertRange(0, priority);
            }
            list.items = kept;
            list.history = (list.history ?? new List<StudyTask>()).Where(x =>
                !released.Any(item => item.word.Equals(x.word))).ToList();
            list.tasks.Clear();
            list.retries.Clear();
            list.taskCursor = 0;
            list.status = "ended";
            list.phase = "done";
            if (state.activeListId == list.id) state.activeListId = null;
            state.undo.RemoveAll(x => x.listId == list.id);
            Save();
            return new StudyEndResult { kind = kind, kept = kept.Count, released = released.Count };
        }

        public StudyTask CurrentTask()
        {
            SettleCrossDay(DateTime.Now);
            StudyList list = Active;
            if (list == null || list.phase != "quiz" || list.status != "active") return null;
            while (true)
            {
                while (list.taskCursor < list.tasks.Count)
                {
                    StudyTask task = list.tasks[list.taskCursor];
                    if (notebooks.GetNotebook(task.word) == Notebooks.Mastered)
                    {
                        StudyItem item = list.items.First(x => x.word.Equals(task.word));
                        item.mastered = true;
                        list.taskCursor++;
                        continue;
                    }
                    return task;
                }
                if (list.retries.Count == 0)
                {
                    Finish(list);
                    Save();
                    return null;
                }
                list.tasks = list.retries;
                list.retries = new List<StudyTask>();
                list.taskCursor = 0;
            }
        }

        public string SkipMissingExample()
        {
            StudyTask task = CurrentTask();
            if (task == null || task.mode != "example") return null;
            if (!string.IsNullOrWhiteSpace(task.word.examples) && task.word.examples.Contains("[[")) return null;
            StudyItem item = Active.items.First(x => x.word.Equals(task.word));
            item.exampleComplete = true;
            task.skipped = true;
            task.answeredAt = DateTime.Now;
            Active.history.Add(CopyHistoryTask(task));
            Active.taskCursor++;
            Save();
            return "该单词暂时没有例句";
        }

        public AnswerOutcome Submit(string input, DateTime now)
        {
            SettleCrossDay(now);
            StudyList list = Active;
            StudyTask task = CurrentTask();
            if (task == null) throw new InvalidOperationException("当前没有题目。");
            RecordUndo(task.word, task.mode);
            bool correct = IsCorrect(task.word, input);
            bool first = !task.replay && task.attempt == 0;
            AnswerOutcome result = notebooks.RecordStudyAnswer(task.word, correct, task.mode, first,
                state.settings.exampleCorrectTarget, state.settings.spellingCorrectTarget, now);
            task.attempt++;
            task.answeredAt = now;
            task.correct = correct;
            task.submittedAnswer = input;
            list.history.Add(CopyHistoryTask(task));
            StudyItem item = list.items.First(x => x.word.Equals(task.word));
            if (item.firstAnsweredAt == DateTime.MinValue) item.firstAnsweredAt = now;
            if (correct)
            {
                if (task.mode == "example") item.exampleComplete = true;
                else item.spellingComplete = true;
            }
            else
            {
                list.retries.Add(new StudyTask { word = task.word, mode = task.mode, replay = true });
            }
            list.taskCursor++;
            CurrentTask();
            Save();
            return result;
        }

        public WordEntry MasterCurrent(DateTime now)
        {
            SettleCrossDay(now);
            StudyList list = Active;
            if (list == null) return null;
            StudyTask question = list.phase == "quiz" ? CurrentTask() : null;
            WordEntry word = list.phase == "preview" ? (CurrentPreview() == null ? null : CurrentPreview().word)
                : (question == null ? null : question.word);
            if (word == null) return null;
            RecordUndo(word, "master:" + (question == null ? "preview" : question.mode));
            notebooks.Master(word);
            foreach (StudyList other in state.lists)
                foreach (StudyItem item in other.items.Where(x => x.word.Equals(word))) item.mastered = true;
            if (list.phase == "preview")
            {
                list.previewStage = 0;
                list.previewCursor++;
                if (CurrentPreview() == null) PrepareTasks(list);
            }
            else
            {
                question.mastered = true;
                question.answeredAt = now;
                list.history.Add(CopyHistoryTask(question));
                list.retries.RemoveAll(x => x.word.Equals(word));
                list.taskCursor++;
                CurrentTask();
            }
            Save();
            return word;
        }

        private static StudyTask CopyHistoryTask(StudyTask task)
        {
            return new StudyTask { word = task.word, mode = task.mode, replay = task.replay,
                attempt = task.attempt, answeredAt = task.answeredAt, correct = task.correct,
                submittedAnswer = task.submittedAnswer, skipped = task.skipped, mastered = task.mastered };
        }

        private void RecordUndo(WordEntry word, string mode)
        {
            if (state.settings.undoLimit <= 0) return;
            List<StudyUndo> prior = state.undo;
            state.undo = new List<StudyUndo>();
            string snapshot = Serialize(state);
            state.undo = prior;
            state.undo.Add(new StudyUndo
            {
                stateBefore = snapshot, notebooksBefore = notebooks.Snapshot(),
                word = word, mode = mode, listId = state.activeListId
            });
            if (state.undo.Count > state.settings.undoLimit) state.undo.RemoveAt(0);
        }

        public WordEntry Undo()
        {
            if (state.undo.Count == 0) return null;
            StudyUndo frame = state.undo[state.undo.Count - 1];
            StudyList settled = state.lists.FirstOrDefault(x => x.id == frame.listId && x.status == "settled");
            if (settled != null)
            {
                // Crossing the 04:01 boundary is irreversible for list membership: undo may
                // restore the word, but it must never reopen yesterday's unfinished session.
                StudyState older = Deserialize<StudyState>(frame.stateBefore);
                StudyList oldList = older.lists.FirstOrDefault(x => x.id == frame.listId);
                StudyItem prior = oldList == null ? null : oldList.items.FirstOrDefault(x => x.word.Equals(frame.word));
                StudyItem item = settled.items.FirstOrDefault(x => x.word.Equals(frame.word));
                if (prior != null && item != null)
                {
                    item.exampleComplete = prior.exampleComplete;
                    item.spellingComplete = prior.spellingComplete;
                    item.firstAnsweredAt = prior.firstAnsweredAt;
                    item.mastered = prior.mastered;
                    item.released = !prior.mastered && !(prior.exampleComplete && prior.spellingComplete);
                    state.priorityWords.RemoveAll(x => x.Equals(frame.word));
                    StudyWord word = FindWord(frame.word);
                    if (item.released)
                    {
                        state.priorityWords.Insert(0, frame.word);
                        if (word != null) word.firstExtractedAt = DateTime.MinValue;
                    }
                }
                state.undo.RemoveAt(state.undo.Count - 1);
                notebooks.RestoreSnapshot(frame.notebooksBefore);
                Save();
                return frame.word;
            }
            StudyList after = Active;
            StudyTask current = after == null ? null : CurrentTask();
            StudyItem preview = after == null ? null : CurrentPreview();
            StudyState before = Deserialize<StudyState>(frame.stateBefore);
            before.undo = state.undo.Take(state.undo.Count - 1).ToList();
            before.settings = state.settings;
            StudyList target = before.lists.FirstOrDefault(x => x.id == frame.listId);
            if (target != null && after != null && after.id == target.id)
            {
                if (after.phase == "quiz" &&
                    (target.phase == "quiz" || target.phase == "preview"))
                {
                    target.tasks = Deserialize<List<StudyTask>>(Serialize(after.tasks));
                    target.retries = Deserialize<List<StudyTask>>(Serialize(after.retries));
                    target.taskCursor = after.taskCursor;
                    bool wasMaster = frame.mode.StartsWith("master:");
                    string taskMode = wasMaster ? frame.mode.Substring("master:".Length) : frame.mode;
                    target.retries.RemoveAll(x => x.word.Equals(frame.word)
                        && (wasMaster || x.mode == taskMode));
                    int insert = current == null ? target.taskCursor : target.taskCursor + 1;
                    insert = Math.Min(insert, target.tasks.Count);
                    if (wasMaster)
                    {
                        StudyItem original = target.items.FirstOrDefault(x => x.word.Equals(frame.word));
                        if (original != null && !original.exampleComplete &&
                            (target.kind == "new" ? state.settings.newExample :
                            target.kind == "list_review" ? state.settings.listExample : state.settings.problemExample))
                            target.tasks.Insert(insert++, new StudyTask { word = frame.word, mode = "example" });
                        if (original != null && !original.spellingComplete &&
                            (target.kind == "new" ? state.settings.newSpelling :
                            target.kind == "list_review" ? state.settings.listSpelling : state.settings.problemSpelling))
                            target.tasks.Insert(insert++, new StudyTask { word = frame.word, mode = "spelling" });
                        if (original == null || (original.exampleComplete && original.spellingComplete))
                            target.tasks.Insert(insert, new StudyTask { word = frame.word,
                                mode = taskMode == "example" ? "example" : "spelling" });
                    }
                    else
                    {
                        for (int i = target.taskCursor - 1; i >= 0; i--)
                        {
                            StudyTask prior = target.tasks[i];
                            if (!prior.word.Equals(frame.word) || prior.mode != taskMode) continue;
                            prior.answeredAt = DateTime.MinValue;
                            prior.attempt = 0;
                            prior.correct = false;
                            break;
                        }
                        target.tasks.Insert(insert, new StudyTask { word = frame.word,
                            mode = taskMode, replay = false });
                    }
                    target.status = "active";
                    target.phase = "quiz";
                    before.activeListId = target.id;
                }
                else if (after.phase == "preview" && target.phase == "preview")
                {
                    StudyItem moved = target.items.FirstOrDefault(x => x.word.Equals(frame.word));
                    if (moved != null)
                    {
                        target.items.Remove(moved);
                        int currentIndex = preview == null ? target.items.Count :
                            target.items.FindIndex(x => x.word.Equals(preview.word));
                        target.items.Insert(Math.Min(Math.Max(currentIndex + 1, 0), target.items.Count), moved);
                        target.previewCursor = Math.Max(currentIndex, 0);
                        target.previewStage = after.previewStage;
                    }
                    target.status = "active";
                    before.activeListId = target.id;
                }
            }
            state = before;
            notebooks.RestoreSnapshot(frame.notebooksBefore);
            Save();
            return frame.word;
        }

        public void SettleCrossDay(DateTime now)
        {
            string today = StudyDayKey(now);
            bool changed = false;
            foreach (StudyList list in state.lists.Where(x => x.status == "active" &&
                string.CompareOrdinal(x.studyDate, today) < 0).ToList())
            {
                List<WordEntry> released = new List<WordEntry>();
                foreach (StudyItem item in list.items)
                {
                    if (item.mastered || notebooks.GetNotebook(item.word) == Notebooks.Mastered) continue;
                    if (item.exampleComplete && item.spellingComplete) continue;
                    item.released = true;
                    StudyWord word = FindWord(item.word);
                    if (word == null) word = EnsureWord(new StudyWord { word = item.word });
                    word.firstExtractedAt = DateTime.MinValue;
                    word.priorityAt = now;
                    if (!released.Any(x => x.Equals(item.word))) released.Add(item.word);
                }
                state.priorityWords.RemoveAll(x => released.Any(y => y.Equals(x)));
                state.priorityWords.InsertRange(0, released);
                list.status = "settled";
                list.phase = "done";
                if (state.activeListId == list.id) state.activeListId = null;
                changed = true;
            }
            if (changed) Save();
        }

        private void Finish(StudyList list)
        {
            list.status = "completed";
            list.phase = "done";
            if (state.activeListId == list.id) state.activeListId = null;
        }

        public void Save()
        {
            WriteAtomically(statePath, Serialize(state));
            string folder = Path.Combine(root, "study_lists");
            Directory.CreateDirectory(folder);
            foreach (StudyList list in state.lists.Where(x => x.kind == "new"))
                WriteAtomically(Path.Combine(folder, list.id + ".json"), Serialize(list));
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        private static string Serialize(object value) { return new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(value); }
        private static T Deserialize<T>(string json) { return new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Deserialize<T>(json); }
        private static void WriteAtomically(string path, string content)
        {
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        private void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                T swap = list[i]; list[i] = list[j]; list[j] = swap;
            }
        }
    }
}
