using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace EnglishDictationTool
{
    internal static class NotebookTests
    {
        public static int Run(string fixtureRoot, string reportPath)
        {
            if (Directory.Exists(fixtureRoot))
                throw new IOException("测试目录已存在，拒绝覆盖：" + fixtureRoot);
            Directory.CreateDirectory(fixtureRoot);

            WordEntry first = Word("initial", "最初的");
            string oldJson = new JavaScriptSerializer().Serialize(new List<WordEntry> { first });
            File.WriteAllText(Path.Combine(fixtureRoot, "wrong_words.json"), oldJson, new UTF8Encoding(false));

            NotebookStore store = new NotebookStore(fixtureRoot);
            Require(store.Count(Notebooks.Wrong) == 1, "旧错题迁移");
            Require(File.ReadAllText(Path.Combine(fixtureRoot, "wrong_words.pre-migration.json"), Encoding.UTF8) == oldJson,
                "旧错题原文备份");
            Require(store.ReviewFirstLetter, "首字母默认开启");
            Require(store.ReviewCorrectTarget == 3, "答对阈值默认值");

            AnswerOutcome one = store.RecordAnswer(first, true, true);
            Require(!one.MovedToErrorProne && one.CorrectCount == 1, "首次答对计数");
            store.RecordAnswer(first, false, true);
            Require(store.GetCorrectCount(first) == 0, "答错清零");
            store.RecordAnswer(first, true, true);
            store.RecordAnswer(first, true, true);
            AnswerOutcome final = store.RecordAnswer(first, true, true);
            Require(final.MovedToErrorProne, "达标转入易错本");
            Require(store.GetNotebook(first) == Notebooks.ErrorProne, "错题本移出");
            Require(store.Count(Notebooks.Wrong) == 0, "旧格式同步");

            store.ReviewFirstLetter = false;
            store.ReviewCorrectTarget = 4;
            store.MasteryShortcut = Keys.Control | Keys.Alt | Keys.G;
            store.Master(first);
            Require(store.GetNotebook(first) == Notebooks.Mastered, "斩词入已掌握");

            WordEntry[] later = new WordEntry[6];
            for (int i = 0; i < later.Length; i++)
            {
                later[i] = Word("word" + i, "释义" + i);
                store.MoveToNotebook(later[i], Notebooks.Wrong);
            }
            Require(store.RecentWords.Count == 5, "待定队列最多五词");
            Require(store.RecentWords[0].Equals(later[5]) && store.RecentWords[4].Equals(later[1]),
                "最近五次顺序");
            store.MoveToNotebook(later[3], Notebooks.ErrorProne);
            Require(store.RecentWords.Count == 5 && store.RecentWords[0].Equals(later[3]),
                "手动调整刷新队列且不重复");

            NotebookStore reloaded = new NotebookStore(fixtureRoot);
            Require(reloaded.GetNotebook(first) == Notebooks.Mastered, "已掌握持久化");
            Require(reloaded.GetNotebook(later[3]) == Notebooks.ErrorProne, "手动归属持久化");
            Require(!reloaded.ReviewFirstLetter && reloaded.ReviewCorrectTarget == 4,
                "复习设置持久化");
            Require(reloaded.MasteryShortcut == (Keys.Control | Keys.Alt | Keys.G), "斩词键持久化");
            Require(reloaded.RecentWords.Count == 5, "待定队列持久化");

            List<WordEntry> legacy = new JavaScriptSerializer().Deserialize<List<WordEntry>>(
                File.ReadAllText(Path.Combine(fixtureRoot, "wrong_words.json"), Encoding.UTF8));
            Require(legacy.Count == reloaded.Count(Notebooks.Wrong), "旧错题格式兼容");

            string dataBook = Path.Combine(fixtureRoot, "data", "book1");
            Directory.CreateDirectory(dataBook);
            File.WriteAllText(Path.Combine(dataBook, "unit1.csv"),
                "english,chinese,examples\ninitial,最初的,\nanother,另一个,\n", new UTF8Encoding(false));
            GameEngine engine = new GameEngine(new DataLoader(Path.Combine(fixtureRoot, "data")), reloaded);
            engine.StartGame("book1", new List<string> { "unit1" }, "all", "sequential", "word", false, false);
            Require(engine.CurrentDeck.Count == 1 && GameEngine.CleanEnglish(engine.GetNextQuestion()) == "another",
                "已掌握词不进入普通练习");
            Require(engine.StartReviewMode() && !engine.ShowFirstLetter,
                "复习首字母设置生效");

            Dictionary<string, object> report = new Dictionary<string, object>
            {
                { "ok", true },
                { "legacyMigration", true },
                { "correctThresholdAndReset", true },
                { "masteryAndManualMovement", true },
                { "gameModeIntegration", true },
                { "recentQueueFive", true },
                { "persistedSettings", true },
                { "fixture", fixtureRoot }
            };
            File.WriteAllText(reportPath, new JavaScriptSerializer().Serialize(report), new UTF8Encoding(false));
            return 0;
        }

        private static WordEntry Word(string english, string chinese)
        {
            return new WordEntry { english = english, chinese = chinese, examples = string.Empty };
        }

        private static void Require(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("测试失败：" + name);
        }
    }
}
