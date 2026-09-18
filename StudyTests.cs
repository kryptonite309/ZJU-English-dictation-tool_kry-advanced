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
            NotebookStore notebooks = new NotebookStore(root);
            DataLoader loader = new DataLoader(Path.Combine(root, "data"));
            StudyStore store = new StudyStore(root, loader, notebooks);
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
            Dictionary<string, object> result = new Dictionary<string, object>
            {
                { "ok", true }, { "fixture", root }, { "studyDayBoundary", true },
                { "crossDayPriority", true }, { "undo", true }, { "fuzzyAnswers", true },
                { "listReviewOrdering", true }, { "backupIsolation", true },
                { "readOnlyHistory", true }, { "appearancePersistence", true },
                { "pronunciationPersistence", true }
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
