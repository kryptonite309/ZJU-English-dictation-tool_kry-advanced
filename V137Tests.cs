using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace EnglishDictationTool
{
    internal static class V137Tests
    {
        public static int Run(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            string root = Path.Combine(outputDirectory, "v137-data-" + Guid.NewGuid().ToString("N"));
            List<string> failures = new List<string>();
            try
            {
                BuildVocabulary(root);
                TestCrossDayAndManualSelection(root, failures);
                TestProblemFairness(root, failures);
                TestProblemFreeMode(root, failures);
                TestIncorrectSettlementRepair(root, failures);
            }
            catch (Exception error)
            {
                failures.Add(error.GetType().Name + ": " + error.Message + "\n" + error.StackTrace);
            }

            Dictionary<string, object> report = new Dictionary<string, object>
            {
                { "ok", failures.Count == 0 },
                { "failures", failures }
            };
            File.WriteAllText(Path.Combine(outputDirectory, "v137-report.json"),
                new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(report),
                new UTF8Encoding(false));
            try { if (Directory.Exists(root)) Directory.Delete(root, true); }
            catch { }
            return failures.Count == 0 ? 0 : 7;
        }

        private static void BuildVocabulary(string root)
        {
            string book = Path.Combine(root, "data", "book1");
            Directory.CreateDirectory(book);
            File.WriteAllText(Path.Combine(book, "unit1.csv"),
                "english,chinese,examples\n"
                + "alpha,阿尔法,e.g. [[alpha]] is first.\n"
                + "bravo,布拉沃,e.g. [[bravo]] follows.\n"
                + "charlie,查理,e.g. [[charlie]] appears.\n"
                + "delta,德尔塔,e.g. [[delta]] changes.\n"
                + "echo,回声,e.g. an [[echo]].\n"
                + "foxtrot,狐步,e.g. [[foxtrot]] now.\n",
                new UTF8Encoding(false));
        }

        private static void TestCrossDayAndManualSelection(string baseRoot, List<string> failures)
        {
            string root = Path.Combine(baseRoot, "cross-day");
            CopyVocabulary(baseRoot, root);
            NotebookStore notebooks = new NotebookStore(root);
            DataLoader loader = new DataLoader(Path.Combine(root, "data"));
            StudyStore store = new StudyStore(root, loader, notebooks);
            store.Settings.newExample = false;
            store.Settings.newDictation = false;
            store.Settings.newSpelling = true;
            store.Settings.listExample = false;
            store.Settings.listDictation = false;
            store.Settings.listSpelling = true;
            store.SaveSettings();

            DateTime day = new DateTime(2026, 10, 2, 10, 0, 0);
            StudyList first = store.ExtractNew(new Dictionary<string, int> { { "book1", 2 } }, day);
            CompletePreview(store, first.items.Count);
            store.Submit("alpha", day.AddMinutes(1));
            store.Submit("wrong", day.AddMinutes(2));
            store.SettleCrossDay(day.AddDays(1));
            Check(first.status == "active" && store.IsCrossDay(first, day.AddDays(1))
                && first.items.All(x => !x.released), failures,
                "regular list remains frozen across study days");
            store.ContinueCrossDay("new", day.AddDays(1));
            Check(!store.IsCrossDay(first, day.AddDays(1)), failures,
                "continue updates last active study day");
            StudyEndResult ended = store.EndActive("new", day.AddDays(1));
            Check(ended.kept == 1 && ended.released == 1 && first.items.Count == 1,
                failures, "ending new list keeps completed and returns unfinished");
            Check(store.AvailableCounts()["book1"] == 5, failures,
                "returned new word is a normal unextracted word");

            StudyList second = store.ExtractNew(new Dictionary<string, int> { { "book1", 2 } },
                day.AddDays(1).AddMinutes(1));
            Check(second.items[0].word.english == "bravo" && !second.items[0].carriedOver,
                failures, "returned new word has no carry-over priority flag");
            CompletePreview(store, second.items.Count);
            foreach (StudyItem item in second.items.ToList())
                store.Submit(item.word.english, day.AddDays(1).AddMinutes(2));

            StudyList manual = store.StartSelectedListReview(new[] { second.id, first.id },
                day.AddDays(2));
            Check(manual.items.Select(x => x.word.english).SequenceEqual(
                new[] { "bravo", "charlie", "alpha" }), failures,
                "manual selection preserves list order and word order");
            store.EndActive("list_review", day.AddDays(2).AddMinutes(1));
            StudyList deduped = store.StartSelectedListReview(new[] { manual.id, second.id },
                day.AddDays(2).AddMinutes(2));
            Check(deduped.items.Select(x => x.word.english).SequenceEqual(
                new[] { "bravo", "charlie", "alpha" }), failures,
                "manual selection keeps first occurrence when lists overlap");
            store.SettleCrossDay(day.AddDays(3));
            Check(deduped.status == "active" && deduped.items.Count == 3
                && store.IsCrossDay(deduped, day.AddDays(3)), failures,
                "historical review remains a complete frozen list across days");
            StudyEndResult reviewEnd = store.EndActive("list_review", day.AddDays(3));
            Check(reviewEnd.released == 3 && deduped.items.Count == 3
                && store.ReviewableLists().Any(x => x.id == deduped.id), failures,
                "ending historical review preserves complete reusable list");
        }

        private static void TestProblemFairness(string baseRoot, List<string> failures)
        {
            string root = Path.Combine(baseRoot, "fairness");
            CopyVocabulary(baseRoot, root);
            NotebookStore notebooks = new NotebookStore(root);
            DataLoader loader = new DataLoader(Path.Combine(root, "data"));
            List<WordEntry> words = loader.LoadWordList("book1", new[] { "unit1" }).Take(4).ToList();
            DateTime start = new DateTime(2026, 10, 2, 10, 0, 0);
            foreach (WordEntry word in words) notebooks.RecordAnswer(word, false, false);
            // Delta receives the highest error count and must win time-based tie breakers.
            notebooks.RecordAnswer(words[3], false, false);
            notebooks.RecordAnswer(words[3], false, false);

            StudyStore store = new StudyStore(root, loader, notebooks);
            store.Settings.problemCount = 2;
            store.Settings.problemExample = false;
            store.Settings.problemDictation = false;
            store.Settings.problemSpelling = true;
            store.SaveSettings();
            StudyList first = store.StartProblemReview(start);
            store.EndActive("problem_review", start.AddMinutes(1));
            StudyList second = store.StartProblemReview(start.AddDays(1));
            store.EndActive("problem_review", start.AddDays(1).AddMinutes(1));
            int[] counts = words.Select(x => notebooks.GetRecord(x).problemDrawCount).ToArray();
            Check(counts.Max() - counts.Min() < 2 && counts.All(x => x == 1), failures,
                "problem draw counts stay balanced within one");
            store.Settings.problemCount = 1;
            store.SaveSettings();
            StudyList tie = store.StartProblemReview(start.AddDays(2));
            Check(tie.items.Count == 1 && tie.items[0].word.english == "delta", failures,
                "error count is prioritized before last-answer time when fair counts tie");
            Check(first.items.Select(x => DataLoader.WordKey(x.word)).Intersect(
                second.items.Select(x => DataLoader.WordKey(x.word))).Count() == 0,
                failures, "fair round selects not-yet-selected words first");
        }

        private static void TestProblemFreeMode(string baseRoot, List<string> failures)
        {
            string root = Path.Combine(baseRoot, "problem-free");
            CopyVocabulary(baseRoot, root);
            DataLoader loader = new DataLoader(Path.Combine(root, "data"));
            NotebookStore notebooks = new NotebookStore(root);
            List<WordEntry> words = loader.LoadWordList("book1", new[] { "unit1" }).Take(3).ToList();
            notebooks.MoveToNotebook(words[0], Notebooks.Wrong);
            notebooks.MoveToNotebook(words[1], Notebooks.ErrorProne);
            notebooks.MoveToNotebook(words[2], Notebooks.Wrong);
            GameEngine engine = new GameEngine(loader, notebooks);
            bool started = engine.StartProblemFreeMode(notebooks.GetProblemWords(), false,
                false, true, new[] { "spelling", "dictation", "example" }, false, true);
            Check(started && engine.CurrentDeck.Count == 3
                && engine.CurrentDeck.Select(DataLoader.WordKey).Distinct().Count() == 3
                && engine.RetryOnWrong, failures,
                "problem free mode uses all unique wrong and error-prone words with retry");
            Check(StudySettings.Defaults().problemFreeRetryOnWrong, failures,
                "problem free retry defaults to enabled");
        }

        private static void TestIncorrectSettlementRepair(string baseRoot, List<string> failures)
        {
            string root = Path.Combine(baseRoot, "repair");
            CopyVocabulary(baseRoot, root);
            DataLoader loader = new DataLoader(Path.Combine(root, "data"));
            List<WordEntry> words = loader.LoadWordList("book1", new[] { "unit1" }).Take(2).ToList();
            DateTime sourceTime = new DateTime(2026, 9, 26, 15, 14, 48);
            DateTime reviewTime = new DateTime(2026, 9, 30, 19, 24, 54);
            DateTime badNewTime = new DateTime(2026, 10, 2, 15, 0, 7);
            StudySettings settings = StudySettings.Defaults();
            settings.newExample = false; settings.newDictation = false; settings.newSpelling = true;
            settings.listExample = false; settings.listDictation = false; settings.listSpelling = true;
            StudyList source = List("source", "new", sourceTime, "completed", words, false);
            source.items.ForEach(x => x.spellingComplete = true);
            StudyList badReview = List("20260930_192454", "list_review", reviewTime,
                "settled", words, false);
            badReview.items[0].spellingComplete = true;
            badReview.items[1].released = true;
            badReview.history.Add(new StudyTask { word = words[0], mode = "spelling",
                correct = true, answeredAt = reviewTime.AddMinutes(1), notebookCounted = true });
            StudyList badNew = List("20261002_150007", "new", badNewTime, "active",
                new List<WordEntry> { words[1] }, true);
            StudyState state = new StudyState
            {
                version = 1, settings = settings,
                words = words.Select(x => new StudyWord { word = x, book = "book1", unit = "unit1",
                    firstExtractedAt = badNewTime, priorityAt = reviewTime,
                    acceptedAnswers = new List<string>() }).ToList(),
                lists = new List<StudyList> { source, badReview, badNew },
                priorityWords = new List<WordEntry> { words[1] }, undo = new List<StudyUndo>(),
                activeListId = badNew.id
            };
            File.WriteAllText(Path.Combine(root, "study_state.json"),
                new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(state),
                new UTF8Encoding(false));
            NotebookStore notebooks = new NotebookStore(root);
            StudyStore repaired = new StudyStore(root, loader, notebooks);
            StudyList restored = repaired.Lists.Single(x => x.id == badReview.id);
            StudyList cleanedNew = repaired.Lists.Single(x => x.id == badNew.id);
            Check(restored.status == "active" && restored.paused
                && restored.items.All(x => !x.released), failures,
                "incorrectly settled historical review is restored as paused active list");
            Check(cleanedNew.status == "ended" && cleanedNew.items.Count == 0
                && repaired.AvailableCounts()["book1"] == 4, failures,
                "unattempted duplicate new-list item is removed without returning reviewed word to pool");
        }

        private static StudyList List(string id, string kind, DateTime time, string status,
            List<WordEntry> words, bool carried)
        {
            return new StudyList
            {
                id = id, kind = kind, createdAt = time, studyDate = StudyStore.StudyDayKey(time),
                lastActiveStudyDate = StudyStore.StudyDayKey(time), status = status,
                phase = status == "active" ? "preview" : "done",
                sourceListIds = new List<string>(),
                items = words.Select(x => new StudyItem { word = x, sourceBook = "book1",
                    sourceUnit = "unit1", carriedOver = carried }).ToList(),
                tasks = new List<StudyTask>(), retries = new List<StudyTask>(),
                history = new List<StudyTask>(), questionOrderSeed = 1,
                pausedPreviewReviewIndex = -1, pausedQuizReviewIndex = -1
            };
        }

        private static void CompletePreview(StudyStore store, int words)
        {
            for (int index = 0; index < words * 3; index++) store.AdvancePreview();
        }

        private static void CopyVocabulary(string sourceRoot, string targetRoot)
        {
            string source = Path.Combine(sourceRoot, "data", "book1", "unit1.csv");
            string target = Path.Combine(targetRoot, "data", "book1");
            Directory.CreateDirectory(target);
            File.Copy(source, Path.Combine(target, "unit1.csv"), true);
        }

        private static void Check(bool condition, List<string> failures, string name)
        {
            if (!condition) failures.Add(name);
        }
    }
}
