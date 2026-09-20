using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace EnglishDictationTool
{
    internal static class StudyTests
    {
        public static int Run(string root, string report)
        {
            if (Directory.Exists(root)) throw new IOException("测试目录已存在，拒绝覆盖：" + root);
            string book = Path.Combine(root, "data", "book1");
            Directory.CreateDirectory(book);
            File.WriteAllText(Path.Combine(book, "unit1.csv"),
                "english,chinese,examples\nalpha,阿尔法,e.g. [[alpha]] is first.\nbravo,布拉沃,e.g. [[bravo]] is second.\n",
                new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(book, "unit2.csv"),
                "english,chinese,examples\ncharlie,查理,e.g. [[charlie]] is third.\ndive into / in,投入,e.g. They [[dive]] in.\n",
                new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(root, "data", "parts_of_speech.csv"),
                "english,part_of_speech\nalpha,n.\nbravo,v.\ncharlie,adj.\ndive into / in,v.\n",
                new UTF8Encoding(false));
            NotebookStore notebooks = new NotebookStore(root);
            DataLoader loader = new DataLoader(Path.Combine(root, "data"));
            StudyStore store = new StudyStore(root, loader, notebooks);
            WordEntry alphaWithPart = loader.LoadWordList("book1", new[] { "unit1" })[0];
            WordEntry phraseWithoutPart = loader.LoadWordList("book1", new[] { "unit2" })[1];
            Require(alphaWithPart.partOfSpeech == "n."
                && PartOfSpeech.DisplayChinese(alphaWithPart).StartsWith("n. ")
                && GameEngine.ChineseHint(alphaWithPart).StartsWith("n. "),
                "单词从离线映射加载词性并显示在释义前");
            Require(string.IsNullOrEmpty(phraseWithoutPart.partOfSpeech)
                && PartOfSpeech.DisplayChinese(phraseWithoutPart) == "投入",
                "词组即使映射中有值也不显示词性");
            Require(PartOfSpeech.Normalize("noun / vt. / vi.") == "n./v."
                && PartOfSpeech.SelectForDefinition("due", "(not before noun) expected 预期的",
                    "n./adj.") == "adj."
                && PartOfSpeech.SelectForDefinition("spill", "v.(cause to) flow 溢出",
                    "n./v.") == "v."
                && PartOfSpeech.DisplayChinese(new WordEntry { english = "cord",
                    chinese = "1) an electrical wire 电线\n2) a piece of rope 细绳",
                    partOfSpeech = "n." }).StartsWith("n. "),
                "词性缩写归一且当前释义标记优先");

            string duplicateRoot = Path.Combine(root, "duplicate_words_test");
            string duplicateBook = Path.Combine(duplicateRoot, "data", "book1");
            Directory.CreateDirectory(duplicateBook);
            File.WriteAllText(Path.Combine(duplicateBook, "unit1.csv"),
                "english,chinese,examples\nerase,释义一,e.g. [[erase]] it.；e.g. It was [[erased]].\nerase,释义二,e.g. [[erase]] it.；e.g. It was [[erased]].\nalpha,阿尔法,e.g. [[alpha]].\n",
                new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(duplicateBook, "unit2.csv"),
                "english,chinese,examples\nalpha,第二处释义,e.g. [[alpha]] again.\n",
                new UTF8Encoding(false));
            DataLoader duplicateLoader = new DataLoader(Path.Combine(duplicateRoot, "data"));
            List<WordEntry> duplicateWords = duplicateLoader.LoadWordList("book1",
                new[] { "unit1", "unit2" });
            WordEntry mergedErase = duplicateWords.Single(x => x.english == "erase");
            Require(duplicateWords.Count == 2 && mergedErase.chinese.Contains("释义一")
                && mergedErase.chinese.Contains("释义二")
                && mergedErase.examples.Split('；').Length == 2
                && ExampleCloze.Questions(mergedErase).Count == 2,
                "相同英文合并为一个词并保留全部释义与不重复例句");
            NotebookStore duplicateNotebooks = new NotebookStore(duplicateRoot);
            WordEntry eraseOne = new WordEntry { english = "erase", chinese = "释义一",
                examples = "e.g. [[erase]] it." };
            WordEntry eraseTwo = new WordEntry { english = "erase", chinese = "释义二",
                examples = "e.g. It was [[erased]]." };
            duplicateNotebooks.MoveToNotebook(eraseOne, Notebooks.Wrong);
            duplicateNotebooks.MoveToNotebook(eraseTwo, Notebooks.Mastered);
            Require(duplicateNotebooks.Records.Count(x => DataLoader.WordKey(x.word) == "erase") == 1
                && duplicateNotebooks.GetNotebook(eraseOne) == Notebooks.Mastered,
                "同一英文在单词本中共用归属且斩词全局生效");
            GameEngine duplicateEngine = new GameEngine(duplicateLoader, duplicateNotebooks);
            duplicateEngine.StartGame("book1", new List<string> { "unit1", "unit2" }, "all",
                "sequential", "word", false, false);
            Require(duplicateEngine.CurrentDeck.Count == 1
                && duplicateEngine.CurrentDeck[0].english == "alpha",
                "自由练习不重复出题且已斩同名词不再出现");

            string multiExampleRoot = Path.Combine(root, "multiple_examples_test");
            string multiExampleBook = Path.Combine(multiExampleRoot, "data", "book1");
            Directory.CreateDirectory(multiExampleBook);
            File.WriteAllText(Path.Combine(multiExampleBook, "unit1.csv"),
                "english,chinese,examples\nerase,释义一,e.g. [[erase]] it.\nerase,释义二,e.g. It was [[erased]].\n",
                new UTF8Encoding(false));
            DataLoader multiExampleLoader = new DataLoader(Path.Combine(multiExampleRoot, "data"));
            NotebookStore multiExampleNotebooks = new NotebookStore(multiExampleRoot);
            StudyStore multiExampleStore = new StudyStore(multiExampleRoot,
                multiExampleLoader, multiExampleNotebooks);
            multiExampleStore.Settings.newExample = true;
            multiExampleStore.Settings.newSpelling = false;
            multiExampleStore.Settings.exampleCorrectTarget = 1;
            multiExampleStore.Settings.spellingCorrectTarget = 0;
            multiExampleStore.SaveSettings();
            WordEntry multiErase = multiExampleLoader.LoadWordList("book1", new[] { "unit1" }).Single();
            multiExampleNotebooks.MoveToNotebook(multiErase, Notebooks.Wrong);
            DateTime multiExampleNow = DateTime.Now.AddHours(1);
            StudyList multiExampleList = multiExampleStore.ExtractNew(
                new Dictionary<string, int> { { "book1", 1 } }, multiExampleNow);
            for (int step = 0; step < 3; step++) multiExampleStore.AdvancePreview();
            Require(multiExampleList.items.Count == 1 && multiExampleList.tasks.Count == 2
                && multiExampleList.tasks.Select(x => x.examplePrompt).Distinct().Count() == 2,
                "同一单词的所有不同例句都生成独立题目");
            Require(multiExampleStore.Submit("erase", multiExampleNow.AddMinutes(1)).Correct
                && multiExampleNotebooks.GetRecord(multiErase).exampleCorrectCount == 0
                && multiExampleList.history.Count(x => x.notebookCounted) == 0,
                "尚有例句未完成时不提前计入答对次数");
            multiExampleStore.Undo();
            Require(multiExampleStore.CurrentTask().exampleAnswer == "erased",
                "撤销上一条例句时保留当前例句");
            Require(multiExampleStore.Submit("erased", multiExampleNow.AddMinutes(2)).Correct
                && multiExampleNotebooks.GetRecord(multiErase).exampleCorrectCount == 0
                && multiExampleStore.CurrentTask().exampleAnswer == "erase",
                "撤销的具体例句会在当前题后精确重做");
            Require(multiExampleStore.Submit("erase", multiExampleNow.AddMinutes(3)).Correct
                && multiExampleNotebooks.GetRecord(multiErase).exampleCorrectCount == 1
                && multiExampleStore.Lists.Single().history.Count(x => x.notebookCounted) == 1
                && multiExampleStore.Active == null,
                "全部例句完成后每词每列表只统计一次");
            GameEngine multiExampleEngine = new GameEngine(multiExampleLoader,
                new NotebookStore(multiExampleRoot));
            multiExampleNotebooks.MoveToNotebook(multiErase, Notebooks.None);
            multiExampleEngine = new GameEngine(multiExampleLoader, multiExampleNotebooks);
            multiExampleEngine.StartGame("book1", new List<string> { "unit1" }, "all",
                "sequential", "example", false, false);
            Require(multiExampleEngine.CurrentDeck.Count == 2
                && multiExampleEngine.GetExampleQuestion().answer == "erase",
                "自由练习也会展开同词的所有例句");
            multiExampleEngine.CheckAnswer("erase");
            Require(multiExampleEngine.GetExampleQuestion().answer == "erased",
                "自由练习中第二条例句不会被合并丢失");
            string duplicateSecondBook = Path.Combine(duplicateRoot, "data", "book2");
            Directory.CreateDirectory(duplicateSecondBook);
            File.WriteAllText(Path.Combine(duplicateSecondBook, "unit1.csv"),
                "english,chinese,examples\nalpha,另一词书的阿尔法,e.g. [[alpha]].\nbeta,贝塔,e.g. [[beta]].\n",
                new UTF8Encoding(false));
            StudyStore duplicateStore = new StudyStore(duplicateRoot, duplicateLoader, duplicateNotebooks);
            StudyList duplicateQuotaList = duplicateStore.ExtractNew(new Dictionary<string, int>
                { { "book1", 1 }, { "book2", 1 } }, DateTime.Now);
            Require(duplicateQuotaList.items.Count == 2
                && duplicateQuotaList.items.Select(x => DataLoader.WordKey(x.word)).Distinct().Count() == 2
                && duplicateQuotaList.items.Any(x => x.word.english == "beta"),
                "跨词书配额会跳过已选同名词并继续补足新词");

            string duplicateMigrationRoot = Path.Combine(root, "duplicate_migration_test");
            string duplicateMigrationBook = Path.Combine(duplicateMigrationRoot, "data", "book1");
            Directory.CreateDirectory(duplicateMigrationBook);
            File.WriteAllText(Path.Combine(duplicateMigrationBook, "unit1.csv"),
                "english,chinese,examples\nerase,释义一,e.g. [[erase]] it.\nerase,释义二,e.g. It was [[erased]].\n",
                new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(duplicateMigrationRoot, "data", "parts_of_speech.csv"),
                "english,part_of_speech\nerase,v.\n", new UTF8Encoding(false));
            NotebookStore duplicateMigrationNotebooks = new NotebookStore(duplicateMigrationRoot);
            DateTime migrationNow = DateTime.Now;
            StudySettings migrationSettings = StudySettings.Defaults();
            migrationSettings.newExample = true;
            migrationSettings.newSpelling = false;
            StudyState legacyDuplicateState = new StudyState
            {
                version = 1,
                settings = migrationSettings,
                words = new List<StudyWord>
                {
                    new StudyWord { word = eraseOne, book = "book1", unit = "unit1",
                        firstExtractedAt = migrationNow, acceptedAnswers = new List<string>() },
                    new StudyWord { word = eraseTwo, book = "book1", unit = "unit1",
                        firstExtractedAt = migrationNow, acceptedAnswers = new List<string>() }
                },
                lists = new List<StudyList>
                {
                    new StudyList
                    {
                        id = "duplicate_active", kind = "new", createdAt = migrationNow,
                        studyDate = StudyStore.StudyDayKey(migrationNow), status = "active", phase = "quiz",
                        sourceListIds = new List<string>(), previewCursor = 2,
                        items = new List<StudyItem>
                        {
                            new StudyItem { word = eraseOne, exampleComplete = true,
                                firstAnsweredAt = migrationNow },
                            new StudyItem { word = eraseTwo, exampleComplete = false }
                        },
                        tasks = new List<StudyTask>
                        {
                            new StudyTask { word = eraseOne, mode = "example" },
                            new StudyTask { word = eraseTwo, mode = "example" }
                        },
                        retries = new List<StudyTask>(),
                        history = new List<StudyTask>
                        {
                            new StudyTask { word = eraseOne, mode = "example",
                                examplePrompt = "e.g. ________ it.", exampleAnswer = "erase",
                                exampleAnswers = new List<string> { "erase" }, correct = true,
                                answeredAt = migrationNow }
                        }
                    }
                },
                priorityWords = new List<WordEntry>(), undo = new List<StudyUndo>(),
                activeListId = "duplicate_active"
            };
            string legacyDuplicateJson = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }
                .Serialize(legacyDuplicateState).Replace(",\"notebookCounted\":false", string.Empty);
            File.WriteAllText(Path.Combine(duplicateMigrationRoot, "study_state.json"),
                legacyDuplicateJson, new UTF8Encoding(false));
            StudyStore migratedDuplicates = new StudyStore(duplicateMigrationRoot,
                new DataLoader(Path.Combine(duplicateMigrationRoot, "data")), duplicateMigrationNotebooks);
            StudyList migratedDuplicateList = migratedDuplicates.Lists.Single();
            Require(migratedDuplicateList.items.Count == 1
                && migratedDuplicateList.items[0].word.chinese.Contains("释义一")
                && migratedDuplicateList.items[0].word.chinese.Contains("释义二")
                && !migratedDuplicateList.items[0].exampleComplete
                && migratedDuplicateList.tasks.Count == 1
                && migratedDuplicateList.tasks[0].exampleAnswer == "erased"
                && migratedDuplicateList.history.Count(x => x.notebookCounted) == 1
                && migratedDuplicateList.items[0].word.partOfSpeech == "v."
                && PartOfSpeech.DisplayChinese(migratedDuplicateList.items[0].word).StartsWith("v. "),
                "旧进度合并后保留已做例句并只补做未出现例句");

            string archivedMigrationRoot = Path.Combine(root, "archived_pos_migration_test");
            string archivedMigrationBook = Path.Combine(archivedMigrationRoot, "data", "book1");
            Directory.CreateDirectory(archivedMigrationBook);
            File.WriteAllText(Path.Combine(archivedMigrationBook, "unit1.csv"),
                "english,chinese,examples\narchive,档案,e.g. an [[archive]].\n",
                new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(archivedMigrationRoot, "data", "parts_of_speech.csv"),
                "english,part_of_speech\narchive,n.\n", new UTF8Encoding(false));
            WordEntry archivedWord = new WordEntry { english = "archive", chinese = "档案",
                examples = "e.g. an [[archive]]." };
            StudyState archivedState = new StudyState
            {
                version = 1, settings = StudySettings.Defaults(), words = new List<StudyWord>(),
                lists = new List<StudyList>
                {
                    new StudyList
                    {
                        id = "archived", kind = "new", createdAt = migrationNow,
                        studyDate = StudyStore.StudyDayKey(migrationNow), status = "settled",
                        phase = "done", sourceListIds = new List<string>(), previewCursor = 1,
                        items = new List<StudyItem> { new StudyItem { word = archivedWord,
                            spellingComplete = true, firstAnsweredAt = migrationNow } },
                        tasks = new List<StudyTask>(), retries = new List<StudyTask>(),
                        history = new List<StudyTask> { new StudyTask { word = archivedWord,
                            mode = "spelling", correct = true, answeredAt = migrationNow } }
                    }
                },
                priorityWords = new List<WordEntry>(), undo = new List<StudyUndo>(),
                activeListId = null
            };
            File.WriteAllText(Path.Combine(archivedMigrationRoot, "study_state.json"),
                new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(archivedState),
                new UTF8Encoding(false));
            NotebookStore archivedNotebooks = new NotebookStore(archivedMigrationRoot);
            NotebookState legacyNotebookSnapshot = new NotebookState
            {
                version = 1, reviewFirstLetter = true, reviewCorrectTarget = 3,
                masteryShortcut = 1,
                records = new List<NotebookRecord> { new NotebookRecord { word = archivedWord,
                    notebook = Notebooks.Wrong, errorCount = 1 } },
                recent = new List<WordEntry> { archivedWord }
            };
            archivedNotebooks.RestoreSnapshot(
                new JavaScriptSerializer().Serialize(legacyNotebookSnapshot));
            Require(archivedNotebooks.Records.Single().word.partOfSpeech == "n.",
                "撤销恢复的旧单词本快照会重新补齐词性");
            StudyStore migratedArchive = new StudyStore(archivedMigrationRoot,
                new DataLoader(Path.Combine(archivedMigrationRoot, "data")),
                archivedNotebooks);
            StudyList archivedList = migratedArchive.Lists.Single();
            Require(archivedList.items[0].word.partOfSpeech == "n."
                && archivedList.history[0].word.partOfSpeech == "n.",
                "已离开主单词池的历史列表词条也会补齐词性");
            Require(store.Settings.exampleFirstLetterHints
                && store.Settings.exampleHintKey == (int)System.Windows.Forms.Keys.Enter,
                "所有例句首字母提示默认开启且提示键为 Enter");
            Require(!ExampleRevealFlow.ShowsFirstLetter(0, true)
                && !ExampleRevealFlow.ShowsMeaning(0, true)
                && ExampleRevealFlow.ShowsFirstLetter(1, true)
                && !ExampleRevealFlow.ShowsMeaning(1, true)
                && ExampleRevealFlow.ShowsMeaning(2, true)
                && ExampleRevealFlow.ReadyToSubmit(2, true),
                "例句提示按无提示、首字母、中文、提交分阶段进行");
            Require(!ExampleRevealFlow.ShowsFirstLetter(1, false)
                && ExampleRevealFlow.ShowsMeaning(1, false)
                && ExampleRevealFlow.ReadyToSubmit(1, false),
                "关闭首字母后第一次提示键直接显示中文");
            Require(ExampleRevealFlow.HasTypedAnswer("typed answer")
                && !ExampleRevealFlow.HasTypedAnswer("   "),
                "例句输入非空时 Enter 可直接提交，纯空白仍逐层提示");
            Require(StudyStore.StudyDayKey(new DateTime(2026, 9, 17, 4, 0, 59)) == "2026-09-16",
                "4 点整归前一学习日");
            Require(StudyStore.StudyDayKey(new DateTime(2026, 9, 17, 4, 1, 0)) == "2026-09-17",
                "4 点 1 分归新学习日");
            DateTime firstDay = DateTime.Now.Date.AddDays(1).AddHours(10);
            StudyList first = store.ExtractNew(new Dictionary<string, int> { { "book1", 2 } }, firstDay);
            Require(first.items.Count == 2 && first.items[0].word.english == "alpha"
                && first.items[1].word.english == "bravo", "单元顺序提取");
            Require(File.Exists(Path.Combine(root, "study_lists", first.id + ".json")), "独立列表文件");
            Require(store.AvailableCounts()["book1"] == 2, "提取后余量");
            for (int i = 0; i < 6; i++) store.AdvancePreview();
            Require(store.Active.phase == "quiz" && store.CurrentTask().word.english == "alpha", "展示后拼写");
            Require(store.Submit("ALPHA", firstDay.AddMinutes(1)).Correct, "大小写宽容");
            Require(!store.Submit("brav0", firstDay.AddMinutes(2)).Correct, "拼写错误不宽容");
            Require(first.history.Count == 2 && first.history[0].submittedAnswer == "ALPHA"
                && first.history[1].submittedAnswer == "brav0" && !first.history[1].correct,
                "已答题目保留只读回看记录与当时作答");
            Require(store.CurrentTask().replay, "错题进入复现轮");
            Require(first.history.Count == 2, "进入复现轮不会丢失此前题目历史");
            Require(notebooks.GetNotebook(first.items[1].word) == Notebooks.Wrong, "答错进入错题本");
            store.SettleCrossDay(firstDay.AddDays(1));
            Require(first.status == "settled" && first.items[1].released && !first.items[0].released,
                "跨日未复现返池，已答对留原列表");
            Require(store.AvailableCounts()["book1"] == 3, "返池恢复未提取余量");
            StudyList second = store.ExtractNew(new Dictionary<string, int> { { "book1", 2 } },
                firstDay.AddDays(1).AddMinutes(1));
            Require(second.items[0].word.english == "bravo" && second.items[0].carriedOver,
                "跨日词优先于单元顺序");
            Require(second.items[1].word.english == "charlie", "余下继续按单元抽取");
            for (int i = 0; i < 6; i++) store.AdvancePreview();
            Require(store.CurrentTask().word.english == "bravo", "继续后的首题");
            store.Submit("bravo", firstDay.AddDays(1).AddMinutes(2));
            Require(store.CurrentTask().word.english == "charlie", "第二题未受影响");
            WordEntry undone = store.Undo();
            Require(undone.english == "bravo" && store.CurrentTask().word.english == "charlie",
                "撤销后当前题不变");
            store.Submit("charlie", firstDay.AddDays(1).AddMinutes(3));
            Require(store.CurrentTask().word.english == "bravo", "撤销词下题再做");
            store.Submit("bravo", firstDay.AddDays(1).AddMinutes(4));
            Require(store.Active == null, "所有词答对后完成列表");
            WordEntry dive = loader.LoadWordList("book1", new[] { "unit2" })[1];
            Require(store.IsCorrect(dive, "dive into / in"), "原文严格匹配");
            Require(!store.IsCorrect(dive, "dive in"), "默认不接受斜线展开");
            store.Settings.fuzzyAnswers = true;
            store.SaveSettings();
            Require(store.IsCorrect(dive, "dive into") && store.IsCorrect(dive, "dive in"),
                "模糊作答展开并集");
            store.SetAcceptedAnswers(dive, new[] { "jump in" });
            Require(store.IsCorrect(dive, "jump  in"), "手动答案与多空格");

            WordEntry unlearn = new WordEntry
            {
                english = "unlearn", chinese = "忘掉已学内容",
                examples = "e.g. You must start by [[unlearning]] bad habits."
            };
            ExampleQuestion unlearnQuestion = ExampleCloze.First(unlearn);
            Require(unlearnQuestion != null && unlearnQuestion.answer == "unlearning"
                && unlearnQuestion.prompt.Contains("by ________ bad"),
                "例句填空读取标记中的实际词形");
            Require(store.IsCorrect(unlearn, "UNLEARNING", "example", unlearnQuestion.answers)
                && !store.IsCorrect(unlearn, "unlearn", "example", unlearnQuestion.answers),
                "例句题接受句中词形并拒绝不合语法的原形");
            Require(store.IsCorrect(unlearn, "unlearn") && !store.IsCorrect(unlearn, "unlearning"),
                "普通拼写仍判断词条原形");
            WordEntry phraseCloze = new WordEntry
            {
                english = "come and go", chinese = "来来去去",
                examples = "e.g. The pain [[comes]] and [[goes]]."
            };
            ExampleQuestion phraseQuestion = ExampleCloze.First(phraseCloze);
            Require(phraseQuestion != null && phraseQuestion.answer == "comes and goes"
                && phraseQuestion.prompt == "e.g. The pain ________.",
                "分散标记的短语合并成一个自然填空");
            WordEntry noisyCloze = new WordEntry
            {
                english = "resurface", chinese = "再次浮出水面",
                examples = "e.g. When the [[divers]] did not [[resurface]], help arrived."
            };
            ExampleQuestion noisyQuestion = ExampleCloze.First(noisyCloze);
            Require(noisyQuestion != null && noisyQuestion.answer == "resurface"
                && noisyQuestion.prompt.Contains("the divers did not ________"),
                "忽略例句中与当前词条无关的额外标记");

            string clozeRoot = Path.Combine(root, "cloze_inflection_test");
            string clozeBook = Path.Combine(clozeRoot, "data", "book1");
            Directory.CreateDirectory(clozeBook);
            File.WriteAllText(Path.Combine(clozeBook, "unit1.csv"),
                "english,chinese,examples\nunlearn,忘掉已学内容,e.g. You must start by [[unlearning]] bad habits.\n",
                new UTF8Encoding(false));
            NotebookStore clozeNotebooks = new NotebookStore(clozeRoot);
            StudyStore clozeStore = new StudyStore(clozeRoot,
                new DataLoader(Path.Combine(clozeRoot, "data")), clozeNotebooks);
            clozeStore.Settings.newExample = true;
            clozeStore.Settings.newSpelling = false;
            clozeStore.SaveSettings();
            DateTime clozeDay = firstDay.AddHours(1);
            clozeStore.ExtractNew(new Dictionary<string, int> { { "book1", 1 } }, clozeDay);
            for (int step = 0; step < 3; step++) clozeStore.AdvancePreview();
            Require(clozeStore.CurrentTask().exampleAnswer == "unlearning", "每日学习保存当前例句答案");
            AnswerOutcome wrongForm = clozeStore.Submit("unlearn", clozeDay.AddMinutes(1));
            Require(!wrongForm.Correct && wrongForm.CorrectAnswer == "unlearning",
                "错误反馈显示例句实际答案");
            AnswerOutcome correctedForm = clozeStore.Submit("unlearning", clozeDay.AddMinutes(2));
            Require(correctedForm.Correct && clozeStore.Active == null,
                "复现时按实际词形答对并完成列表");
            AppearanceSettings legacyFeedbackSettings = AppearanceSettings.Defaults();
            legacyFeedbackSettings.prompts["correct"] = "✓ 正确：{word}";
            File.WriteAllText(Path.Combine(root, "appearance.json"),
                new JavaScriptSerializer().Serialize(legacyFeedbackSettings),
                new UTF8Encoding(false));
            AppearanceStore feedbackAppearance = new AppearanceStore(root);
            WordEntry formulate = new WordEntry { english = "formulate", chinese = "制定" };
            Require(feedbackAppearance.Prompt("correct", formulate, "formulating",
                    "formulating") == "✓ 正确：formulating",
                "旧默认答对提示迁移为显示例句中的实际答案词形");
            GameEngine clozeEngine = new GameEngine(
                new DataLoader(Path.Combine(clozeRoot, "data")), clozeNotebooks);
            clozeEngine.AnswerValidator = clozeStore.IsCorrect;
            clozeEngine.StartGame("book1", new List<string> { "unit1" }, "all", "sequential",
                "example", false, false);
            Require(clozeEngine.GetExampleQuestion().answer == "unlearning"
                && clozeEngine.CheckAnswer("unlearning").Correct,
                "自由练习例句题同样按当前句中的词形判定");
            StudyList review = store.StartListReview(firstDay.AddDays(2));
            Require(review.sourceListIds.Count == 2 && review.sourceListIds[0] == first.id
                && review.sourceListIds[1] == second.id, "按时间正序呈现过去列表");
            Require(review.items.Count(x => x.word.english == "bravo") == 1,
                "跨日返池词不从旧列表复习");
            store.SettleCrossDay(firstDay.AddDays(3));
            store.Settings.problemExample = true;
            store.Settings.problemSpelling = true;
            store.SaveSettings();
            StudyList problem1 = store.StartProblemReview(firstDay.AddDays(3).AddMinutes(1));
            Require(problem1.tasks.Count == 2 && problem1.tasks[0].mode == "example"
                && problem1.tasks[1].mode == "spelling", "例句与拼写同时开启且例句在前");
            Require(!store.Submit("wrong", firstDay.AddDays(3).AddMinutes(2)).Correct,
                "例句答错进入复现");
            store.Submit("bravo", firstDay.AddDays(3).AddMinutes(3));
            Require(store.CurrentTask().replay && store.CurrentTask().mode == "example",
                "首次轮次后复现例句错题");
            store.Submit("bravo", firstDay.AddDays(3).AddMinutes(4));
            Require(notebooks.GetRecord(problem1.items[0].word).exampleCorrectCount == 0,
                "复现答对不计入例句正确次数");
            Require(notebooks.GetRecord(problem1.items[0].word).spellingCorrectCount == 1,
                "拼写首次答对独立计数");
            for (int day = 4; day <= 5; day++)
            {
                store.StartProblemReview(firstDay.AddDays(day));
                store.Submit("bravo", firstDay.AddDays(day).AddMinutes(1));
                store.Submit("bravo", firstDay.AddDays(day).AddMinutes(2));
            }
            Require(notebooks.GetNotebook(problem1.items[0].word) == Notebooks.ErrorProne,
                "例句 0 / 拼写 3 后移入易错本");
            store.StartProblemReview(firstDay.AddDays(6));
            store.Submit("bravo", firstDay.AddDays(6).AddMinutes(1));
            store.Submit("bravo", firstDay.AddDays(6).AddMinutes(2));
            Require(notebooks.GetNotebook(problem1.items[0].word) == Notebooks.ErrorProne,
                "易错本不会因答对自动斩除");
            List<StudyList> newer = new List<StudyList>();
            for (int day = 7; day <= 9; day++)
            {
                DateTime when = firstDay.AddDays(day);
                StudyList batch = store.ExtractNew(new Dictionary<string, int> { { "book1", 1 } }, when);
                newer.Add(batch);
                for (int step = 0; step < 3; step++) store.AdvancePreview();
                store.Submit(batch.items[0].word.english, when.AddMinutes(1));
            }
            store.Settings.listCount = 4;
            store.SaveSettings();
            StudyList latestFour = store.StartListReview(firstDay.AddDays(10));
            Require(latestFour.sourceListIds.Count == 4 && latestFour.sourceListIds[0] == second.id
                && latestFour.sourceListIds[1] == newer[0].id
                && latestFour.sourceListIds[3] == newer[2].id,
                "b 按最近四个列表筛选，再按时间正序呈现");
            store.SettleCrossDay(firstDay.AddDays(11));
            string masteryRoot = Path.Combine(root, "mastery_test");
            string masteryBook = Path.Combine(masteryRoot, "data", "book1");
            Directory.CreateDirectory(masteryBook);
            File.WriteAllText(Path.Combine(masteryBook, "unit1.csv"),
                "english,chinese,examples\nalpha,阿尔法,\nbravo,布拉沃,\n", new UTF8Encoding(false));
            NotebookStore masteryNotebooks = new NotebookStore(masteryRoot);
            StudyStore masteryStore = new StudyStore(masteryRoot,
                new DataLoader(Path.Combine(masteryRoot, "data")), masteryNotebooks);
            masteryStore.ExtractNew(new Dictionary<string, int> { { "book1", 2 } }, firstDay.AddDays(12));
            WordEntry mastered = masteryStore.MasterCurrent(firstDay.AddDays(12).AddMinutes(1));
            Require(mastered.english == "alpha" && masteryNotebooks.GetNotebook(mastered) == Notebooks.Mastered
                && masteryStore.CurrentPreview().word.english == "bravo", "斩词最高优先级并跳到下一词");
            masteryStore.Undo();
            Require(masteryNotebooks.GetNotebook(mastered) == Notebooks.None
                && masteryStore.CurrentPreview().word.english == "bravo", "撤销斩词后当前展示不变");
            for (int step = 0; step < 3; step++) masteryStore.AdvancePreview();
            Require(masteryStore.CurrentPreview().word.english == "alpha", "撤销的词排在下一词");
            string rolloverRoot = Path.Combine(root, "rollover_undo_test");
            string rolloverBook = Path.Combine(rolloverRoot, "data", "book1");
            Directory.CreateDirectory(rolloverBook);
            File.WriteAllText(Path.Combine(rolloverBook, "unit1.csv"),
                "english,chinese,examples\nbravo,布拉沃,\n", new UTF8Encoding(false));
            NotebookStore rolloverNotebooks = new NotebookStore(rolloverRoot);
            StudyStore rolloverStore = new StudyStore(rolloverRoot,
                new DataLoader(Path.Combine(rolloverRoot, "data")), rolloverNotebooks);
            DateTime rolloverDay = firstDay.AddDays(13);
            rolloverStore.ExtractNew(new Dictionary<string, int> { { "book1", 1 } }, rolloverDay);
            for (int step = 0; step < 3; step++) rolloverStore.AdvancePreview();
            rolloverStore.Submit("wrong", rolloverDay.AddMinutes(1));
            rolloverStore.SettleCrossDay(rolloverDay.AddDays(1));
            rolloverStore.Undo();
            Require(rolloverStore.Active == null && rolloverNotebooks.Count(Notebooks.Wrong) == 0,
                "跨日撤销不会重新打开昨天列表");
            Require(rolloverStore.ExtractNew(new Dictionary<string, int> { { "book1", 1 } },
                rolloverDay.AddDays(1).AddMinutes(1)).items[0].word.english == "bravo",
                "跨日撤销后单词仍在优先词池");

            string independentRoot = Path.Combine(root, "independent_sessions_test");
            string independentBook = Path.Combine(independentRoot, "data", "book1");
            Directory.CreateDirectory(independentBook);
            File.WriteAllText(Path.Combine(independentBook, "unit1.csv"),
                "english,chinese,examples\nalpha,阿尔法,e.g. [[alpha]].\nbravo,布拉沃,e.g. [[bravo]].\ncharlie,查理,e.g. [[charlie]].\ndelta,德尔塔,e.g. [[delta]].\n",
                new UTF8Encoding(false));
            NotebookStore independentNotebooks = new NotebookStore(independentRoot);
            DataLoader independentLoader = new DataLoader(Path.Combine(independentRoot, "data"));
            StudyStore independentStore = new StudyStore(independentRoot, independentLoader, independentNotebooks);
            DateTime independentDay = firstDay.AddDays(20);
            StudyList sourceList = independentStore.ExtractNew(
                new Dictionary<string, int> { { "book1", 2 } }, independentDay);
            for (int step = 0; step < 6; step++) independentStore.AdvancePreview();
            independentStore.Submit("alpha", independentDay.AddMinutes(1));
            independentStore.Submit("bravo", independentDay.AddMinutes(2));
            StudyList unfinishedNew = independentStore.ExtractNew(
                new Dictionary<string, int> { { "book1", 2 } }, independentDay.AddDays(1));
            StudyList unfinishedReview = independentStore.StartListReview(independentDay.AddDays(1).AddMinutes(1));
            independentNotebooks.MoveToNotebook(sourceList.items[0].word, Notebooks.Wrong);
            StudyList unfinishedProblems = independentStore.StartProblemReview(
                independentDay.AddDays(1).AddMinutes(1).AddSeconds(1));
            Require(independentStore.ActiveFor("new") == unfinishedNew
                && independentStore.ActiveFor("list_review") == unfinishedReview
                && independentStore.ActiveFor("problem_review") == unfinishedProblems
                && independentStore.Active == unfinishedProblems, "三个每日模块分别保存未完成列表");
            independentStore.Settings.newExample = true;
            independentStore.Settings.newSpelling = false;
            independentStore.SaveSettings();
            independentStore.Resume("new");
            for (int step = 0; step < 6; step++) independentStore.AdvancePreview();
            Require(independentStore.CurrentTask().mode == "example"
                && independentStore.Active.tasks.All(x => x.mode == "example"),
                "重新进入时立即应用新学题型设置");
            independentStore.Settings.newExample = false;
            independentStore.Settings.newSpelling = true;
            independentStore.SaveSettings();
            independentStore.Resume("new");
            Require(independentStore.CurrentTask().mode == "spelling"
                && independentStore.Active.tasks.All(x => x.mode == "spelling"),
                "再次更改题型后重建未完成题目");
            independentStore.Submit("charlie", independentDay.AddDays(1).AddMinutes(2));
            StudyEndResult endedNew = independentStore.EndActive("new",
                independentDay.AddDays(1).AddMinutes(3));
            Require(endedNew.kept == 1 && endedNew.released == 1
                && independentStore.ActiveFor("new") == null
                && independentStore.ActiveFor("list_review") != null
                && independentStore.ActiveFor("problem_review") != null,
                "手动结束新学仅保留完成词且不影响复习模块");
            StudyList priorityList = independentStore.ExtractNew(
                new Dictionary<string, int> { { "book1", 1 } }, independentDay.AddDays(1).AddMinutes(4));
            Require(priorityList.items[0].word.english == "delta" && priorityList.items[0].carriedOver,
                "手动结束释放词回到新词池顶端");
            independentStore.Settings.listExample = false;
            independentStore.Settings.listSpelling = true;
            independentStore.SaveSettings();
            independentStore.Resume("list_review");
            independentStore.Submit("alpha", independentDay.AddDays(1).AddMinutes(5));
            StudyEndResult endedReview = independentStore.EndActive("list_review",
                independentDay.AddDays(1).AddMinutes(6));
            Require(endedReview.kept == 1 && endedReview.released == 1,
                "手动结束历史复习只保留已完成词");
            StudyList resumedReview = independentStore.StartListReview(independentDay.AddDays(1).AddMinutes(7));
            Require(resumedReview.items.Count == 1 && resumedReview.items[0].word.english == "bravo",
                "历史复习未完成词可在当天重新进入后续列表");

            string libraryRoot = Path.Combine(root, "library_import_test");
            Directory.CreateDirectory(Path.Combine(libraryRoot, "data", "book1"));
            File.WriteAllText(Path.Combine(libraryRoot, "data", "book1", "unit1.csv"),
                "english,chinese\nbase,基础\n", new UTF8Encoding(false));
            string externalCsv = Path.Combine(libraryRoot, "external.csv");
            File.WriteAllText(externalCsv,
                "english,chinese,examples,part_of_speech\nimported,导入的,e.g. [[imported]].,noun\n",
                new UTF8Encoding(false));
            DataLoader libraryLoader = new DataLoader(Path.Combine(libraryRoot, "data"));
            Require(libraryLoader.ValidateCsvFile(externalCsv) == 1, "外部 CSV 表头与词数校验");
            libraryLoader.ImportCsv(externalCsv, "custom", "unitA", false);
            WordEntry importedWithPart = libraryLoader.LoadWordList("custom", new[] { "unitA" })[0];
            Require(libraryLoader.GetAvailableBooks().Contains("custom")
                && importedWithPart.english == "imported"
                && importedWithPart.partOfSpeech == "n."
                && PartOfSpeech.DisplayChinese(importedWithPart) == "n. 导入的",
                "外部 CSV 导入为新词书");
            NotebookStore libraryNotebooks = new NotebookStore(libraryRoot);
            StudyStore libraryStore = new StudyStore(libraryRoot, libraryLoader, libraryNotebooks);
            libraryStore.Settings.defaultBookCounts["custom"] = 4;
            libraryStore.SaveSettings();
            libraryStore.ExtractNew(new Dictionary<string, int> { { "custom", 1 } },
                independentDay.AddDays(2));
            libraryLoader.RenameBook("custom", "renamed-book");
            libraryStore.RenameBookReferences("custom", "renamed-book");
            StudyStore reloadedLibrary = new StudyStore(libraryRoot,
                new DataLoader(Path.Combine(libraryRoot, "data")), new NotebookStore(libraryRoot));
            Require(reloadedLibrary.Settings.defaultBookCounts.ContainsKey("renamed-book")
                && !reloadedLibrary.Settings.defaultBookCounts.ContainsKey("custom")
                && reloadedLibrary.AllWords().Any(x => x.book == "renamed-book"
                    && x.word.english == "imported"), "词书重命名同步学习状态和默认配额");

            StudyStore reloaded = new StudyStore(root, loader, new NotebookStore(root));
            Require(reloaded.Lists.Count >= 3 && reloaded.Settings.fuzzyAnswers, "学习状态持久化");
            Require(reloaded.Lists.First(x => x.id == first.id).history.Count == 2,
                "回看记录持久化");
            AppearanceStore appearance = new AppearanceStore(root);
            string sample = Path.Combine(root, "background_source.png");
            using (Bitmap bitmap = new Bitmap(80, 60))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                    graphics.Clear(Color.DarkSlateBlue);
                bitmap.Save(sample, ImageFormat.Png);
            }
            string imported = appearance.ImportImage(sample);
            AppearanceSettings visual = appearance.CopySettings();
            visual.backgrounds["new"].image = imported;
            visual.backgrounds["new"].shade = 47;
            visual.backgrounds["calendar"].color = Color.FromArgb(20, 40, 60).ToArgb();
            visual.text["word"].size = 31;
            visual.prompts["error"] = "答错：{input}；应为 {answer}";
            appearance.Save(visual);
            AppearanceStore reloadedAppearance = new AppearanceStore(root);
            Require(File.Exists(reloadedAppearance.ImagePath(reloadedAppearance.Settings.backgrounds["new"]))
                && reloadedAppearance.Settings.backgrounds["new"].shade == 47
                && reloadedAppearance.Settings.text["word"].size == 31
                && reloadedAppearance.Prompt("error", first.items[0].word, "x").Contains("应为 alpha"),
                "字体、提示文案与导入图片持久化");
            PronunciationStore pronunciation = new PronunciationStore(root);
            Require(pronunciation.Settings.enabled && pronunciation.Settings.automatic
                && pronunciation.Settings.volume == 100, "朗读初始设置");
            PronunciationSettings speech = pronunciation.Settings;
            speech.automatic = false;
            speech.rate = -2;
            speech.volume = 75;
            pronunciation.Save(speech);
            PronunciationStore reloadedSpeech = new PronunciationStore(root);
            Require(!reloadedSpeech.Settings.automatic && reloadedSpeech.Settings.rate == -2
                && reloadedSpeech.Settings.volume == 75, "朗读设置持久化");
            BackupService backups = new BackupService(root);
            BackupInfo manual = backups.Create(true, 1);
            backups.Create(false, 1);
            backups.Create(false, 1);
            Require(backups.List("manual").Count == 1 && backups.List("auto").Count == 1,
                "手动备份隔离且自动备份轮替");
            Require(File.Exists(Path.Combine(manual.path, "study_state.json"))
                && File.Exists(Path.Combine(manual.path, "data", "book1", "unit1.csv"))
                && File.Exists(Path.Combine(manual.path, "appearance.json"))
                && File.Exists(Path.Combine(manual.path, "pronunciation.json"))
                && File.Exists(Path.Combine(manual.path, imported)), "全量备份包含外观与图片");
            string cleanupRoot = Path.Combine(root, "backup_readonly_cleanup_test");
            Directory.CreateDirectory(Path.Combine(cleanupRoot, "data", "book1"));
            File.WriteAllText(Path.Combine(cleanupRoot, "data", "book1", "unit1.csv"),
                "english,chinese\nalpha,阿尔法\n", new UTF8Encoding(false));
            string readOnly = Path.Combine(cleanupRoot, "runtime-note.txt");
            File.WriteAllText(readOnly, "keep", new UTF8Encoding(false));
            File.SetAttributes(readOnly, File.GetAttributes(readOnly) | FileAttributes.ReadOnly);
            string gitPack = Path.Combine(cleanupRoot, "data", "developer-copy", ".git", "objects", "pack");
            Directory.CreateDirectory(gitPack);
            File.WriteAllText(Path.Combine(gitPack, "pack-test.idx"), "git metadata", new UTF8Encoding(false));
            foreach (string transient in new[] { "_build-test", "_qa-test", "_compat-test",
                "_upgrade_backups", "tmp" })
            {
                string folder = Path.Combine(cleanupRoot, "data", transient);
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "large-test.bin"), "temporary",
                    new UTF8Encoding(false));
            }
            File.WriteAllText(Path.Combine(cleanupRoot, "data", "_test-report.json"), "{}",
                new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(cleanupRoot, "data", "formal-preview.png"), "preview",
                new UTF8Encoding(false));
            string keptSource = Path.Combine(cleanupRoot, "data", "source");
            Directory.CreateDirectory(keptSource);
            File.WriteAllText(Path.Combine(keptSource, "README.md"), "source is retained",
                new UTF8Encoding(false));
            BackupService cleanupBackups = new BackupService(cleanupRoot);
            cleanupBackups.Create(false, 1);
            cleanupBackups.Create(false, 1);
            BackupInfo latestCleanup = cleanupBackups.List("auto").Single();
            Require(File.Exists(Path.Combine(latestCleanup.path, "runtime-note.txt"))
                && File.Exists(Path.Combine(latestCleanup.path, "data", "source", "README.md"))
                && !Directory.Exists(Path.Combine(latestCleanup.path, "data", "developer-copy", ".git"))
                && !Directory.Exists(Path.Combine(latestCleanup.path, "data", "_qa-test"))
                && !Directory.Exists(Path.Combine(latestCleanup.path, "data", "_compat-test"))
                && !Directory.Exists(Path.Combine(latestCleanup.path, "data", "tmp"))
                && !File.Exists(Path.Combine(latestCleanup.path, "data", "_test-report.json"))
                && !File.Exists(Path.Combine(latestCleanup.path, "data", "formal-preview.png")),
                "自动备份保留运行与源码数据并排除开发测试临时文件");
            Dictionary<string, object> result = new Dictionary<string, object>
            {
                { "ok", true }, { "fixture", root }, { "studyDayBoundary", true },
                { "crossDayPriority", true }, { "undo", true }, { "fuzzyAnswers", true },
                { "listReviewOrdering", true }, { "backupIsolation", true },
                { "readOnlyHistory", true }, { "appearancePersistence", true },
                { "pronunciationPersistence", true }, { "independentSessions", true },
                { "manualEnd", true }, { "liveQuestionSettings", true },
                { "csvImportAndBookRename", true }, { "exampleInflections", true },
                { "correctFeedbackInflection", true },
                { "backupReadOnlyCleanup", true }, { "backupTransientExclusion", true },
                { "stagedExampleHints", true },
                { "typedExampleDirectSubmit", true }, { "duplicateWordMerging", true },
                { "duplicateProgressMigration", true }, { "allExamplesRequired", true },
                { "exampleStatsOncePerList", true }, { "partOfSpeechDisplay", true },
                { "partOfSpeechMigration", true }, { "partOfSpeechImport", true }
            };
            File.WriteAllText(report, new JavaScriptSerializer().Serialize(result), new UTF8Encoding(false));
            return 0;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("每日学习测试失败：" + message);
        }
    }
}
