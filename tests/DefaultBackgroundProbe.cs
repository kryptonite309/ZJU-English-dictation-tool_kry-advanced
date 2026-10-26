using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;

public static class DefaultBackgroundProbe
{
    static void Check(bool ok, string label, List<string> checks)
    { if (!ok) throw new Exception(label); checks.Add("PASS " + label); }
    static Dictionary<string, object> Map(object value)
    { return (Dictionary<string, object>)value; }
    public static string Run(string executable, string root, string legacyTemplate)
    {
        var checks = new List<string>(); var json = new JavaScriptSerializer();
        try
        {
            if (!File.Exists(Path.Combine(root, "isolated-background-test.flag")))
                throw new Exception("Use a marked disposable fixture only.");
            var assembly = Assembly.LoadFile(executable);
            var settingsType = assembly.GetType("EnglishDictationTool.AppearanceSettings", true);
            object defaults = settingsType.GetMethod("Defaults").Invoke(null, null);
            var all = Map(Map(json.DeserializeObject(json.Serialize(defaults)))["backgrounds"]);
            foreach (var entry in all) Check(string.IsNullOrEmpty(Convert.ToString(Map(entry.Value)["image"])),
                "empty built-in default for " + entry.Key, checks);
            var legacy = Map(json.DeserializeObject(File.ReadAllText(legacyTemplate)));
            var backgrounds = Map(legacy["backgrounds"]);
            Map(backgrounds["main"])["image"] = Path.Combine(root, "appearance_images", "default.mp4");
            Map(backgrounds["free"])["image"] = "appearance_images/my-custom-image.png";
            string external = Path.Combine(root, "custom", "default.mp4");
            Map(backgrounds["calendar"])["image"] = external;
            Map(Map(legacy["text"])["meaning"])["font"] = "Consolas";
            Map(Map(legacy["text"])["meaning"])["color"] = -1234567;
            Map(legacy["prompts"])["correct"] = "My answer {answer}";
            Directory.CreateDirectory(Path.Combine(root, "appearance_images"));
            string oldMain = Path.Combine(root, "appearance_images", "default.mp4");
            string oldStudy = Path.Combine(root, "appearance_images", "default-study.mp4");
            File.WriteAllText(oldMain, "Synthetic media sentinel");
            File.WriteAllText(oldStudy, "Synthetic media sentinel");
            string path = Path.Combine(root, "appearance.json");
            string before = json.Serialize(legacy); File.WriteAllText(path, before);
            var storeType = assembly.GetType("EnglishDictationTool.AppearanceStore", true);
            object store = Activator.CreateInstance(storeType, new object[] { root });
            object settings = storeType.GetProperty("Settings").GetValue(store, null);
            var current = Map(json.DeserializeObject(json.Serialize(settings)));
            var currentBackgrounds = Map(current["backgrounds"]);
            Check(Convert.ToString(Map(currentBackgrounds["main"])["image"]) == "", "existing absolute bundled main reference cleared", checks);
            foreach (string scene in new[] { "new", "list_review", "problem_review" })
                Check(Convert.ToString(Map(currentBackgrounds[scene])["image"]) == "", "existing bundled study reference cleared for " + scene, checks);
            Check(Convert.ToString(Map(currentBackgrounds["free"])["image"]) == "appearance_images/my-custom-image.png", "custom background preserved", checks);
            Check(Convert.ToString(Map(currentBackgrounds["calendar"])["image"]) == external, "same-named custom file outside bundled directory preserved", checks);
            Check(Convert.ToString(Map(Map(current["text"])["meaning"])["font"]) == "Consolas"
                && Convert.ToInt32(Map(Map(current["text"])["meaning"])["color"]) == -1234567, "custom font and color preserved", checks);
            Check(Convert.ToString(Map(current["prompts"])["correct"]) == "My answer {answer}", "custom feedback preserved", checks);
            Check(Convert.ToInt32(Map(currentBackgrounds["main"])["panelTransparency"]) == Convert.ToInt32(Map(backgrounds["main"])["panelTransparency"]), "panel transparency unchanged", checks);
            Check(File.ReadAllText(path) == before, "loading does not rewrite the user's configuration", checks);
            Check(File.Exists(oldMain) && File.Exists(oldStudy), "existing wallpaper files are not deleted", checks);
            storeType.GetMethod("Save").Invoke(store, new object[] { settings });
            object reloaded = Activator.CreateInstance(storeType, new object[] { root });
            var saved = Map(Map(json.DeserializeObject(json.Serialize(storeType.GetProperty("Settings").GetValue(reloaded, null))))["backgrounds"]);
            Check(Convert.ToString(Map(saved["main"])["image"]) == "" && Convert.ToString(Map(saved["free"])["image"]) == "appearance_images/my-custom-image.png", "saved clear-default state and custom background survive reload", checks);
            return json.Serialize(new { ok = true, checks = checks });
        }
        catch (Exception error)
        {
            while (error.InnerException != null) error = error.InnerException;
            return json.Serialize(new { ok = false, checks = checks, message = error.Message });
        }
    }
}
