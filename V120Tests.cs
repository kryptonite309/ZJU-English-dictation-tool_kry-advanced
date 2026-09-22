using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace EnglishDictationTool
{
    internal static class V120Tests
    {
        public static int Run(string fixture, string reportPath)
        {
            if (Directory.Exists(fixture)) Directory.Delete(fixture, true);
            Directory.CreateDirectory(Path.Combine(fixture, "data", "book-test"));
            File.WriteAllText(Path.Combine(fixture, "data", "book-test", "u1.csv"),
                "english,chinese,examples,part_of_speech\r\n"
                + "apple,苹果,This is an [[apple]].,n.\r\n"
                + "apply,应用,I am [[applying]] it.,v.\r\n",
                new UTF8Encoding(false));
            bool difference = SpellingDifference.Report("aple", new[] { "apple" }).Contains("缺少");
            DataLoader loader = new DataLoader(Path.Combine(fixture, "data"));
            NotebookStore notebooks = new NotebookStore(fixture);
            WordEntry apple = loader.LoadWordList("book-test", new[] { "u1" })
                .First(x => GameEngine.CleanEnglish(x) == "apple");

            DateTime now = new DateTime(2026, 9, 22, 12, 0, 0);
            notebooks.RecordStudyAnswer(apple, false, "spelling", true, 1, 1, 1, now);
            notebooks.RecordStudyAnswer(apple, true, "example", true, 1, 1, 1, now.AddSeconds(1));
            bool waitsForDictation = notebooks.GetNotebook(apple) == Notebooks.Wrong;
            notebooks.RecordStudyAnswer(apple, true, "dictation", true, 1, 1, 1, now.AddSeconds(2));
            bool waitsForSpelling = notebooks.GetNotebook(apple) == Notebooks.Wrong;
            AnswerOutcome threshold = notebooks.RecordStudyAnswer(apple, true, "spelling", true,
                1, 1, 1, now.AddSeconds(3));
            bool tripleThreshold = waitsForDictation && waitsForSpelling
                && threshold.MovedToErrorProne && notebooks.GetNotebook(apple) == Notebooks.ErrorProne;
            notebooks.MoveToNotebook(apple, Notebooks.None);

            StudyStore study = new StudyStore(fixture, loader, notebooks);
            StudySettings settings = study.Settings;
            settings.newExample = false; settings.newDictation = true; settings.newSpelling = true;
            settings.newTaskOrder = new List<string> { "dictation", "spelling", "example" };
            settings.newQuestionOrder = "unit_random";
            study.UpdateSettings(settings);
            StudyList list = study.ExtractNew(new Dictionary<string, int> { { "book-test", 1 } }, now);
            study.AdvancePreview(); study.AdvancePreview(); study.AdvancePreview();
            StudyProgress afterPreview = study.Progress(list);
            bool progressPreview = afterPreview.completed == 1 && afterPreview.total == 3;
            StudyTask first = study.CurrentTask();
            bool dictationFirst = first != null && first.mode == "dictation";
            study.Submit("wrong", now.AddMinutes(1));
            StudyProgress afterWrong = study.Progress(list);
            bool wrongDoesNotAdvance = afterWrong.completed == 1;
            StudyTask spelling = study.CurrentTask();
            study.Submit(GameEngine.CleanEnglish(spelling.word), now.AddMinutes(2));
            StudyProgress afterSpelling = study.Progress(list);
            bool spellingAdvances = afterSpelling.completed == 2;
            StudyTask retry = study.CurrentTask();
            study.SaveSessionState(list.id, "app", 1, -1, -1, true);
            StudyStore reloaded = new StudyStore(fixture, loader, notebooks);
            StudyList resumed = reloaded.Resume("new");
            bool pauseRestored = resumed != null && resumed.paused && resumed.pausedInput == "app"
                && reloaded.CurrentTask() != null && reloaded.CurrentTask().mode == retry.mode;
            StudySettings updatedSettings = new JavaScriptSerializer().Deserialize<StudySettings>(
                new JavaScriptSerializer().Serialize(reloaded.Settings));
            updatedSettings.newDictation = false;
            reloaded.UpdateSettings(updatedSettings);
            bool settingsAppliedWhilePaused = reloaded.ActiveFor("new") == null
                && reloaded.Progress(resumed).percent == 100;
            reloaded.ClearPause(list.id);
            bool completedProgress = reloaded.Progress(resumed).percent == 100;
            reloaded.AddActiveMilliseconds(list.id, 61000);
            bool durationSaved = reloaded.Lists.First(x => x.id == list.id).activeMilliseconds == 61000;

            GameEngine engine = new GameEngine(loader, notebooks) { AnswerValidator = reloaded.IsCorrect };
            engine.StartGame("book-test", new List<string> { "u1" }, "all", "sequential",
                false, true, true, new[] { "dictation", "spelling", "example" }, false, true);
            PracticeStore practice = new PracticeStore(fixture);
            practice.Begin(engine);
            practice.SavePosition(engine, "typed", 0, true, true);
            PracticeStore practiceReloaded = new PracticeStore(fixture);
            GameEngine restoredEngine = new GameEngine(loader, notebooks);
            practiceReloaded.Resume(restoredEngine);
            bool practiceRestored = practiceReloaded.Current.paused
                && practiceReloaded.Current.input == "typed"
                && restoredEngine.CurrentDeck.Count == engine.CurrentDeck.Count;

            AppearanceStore appearance = new AppearanceStore(fixture);
            bool pauseAppearance = appearance.Settings.backgrounds.ContainsKey("pause")
                && appearance.Settings.backgrounds["pause_new"].inherit;
            bool backupDefault = StudySettings.Defaults().backupBeforeManualEnd;
            bool timerDefault = StudySettings.Defaults().timerEnabled
                && StudySettings.Defaults().timerPrecision == "minute";
            string minuteTimer = LearningTimer.DisplayText(61000, "minute");
            string millisecondTimer = LearningTimer.DisplayText(3723456, "millisecond");
            bool timerDisplay = minuteTimer.Contains("\r\n1 分钟")
                && millisecondTimer.Contains("\r\n01:02:03.456");
            string updateFolder = Path.Combine(fixture, "update-test");
            Directory.CreateDirectory(updateFolder);
            string updateSource = Path.Combine(updateFolder, "new.exe");
            string updateTarget = Path.Combine(updateFolder, "target.exe");
            string updatePrevious = Path.Combine(updateFolder, "old.exe");
            string currentExecutable = System.Windows.Forms.Application.ExecutablePath;
            File.Copy(currentExecutable, updateSource, true);
            File.WriteAllText(updateTarget, "old", Encoding.UTF8);
            File.Copy(updateTarget, updatePrevious, true);
            int updateExit = UpdateService.ApplyUpdate(updateSource, updateTarget, updatePrevious,
                -1, Path.Combine(updateFolder, "report.json"), false);
            bool updaterReplace = updateExit == 0
                && new FileInfo(updateTarget).Length == new FileInfo(updateSource).Length;
            File.WriteAllText(updatePrevious, "rollback", Encoding.UTF8);
            int rollbackExit = UpdateService.ApplyUpdate(Path.Combine(updateFolder, "missing.exe"),
                updateTarget, updatePrevious, -1, Path.Combine(updateFolder, "rollback-report.json"), false);
            bool updaterRollback = rollbackExit != 0
                && File.ReadAllText(updateTarget, Encoding.UTF8).Contains("rollback");

            Dictionary<string, object> report = new Dictionary<string, object>
            {
                { "ok", difference && tripleThreshold && progressPreview && dictationFirst
                    && wrongDoesNotAdvance && spellingAdvances && pauseRestored
                    && settingsAppliedWhilePaused && completedProgress && durationSaved && practiceRestored
                    && pauseAppearance && backupDefault && timerDefault && timerDisplay },
                { "spellingDifference", difference }, { "tripleThreshold", tripleThreshold },
                { "dictationFirst", dictationFirst }, { "wrongDoesNotAdvance", wrongDoesNotAdvance },
                { "progress", completedProgress }, { "pauseRestored", pauseRestored },
                { "settingsAppliedWhilePaused", settingsAppliedWhilePaused },
                { "durationSaved", durationSaved }, { "practiceRestored", practiceRestored },
                { "pauseAppearance", pauseAppearance }, { "backupDefault", backupDefault },
                { "timerDefault", timerDefault }, { "timerDisplay", timerDisplay },
                { "updaterReplace", updaterReplace },
                { "updaterRollback", updaterRollback }
            };
            report["ok"] = (bool)report["ok"] && updaterReplace && updaterRollback;
            File.WriteAllText(reportPath, new JavaScriptSerializer().Serialize(report),
                new UTF8Encoding(false));
            return (bool)report["ok"] ? 0 : 7;
        }
    }
}
