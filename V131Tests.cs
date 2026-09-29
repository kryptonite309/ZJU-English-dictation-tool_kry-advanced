using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace EnglishDictationTool
{
    internal static class V131Tests
    {
        private static readonly List<string> checks = new List<string>();
        private static void Check(bool value, string name)
        {
            checks.Add((value ? "PASS " : "FAIL ") + name);
            if (!value) throw new Exception(name);
        }
        private static object Field(object obj, string name)
        { return obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj); }
        private static object Call(object obj, string name, params object[] args)
        { return obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args); }
        private static void Capture(Control view, string path)
        {
            Form host = view as Form ?? view.FindForm();
            bool reveal = host != null && !host.Visible;
            if (reveal)
            {
                host.StartPosition = FormStartPosition.Manual;
                host.Location = new Point(-30000, -30000);
                host.ShowInTaskbar = false; host.Show();
            }
            Application.DoEvents();
            view.CreateControl(); view.PerformLayout();
            using (Bitmap image = new Bitmap(view.Width, view.Height))
            { view.DrawToBitmap(image, new Rectangle(Point.Empty, view.Size)); image.Save(path); }
            if (reveal) host.Hide();
        }
        public static int Run(string root)
        {
            try
            {
                if (Path.GetFullPath(root).TrimEnd('\\') != AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'))
                    throw new Exception("Run this test only from its dedicated QA executable folder.");
                Directory.CreateDirectory(Path.Combine(root, "data", "book-test"));
                File.WriteAllText(Path.Combine(root, "isolated-build.txt"), "QA isolation");
                File.WriteAllText(Path.Combine(root, "data", "book-test", "u1.csv"),
                    "english,chinese,examples,part_of_speech\r\nformulate,制定,They are [[formulating]] a plan.,v.\r\napple,苹果,This is an [[apple]].,n.\r\n", new UTF8Encoding(false));
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                var loader = new DataLoader(Path.Combine(root, "data"));
                var words = loader.LoadWordList("book-test", new[] { "u1" });
                WordEntry word = words.First(w => GameEngine.CleanEnglish(w) == "formulate");
                Check(ExampleCloze.HighlightedExamples(word).Contains("[[formulating]]"), "highlight actual inflection");
                var appearance = new AppearanceStore(root);
                Check(appearance.Settings.text["exampleTarget"].weight == 700, "default target emphasis");
                var custom = appearance.CopySettings();
                custom.text["exampleTarget"].weight = 900;
                custom.text["exampleTarget"].color = Color.LightGreen.ToArgb();
                appearance.Save(custom);
                Check(new AppearanceStore(root).Settings.text["exampleTarget"].weight == 900, "persist target custom style");
                var pron = new PronunciationStore(root);
                pron.Settings.enabled = false;
                // Do not generate audio during automated rendering.
                File.WriteAllText(Path.Combine(root, "pronunciation.json"),
                    new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(pron.Settings));
                var notebooks = new NotebookStore(root);
                var store = new StudyStore(root, loader, notebooks);
                store.Settings.newExample = true; store.Settings.newSpelling = false; store.Settings.newDictation = false;
                store.Settings.timerPrecision = "millisecond";
                store.SaveSettings();
                var list = store.ExtractNew(new Dictionary<string, int> { { "book-test", 1 } }, DateTime.Now);
                while (list.phase == "preview") store.AdvancePreview();
                using (var form = new StudySessionForm(store, notebooks, () => true, appearance))
                {
                    var answer = (TextBox)Field(form, "answer");
                    form.GetType().GetField("exampleRevealStage", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(form, 10);
                    answer.Text = "wrong"; Call(form, "Submit");
                    Check(Field(form, "feedback") != null && form.VisibleText.Contains("你的答案：wrong"), "wrong answer full display");
                    Check(form.VisibleText.Contains("formulating"), "correct inflected answer retained");
                    Capture(form, Path.Combine(root, "wrong-feedback.png"));
                    Call(form, "ProcessCmdKey", new Message(), Keys.Escape);
                    Check((bool)Field(form, "isPaused") && Field(form, "summaryView") == null, "feedback pauses without exiting");
                    Call(form, "ResumeSession");
                    Check(Field(form, "feedback") != null, "resume retains answered word");
                    Check(new StudyStore(root, loader, notebooks).Lists.First(x => x.id == list.id).feedback != null, "feedback persisted");
                    Call(form, "ContinueFeedback");
                    Check(store.Active != null && store.CurrentTask().replay, "last wrong answer enters retry before completion");
                    form.GetType().GetField("exampleRevealStage", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(form, 10);
                    answer.Text = store.ExpectedAnswer(store.CurrentTask()); Call(form, "Submit");
                    Check(store.Active == null && Field(form, "feedback") == null, "correct retry advances to completion without feedback");
                    Call(form, "ProcessCmdKey", new Message(), Keys.Enter);
                    Check(Field(form, "summaryView") != null, "Enter opens summary");
                    var summary = SessionSummary.FromStudy(list);
                    Check(summary.firstCorrect == 0 && summary.total == 1, "retry does not inflate first accuracy");
                    Capture(form, Path.Combine(root, "summary.png"));
                    Call(form, "ProcessCmdKey", new Message(), Keys.Escape);
                    Check(form.IsDisposed, "Esc closes only on summary");
                }
                using (var form = new MainForm())
                {
                    form.ClientSize = new Size(1280, 820);
                    Capture(form, Path.Combine(root, "home.png"));
                    var engine = (GameEngine)Field(form, "engine");
                    var practice = (PracticeStore)Field(form, "practice");
                    engine.StartGame("book-test", new List<string> { "u1" }, "all", "sequential",
                        false, false, true, new[] { "spelling" }, false, true);
                    practice.Begin(engine);
                    Call(form, "AskNextQuestion");
                    var input = (TextBox)Field(form, "inputLine");
                    input.Text = "wrong"; Call(form, "SubmitAnswer");
                    Check(Field(form, "freeFeedback") != null, "free practice answer retained");
                    Call(form, "BeginFreePauseOverlay", true); Call(form, "ResumeFreePractice");
                    Check(Field(form, "freeFeedback") != null && !input.Enabled, "free practice resume feedback");
                    Check(new PracticeStore(root).Current.feedback != null, "free practice feedback persisted");
                    Call(form, "ContinueFreeFeedback");
                    input.Text = GameEngine.CleanEnglish(engine.GetNextQuestion());
                    Call(form, "SubmitAnswer");
                    Check(Field(form, "freeFeedback") == null && object.ReferenceEquals(Field(form, "currentWord"), engine.GetNextQuestion()),
                        "free correct answer jumps and replay target follows new question");
                    Call(form, "ShowFreeSummary", true);
                    Check(Field(form, "freeSummary") != null && !practice.Current.active, "manual end shows summary");
                    var manual = SessionSummary.FromPractice(practice.Current, practice.Attempts, true);
                    Check(manual.firstCorrect == 1 && manual.total == 1 && manual.Accuracy == "100%",
                        "manual accuracy uses answered questions and counts corrected retry once");
                    var empty = SessionSummary.FromPractice(practice.Current, new PracticeAttempt[0], true);
                    Check(empty.total == 0 && empty.Accuracy == "未进行有效作答", "manual end without answers");
                    var initialWrong = SessionSummary.FromPractice(practice.Current, practice.Attempts.Take(1), true);
                    Check(initialWrong.total == 1 && initialWrong.Accuracy == "0%", "manual wrong answer denominator");
                    Capture((Control)Field(form, "freeSummary"), Path.Combine(root, "free-summary.png"));
                }
                Check(AppPaths.FindProjectRoot().TrimEnd('\\') == root.TrimEnd('\\'), "project root pinned to isolated folder");
                using (var host = new Form { ClientSize = new Size(1100, 780) })
                {
                    host.Controls.Add(new StudyCalendarView(store, appearance) { Dock = DockStyle.Fill });
                    Capture(host, Path.Combine(root, "calendar.png"));
                }
                File.WriteAllLines(Path.Combine(root, "v131-report.txt"), checks, Encoding.UTF8);
                return 0;
            }
            catch (Exception error)
            {
                checks.Add(error.ToString());
                File.WriteAllLines(Path.Combine(root, "v131-report.txt"), checks, Encoding.UTF8);
                return 1;
            }
        }
    }
}
