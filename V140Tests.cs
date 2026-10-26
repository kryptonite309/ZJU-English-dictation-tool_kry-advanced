using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace EnglishDictationTool
{
    internal static class V140Tests
    {
        public static int CheckCopiedState(string reportPath)
        {
            string root = AppPaths.FindProjectRoot();
            if (!File.Exists(Path.Combine(root, "isolated-build.txt"))) return 2;
            JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            string path = Path.Combine(root, "study_state.json");
            StudyState before = json.Deserialize<StudyState>(File.ReadAllText(path, Encoding.UTF8));
            NotebookStore notebooks = new NotebookStore(root);
            DataLoader loader = new DataLoader(Path.Combine(root, "data"));
            StudyStore study = new StudyStore(root, loader, notebooks);
            study.SaveSettings();
            StudyState after = json.Deserialize<StudyState>(File.ReadAllText(path, Encoding.UTF8));
            int beforeHistory = before.lists.Sum(x => x.history == null ? 0 : x.history.Count);
            int afterHistory = after.lists.Sum(x => x.history == null ? 0 : x.history.Count);
            bool ok = before.lists.Select(x => x.id).SequenceEqual(after.lists.Select(x => x.id))
                && afterHistory >= beforeHistory && before.words.Count == after.words.Count
                && before.settings.freeBook == after.settings.freeBook
                && before.settings.freeUnits.SequenceEqual(after.settings.freeUnits);
            File.WriteAllText(reportPath, json.Serialize(new { ok = ok, root = root,
                listsBefore = before.lists.Count, listsAfter = after.lists.Count,
                historyBefore = beforeHistory, historyAfter = afterHistory,
                wordsBefore = before.words.Count, wordsAfter = after.words.Count,
                favoriteKey = after.settings.favoriteKey, freeSource = after.settings.freeSource }),
                new UTF8Encoding(false));
            return ok ? 0 : 8;
        }
        private static readonly List<string> checks = new List<string>();
        private static void Check(bool condition, string name)
        {
            checks.Add((condition ? "PASS " : "FAIL ") + name);
            if (!condition) throw new Exception(name);
        }
        private static object Field(object obj, string name)
        { return obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj); }
        private static void Set(object obj, string name, object value)
        { obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value); }
        private static object Call(object obj, string name, params object[] args)
        { return obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args); }
        private static void Capture(Form form, string path)
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-30000, -30000);
            form.ShowInTaskbar = false;
            if (!form.Visible) form.Show();
            Application.DoEvents(); form.PerformLayout();
            using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
            { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(path); }
        }

        public static int Run(string root)
        {
            try
            {
                if (Path.GetFullPath(root).TrimEnd('\\') != AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'))
                    throw new Exception("Run only from a dedicated QA executable folder.");
                Directory.CreateDirectory(Path.Combine(root, "data", "book1"));
                File.WriteAllText(Path.Combine(root, "isolated-build.txt"), "v140 QA");
                File.WriteAllText(Path.Combine(root, "data", "book1", "unit1.csv"),
                    "english,chinese,examples\nzebra,斑马,A [[zebra]] runs.\napple,苹果,An [[apple]].\n", new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(root, "data", "book1", "unit2.csv"),
                    "english,chinese,examples\ncherry,樱桃,A [[cherry]].\nzebra,斑马,Another [[zebra]].\n", new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(root, "data", "book1", "unit10.csv"),
                    "english,chinese,examples\nbanana,香蕉,A [[banana]].\n", new UTF8Encoding(false));
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                PronunciationStore pronunciation = new PronunciationStore(root);
                pronunciation.Settings.enabled = false; pronunciation.Save(pronunciation.Settings);
                DataLoader loader = new DataLoader(Path.Combine(root, "data"));
                NotebookStore notebooks = new NotebookStore(root);
                StudyStore study = new StudyStore(root, loader, notebooks);
                List<WordEntry> words = loader.LoadWordList("book1", loader.GetUnitsForBook("book1"));
                WordEntry zebra = words.First(x => x.english == "zebra");
                WordEntry apple = words.First(x => x.english == "apple");
                WordEntry banana = words.First(x => x.english == "banana");
                Check(study.Settings.favoriteKey == (int)(Keys.Control | Keys.L), "default favorite shortcut");
                Check(notebooks.ToggleFavorite(zebra) && notebooks.GetNotebook(zebra) == Notebooks.None
                    && notebooks.GetRecord(zebra).errorCount == 0, "favorite is independent of notebook and attempts");
                notebooks.Master(zebra);
                notebooks.RecordAnswer(apple, false, false);
                notebooks.ToggleFavorite(apple);
                notebooks.ClearWrongWords();
                Check(notebooks.IsFavorite(apple) && notebooks.GetNotebook(apple) == Notebooks.None,
                    "clearing wrong notebook preserves favorites");
                string snapshot = notebooks.Snapshot();
                notebooks.ToggleFavorite(banana);
                notebooks.ToggleFavorite(apple);
                notebooks.RestoreSnapshot(snapshot);
                Check(notebooks.IsFavorite(banana) && !notebooks.IsFavorite(apple), "learning undo preserves latest favorite edits");
                notebooks = new NotebookStore(root);
                Check(notebooks.GetFavoriteWords().Count == 2 && notebooks.IsFavorite(zebra), "favorites persist without duplicates");
                GameEngine engine = new GameEngine(loader, notebooks);
                engine.StartFavorites("all", "sequential", false, false, true,
                    new[] { "spelling" }, false, true);
                Check(engine.CurrentDeck.Select(x => x.english).SequenceEqual(new[] { "zebra", "banana" })
                    && engine.GetNextQuestion().english == "zebra", "favorite practice includes mastered and follows unit order");
                GameEngine restoredEngine = new GameEngine(loader, notebooks);
                restoredEngine.RestoreState(engine.ExportState());
                Check(restoredEngine.IsFavoriteMode && restoredEngine.GetNextQuestion().english == "zebra",
                    "favorite practice restores exactly");
                engine.StartFavorites("all", "unit_random", true, true, true,
                    new[] { "example", "dictation", "spelling" }, false, true);
                Check(engine.CurrentDeck.Count == 7, "favorite practice expands all distinct examples and selected types");
                Check(!NotebookManagerForm.MatchesSearch(apple, "app", true)
                    && NotebookManagerForm.MatchesSearch(apple, " APPLE ", true)
                    && NotebookManagerForm.MatchesSearch(apple, "苹", false), "precise and broad searches");
                using (NotebookManagerForm manager = new NotebookManagerForm(loader, notebooks))
                {
                    ((ComboBox)Field(manager, "sort")).SelectedIndex = 2;
                    ListView list = (ListView)Field(manager, "words");
                    Check(list.Items.Cast<ListViewItem>().Select(x => x.Text)
                        .SequenceEqual(new[] { "zebra", "apple", "cherry", "banana" }), "manager natural unit and CSV row order");
                    ((ComboBox)Field(manager, "filter")).SelectedIndex = 6;
                    Check(list.Items.Count == 2, "favorite manager filter");
                    ((TextBox)Field(manager, "search")).Text = "zebra";
                    Check(list.Items.Count == 1 && list.Items[0].SubItems[2].Text.Contains("unit2"),
                        "manager displays all source units for duplicates");
                    Capture(manager, Path.Combine(root, "favorite-manager.png"));
                }
                // Use the actual free-practice form and store in this isolated QA runtime.
                notebooks.MoveToNotebook(zebra, Notebooks.None);
                study = new StudyStore(root, loader, notebooks);
                study.Settings.freeExample = true; study.Settings.freeSpelling = false;
                study.Settings.freeDictation = false; study.Settings.timerEnabled = false;
                study.SaveSettings();
                using (MainForm main = new MainForm())
                {
                    CheckedListBox units = (CheckedListBox)Field(main, "unitList");
                    Check(units.Items.Count == 3 && units.CheckedItems.Count == 3, "all units initially selected");
                    Call(main, "ToggleAllFreeUnits");
                    Check(units.CheckedItems.Count == 0 && ((Button)Field(main, "unitToggleButton")).Text == "一键全选", "cancel all units");
                    Call(main, "CollectFreeSettingsControls", ((StudyStore)Field(main, "study")).Settings, true);
                    ((StudyStore)Field(main, "study")).SaveSettings();
                    Call(main, "PopulateUnits");
                    Check(units.CheckedItems.Count == 0, "empty unit selection persists instead of silently resetting");
                    Call(main, "ToggleAllFreeUnits");
                    Check(units.CheckedItems.Count == units.Items.Count, "one-click all units");
                    FreePracticeForm free = (FreePracticeForm)Field(main, "freePracticeWindow");
                    free.StartPosition = FormStartPosition.Manual; free.Location = new Point(-30000, -30000);
                    free.ShowInTaskbar = false;
                    Call(main, "StartGame");
                    engine = (GameEngine)Field(main, "engine");
                    TextBox input = (TextBox)Field(main, "inputLine");
                    PracticeStore practice = (PracticeStore)Field(main, "practice");
                    input.Text = "zebra"; Set(main, "exampleRevealStage", 10); Call(main, "SubmitAnswer");
                    Check(engine.CurrentIndex == 1 && practice.Attempts.Count == 1, "correct free answer advances and records once");
                    input.Text = "pending answer"; Set(main, "exampleRevealStage", 2);
                    Call(main, "PreviousFreePage");
                    string readOnly = ((StyledCanvas)Field(main, "logArea")).ContentText;
                    Check((int)Field(main, "freeHistoryIndex") == 0 && !input.Enabled
                        && readOnly.Contains("zebra") && readOnly.Contains("你的答案：zebra"), "free-practice read-only answer history");
                    Call(main, "SubmitAnswer"); Call(main, "MasterCurrentWord"); Call(main, "SaveFreePracticeWindowState");
                    Check(engine.CurrentIndex == 1 && practice.Attempts.Count == 1
                        && practice.Current.input == "pending answer", "history cannot answer, master or overwrite pending input");
                    Check(new PracticeStore(root).Current.viewedAttemptIndex == 0, "history page persisted");
                    Call(main, "NextFreePage");
                    Check(input.Enabled && input.Text == "pending answer"
                        && (int)Field(main, "exampleRevealStage") == 2, "return restores input and hint stage");
                    Call(main, "PreviousFreePage");
                    Call(main, "BeginFreePauseOverlay", true); Call(main, "ResumeFreePractice");
                    Check(!input.Enabled && practice.Current.input == "pending answer", "pause and resume preserve history and current answer");
                    Call(main, "NextFreePage");
                    input.Text = "wrong"; Set(main, "exampleRevealStage", 10); Call(main, "SubmitAnswer");
                    Check(Field(main, "freeFeedback") != null, "wrong answer still shows full feedback");
                    Call(main, "PreviousFreePage"); Call(main, "NextFreePage");
                    Check(Field(main, "freeFeedback") != null && !input.Enabled, "return to wrong feedback without skipping it");
                    Capture((FreePracticeForm)Field(main, "freePracticeWindow"), Path.Combine(root, "free-history.png"));
                    Call(main, "PreviousFreePage");
                    Capture((FreePracticeForm)Field(main, "freePracticeWindow"), Path.Combine(root, "free-history-readonly.png"));
                    main.Close();
                }
                using (MainForm main = new MainForm())
                {
                    Check((int)Field(main, "freeHistoryIndex") >= 0
                        && !((TextBox)Field(main, "inputLine")).Enabled, "restart restores read-only history");
                    main.Close();
                }
                // Study preview, quiz and feedback use the same configurable favorite key.
                study = new StudyStore(root, loader, new NotebookStore(root));
                study.Settings.favoriteKey = (int)(Keys.Control | Keys.Shift | Keys.L); study.SaveSettings();
                Check(new StudyStore(root, loader, new NotebookStore(root)).Settings.favoriteKey
                    == (int)(Keys.Control | Keys.Shift | Keys.L), "custom favorite shortcut persists");
                StudyList active = study.ExtractNew(new Dictionary<string, int> { { "book1", 1 } }, DateTime.Now);
                NotebookStore studyNotebooks = new NotebookStore(root);
                using (StudySessionForm form = new StudySessionForm(study, studyNotebooks, () => true, new AppearanceStore(root)))
                {
                    bool before = studyNotebooks.IsFavorite(active.items[0].word);
                    Call(form, "ProcessCmdKey", new Message(), Keys.Control | Keys.Shift | Keys.L);
                    Check(studyNotebooks.IsFavorite(active.items[0].word) != before
                        && active.previewCursor == 0 && active.previewStage == 0, "favorite shortcut does not advance study preview");
                    Capture(form, Path.Combine(root, "study-favorite.png"));
                    form.Close();
                }
                File.WriteAllText(Path.Combine(root, "v140-report.json"),
                    new JavaScriptSerializer().Serialize(new { ok = true, checks = checks }), new UTF8Encoding(false));
                return 0;
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(root, "v140-report.json"), new JavaScriptSerializer().Serialize(
                    new { ok = false, checks = checks, error = error.ToString() }), new UTF8Encoding(false));
                return 8;
            }
        }
    }
}
