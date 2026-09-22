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
        public bool newDictation { get; set; }
        public bool newSpelling { get; set; }
        public bool listExample { get; set; }
        public bool listDictation { get; set; }
        public bool listSpelling { get; set; }
        public bool problemExample { get; set; }
        public bool problemDictation { get; set; }
        public bool problemSpelling { get; set; }
        public bool freeExample { get; set; }
        public bool freeDictation { get; set; }
        public bool freeSpelling { get; set; }
        public List<string> newTaskOrder { get; set; }
        public List<string> listTaskOrder { get; set; }
        public List<string> problemTaskOrder { get; set; }
        public List<string> freeTaskOrder { get; set; }
        public string newQuestionOrder { get; set; }
        public string listQuestionOrder { get; set; }
        public string problemQuestionOrder { get; set; }
        public string freeQuestionOrder { get; set; }
        public bool exampleFirstLetterHints { get; set; }
        public bool dictationMeaningHint { get; set; }
        public bool allowOverlap { get; set; }
        public bool reviewDueOnly { get; set; }
        public bool carryOverCountsInNewCount { get; set; }
        public bool carryOverPreview { get; set; }
        public int exampleCorrectTarget { get; set; }
        public int dictationCorrectTarget { get; set; }
        public int spellingCorrectTarget { get; set; }
        public bool backupBeforeManualEnd { get; set; }
        public bool timerEnabled { get; set; }
        public string timerPrecision { get; set; }
        public int pauseKey { get; set; }
        public bool checkUpdatesOnStartup { get; set; }
        public int undoLimit { get; set; }
        public int autoBackupMinutes { get; set; }
        public int autoBackupKeep { get; set; }
        public int[] reviewDays { get; set; }
        public int previewKey { get; set; }
        public int exampleHintKey { get; set; }
        public int previousPageKey { get; set; }
        public int nextPageKey { get; set; }
        public int undoKey { get; set; }
        public int manualBackupKey { get; set; }
        public Dictionary<string, int> defaultBookCounts { get; set; }

        public static StudySettings Defaults()
        {
            return new StudySettings
            {
                newCount = 20, listCount = 4, problemCount = 15,
                randomExtraction = false, fuzzyAnswers = false,
                newExample = false, newDictation = false, newSpelling = true,
                listExample = false, listDictation = false, listSpelling = true,
                problemExample = false, problemDictation = false, problemSpelling = true,
                freeExample = false, freeDictation = false, freeSpelling = true,
                newTaskOrder = DefaultTaskOrder(), listTaskOrder = DefaultTaskOrder(),
                problemTaskOrder = DefaultTaskOrder(), freeTaskOrder = DefaultTaskOrder(),
                newQuestionOrder = "sequential", listQuestionOrder = "sequential",
                problemQuestionOrder = "sequential", freeQuestionOrder = "sequential",
                exampleFirstLetterHints = true,
                dictationMeaningHint = true,
                allowOverlap = true, reviewDueOnly = false, carryOverCountsInNewCount = true,
                carryOverPreview = true,
                exampleCorrectTarget = 0, dictationCorrectTarget = 0, spellingCorrectTarget = 3,
                backupBeforeManualEnd = true, timerEnabled = true, timerPrecision = "minute",
                pauseKey = (int)(System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift
                    | System.Windows.Forms.Keys.P),
                checkUpdatesOnStartup = true,
                undoLimit = 5, autoBackupMinutes = 10, autoBackupKeep = 6,
                reviewDays = new[] { 1, 3, 7, 14 },
                previewKey = (int)System.Windows.Forms.Keys.Enter,
                exampleHintKey = (int)System.Windows.Forms.Keys.Enter,
                previousPageKey = (int)(System.Windows.Forms.Keys.Shift | System.Windows.Forms.Keys.Q),
                nextPageKey = (int)(System.Windows.Forms.Keys.Shift | System.Windows.Forms.Keys.E),
                undoKey = (int)(System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Z),
                manualBackupKey = (int)(System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift | System.Windows.Forms.Keys.B),
                defaultBookCounts = new Dictionary<string, int>()
            };
        }

        private static List<string> DefaultTaskOrder()
        {
            return new List<string> { "example", "dictation", "spelling" };
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
        public string sourceBook { get; set; }
        public string sourceUnit { get; set; }
        public bool released { get; set; }
        public bool mastered { get; set; }
        public bool exampleComplete { get; set; }
        public bool dictationComplete { get; set; }
        public bool spellingComplete { get; set; }
        public DateTime firstAnsweredAt { get; set; }
        public bool carriedOver { get; set; }
    }

    internal sealed class StudyTask
    {
        public WordEntry word { get; set; }
        public string mode { get; set; }
        public string examplePrompt { get; set; }
        public string exampleAnswer { get; set; }
        public List<string> exampleAnswers { get; set; }
        public bool replay { get; set; }
        public int attempt { get; set; }
        public DateTime answeredAt { get; set; }
        public bool correct { get; set; }
        public string submittedAnswer { get; set; }
        public bool skipped { get; set; }
        public bool mastered { get; set; }
        public bool notebookCounted { get; set; }
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
        public int questionOrderSeed { get; set; }
        public bool paused { get; set; }
        public string pausedInput { get; set; }
        public int pausedRevealStage { get; set; }
        public int pausedPreviewReviewIndex { get; set; }
        public int pausedQuizReviewIndex { get; set; }
        public long activeMilliseconds { get; set; }
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

    internal sealed class StudyProgress
    {
        public int completed { get; set; }
        public int total { get; set; }
        public int percent
        {
            get
            {
                if (total <= 0) return 100;
                return Math.Max(0, Math.Min(100, completed * 100 / total));
            }
        }
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
        private readonly HashSet<string> temporarilyDisabledDictation = new HashSet<string>();
        private Dictionary<string, string> vocabularyPartsOfSpeech;
        private StudyState state;
        public event EventHandler Changed;

        public StudyStore(string projectRoot, DataLoader dataLoader, NotebookStore notebookStore)
        {
            root = projectRoot;
            statePath = Path.Combine(root, "study_state.json");
            loader = dataLoader;
            notebooks = notebookStore;
            bool legacyTaskCounts = false;
            if (File.Exists(statePath))
            {
                string stateJson = File.ReadAllText(statePath, Encoding.UTF8);
                state = Deserialize<StudyState>(stateJson);
                if (state == null || state.version != 1 || state.settings == null || state.words == null
                    || state.lists == null || state.priorityWords == null || state.undo == null)
                    throw new InvalidDataException("学习状态文件无效，未覆盖数据。请从备份恢复。");
                if (stateJson.IndexOf("\"exampleFirstLetterHints\"", StringComparison.Ordinal) < 0)
                    state.settings.exampleFirstLetterHints = true;
                if (state.settings.exampleHintKey == 0)
                    state.settings.exampleHintKey = (int)System.Windows.Forms.Keys.Enter;
                if (state.settings.previousPageKey == 0)
                    state.settings.previousPageKey = (int)(System.Windows.Forms.Keys.Shift
                        | System.Windows.Forms.Keys.Q);
                if (state.settings.nextPageKey == 0)
                    state.settings.nextPageKey = (int)(System.Windows.Forms.Keys.Shift
                        | System.Windows.Forms.Keys.E);
                if (stateJson.IndexOf("\"dictationMeaningHint\"", StringComparison.Ordinal) < 0)
                    state.settings.dictationMeaningHint = true;
                if (stateJson.IndexOf("\"freeSpelling\"", StringComparison.Ordinal) < 0)
                    state.settings.freeSpelling = true;
                if (stateJson.IndexOf("\"backupBeforeManualEnd\"", StringComparison.Ordinal) < 0)
                    state.settings.backupBeforeManualEnd = true;
                if (stateJson.IndexOf("\"timerEnabled\"", StringComparison.Ordinal) < 0)
                    state.settings.timerEnabled = true;
                if (stateJson.IndexOf("\"checkUpdatesOnStartup\"", StringComparison.Ordinal) < 0)
                    state.settings.checkUpdatesOnStartup = true;
                if (state.settings.pauseKey == 0)
                    state.settings.pauseKey = (int)(System.Windows.Forms.Keys.Control
                        | System.Windows.Forms.Keys.Shift | System.Windows.Forms.Keys.P);
                NormalizeSettings(state.settings);
                legacyTaskCounts = stateJson.IndexOf("\"notebookCounted\"",
                    StringComparison.Ordinal) < 0;
                foreach (StudyList list in state.lists)
                {
                    if (list.items == null) list.items = new List<StudyItem>();
                    if (list.tasks == null) list.tasks = new List<StudyTask>();
                    if (list.retries == null) list.retries = new List<StudyTask>();
                    if (list.sourceListIds == null) list.sourceListIds = new List<string>();
                    if (list.questionOrderSeed == 0) list.questionOrderSeed = StableHash(list.id);
                    if (stateJson.IndexOf("\"pausedPreviewReviewIndex\"", StringComparison.Ordinal) < 0)
                        list.pausedPreviewReviewIndex = -1;
                    if (stateJson.IndexOf("\"pausedQuizReviewIndex\"", StringComparison.Ordinal) < 0)
                        list.pausedQuizReviewIndex = -1;
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
                NormalizeSettings(state.settings);
                Save();
            }
            if (legacyTaskCounts) MarkExistingHistoryCounted(state);
            if (NormalizeDuplicateWords(state) || legacyTaskCounts)
            {
                foreach (StudyList list in state.lists.Where(x => x.status == "active"
                    && x.phase == "quiz")) RebuildPendingTasks(list);
                Save();
            }
            bool filledSources = false;
            foreach (StudyList list in state.lists)
                foreach (StudyItem item in list.items ?? new List<StudyItem>())
                {
                    if (!string.IsNullOrWhiteSpace(item.sourceBook)) continue;
                    StudyWord source = FindWord(item.word);
                    if (source == null) continue;
                    item.sourceBook = source.book;
                    item.sourceUnit = source.unit;
                    filledSources = true;
                }
            if (filledSources) Save();
            SettleCrossDay(DateTime.Now);
        }

        private void MarkExistingHistoryCounted(StudyState target)
        {
            if (target == null || target.lists == null) return;
            foreach (StudyList list in target.lists)
            {
                if (list.history == null) continue;
                foreach (IGrouping<string, StudyTask> group in list.history.Where(x => x != null
                    && x.word != null && x.answeredAt != DateTime.MinValue).GroupBy(x =>
                        DataLoader.WordKey(x.word) + "\u001f" + (x.mode ?? string.Empty)))
                {
                    if (group.Any(x => x.notebookCounted)) continue;
                    StudyTask counted = group.FirstOrDefault(x => !x.replay) ?? group.First();
                    counted.notebookCounted = true;
                }
            }
            if (target.undo == null) return;
            foreach (StudyUndo frame in target.undo.Where(x => x != null
                && !string.IsNullOrWhiteSpace(x.stateBefore)))
            {
                StudyState snapshot = Deserialize<StudyState>(frame.stateBefore);
                if (snapshot == null) continue;
                MarkExistingHistoryCounted(snapshot);
                frame.stateBefore = Serialize(snapshot);
            }
        }

        private bool NormalizeDuplicateWords(StudyState target)
        {
            if (target == null || target.words == null || target.lists == null) return false;
            bool changed = false;
            Dictionary<string, string> libraryParts = VocabularyPartsOfSpeech();
            Dictionary<string, WordEntry> bySignature = new Dictionary<string, WordEntry>(
                StringComparer.Ordinal);
            List<StudyWord> mergedWords = new List<StudyWord>();
            foreach (IGrouping<string, StudyWord> group in target.words.Where(x => x != null
                && x.word != null).GroupBy(x => (x.book ?? string.Empty).ToLowerInvariant()
                    + "\u001f" + DataLoader.WordKey(x.word)))
            {
                List<StudyWord> members = group.ToList();
                StudyWord first = members[0];
                WordEntry mergedWord = DataLoader.MergeWordEntry(members.Select(x => x.word));
                string vocabularyPart;
                string wordKey = DataLoader.WordKey(mergedWord);
                if ((libraryParts.TryGetValue((first.book ?? string.Empty).ToLowerInvariant()
                        + "\u001f" + wordKey, out vocabularyPart)
                    || libraryParts.TryGetValue("\u001f" + wordKey, out vocabularyPart)))
                    mergedWord.partOfSpeech = PartOfSpeech.IsPhrase(mergedWord.english)
                        ? string.Empty : PartOfSpeech.Merge(mergedWord.partOfSpeech, vocabularyPart);
                List<DateTime> extracted = members.Select(x => x.firstExtractedAt)
                    .Where(x => x != DateTime.MinValue).ToList();
                StudyWord combined = new StudyWord
                {
                    word = mergedWord,
                    book = first.book,
                    unit = members.Select(x => x.unit).FirstOrDefault(x => !string.IsNullOrEmpty(x)),
                    firstExtractedAt = extracted.Count == 0 ? DateTime.MinValue : extracted.Min(),
                    priorityAt = members.Max(x => x.priorityAt),
                    acceptedAnswers = members.SelectMany(x => x.acceptedAnswers
                        ?? new List<string>()).Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                };
                mergedWords.Add(combined);
                foreach (StudyWord member in members)
                    bySignature[FullWordKey(member.word)] = combined.word;
                if (members.Count > 1 || !combined.word.Equals(first.word)) changed = true;
            }
            target.words = mergedWords;

            Func<WordEntry, WordEntry> canonical = delegate(WordEntry word)
            {
                if (word == null) return null;
                WordEntry known;
                if (bySignature.TryGetValue(FullWordKey(word), out known)) return known;
                string key = DataLoader.WordKey(word);
                StudyWord match = target.words.FirstOrDefault(x => DataLoader.WordKey(x.word) == key);
                if (match != null) return match.word;
                string vocabularyPart;
                if (libraryParts.TryGetValue("\u001f" + key, out vocabularyPart))
                {
                    string mergedPart = PartOfSpeech.IsPhrase(word.english) ? string.Empty
                        : PartOfSpeech.Merge(word.partOfSpeech, vocabularyPart);
                    if (!string.Equals(word.partOfSpeech ?? string.Empty, mergedPart,
                        StringComparison.Ordinal))
                    {
                        word.partOfSpeech = mergedPart;
                        changed = true;
                    }
                }
                return word;
            };

            foreach (StudyList list in target.lists)
            {
                if (list.items == null) list.items = new List<StudyItem>();
                int oldCursor = list.previewCursor;
                HashSet<string> previewed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                List<StudyItem> items = new List<StudyItem>();
                Dictionary<string, StudyItem> byEnglish = new Dictionary<string, StudyItem>(
                    StringComparer.OrdinalIgnoreCase);
                for (int index = 0; index < list.items.Count; index++)
                {
                    StudyItem source = list.items[index];
                    WordEntry word = canonical(source.word);
                    string key = DataLoader.WordKey(word);
                    StudyItem item;
                    if (!byEnglish.TryGetValue(key, out item))
                    {
                        item = new StudyItem
                        {
                            word = word,
                            released = source.released,
                            mastered = source.mastered,
                            exampleComplete = source.exampleComplete,
                            spellingComplete = source.spellingComplete,
                            firstAnsweredAt = source.firstAnsweredAt,
                            carriedOver = source.carriedOver
                        };
                        byEnglish[key] = item;
                        items.Add(item);
                    }
                    else
                    {
                        changed = true;
                        item.released = item.released && source.released;
                        item.mastered = item.mastered || source.mastered;
                        item.exampleComplete = item.exampleComplete || source.exampleComplete;
                        item.spellingComplete = item.spellingComplete || source.spellingComplete;
                        item.carriedOver = item.carriedOver || source.carriedOver;
                        if (item.firstAnsweredAt == DateTime.MinValue
                            || (source.firstAnsweredAt != DateTime.MinValue
                                && source.firstAnsweredAt < item.firstAnsweredAt))
                            item.firstAnsweredAt = source.firstAnsweredAt;
                    }
                    if (index < oldCursor) previewed.Add(key);
                    if (!word.Equals(source.word)) changed = true;
                }
                list.items = items;
                list.previewCursor = list.items.TakeWhile(x => previewed.Contains(
                    DataLoader.WordKey(x.word))).Count();
                if (list.previewCursor != oldCursor) changed = true;

                Dictionary<string, WordEntry> listWords = list.items.ToDictionary(
                    x => DataLoader.WordKey(x.word), x => x.word, StringComparer.OrdinalIgnoreCase);
                foreach (List<StudyTask> tasks in new[] { list.tasks, list.retries, list.history }
                    .Where(x => x != null))
                    foreach (StudyTask task in tasks.Where(x => x != null && x.word != null))
                    {
                        WordEntry before = task.word;
                        WordEntry listWord;
                        task.word = listWords.TryGetValue(DataLoader.WordKey(before), out listWord)
                            ? listWord : canonical(before);
                        if (!task.word.Equals(before)) changed = true;
                    }
            }

            if (target.priorityWords == null) target.priorityWords = new List<WordEntry>();
            target.priorityWords = target.priorityWords.Where(x => x != null).Select(canonical)
                .GroupBy(DataLoader.WordKey, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First()).ToList();
            if (target.undo == null) target.undo = new List<StudyUndo>();
            foreach (StudyUndo frame in target.undo.Where(x => x != null))
            {
                if (frame.word != null) frame.word = canonical(frame.word);
                if (string.IsNullOrWhiteSpace(frame.stateBefore)) continue;
                StudyState snapshot = Deserialize<StudyState>(frame.stateBefore);
                if (snapshot != null)
                {
                    NormalizeDuplicateWords(snapshot);
                    frame.stateBefore = Serialize(snapshot);
                }
            }
            return changed;
        }

        private static string FullWordKey(WordEntry word)
        {
            if (word == null) return string.Empty;
            return DataLoader.WordKey(word) + "\u001f" + DataLoader.Sanitize(word.chinese)
                + "\u001f" + DataLoader.Sanitize(word.examples)
                + "\u001f" + PartOfSpeech.Normalize(word.partOfSpeech);
        }

        private Dictionary<string, string> VocabularyPartsOfSpeech()
        {
            if (vocabularyPartsOfSpeech != null) return vocabularyPartsOfSpeech;
            vocabularyPartsOfSpeech = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string book in loader.GetAvailableBooks())
            {
                foreach (string unit in loader.GetUnitsForBook(book))
                {
                    foreach (WordEntry word in loader.LoadWordList(book, new[] { unit }))
                    {
                        string key = book.ToLowerInvariant() + "\u001f" + DataLoader.WordKey(word);
                        string existing;
                        vocabularyPartsOfSpeech.TryGetValue(key, out existing);
                        vocabularyPartsOfSpeech[key] = PartOfSpeech.Merge(existing, word.partOfSpeech);
                        string globalKey = "\u001f" + DataLoader.WordKey(word);
                        vocabularyPartsOfSpeech.TryGetValue(globalKey, out existing);
                        vocabularyPartsOfSpeech[globalKey] = PartOfSpeech.Merge(existing,
                            word.partOfSpeech);
                    }
                }
            }
            return vocabularyPartsOfSpeech;
        }

        public StudySettings Settings { get { return state.settings; } }
        public void UpdateSettings(StudySettings settings)
        {
            StudySettings previous = state.settings;
            Dictionary<string, bool> rebuild = new Dictionary<string, bool>
            {
                { "new", TaskSettingsSignature(previous, "new") != TaskSettingsSignature(settings, "new") },
                { "list_review", TaskSettingsSignature(previous, "list_review") != TaskSettingsSignature(settings, "list_review") },
                { "problem_review", TaskSettingsSignature(previous, "problem_review") != TaskSettingsSignature(settings, "problem_review") }
            };
            state.settings = settings;
            try
            {
                SaveSettings();
                foreach (KeyValuePair<string, bool> changed in rebuild.Where(x => x.Value))
                {
                    StudyList list = ActiveFor(changed.Key);
                    if (list == null || list.phase != "quiz") continue;
                    StudyTask before = list.tasks != null && list.taskCursor >= 0
                        && list.taskCursor < list.tasks.Count ? list.tasks[list.taskCursor] : null;
                    RebuildPendingTasks(list);
                    StudyTask after = list.tasks != null && list.taskCursor >= 0
                        && list.taskCursor < list.tasks.Count ? list.tasks[list.taskCursor] : null;
                    if (!SamePendingTask(before, after))
                    {
                        list.pausedInput = string.Empty;
                        list.pausedRevealStage = 0;
                        list.pausedPreviewReviewIndex = -1;
                        list.pausedQuizReviewIndex = -1;
                    }
                }
                Save();
            }
            catch { state.settings = previous; throw; }
        }

        private static string TaskSettingsSignature(StudySettings settings, string kind)
        {
            if (settings == null) return string.Empty;
            bool example = kind == "new" ? settings.newExample
                : kind == "list_review" ? settings.listExample : settings.problemExample;
            bool dictation = kind == "new" ? settings.newDictation
                : kind == "list_review" ? settings.listDictation : settings.problemDictation;
            bool spelling = kind == "new" ? settings.newSpelling
                : kind == "list_review" ? settings.listSpelling : settings.problemSpelling;
            IEnumerable<string> order = kind == "new" ? settings.newTaskOrder
                : kind == "list_review" ? settings.listTaskOrder : settings.problemTaskOrder;
            string questionOrder = kind == "new" ? settings.newQuestionOrder
                : kind == "list_review" ? settings.listQuestionOrder : settings.problemQuestionOrder;
            return string.Join("|", new[] { example.ToString(), dictation.ToString(), spelling.ToString(),
                string.Join(",", order ?? Enumerable.Empty<string>()), questionOrder ?? string.Empty });
        }

        private static bool SamePendingTask(StudyTask left, StudyTask right)
        {
            if (left == null || right == null) return left == null && right == null;
            return left.mode == right.mode && left.word != null && right.word != null
                && left.word.Equals(right.word)
                && NormalizeExamplePart(left.examplePrompt) == NormalizeExamplePart(right.examplePrompt)
                && NormalizeExamplePart(left.exampleAnswer) == NormalizeExamplePart(right.exampleAnswer);
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
            NormalizeSettings(state.settings);
            if (state.settings.newCount < 0 || state.settings.listCount < 0 || state.settings.problemCount < 0
                || state.settings.undoLimit < 0 || state.settings.undoLimit > 5
                || state.settings.autoBackupMinutes < 1 || state.settings.autoBackupKeep < 1
                || state.settings.exampleCorrectTarget < 0 || state.settings.dictationCorrectTarget < 0
                || state.settings.spellingCorrectTarget < 0)
                throw new ArgumentOutOfRangeException("settings", "学习设置包含超出范围的值。");
            if (state.settings.reviewDays == null || state.settings.reviewDays.Length == 0
                || state.settings.reviewDays.Any(x => x < 0))
                throw new ArgumentOutOfRangeException("settings", "复习间隔天数无效。");
            if ((!state.settings.newExample && !state.settings.newDictation && !state.settings.newSpelling)
                || (!state.settings.listExample && !state.settings.listDictation && !state.settings.listSpelling)
                || (!state.settings.problemExample && !state.settings.problemDictation && !state.settings.problemSpelling)
                || (!state.settings.freeExample && !state.settings.freeDictation && !state.settings.freeSpelling))
                throw new ArgumentException("四个学习部分各至少启用一种题型。");
            if (state.settings.defaultBookCounts == null) state.settings.defaultBookCounts = new Dictionary<string, int>();
            if (state.settings.exampleHintKey == 0)
                state.settings.exampleHintKey = (int)System.Windows.Forms.Keys.Enter;
            if (state.settings.previousPageKey == 0)
                state.settings.previousPageKey = (int)(System.Windows.Forms.Keys.Shift
                    | System.Windows.Forms.Keys.Q);
            if (state.settings.nextPageKey == 0)
                state.settings.nextPageKey = (int)(System.Windows.Forms.Keys.Shift
                    | System.Windows.Forms.Keys.E);
            if (state.undo.Count > state.settings.undoLimit)
                state.undo.RemoveRange(0, state.undo.Count - state.settings.undoLimit);
            Save();
        }

        private static void NormalizeSettings(StudySettings settings)
        {
            if (settings == null) return;
            settings.newTaskOrder = NormalizeTaskOrder(settings.newTaskOrder);
            settings.listTaskOrder = NormalizeTaskOrder(settings.listTaskOrder);
            settings.problemTaskOrder = NormalizeTaskOrder(settings.problemTaskOrder);
            settings.freeTaskOrder = NormalizeTaskOrder(settings.freeTaskOrder);
            settings.newQuestionOrder = NormalizeQuestionOrder(settings.newQuestionOrder);
            settings.listQuestionOrder = NormalizeQuestionOrder(settings.listQuestionOrder);
            settings.problemQuestionOrder = NormalizeQuestionOrder(settings.problemQuestionOrder);
            settings.freeQuestionOrder = NormalizeQuestionOrder(settings.freeQuestionOrder);
            if (settings.timerPrecision != "millisecond") settings.timerPrecision = "minute";
        }

        private static List<string> NormalizeTaskOrder(IEnumerable<string> order)
        {
            List<string> result = (order ?? Enumerable.Empty<string>())
                .Where(x => x == "example" || x == "dictation" || x == "spelling")
                .Distinct().ToList();
            foreach (string mode in new[] { "example", "dictation", "spelling" })
                if (!result.Contains(mode)) result.Add(mode);
            return result;
        }

        private static string NormalizeQuestionOrder(string order)
        {
            return order == "unit_random" || order == "book_random" ? order : "sequential";
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
                        StudyWord inResult = result.FirstOrDefault(x => x.book == book
                            && DataLoader.WordKey(x.word) == DataLoader.WordKey(word));
                        if (inResult != null)
                        {
                            inResult.word = DataLoader.MergeWordEntry(new[] { inResult.word, word });
                            continue;
                        }
                        StudyWord existing = state.words.FirstOrDefault(x => x.book == book
                            && DataLoader.WordKey(x.word) == DataLoader.WordKey(word));
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
                        result.Add(existing);
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

        public bool IsCorrect(WordEntry word, string input, string mode,
            IEnumerable<string> expectedAnswers)
        {
            if (!string.Equals(mode, "example", StringComparison.OrdinalIgnoreCase))
                return IsCorrect(word, input);
            List<string> answers = (expectedAnswers ?? Enumerable.Empty<string>())
                .Select(NormalizeAnswer).Where(x => x.Length > 0).ToList();
            if (state.settings.fuzzyAnswers)
            {
                StudyWord record = FindWord(word);
                if (record != null && record.acceptedAnswers != null)
                    answers.AddRange(record.acceptedAnswers.Select(NormalizeAnswer));
            }
            return answers.Distinct().Contains(NormalizeAnswer(input));
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
                    && !selected.Any(y => DataLoader.WordKey(y.word) == DataLoader.WordKey(x.word))).ToList();
                List<StudyWord> priority = state.priorityWords.Select(word => available.FirstOrDefault(x =>
                        DataLoader.WordKey(x.word) == DataLoader.WordKey(word)))
                    .Where(x => x != null).ToList();
                List<StudyWord> ordinary = available.Where(x => !priority.Any(y =>
                    DataLoader.WordKey(y.word) == DataLoader.WordKey(x.word))).ToList();
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
                StudyWord record = EnsureWord(selected.First(x =>
                    DataLoader.WordKey(x.word) == DataLoader.WordKey(item.word)));
                item.sourceBook = record.book;
                item.sourceUnit = record.unit;
                item.carriedOver = state.priorityWords.Any(x =>
                    DataLoader.WordKey(x) == DataLoader.WordKey(item.word));
                record.firstExtractedAt = now;
                record.priorityAt = DateTime.MinValue;
                state.priorityWords.RemoveAll(x => DataLoader.WordKey(x) == DataLoader.WordKey(item.word));
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
            words = DataLoader.MergeWordEntries(words);
            string baseId = now.ToString("yyyyMMdd_HHmmss");
            string id = baseId;
            int suffix = 2;
            while (state.lists.Any(x => x.id == id)) id = baseId + "_" + suffix++;
            StudyList list = new StudyList
            {
                id = id, kind = kind, createdAt = now, studyDate = StudyDayKey(now),
                status = "active", phase = "quiz", sourceListIds = new List<string>(),
                items = words.Select(x =>
                {
                    StudyWord source = FindWord(x);
                    return new StudyItem { word = x,
                        sourceBook = source == null ? string.Empty : source.book,
                        sourceUnit = source == null ? string.Empty : source.unit };
                }).ToList(),
                tasks = new List<StudyTask>(), retries = new List<StudyTask>(), history = new List<StudyTask>(),
                questionOrderSeed = StableHash(id), pausedPreviewReviewIndex = -1,
                pausedQuizReviewIndex = -1
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
            if (list.phase == "quiz" && !list.paused) RebuildPendingTasks(list);
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
            bool example = ModeEnabled(list.kind, "example");
            bool dictation = ModeEnabled(list.kind, "dictation");
            bool spelling = ModeEnabled(list.kind, "spelling");
            foreach (StudyItem item in OrderedItems(list))
            {
                if (item.mastered || item.released || notebooks.GetNotebook(item.word) == Notebooks.Mastered) continue;
                List<StudyTask> itemHistory = (list.history ?? new List<StudyTask>())
                    .Where(x => x.word.Equals(item.word)).ToList();
                List<ExampleQuestion> questions = ExampleCloze.Questions(item.word);
                bool exampleDone = ExamplesComplete(item.word, itemHistory);
                bool dictationDone = itemHistory.Any(x => x.mode == "dictation"
                    && (x.correct || x.mastered));
                bool spellingDone = itemHistory.Any(x => x.mode == "spelling"
                    && (x.correct || x.mastered));
                item.exampleComplete = !example || exampleDone;
                item.dictationComplete = !dictation || dictationDone;
                item.spellingComplete = !spelling || spellingDone;
                foreach (string mode in TaskOrder(list.kind))
                {
                    if (mode == "example" && example && !exampleDone)
                    {
                        if (questions.Count == 0)
                        {
                            List<StudyTask> attempts = itemHistory.Where(x => x.mode == "example").ToList();
                            list.tasks.Add(CreateTask(item.word, "example",
                                attempts.Any(x => !x.correct && !x.skipped), attempts.Count));
                        }
                        else
                        {
                            foreach (ExampleQuestion question in questions)
                            {
                                List<StudyTask> attempts = itemHistory.Where(x => x.mode == "example"
                                    && SameExample(x, question)).ToList();
                                if (attempts.Any(x => x.correct || x.skipped || x.mastered)) continue;
                                list.tasks.Add(CreateExampleTask(item.word, question,
                                    attempts.Any(x => !x.correct && !x.skipped), attempts.Count));
                            }
                        }
                    }
                    else if (mode == "dictation" && dictation && !dictationDone)
                    {
                        List<StudyTask> attempts = itemHistory.Where(x => x.mode == "dictation").ToList();
                        list.tasks.Add(CreateTask(item.word, "dictation",
                            attempts.Any(x => !x.correct), attempts.Count));
                    }
                    else if (mode == "spelling" && spelling && !spellingDone)
                    {
                        List<StudyTask> attempts = itemHistory.Where(x => x.mode == "spelling").ToList();
                        list.tasks.Add(CreateTask(item.word, "spelling",
                            attempts.Any(x => !x.correct), attempts.Count));
                    }
                }
            }
            if (list.tasks.Count == 0) Finish(list);
        }

        private bool ModeEnabled(string kind, string mode)
        {
            if (mode == "dictation" && temporarilyDisabledDictation.Contains(kind)) return false;
            if (kind == "new") return mode == "example" ? state.settings.newExample
                : mode == "dictation" ? state.settings.newDictation : state.settings.newSpelling;
            if (kind == "list_review") return mode == "example" ? state.settings.listExample
                : mode == "dictation" ? state.settings.listDictation : state.settings.listSpelling;
            return mode == "example" ? state.settings.problemExample
                : mode == "dictation" ? state.settings.problemDictation : state.settings.problemSpelling;
        }

        public bool DictationEnabled(string kind) { return ModeEnabled(kind, "dictation"); }

        public void TemporarilyDisableDictation(string kind)
        {
            temporarilyDisabledDictation.Add(kind);
            StudyList list = ActiveFor(kind);
            if (list != null && list.phase == "quiz")
            {
                bool wasPaused = list.paused;
                RebuildPendingTasks(list);
                list.paused = wasPaused;
                Save();
            }
        }

        private List<string> TaskOrder(string kind)
        {
            return kind == "new" ? state.settings.newTaskOrder : kind == "list_review"
                ? state.settings.listTaskOrder : state.settings.problemTaskOrder;
        }

        private string QuestionOrder(string kind)
        {
            return kind == "new" ? state.settings.newQuestionOrder : kind == "list_review"
                ? state.settings.listQuestionOrder : state.settings.problemQuestionOrder;
        }

        private List<StudyItem> OrderedItems(StudyList list)
        {
            List<StudyItem> items = new List<StudyItem>(list.items ?? new List<StudyItem>());
            string mode = QuestionOrder(list.kind);
            if (mode == "sequential") return items;
            Func<StudyItem, string> key = item =>
            {
                StudyWord source = FindWord(item.word);
                string book = !string.IsNullOrWhiteSpace(item.sourceBook) ? item.sourceBook
                    : source == null ? string.Empty : source.book ?? string.Empty;
                string unit = !string.IsNullOrWhiteSpace(item.sourceUnit) ? item.sourceUnit
                    : source == null ? string.Empty : source.unit ?? string.Empty;
                return mode == "unit_random" ? book + "\u001f" + unit : book;
            };
            List<StudyItem> result = new List<StudyItem>();
            foreach (IGrouping<string, StudyItem> group in items.GroupBy(key))
            {
                List<StudyItem> shuffled = group.ToList();
                ShuffleDeterministic(shuffled, list.questionOrderSeed ^ StableHash(group.Key));
                result.AddRange(shuffled);
            }
            return result;
        }

        public StudyEndResult EndActive(string kind, DateTime now)
        {
            SettleCrossDay(now);
            StudyList list = ActiveFor(kind);
            if (list == null) throw new InvalidOperationException("当前部分没有未完成列表。");
            bool example = ModeEnabled(list.kind, "example");
            bool dictation = ModeEnabled(list.kind, "dictation");
            bool spelling = ModeEnabled(list.kind, "spelling");
            List<StudyItem> kept = new List<StudyItem>();
            List<StudyItem> released = new List<StudyItem>();
            for (int index = 0; index < list.items.Count; index++)
            {
                StudyItem item = list.items[index];
                bool mastered = item.mastered || notebooks.GetNotebook(item.word) == Notebooks.Mastered;
                bool previewed = list.kind != "new" || list.phase != "preview" || index < list.previewCursor;
                List<StudyTask> history = (list.history ?? new List<StudyTask>())
                    .Where(x => x.word.Equals(item.word)).ToList();
                bool exampleDone = !example || ExamplesComplete(item.word, history);
                bool dictationDone = !dictation || history.Any(x => x.mode == "dictation"
                    && (x.correct || x.mastered));
                bool spellingDone = !spelling || history.Any(x => x.mode == "spelling"
                    && (x.correct || x.mastered));
                if (mastered || (previewed && exampleDone && dictationDone && spellingDone)) kept.Add(item);
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
            list.paused = false;
            list.pausedInput = string.Empty;
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
                    EnsureExampleTask(task);
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
            if (EnsureExampleTask(task)) return null;
            StudyItem item = Active.items.First(x => x.word.Equals(task.word));
            item.exampleComplete = true;
            task.skipped = true;
            task.notebookCounted = true;
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
            bool correct = IsCorrect(task.word, input, task.mode, task.exampleAnswers);
            string expectedAnswer = ExpectedAnswer(task);
            bool eligible = !task.replay && task.attempt == 0;
            bool alreadyCounted = list.history.Any(x => x != null && x.word != null
                && DataLoader.WordKey(x.word) == DataLoader.WordKey(task.word)
                && x.mode == task.mode && x.notebookCounted);
            task.attempt++;
            task.answeredAt = now;
            task.correct = correct;
            task.submittedAnswer = input;

            // A word may have several example sentences. Each sentence must be completed,
            // but notebook/error statistics are still recorded only once per word/type/list.
            bool allExamplesCorrect = task.mode != "example" || !correct
                ? false : ExampleCloze.Questions(task.word).All(question =>
                    SameExample(task, question) || list.history.Any(history =>
                        history.mode == "example" && (history.correct || history.mastered)
                        && SameExample(history, question)));
            bool shouldCount = eligible && !alreadyCounted
                && (!correct || task.mode != "example" || allExamplesCorrect);
            AnswerOutcome result;
            if (shouldCount)
            {
                result = notebooks.RecordStudyAnswer(task.word, correct, task.mode, true,
                    state.settings.exampleCorrectTarget, state.settings.dictationCorrectTarget,
                    state.settings.spellingCorrectTarget, now);
                task.notebookCounted = true;
            }
            else result = new AnswerOutcome { Correct = correct };
            result.CorrectAnswer = expectedAnswer;
            list.history.Add(CopyHistoryTask(task));
            StudyItem item = list.items.First(x => x.word.Equals(task.word));
            if (item.firstAnsweredAt == DateTime.MinValue) item.firstAnsweredAt = now;
            if (correct)
            {
                if (task.mode == "example") item.exampleComplete = ExamplesComplete(task.word, list.history);
                else if (task.mode == "dictation") item.dictationComplete = true;
                else item.spellingComplete = true;
            }
            else
            {
                StudyTask retry = CreateTask(task.word, task.mode, true, task.attempt);
                retry.examplePrompt = task.examplePrompt;
                retry.exampleAnswer = task.exampleAnswer;
                retry.exampleAnswers = task.exampleAnswers == null ? null : new List<string>(task.exampleAnswers);
                list.retries.Add(retry);
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

        public string ExpectedAnswer(StudyTask task)
        {
            if (task == null) return string.Empty;
            if (task.mode == "example" && EnsureExampleTask(task)) return task.exampleAnswer;
            return GameEngine.CleanEnglish(task.word);
        }

        public List<string> AcceptedAnswersForTask(StudyTask task)
        {
            if (task == null) return new List<string>();
            if (task.mode == "example")
            {
                EnsureExampleTask(task);
                List<string> values = task.exampleAnswers == null
                    ? new List<string>() : new List<string>(task.exampleAnswers);
                if (!string.IsNullOrWhiteSpace(task.exampleAnswer)) values.Insert(0, task.exampleAnswer);
                return values.Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }
            StudyWord record = FindWord(task.word);
            List<string> accepted = new List<string> { GameEngine.CleanEnglish(task.word) };
            if (state.settings.fuzzyAnswers && record != null && record.acceptedAnswers != null)
                accepted.AddRange(record.acceptedAnswers);
            return accepted.Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public void SaveSessionState(string listId, string input, int revealStage,
            int previewReviewIndex, int quizReviewIndex, bool paused)
        {
            StudyList list = state.lists.FirstOrDefault(x => x.id == listId && x.status == "active");
            if (list == null) return;
            list.pausedInput = input ?? string.Empty;
            list.pausedRevealStage = Math.Max(0, revealStage);
            list.pausedPreviewReviewIndex = previewReviewIndex;
            list.pausedQuizReviewIndex = quizReviewIndex;
            list.paused = paused;
            Save();
        }

        public void AddActiveMilliseconds(string listId, long milliseconds)
        {
            if (milliseconds <= 0) return;
            StudyList list = state.lists.FirstOrDefault(x => x.id == listId);
            if (list == null) return;
            list.activeMilliseconds = Math.Max(0, list.activeMilliseconds + milliseconds);
            Save();
        }

        public void ClearPause(string listId)
        {
            StudyList list = state.lists.FirstOrDefault(x => x.id == listId && x.status == "active");
            if (list == null) return;
            list.paused = false;
            Save();
        }

        private StudyTask CreateTask(WordEntry word, string mode, bool replay = false, int attempt = 0)
        {
            StudyTask task = new StudyTask
            {
                word = word, mode = mode, replay = replay, attempt = attempt,
                exampleAnswers = new List<string>()
            };
            EnsureExampleTask(task);
            return task;
        }

        private StudyTask CreateExampleTask(WordEntry word, ExampleQuestion question,
            bool replay = false, int attempt = 0)
        {
            return new StudyTask
            {
                word = word,
                mode = "example",
                replay = replay,
                attempt = attempt,
                examplePrompt = question == null ? null : question.prompt,
                exampleAnswer = question == null ? null : question.answer,
                exampleAnswers = question == null
                    ? new List<string>() : new List<string>(question.answers)
            };
        }

        private static bool SameExample(StudyTask task, ExampleQuestion question)
        {
            if (task == null || question == null) return false;
            return NormalizeExamplePart(task.examplePrompt) == NormalizeExamplePart(question.prompt)
                && NormalizeExamplePart(task.exampleAnswer) == NormalizeExamplePart(question.answer);
        }

        private static string NormalizeExamplePart(string value)
        {
            return DataLoader.Sanitize(value ?? string.Empty).ToLowerInvariant();
        }

        private static bool ExamplesComplete(WordEntry word, IEnumerable<StudyTask> history)
        {
            List<StudyTask> attempts = (history ?? Enumerable.Empty<StudyTask>())
                .Where(x => x != null && x.mode == "example").ToList();
            List<ExampleQuestion> questions = ExampleCloze.Questions(word);
            if (questions.Count == 0)
                return attempts.Any(x => x.correct || x.skipped || x.mastered);
            return questions.All(question => attempts.Any(task =>
                (task.correct || task.skipped || task.mastered) && SameExample(task, question)));
        }

        public StudyProgress Progress(StudyList list)
        {
            StudyProgress progress = new StudyProgress();
            if (list == null || list.items == null) return progress;
            bool example = ModeEnabled(list.kind, "example");
            bool dictation = ModeEnabled(list.kind, "dictation");
            bool spelling = ModeEnabled(list.kind, "spelling");
            List<StudyTask> history = list.history ?? new List<StudyTask>();
            for (int index = 0; index < list.items.Count; index++)
            {
                StudyItem item = list.items[index];
                if (item == null || item.word == null || item.released) continue;
                bool mastered = item.mastered
                    || notebooks.GetNotebook(item.word) == Notebooks.Mastered;
                if (list.kind == "new")
                {
                    progress.total++;
                    if (mastered || index < list.previewCursor) progress.completed++;
                }
                if (mastered) continue;
                List<StudyTask> wordHistory = history.Where(x => x != null && x.word != null
                    && DataLoader.WordKey(x.word) == DataLoader.WordKey(item.word)).ToList();
                if (example)
                {
                    List<ExampleQuestion> questions = ExampleCloze.Questions(item.word);
                    progress.total += questions.Count;
                    foreach (ExampleQuestion question in questions)
                        if (wordHistory.Any(task => task.mode == "example" && task.correct
                            && SameExample(task, question))) progress.completed++;
                }
                if (dictation)
                {
                    progress.total++;
                    if (wordHistory.Any(task => task.mode == "dictation" && task.correct))
                        progress.completed++;
                }
                if (spelling)
                {
                    progress.total++;
                    if (wordHistory.Any(task => task.mode == "spelling" && task.correct))
                        progress.completed++;
                }
            }
            if (progress.completed > progress.total) progress.completed = progress.total;
            return progress;
        }

        private bool EnsureExampleTask(StudyTask task)
        {
            if (task == null || task.mode != "example") return false;
            if (!string.IsNullOrWhiteSpace(task.exampleAnswer)
                && !string.IsNullOrWhiteSpace(task.examplePrompt))
            {
                if (task.exampleAnswers == null || task.exampleAnswers.Count == 0)
                    task.exampleAnswers = new List<string> { task.exampleAnswer };
                return true;
            }
            ExampleQuestion question = ExampleCloze.First(task.word);
            if (question == null) return false;
            task.examplePrompt = question.prompt;
            task.exampleAnswer = question.answer;
            task.exampleAnswers = new List<string>(question.answers);
            return true;
        }

        private static StudyTask CopyHistoryTask(StudyTask task)
        {
            return new StudyTask { word = task.word, mode = task.mode, replay = task.replay,
                examplePrompt = task.examplePrompt, exampleAnswer = task.exampleAnswer,
                exampleAnswers = task.exampleAnswers == null ? null : new List<string>(task.exampleAnswers),
                attempt = task.attempt, answeredAt = task.answeredAt, correct = task.correct,
                submittedAnswer = task.submittedAnswer, skipped = task.skipped, mastered = task.mastered,
                notebookCounted = task.notebookCounted };
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
                            target.tasks.Insert(insert++, CreateTask(frame.word, "example"));
                        if (original != null && !original.spellingComplete &&
                            (target.kind == "new" ? state.settings.newSpelling :
                            target.kind == "list_review" ? state.settings.listSpelling : state.settings.problemSpelling))
                            target.tasks.Insert(insert++, CreateTask(frame.word, "spelling"));
                        if (original == null || (original.exampleComplete && original.spellingComplete))
                            target.tasks.Insert(insert, CreateTask(frame.word,
                                taskMode == "example" ? "example" : "spelling"));
                    }
                    else
                    {
                        StudyTask undoneTask = null;
                        for (int i = target.taskCursor - 1; i >= 0; i--)
                        {
                            StudyTask prior = target.tasks[i];
                            if (!prior.word.Equals(frame.word) || prior.mode != taskMode) continue;
                            undoneTask = prior;
                            prior.answeredAt = DateTime.MinValue;
                            prior.attempt = 0;
                            prior.correct = false;
                            prior.notebookCounted = false;
                            break;
                        }
                        StudyTask restored = CreateTask(frame.word, taskMode, false);
                        if (taskMode == "example" && undoneTask != null)
                        {
                            restored.examplePrompt = undoneTask.examplePrompt;
                            restored.exampleAnswer = undoneTask.exampleAnswer;
                            restored.exampleAnswers = undoneTask.exampleAnswers == null ? null
                                : new List<string>(undoneTask.exampleAnswers);
                        }
                        target.tasks.Insert(insert, restored);
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
                    bool exampleDone = !ModeEnabled(list.kind, "example") || item.exampleComplete;
                    bool dictationDone = !ModeEnabled(list.kind, "dictation") || item.dictationComplete;
                    bool spellingDone = !ModeEnabled(list.kind, "spelling") || item.spellingComplete;
                    if (exampleDone && dictationDone && spellingDone) continue;
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
                list.paused = false;
                list.pausedInput = string.Empty;
                if (state.activeListId == list.id) state.activeListId = null;
                changed = true;
            }
            if (changed) Save();
        }

        private void Finish(StudyList list)
        {
            list.status = "completed";
            list.phase = "done";
            list.paused = false;
            list.pausedInput = string.Empty;
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

        private static void ShuffleDeterministic<T>(IList<T> list, int seed)
        {
            Random generator = new Random(seed == int.MinValue ? 0 : Math.Abs(seed));
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = generator.Next(i + 1);
                T swap = list[i]; list[i] = list[j]; list[j] = swap;
            }
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                foreach (char character in value ?? string.Empty) hash = hash * 31 + character;
                return hash == 0 ? 17 : hash;
            }
        }
    }
}
