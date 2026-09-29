using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace EnglishDictationTool
{
    internal static class V135Tests
    {
        public static int Run(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            string fixture = Path.Combine(outputDirectory, "fixture");
            Directory.CreateDirectory(fixture);
            List<string> failures = new List<string>();

            OnboardingProgressStore progressStore = new OnboardingProgressStore(fixture);
            OnboardingProgress initial = progressStore.Load();
            if (initial.completed || initial.step != 0) failures.Add("new onboarding state");
            initial.step = 4;
            progressStore.Save(initial);
            OnboardingProgress resumed = progressStore.Load();
            if (resumed.completed || resumed.step != 4) failures.Add("resume onboarding step");
            initial.completed = true;
            progressStore.Save(initial);
            if (!progressStore.Load().completed) failures.Add("complete onboarding state");

            string legacyFixture = Path.Combine(outputDirectory, "legacy-guide");
            Directory.CreateDirectory(legacyFixture);
            File.WriteAllText(Path.Combine(legacyFixture, "onboarding_state.json"),
                "{\"version\":\"1.3.5\",\"completed\":true,\"step\":8}",
                new UTF8Encoding(false));
            if (new OnboardingProgressStore(legacyFixture).Load().completed)
                failures.Add("legacy automatic guide state was not reset");

            OnboardingStep[] steps = OnboardingController.StepsForTests();
            if (steps.Length != OnboardingController.StepCount) failures.Add("onboarding step count");
            if (steps.Length == 0 || steps[0].Target != "mode_wheel")
                failures.Add("wheel is not first onboarding action");
            HashSet<int> chapters = new HashSet<int>();
            foreach (OnboardingStep step in steps)
            {
                if (string.IsNullOrWhiteSpace(step.Path)) failures.Add("missing real navigation path");
                chapters.Add(step.Chapter);
            }
            if (chapters.Count != OnboardingController.ChapterCount)
                failures.Add("onboarding chapter count");
            if (steps[0].RequiredAction != "wheel"
                || steps[1].RequiredAction != "open_settings")
                failures.Add("real navigation actions");
            for (int index = 0; index < steps.Length; index++)
            {
                using (GuideCalloutForm callout = new GuideCalloutForm(steps[index],
                    index, steps.Length, OnboardingController.ChapterCount, false,
                    string.IsNullOrEmpty(steps[index].RequiredAction)))
                {
                    if (callout.HasClippedTextForTests())
                        failures.Add("clipped onboarding text at step " + (index + 1));
                }
            }
            using (OnboardingSkipForm replaySkip = new OnboardingSkipForm(
                Keys.Alt | Keys.J, true))
            using (OnboardingSkipForm firstSkip = new OnboardingSkipForm(
                Keys.Alt | Keys.J, false))
            {
                if (!replaySkip.ShortcutHintForTests.Contains("Alt")
                    || !replaySkip.ShortcutHintForTests.Contains("J"))
                    failures.Add("replay mastery shortcut hint");
                if (!string.IsNullOrEmpty(firstSkip.ShortcutHintForTests))
                    failures.Add("first guide unexpectedly reveals skip shortcut");
            }
            using (SpotlightRingForm ring = new SpotlightRingForm())
            {
                Color key = ring.TransparencyKey;
                if (key.R != key.G || key.G != key.B || ring.BackColor != key)
                    failures.Add("spotlight colour-key fringe");
            }
            using (OnboardingSkipSuccessForm success = new OnboardingSkipSuccessForm(false))
            using (OnboardingSkipSuccessForm replaySuccess = new OnboardingSkipSuccessForm(true))
            {
                if (success.HasClippedTextForTests() || replaySuccess.HasClippedTextForTests())
                    failures.Add("clipped onboarding skip success text");
            }

            AppearanceStore appearance = new AppearanceStore(fixture);
            string source = Path.Combine(outputDirectory, "sample.png");
            using (Bitmap bitmap = new Bitmap(32, 32))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.CornflowerBlue);
                bitmap.Save(source, System.Drawing.Imaging.ImageFormat.Png);
            }
            string first = appearance.ImportImage(source);
            string second = appearance.ImportImage(source);
            if (!string.Equals(first, second, StringComparison.OrdinalIgnoreCase))
                failures.Add("duplicate wallpaper import");
            if (appearance.WallpaperAssets().Count != 1) failures.Add("wallpaper library count");
            using (AppearanceSettingsForm appearanceForm = new AppearanceSettingsForm(appearance))
            {
                appearanceForm.SelectWallpaperTargetsForTests(first, "main", "calendar");
                if (appearanceForm.WallpaperTargetCountForTests != 11)
                    failures.Add("multi-window wallpaper assignment");
                appearanceForm.SaveForTests();
            }
            AppearanceStore reloadedAppearance = new AppearanceStore(fixture);
            if (reloadedAppearance.Settings.backgrounds["main"].image != first
                || reloadedAppearance.Settings.backgrounds["calendar"].image != first
                || reloadedAppearance.Settings.backgrounds["new"].image == first)
                failures.Add("saved wallpaper assignment");
            AppearanceSettings draft = reloadedAppearance.CopySettings();
            if (reloadedAppearance.WallpaperUsage(draft, first).Count != 2)
                failures.Add("wallpaper usage");
            bool protectedWhileUsed = false;
            try { reloadedAppearance.DeleteWallpaperAsset(draft, first); }
            catch (InvalidOperationException) { protectedWhileUsed = true; }
            if (!protectedWhileUsed) failures.Add("delete used wallpaper guard");
            foreach (BackgroundAppearance background in draft.backgrounds.Values)
                if (string.Equals(background.image, first, StringComparison.OrdinalIgnoreCase))
                    background.image = string.Empty;
            reloadedAppearance.DeleteWallpaperAsset(draft, first);
            if (reloadedAppearance.WallpaperAssets().Count != 0)
                failures.Add("delete unused wallpaper");

            AppearanceSettings defaults = AppearanceSettings.Defaults();
            if (defaults.text.Count < 8 || defaults.backgrounds.Count < 11
                || defaults.prompts.Count < 3) failures.Add("complete appearance defaults");

            string settingsFixture = Path.Combine(outputDirectory, "free-settings");
            Directory.CreateDirectory(Path.Combine(settingsFixture, "data"));
            DataLoader settingsLoader = new DataLoader(Path.Combine(settingsFixture, "data"));
            NotebookStore settingsNotebooks = new NotebookStore(settingsFixture);
            StudyStore settingsStore = new StudyStore(settingsFixture, settingsLoader,
                settingsNotebooks);
            settingsStore.Settings.freeBook = "book3";
            settingsStore.Settings.freeUnits = new List<string> { "u2", "u4" };
            settingsStore.Settings.freeContentMode = "phrases_only";
            settingsStore.Settings.freeShowFirstLetter = true;
            settingsStore.Settings.freeRetryOnWrong = false;
            settingsStore.SaveSettings();
            StudyStore reloadedSettings = new StudyStore(settingsFixture, settingsLoader,
                new NotebookStore(settingsFixture));
            if (reloadedSettings.Settings.freeBook != "book3"
                || reloadedSettings.Settings.freeUnits.Count != 2
                || reloadedSettings.Settings.freeUnits[0] != "u2"
                || reloadedSettings.Settings.freeContentMode != "phrases_only"
                || !reloadedSettings.Settings.freeShowFirstLetter
                || reloadedSettings.Settings.freeRetryOnWrong)
                failures.Add("free practice range persistence");

            Dictionary<string, object> report = new Dictionary<string, object>
            {
                { "ok", failures.Count == 0 },
                { "steps", steps.Length },
                { "wallpaperDeduplicated", string.Equals(first, second,
                    StringComparison.OrdinalIgnoreCase) },
                { "failures", failures }
            };
            File.WriteAllText(Path.Combine(outputDirectory, "v135-report.json"),
                new JavaScriptSerializer().Serialize(report), new UTF8Encoding(false));
            return failures.Count == 0 ? 0 : 5;
        }
    }
}
