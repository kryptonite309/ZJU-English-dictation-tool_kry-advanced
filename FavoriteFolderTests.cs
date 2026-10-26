using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace EnglishDictationTool
{
    internal static class FavoriteFolderTests
    {
        [DllImport("user32.dll")] private static extern bool GetKeyboardState(byte[] state);
        [DllImport("user32.dll")] private static extern bool SetKeyboardState(byte[] state);
        private static readonly List<string> checks = new List<string>();
        private static void Check(bool ok, string name)
        { checks.Add((ok ? "PASS " : "FAIL ") + name); if (!ok) throw new Exception(name); }
        private static object Field(object obj, string name)
        { return obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj); }
        private static object Call(object obj, string name, params object[] args)
        { return obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args); }
        private static void ShowOffscreen(Form form)
        {
            form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-30000, -30000);
            form.ShowInTaskbar = false; form.Show(); Application.DoEvents();
        }
        private static void Capture(Form form, string path)
        {
            if (!form.Visible) ShowOffscreen(form);
            Application.DoEvents(); form.PerformLayout();
            using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
            { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(path); }
        }
        private static bool SendThroughMessageFilter(Control control, Keys keys, bool repeat = false)
        {
            // Thread-local keyboard state only; no real keystrokes sent to any desktop window.
            byte[] saved = new byte[256];
            if (!GetKeyboardState(saved)) throw new Exception("Cannot read QA keyboard state");
            byte[] keyboard = (byte[])saved.Clone();
            keyboard[0x10] = keyboard[0x11] = keyboard[0x12] = 0;
            if ((keys & Keys.Control) != 0) keyboard[0x11] = 0x80;
            if ((keys & Keys.Shift) != 0) keyboard[0x10] = 0x80;
            if ((keys & Keys.Alt) != 0) keyboard[0x12] = 0x80;
            try
            {
                if (!SetKeyboardState(keyboard)) throw new Exception("Cannot set QA thread keyboard state");
                Message message = Message.Create(control.Handle, 0x100, new IntPtr((int)(keys & Keys.KeyCode)),
                    new IntPtr(repeat ? 1L << 30 : 1));
                return Application.FilterMessage(ref message);
            }
            finally { SetKeyboardState(saved); }
        }
        public static int Run(string root)
        {
            try
            {
                if (Path.GetFullPath(root).TrimEnd('\\') != AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'))
                    throw new Exception("Run only in a dedicated QA executable directory.");
                Directory.CreateDirectory(Path.Combine(root, "data", "book1"));
                File.WriteAllText(Path.Combine(root, "isolated-build.txt"), "Favorite collections QA");
                File.WriteAllText(Path.Combine(root, "data", "book1", "unit1.csv"),
                    "english,chinese,examples\napple,苹果,An [[apple]].\nbanana,香蕉,A [[banana]].\ncherry,樱桃,A [[cherry]].\n", new UTF8Encoding(false));
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                PronunciationStore speech = new PronunciationStore(root); speech.Settings.enabled = false; speech.Save(speech.Settings);
                DataLoader loader = new DataLoader(Path.Combine(root, "data"));
                List<WordEntry> words = loader.LoadWordList("book1", new[] { "unit1" });
                WordEntry apple = words[0], banana = words[1], cherry = words[2];
                NotebookState old = new NotebookState { version = 1, reviewFirstLetter = true,
                    reviewCorrectTarget = 3, masteryShortcut = (int)(Keys.Alt | Keys.J),
                    records = new List<NotebookRecord> { new NotebookRecord { word = apple,
                        favorite = true, notebook = Notebooks.Wrong, errorCount = 4 } }, recent = new List<WordEntry>() };
                File.WriteAllText(Path.Combine(root, "notebook_state.json"), new JavaScriptSerializer().Serialize(old), new UTF8Encoding(false));
                NotebookStore store = new NotebookStore(root);
                string originalDefault = store.DefaultFavoriteFolderId;
                Check(store.FavoriteFolders.Count == 1 && store.FavoriteFolderIds(apple).SequenceEqual(new[] { originalDefault })
                    && store.GetRecord(apple).errorCount == 4, "legacy favorites migrate without changing learning records");
                FavoriteFolder second = store.CreateFavoriteFolder("期中重点");
                FavoriteFolder third = store.CreateFavoriteFolder("易混词");
                store.SetFavoriteFolders(apple, new[] { originalDefault, second.id, second.id });
                store.SetFavoriteFolders(banana, new[] { second.id });
                store.SetFavoriteFolders(cherry, new[] { third.id });
                Check(store.FavoriteFolderIds(apple).Count == 2 && store.GetFavoriteWords().Count == 3
                    && store.GetFavoriteWords(second.id).Count == 2, "multiple memberships and deduplicated union");
                store.RenameFavoriteFolder(second.id, "考试重点");
                Check(store.FavoriteFolderIds(apple).Contains(second.id)
                    && store.FavoriteFolderNames(apple).Contains("考试重点"), "rename preserves stable folder identity and memberships");
                bool duplicateRejected = false, emptyRejected = false;
                try { store.RenameFavoriteFolder(second.id, "易混词"); } catch (ArgumentException) { duplicateRejected = true; }
                try { store.CreateFavoriteFolder(" "); } catch (ArgumentException) { emptyRejected = true; }
                Check(duplicateRejected && emptyRejected, "invalid and duplicate folder names rejected");
                store = new NotebookStore(root);
                Check(store.FavoriteFolders.Count == 3 && store.FavoriteFolderIds(apple).Count == 2,
                    "folders and multiple memberships survive reload");
                store.ToggleFavorite(apple);
                Check(store.IsFavorite(apple) && store.FavoriteFolderIds(apple).SequenceEqual(new[] { second.id }),
                    "quick removal changes only its target folder");
                store.SetDefaultFavoriteFolder(third.id); store.ToggleFavorite(banana);
                Check(store.FavoriteFolderIds(banana).Count == 2 && store.FavoriteFolderIds(banana).Contains(third.id),
                    "quick shortcut follows selected default folder");
                string undo = store.Snapshot(); store.RenameFavoriteFolder(second.id, "重点词");
                store.SetFavoriteFolders(apple, new[] { third.id }); store.RestoreSnapshot(undo);
                Check(store.GetFavoriteFolder(second.id).name == "重点词" && store.FavoriteFolderIds(apple).SequenceEqual(new[] { third.id }),
                    "learning undo does not revert folder names or memberships");
                store.DeleteFavoriteFolder(second.id);
                Check(store.IsFavorite(banana) && store.FavoriteFolderIds(banana).SequenceEqual(new[] { third.id })
                    && store.GetRecord(apple).errorCount == 4, "deleting one folder retains other memberships and learning records");
                using (WordFavoriteForm dialog = new WordFavoriteForm(store, apple))
                {
                    CheckedListBox list = (CheckedListBox)Field(dialog, "folders");
                    for (int i = 0; i < list.Items.Count; i++) list.SetItemChecked(i, true);
                    Capture(dialog, Path.Combine(root, "word-favorite-folders.png"));
                    Call(dialog, "SaveMembership");
                }
                Check(store.FavoriteFolderIds(apple).Count == 2, "word dialog saves multiple checked memberships");
                using (FavoriteManagerForm manager = new FavoriteManagerForm(loader, store, Keys.Control | Keys.L))
                {
                    ShowOffscreen(manager);
                    TextBox name = (TextBox)Field(manager, "folderName"); name.Text = "复习清单";
                    Call(manager, "EditFolder", true);
                    FavoriteFolder created = store.FavoriteFolders.First(x => x.name == "复习清单");
                    ((CheckBox)Field(manager, "onlyCurrent")).Checked = false;
                    ((TextBox)Field(manager, "search")).Text = "banana";
                    ListView rows = (ListView)Field(manager, "words");
                    Check(rows.Items.Count == 1, "manager can search full vocabulary to add words");
                    rows.Items[0].Selected = true; Call(manager, "SetMembership", true);
                    Check(store.FavoriteFolderIds(banana).Contains(created.id), "manager adds word to chosen folder");
                    name.Text = "复习计划"; Call(manager, "EditFolder", false);
                    Check(store.GetFavoriteFolder(created.id).name == "复习计划" && store.FavoriteFolderIds(banana).Contains(created.id),
                        "manager renames without losing word membership");
                    ((CheckBox)Field(manager, "onlyCurrent")).Checked = true;
                    Capture(manager, Path.Combine(root, "favorite-manager.png"));
                    manager.ClientSize = new Size(950, 650);
                    Capture(manager, Path.Combine(root, "favorite-manager-small.png"));
                    Button delete = (Button)Field(manager, "delete");
                    Check(delete.Bottom <= delete.Parent.ClientSize.Height
                        && rows.Columns.Cast<ColumnHeader>().Sum(x => x.Width) <= rows.ClientSize.Width,
                        "small manager shows all folder actions and fitted table columns");
                    rows.Items[0].Selected = true; Call(manager, "SetMembership", false);
                    Check(!store.FavoriteFolderIds(banana).Contains(created.id) && store.IsFavorite(banana),
                        "manager removes only current-folder membership");
                    manager.Close();
                }
                StudyStore study = new StudyStore(root, loader, store);
                study.Settings.timerEnabled = false; study.Settings.freeExample = false;
                study.Settings.freeSpelling = true; study.Settings.freeDictation = false; study.SaveSettings();
                StudyList learning = study.ExtractNew(new Dictionary<string, int> { { "book1", 1 } }, DateTime.Now);
                WordEntry shown = learning.items[0].word;
                store.SetFavoriteFolders(shown, new string[0]);
                using (StudySessionForm form = new StudySessionForm(study, store, () => true, new AppearanceStore(root)))
                {
                    ShowOffscreen(form);
                    StyledCanvas canvas = (StyledCanvas)Field(form, "display");
                    TextBox input = (TextBox)Field(form, "answer");
                    Check(!input.Enabled && canvas.ContainsFocus, "preview has a real focusable keyboard target");
                    Check(SendThroughMessageFilter(canvas, Keys.Control | Keys.L) && store.IsFavorite(shown)
                        && learning.previewCursor == 0 && learning.previewStage == 0,
                        "Ctrl+L traverses actual message filter during word preview without advancing");
                    SendThroughMessageFilter(canvas, Keys.Control | Keys.L, true);
                    Check(store.IsFavorite(shown), "held shortcut does not repeatedly toggle membership");
                    SendThroughMessageFilter(canvas, Keys.Control | Keys.L);
                    Check(!store.IsFavorite(shown), "next distinct keypress toggles quick membership off");
                    using (Form unrelated = new Form())
                    {
                        ShowOffscreen(unrelated);
                        Check(!SendThroughMessageFilter(unrelated, Keys.Control | Keys.L) && !store.IsFavorite(shown),
                            "learning shortcut is scoped to its own window");
                        unrelated.Close();
                    }
                    study.Settings.favoriteKey = (int)(Keys.Control | Keys.Shift | Keys.L);
                    SendThroughMessageFilter(canvas, Keys.Control | Keys.L);
                    Check(!store.IsFavorite(shown), "old key no longer acts after customization");
                    SendThroughMessageFilter(canvas, Keys.Control | Keys.Shift | Keys.L);
                    Check(store.IsFavorite(shown), "message filter follows custom shortcut");
                    Capture(form, Path.Combine(root, "study-preview-favorite.png"));
                    form.Close();
                }
                study.ClearPause(learning.id);
                using (Form owner = new Form())
                using (StudySessionForm modal = new StudySessionForm(study, store, () => true, new AppearanceStore(root)))
                {
                    ShowOffscreen(owner);
                    modal.StartPosition = FormStartPosition.Manual; modal.Location = new Point(-30000, -30000);
                    modal.ShowInTaskbar = false;
                    bool modalKeyWorked = false;
                    Exception modalError = null;
                    modal.Shown += delegate
                    {
                        modal.BeginInvoke((Action)delegate
                        {
                            try
                            {
                                bool before = store.FavoriteFolderIds(shown).Contains(store.DefaultFavoriteFolderId);
                                modalKeyWorked = modal.Modal && SendThroughMessageFilter((Control)Field(modal, "display"),
                                    (Keys)study.Settings.favoriteKey)
                                    && before != store.FavoriteFolderIds(shown).Contains(store.DefaultFavoriteFolderId);
                            }
                            catch (Exception error) { modalError = error; }
                            finally { modal.Close(); }
                        });
                    };
                    modal.ShowDialog(owner);
                    if (modalError != null) throw modalError;
                    Check(modalKeyWorked, "favorite shortcut also works in the actual modal learning-window path");
                    owner.Close();
                }
                store.SetFavoriteFolders(apple, new[] { third.id });
                store.SetFavoriteFolders(banana, new[] { originalDefault });
                store.SetFavoriteFolders(cherry, new[] { third.id });
                store.Master(cherry);
                using (MainForm main = new MainForm())
                {
                    ComboBox source = (ComboBox)Field(main, "freeSource"); source.SelectedIndex = 1;
                    ComboBox choice = (ComboBox)Field(main, "freeFavoriteFolder");
                    choice.SelectedIndex = choice.Items.Cast<FavoriteFolder>().ToList().FindIndex(x => x.id == third.id);
                    FreePracticeForm free = (FreePracticeForm)Field(main, "freePracticeWindow");
                    free.StartPosition = FormStartPosition.Manual; free.Location = new Point(-30000, -30000); free.ShowInTaskbar = false;
                    Call(main, "StartGame");
                    GameEngine engine = (GameEngine)Field(main, "engine");
                    Check(engine.CurrentDeck.Select(x => x.english).SequenceEqual(new[] { "apple", "cherry" })
                        && engine.IsFavoriteMode, "free practice uses selected folder and includes mastered favorites");
                    StudyStore mainStudy = (StudyStore)Field(main, "study");
                    Check(mainStudy.Settings.freeFavoriteFolderId == third.id, "selected practice folder persists by stable ID");
                    TextBox input = (TextBox)Field(main, "inputLine"); input.Text = "unfinished input";
                    NotebookStore mainStore = (NotebookStore)Field(main, "notebooks");
                    int before = mainStore.FavoriteFolderIds(engine.GetNextQuestion()).Count;
                    SendThroughMessageFilter(input, (Keys)mainStudy.Settings.favoriteKey);
                    Check(mainStore.FavoriteFolderIds(engine.GetNextQuestion()).Count != before
                        && input.Text == "unfinished input" && engine.CurrentIndex == 0,
                        "free-practice shortcut preserves input and task position");
                    using (StudySettingsForm settings = new StudySettingsForm(mainStudy, mainStore, loader,
                        new BackupService(root), new AppearanceStore(root)))
                    {
                        TabControl tabs = (TabControl)Field(settings, "tabs");
                        Check(tabs.TabPages.Cast<TabPage>().Any(x => x.Text == "收藏夹"), "settings has an explicit favorite-management tab");
                    }
                    main.Close();
                }
                File.WriteAllText(Path.Combine(root, "favorite-folders-report.json"),
                    new JavaScriptSerializer().Serialize(new { ok = true, checks = checks }), new UTF8Encoding(false));
                return 0;
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(root, "favorite-folders-report.json"),
                    new JavaScriptSerializer().Serialize(new { ok = false, checks = checks, error = error.ToString() }), new UTF8Encoding(false));
                return 9;
            }
        }
    }
}
