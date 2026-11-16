using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static class PauseHintRegressionProbe
{
    private static Assembly app;
    private static readonly List<string> checks = new List<string>();
    private static readonly BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static object Make(string name, params object[] args)
    { return Activator.CreateInstance(app.GetType("EnglishDictationTool." + name, true), args); }
    private static object Get(object obj, string name)
    {
        PropertyInfo prop = obj.GetType().GetProperty(name, flags);
        return prop != null ? prop.GetValue(obj, null) : obj.GetType().GetField(name, flags).GetValue(obj);
    }
    private static void Set(object obj, string name, object value)
    {
        PropertyInfo prop = obj.GetType().GetProperty(name, flags);
        if (prop != null) prop.SetValue(obj, value, null);
        else obj.GetType().GetField(name, flags).SetValue(obj, value);
    }
    private static object Call(object obj, string name, params object[] args)
    {
        return obj.GetType().GetMethods(flags).Single(m => m.Name == name
            && m.GetParameters().Length == args.Length).Invoke(obj, args);
    }
    private static void Check(bool value, string name)
    { checks.Add((value ? "PASS " : "FAIL ") + name); }
    private static string Text(object canvas) { return Convert.ToString(Get(canvas, "ContentText")); }
    private static string Signature(object canvas)
    {
        return string.Join("\n", ((IEnumerable)Get(canvas, "blocks")).Cast<object>()
            .Select(b => Get(b, "Role") + "|" + Get(b, "Marked") + "|" + Get(b, "Text")));
    }
    private static string Role(object canvas, string fragment)
    {
        object block = ((IEnumerable)Get(canvas, "blocks")).Cast<object>()
            .FirstOrDefault(b => Convert.ToString(Get(b, "Text")).Contains(fragment));
        return block == null ? "missing" : Convert.ToString(Get(block, "Role"));
    }
    private static void RoundTrip(object main, object canvas, object engine, TextBox input, string name)
    {
        string before = Signature(canvas), answer = input.Text;
        int stage = (int)Get(main, "exampleRevealStage"), index = (int)Get(engine, "CurrentIndex");
        object practice = Get(main, "practice");
        int attempts = ((IEnumerable)Get(practice, "Attempts")).Cast<object>().Count();
        for (int i = 0; i < 3; i++)
        {
            Call(main, "ToggleFreePause");
            Check((bool)Get(main, "freePaused"), name + " pause " + i);
            Call(main, "ToggleFreePause");
        }
        Check(Signature(canvas) == before, name + " text and style survive repeated pause/resume");
        Check(input.Text == answer && (int)Get(main, "exampleRevealStage") == stage
            && (int)Get(engine, "CurrentIndex") == index
            && ((IEnumerable)Get(practice, "Attempts")).Cast<object>().Count() == attempts,
            name + " input, reveal stage and attempts unchanged");
    }
    [STAThread]
    public static int Main(string[] args)
    {
        string root = Path.GetFullPath(args[1]);
        try
        {
            if (!File.Exists(Path.Combine(root, "isolated-build.txt")))
                throw new InvalidOperationException("A marked disposable fixture is required.");
            Environment.SetEnvironmentVariable("ENGLISH_DICTATION_PROJECT_ROOT", root);
            app = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            object pronunciation = Make("PronunciationStore", root);
            Set(Get(pronunciation, "Settings"), "enabled", false);
            Call(pronunciation, "Save", Get(pronunciation, "Settings"));
            object loader = Make("DataLoader", Path.Combine(root, "data"));
            object notebooks = Make("NotebookStore", root);
            object study = Make("StudyStore", root, loader, notebooks);
            object settings = Get(study, "Settings");
            Set(settings, "timerEnabled", false); Set(settings, "checkUpdatesOnStartup", false);
            Call(study, "SaveSettings");
            object appearance = Make("AppearanceStore", root);
            IDictionary styles = (IDictionary)Get(Get(appearance, "Settings"), "text");
            Set(styles["normal"], "size", 11f); Set(styles["example"], "size", 23f);
            Set(styles["meaning"], "size", 21f);
            foreach (object bg in ((IDictionary)Get(Get(appearance, "Settings"), "backgrounds")).Values)
                Set(bg, "image", "");
            Call(appearance, "Save", Get(appearance, "Settings"));
            using (Form main = (Form)Make("MainForm"))
            {
                object engine = Get(main, "engine"), practice = Get(main, "practice");
                settings = Get(Get(main, "study"), "Settings");
                object canvas = Get(main, "logArea");
                TextBox input = (TextBox)Get(main, "inputLine");
                Action<string, bool> begin = delegate(string mode, bool spellingInitial)
                {
                    Call(engine, "StartGame", "book-test", new List<string> { "u1" }, "all", "sequential",
                        mode == "example", mode == "dictation", mode == "spelling", new[] { mode }, spellingInitial, true);
                    Call(practice, "Begin", engine, "free");
                    Call(main, "AskNextQuestion");
                };
                // Opposite settings expose the original accidental coupling between two independent options.
                foreach (bool enabled in new[] { true, false })
                {
                    Set(settings, "exampleFirstLetterHints", enabled);
                    Set(settings, "exampleHintKey", (int)Keys.Enter);
                    begin("example", !enabled);
                    Check(Text(canvas).Contains(enabled ? "显示首字母" : "显示中文"),
                        "initial instruction follows example setting " + enabled);
                    Check(Role(canvas, "They are") == "example", "initial example uses custom example font");
                    RoundTrip(main, canvas, engine, input, "example stage 0 " + enabled);
                    Call(main, "AdvanceFreeExampleOrSubmit");
                    Check(Text(canvas).Contains(enabled ? "显示中文" : "提交答案"),
                        "next instruction matches actual next action " + enabled);
                    if (enabled)
                    {
                        Check(Text(canvas).Contains("f") && !Text(canvas).Contains("中文释义："),
                            "first hint does not reveal meaning early");
                        RoundTrip(main, canvas, engine, input, "example first letter");
                        Call(main, "AdvanceFreeExampleOrSubmit");
                    }
                    Check(Text(canvas).Contains("中文释义：") && Role(canvas, "中文释义：") == "meaning",
                        "revealed meaning uses custom meaning font " + enabled);
                    input.Text = "partially typed";
                    RoundTrip(main, canvas, engine, input, "example meaning " + enabled);
                    Check(Role(canvas, "They are") == "example", "resumed example retains example font " + enabled);
                }
                Set(settings, "exampleFirstLetterHints", true);
                Set(settings, "exampleHintKey", (int)Keys.F6);
                begin("example", false);
                Check(Text(canvas).Contains("F6") && !Text(canvas).Contains("Enter"), "custom hint shortcut displayed");
                RoundTrip(main, canvas, engine, input, "custom shortcut");
                // Typed Enter still submits directly, independent of the configurable reveal key.
                input.Text = "formulating";
                Call(main, "InputLineOnKeyDown", input, new KeyEventArgs(Keys.Enter));
                Check((int)Get(engine, "CurrentIndex") == 1
                    && ((IEnumerable)Get(practice, "Attempts")).Cast<object>().Last() != null,
                    "typed Enter submits inflected answer without extra reveal presses");
                begin("spelling", true);
                input.Text = "draft";
                RoundTrip(main, canvas, engine, input, "spelling with initial");
                Check(Role(canvas, "制定") == "meaning" && Text(canvas).Contains("提示: f"),
                    "resumed spelling keeps meaning style and enabled initial");
                begin("dictation", false);
                Call(main, "RevealFreeDictationMeaning");
                input.Text = "draft";
                RoundTrip(main, canvas, engine, input, "dictation with meaning");
                // Active answer history still returns to the same styled current question.
                Set(settings, "exampleHintKey", (int)Keys.Enter);
                begin("example", false);
                input.Text = "formulating";
                Call(main, "InputLineOnKeyDown", input, new KeyEventArgs(Keys.Enter));
                string current = Signature(canvas);
                Call(main, "PreviousFreePage"); Call(main, "NextFreePage");
                Check(Signature(canvas) == current, "history return retains current example text and styles");
                main.Close();
            }
            bool ok = !checks.Any(x => x.StartsWith("FAIL "));
            File.WriteAllText(Path.Combine(root, "pause-hint-report.json"),
                new JavaScriptSerializer().Serialize(new { ok = ok, checks = checks }), new UTF8Encoding(false));
            return ok ? 0 : 8;
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(root, "pause-hint-report.json"),
                new JavaScriptSerializer().Serialize(new { ok = false, checks = checks, error = error.ToString() }), new UTF8Encoding(false));
            return 9;
        }
    }
}
