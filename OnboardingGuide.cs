using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace EnglishDictationTool
{
    internal sealed class OnboardingProgress
    {
        public string version { get; set; }
        public bool completed { get; set; }
        public bool skipped { get; set; }
        public int step { get; set; }
        public int revision { get; set; }
    }

    internal sealed class OnboardingProgressStore
    {
        private readonly string path;

        public OnboardingProgressStore(string projectRoot)
        {
            path = Path.Combine(projectRoot, "onboarding_state.json");
        }

        public OnboardingProgress Load()
        {
            try
            {
                if (File.Exists(path))
                {
                    OnboardingProgress loaded = new JavaScriptSerializer()
                        .Deserialize<OnboardingProgress>(File.ReadAllText(path, Encoding.UTF8));
                    if (loaded != null && string.Equals(loaded.version, "1.3.5",
                        StringComparison.Ordinal) && loaded.revision == 2)
                    {
                        loaded.step = Math.Max(0, Math.Min(OnboardingController.StepCount - 1,
                            loaded.step));
                        return loaded;
                    }
                }
            }
            catch { }
            return new OnboardingProgress { version = "1.3.5", revision = 2, step = 0 };
        }

        public void Save(OnboardingProgress progress)
        {
            progress.version = "1.3.5";
            progress.revision = 2;
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(progress),
                new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
    }

    internal sealed class OnboardingStep
    {
        public int Chapter;
        public string Surface;
        public string Target;
        public string RequiredAction;
        public string Title;
        public string Path;
        public string Description;
    }

    internal sealed class OnboardingController : IDisposable
    {
        private readonly Form main;
        private readonly HomeDashboardHost dashboard;
        private readonly Func<Keys> masteryShortcut;
        private readonly Func<StudySettingsForm> settingsFactory;
        private readonly Func<AppearanceSettingsForm> appearanceFactory;
        private readonly Func<Form> acceptedAnswersFactory;
        private readonly Func<Form> notebookManagerFactory;
        private readonly Func<Form> statisticsFactory;
        private readonly Action refreshApplication;
        private readonly OnboardingProgressStore store;
        private readonly OnboardingStep[] steps;
        private OnboardingProgress progress;
        private SpotlightPresentation presentation;
        private StudySettingsForm settings;
        private AppearanceSettingsForm appearance;
        private Form utility;
        private string utilityKind;
        private bool replay;
        private bool active;
        private bool closingInternally;
        private bool wheelCompleted;
        private System.Windows.Forms.Timer delayedShow;

        public const int StepCount = 21;
        public const int ChapterCount = 9;
        public bool Active { get { return active; } }

        public OnboardingController(Form mainForm, HomeDashboardHost homeDashboard,
            string projectRoot, Func<Keys> masteryKey,
            Func<StudySettingsForm> createSettings,
            Func<AppearanceSettingsForm> createAppearance,
            Func<Form> createAcceptedAnswers,
            Func<Form> createNotebookManager,
            Func<Form> createStatistics,
            Action refresh)
        {
            main = mainForm;
            dashboard = homeDashboard;
            masteryShortcut = masteryKey;
            settingsFactory = createSettings;
            appearanceFactory = createAppearance;
            acceptedAnswersFactory = createAcceptedAnswers;
            notebookManagerFactory = createNotebookManager;
            statisticsFactory = createStatistics;
            refreshApplication = refresh;
            store = new OnboardingProgressStore(projectRoot);
            steps = CreateSteps();
            dashboard.WheelUsed += OnWheelUsed;
        }

        private static OnboardingStep[] CreateSteps()
        {
            return new[]
            {
                new OnboardingStep {
                    Chapter = 1, Surface = "home", RequiredAction = "wheel",
                    Target = "mode_wheel", Title = "亲手用滚轮切换学习模式",
                    Path = "主界面  学习模式滚轮",
                    Description = "把鼠标移到高亮的模式滚轮上并滚动一次。这里可以切换新学、历史列表复习、错题与易错词复习和自由练习。" },
                new OnboardingStep {
                    Chapter = 2, Surface = "home", RequiredAction = "open_settings",
                    Target = "settings", Title = "从主界面进入设置",
                    Path = "主界面右上角  设置",
                    Description = "请亲自点击高亮的“设置”按钮。后续功能都会从真实设置界面逐步寻找。" },
                new OnboardingStep {
                    Chapter = 2, Surface = "settings", RequiredAction = "settings_tab:每日计划",
                    Target = "tab:每日计划", Title = "找到每日计划",
                    Path = "设置  每日计划",
                    Description = "请点击高亮的“每日计划”标签页。以后调整每天学习和复习数量时，就从这里进入。" },
                new OnboardingStep {
                    Chapter = 2, Surface = "settings",
                    Target = "daily_plan", Title = "安排每日学习计划",
                    Path = "设置 > 每日计划",
                    Description = "这里设置每日新学词数、历史列表复习数量、错题复习数量、抽取顺序和各词书配额。你现在看到的就是正式设置区域。" },
                new OnboardingStep {
                    Chapter = 3, Surface = "settings", RequiredAction = "settings_tab:答题与归档",
                    Target = "tab:答题与归档", Title = "找到题型设置",
                    Path = "设置  答题与归档",
                    Description = "请点击“答题与归档”标签页。题型、顺序、答案判定与单词归属都集中在这里。" },
                new OnboardingStep {
                    Chapter = 3, Surface = "settings",
                    Target = "question_types", Title = "组合题型并调整顺序",
                    Path = "设置 > 答题与归档",
                    Description = "例句填空、听写和普通拼写可以同时启用。下方顺序编辑器决定每个学习模块依次出现哪些题型。" },
                new OnboardingStep {
                    Chapter = 4, Surface = "settings", RequiredAction = "settings_action:accepted_answers",
                    Target = "accepted_answers", Title = "打开自定义答案管理",
                    Path = "设置 > 答题与归档 > 管理可接受答案",
                    Description = "请点击高亮按钮，亲自打开正式的可接受答案管理界面。" },
                new OnboardingStep {
                    Chapter = 4, Surface = "utility", Target = "accepted_answers",
                    Title = "为具体单词补充答案",
                    Path = "设置 > 答题与归档 > 管理可接受答案",
                    Description = "开启模糊作答后，可在这里补充允许答案。程序只会合并词库变体和手动答案，仍不会接受拼写错误。" },
                new OnboardingStep {
                    Chapter = 5, Surface = "settings", RequiredAction = "settings_tab:快捷键",
                    Target = "tab:快捷键", Title = "找到快捷键设置",
                    Path = "设置  快捷键",
                    Description = "请点击“快捷键”标签页。所有学习操作的按键都可以从这里修改。" },
                new OnboardingStep {
                    Chapter = 5, Surface = "settings",
                    Target = "study_controls", Title = "调整学习操作与快捷键",
                    Path = "设置 > 快捷键",
                    Description = "展示、提示、翻页、撤销、备份和暂停等操作都可改键。教程不会改动你当前的设置。" },
                new OnboardingStep {
                    Chapter = 6, Surface = "settings", RequiredAction = "settings_tab:答题与归档",
                    Target = "tab:答题与归档", Title = "返回答题与归档",
                    Path = "设置  答题与归档",
                    Description = "请再次点击“答题与归档”，这次我们从中进入全部单词本管理。" },
                new OnboardingStep {
                    Chapter = 6, Surface = "settings", RequiredAction = "settings_action:notebooks",
                    Target = "notebooks", Title = "打开全部单词本管理",
                    Path = "设置 > 答题与归档 > 管理全部单词本",
                    Description = "请点击高亮按钮，打开实际的单词本管理窗口。" },
                new OnboardingStep {
                    Chapter = 6, Surface = "utility", Target = "notebooks",
                    Title = "查看并调整单词归属",
                    Path = "设置 > 答题与归档 > 管理全部单词本",
                    Description = "这里可访问未归档、错题、易错和已掌握等单词本，也能处理最近操作记录。单词只有手动执行“斩”才会进入已掌握。" },
                new OnboardingStep {
                    Chapter = 7, Surface = "settings", RequiredAction = "settings_tab:学习统计",
                    Target = "tab:学习统计", Title = "找到学习统计",
                    Path = "设置  学习统计",
                    Description = "请点击“学习统计”标签页。统计看板和学习日历的入口就在这里。" },
                new OnboardingStep {
                    Chapter = 7, Surface = "settings", RequiredAction = "settings_action:statistics",
                    Target = "statistics", Title = "打开统计看板与学习日历",
                    Path = "设置 > 学习统计 > 打开学习统计",
                    Description = "请点击高亮按钮，打开真实的统计窗口。" },
                new OnboardingStep {
                    Chapter = 7, Surface = "utility", Target = "statistics",
                    Title = "查看统计看板与学习日历",
                    Path = "设置 > 学习统计 > 打开学习统计",
                    Description = "这里包含今日、近 7 天、近 30 天和累计数据。学习日历可按日期查看，并能跳转到对应学习列表。" },
                new OnboardingStep {
                    Chapter = 8, Surface = "settings", RequiredAction = "settings_tab:外观",
                    Target = "tab:外观", Title = "找到外观设置",
                    Path = "设置  外观",
                    Description = "请点击“外观”标签页。字体、颜色、提示文字和各场景背景的入口都在这里。" },
                new OnboardingStep {
                    Chapter = 8, Surface = "settings", RequiredAction = "settings_action:appearance",
                    Target = "appearance", Title = "进入正式外观编辑器",
                    Path = "设置 > 外观 > 打开外观编辑器",
                    Description = "请点击高亮按钮，亲自打开正式外观编辑器。" },
                new OnboardingStep {
                    Chapter = 8, Surface = "appearance", RequiredAction = "appearance_tab:背景设置",
                    Target = "tab:背景设置", Title = "找到背景设置",
                    Path = "外观编辑器  背景设置",
                    Description = "请点击“背景设置”标签页。图片、视频、壁纸文件和面板透明度都在这个页面。" },
                new OnboardingStep {
                    Chapter = 8, Surface = "appearance", Target = "wallpaper_library",
                    Title = "管理和复用壁纸文件",
                    Path = "设置 > 外观 > 打开外观编辑器 > 背景设置",
                    Description = "壁纸文件库支持同一图片或视频复用于多个场景，也可以查看用途并删除未使用素材。" },
                new OnboardingStep {
                    Chapter = 9, Surface = "appearance", Target = "panel_transparency",
                    Title = "调节面板透明度并恢复默认",
                    Path = "外观编辑器 > 背景设置",
                    Description = "主界面面板透明度只改变大面积黑色面板，不会降低文字清晰度。底部按钮可以恢复本页或全部默认外观。" }
            };
        }

        internal static OnboardingStep[] StepsForTests()
        {
            return CreateSteps();
        }

        public bool ShouldStart()
        {
            return !store.Load().completed;
        }

        public void Begin(bool replayMode)
        {
            if (active) return;
            replay = replayMode;
            progress = store.Load();
            if (replay)
            {
                progress = new OnboardingProgress { version = "1.3.5", revision = 2, step = 0 };
            }

            using (OnboardingWelcomeForm welcome = new OnboardingWelcomeForm(
                replay, progress.step, steps.Length))
            {
                welcome.ShowDialog(main);
                if (welcome.Choice == OnboardingWelcomeChoice.Cancel) return;
                if (welcome.Choice == OnboardingWelcomeChoice.Skip)
                {
                    using (OnboardingSkipForm skip = new OnboardingSkipForm(
                        masteryShortcut == null ? (Keys.Alt | Keys.J)
                            : masteryShortcut(), replay))
                    {
                        skip.ShowDialog(main);
                        if (skip.SkipSucceeded)
                        {
                            ShowSkipSuccess(main);
                            if (!replay)
                            {
                                progress.completed = true;
                                progress.skipped = true;
                                progress.step = steps.Length - 1;
                                store.Save(progress);
                            }
                            return;
                        }
                    }
                }
            }
            active = true;
            wheelCompleted = progress.step > 0;
            ShowStep();
        }

        private void ShowStep()
        {
            ClosePresentation();
            if (!active) return;
            if (!replay)
            {
                progress.completed = false;
                store.Save(progress);
            }

            OnboardingStep step = steps[progress.step];
            Form owner;
            Func<Rectangle> target;
            if (string.Equals(step.Surface, "home", StringComparison.Ordinal))
            {
                CloseUtility();
                CloseAppearance();
                CloseSettings();
                main.Show();
                main.Activate();
                owner = main;
                target = delegate { return dashboard.GuideTargetScreen(step.Target); };
            }
            else if (string.Equals(step.Surface, "settings", StringComparison.Ordinal))
            {
                CloseUtility();
                CloseAppearance();
                EnsureSettings();
                owner = settings;
                if (step.Target.StartsWith("tab:", StringComparison.Ordinal))
                {
                    string tab = step.Target.Substring(4);
                    target = delegate { return settings.GuideTabScreen(tab); };
                }
                else
                {
                    settings.SelectGuideTarget(step.Target);
                    target = delegate { return settings.GuideTargetScreen(step.Target); };
                }
            }
            else if (string.Equals(step.Surface, "appearance", StringComparison.Ordinal))
            {
                CloseUtility();
                EnsureAppearance();
                owner = appearance;
                if (step.Target.StartsWith("tab:", StringComparison.Ordinal))
                {
                    string tab = step.Target.Substring(4);
                    target = delegate { return appearance.GuideTabScreen(tab); };
                }
                else
                {
                    appearance.SelectGuideTarget(step.Target);
                    target = delegate { return appearance.GuideTargetScreen(step.Target); };
                }
            }
            else
            {
                CloseAppearance();
                EnsureSettings();
                EnsureUtility(step.Target);
                owner = utility;
                target = UtilityTargetScreen;
            }
            SchedulePresentation(owner, target, step);
        }

        private void SchedulePresentation(Form owner, Func<Rectangle> target,
            OnboardingStep step)
        {
            if (delayedShow != null) { delayedShow.Stop(); delayedShow.Dispose(); }
            delayedShow = new System.Windows.Forms.Timer { Interval = 90 };
            delayedShow.Tick += delegate
            {
                delayedShow.Stop();
                delayedShow.Dispose();
                delayedShow = null;
                if (!active || owner == null || owner.IsDisposed) return;
                presentation = new SpotlightPresentation(owner, target, step,
                    progress.step, steps.Length, ChapterCount, replay,
                    string.IsNullOrEmpty(step.RequiredAction)
                        || (step.RequiredAction == "wheel" && wheelCompleted));
                presentation.PreviousRequested += Previous;
                presentation.NextRequested += Next;
                presentation.SkipRequested += SkipOrExitReplay;
                presentation.ExitRequested += RequestExit;
                presentation.Show();
            };
            delayedShow.Start();
        }

        private void EnsureSettings()
        {
            if (settings != null && !settings.IsDisposed) return;
            settings = settingsFactory();
            settings.GuideTabSelected += OnSettingsTabSelected;
            settings.GuideActionRequested += OnSettingsActionRequested;
            settings.FormClosing += OnGuideWindowClosing;
            settings.Show(main);
            settings.Activate();
        }

        private void EnsureAppearance()
        {
            EnsureSettings();
            if (appearance != null && !appearance.IsDisposed) return;
            appearance = appearanceFactory();
            appearance.GuideTabSelected += OnAppearanceTabSelected;
            appearance.FormClosing += OnGuideWindowClosing;
            appearance.Show(settings);
            appearance.Activate();
        }

        private void EnsureUtility(string kind)
        {
            EnsureSettings();
            if (utility != null && !utility.IsDisposed
                && string.Equals(utilityKind, kind, StringComparison.Ordinal)) return;
            CloseUtility();
            if (string.Equals(kind, "accepted_answers", StringComparison.Ordinal))
                utility = acceptedAnswersFactory();
            else if (string.Equals(kind, "notebooks", StringComparison.Ordinal))
                utility = notebookManagerFactory();
            else
                utility = statisticsFactory();
            utilityKind = kind;
            utility.FormClosing += OnGuideWindowClosing;
            utility.Show(settings);
            utility.Activate();
        }

        private Rectangle UtilityTargetScreen()
        {
            if (utility == null || utility.IsDisposed || !utility.Visible)
                return Rectangle.Empty;
            Rectangle client = utility.RectangleToScreen(utility.ClientRectangle);
            client.Inflate(-22, -22);
            return client.Width > 20 && client.Height > 20 ? client :
                utility.RectangleToScreen(utility.ClientRectangle);
        }

        private void OnGuideWindowClosing(object sender, FormClosingEventArgs e)
        {
            if (!active || closingInternally) return;
            e.Cancel = true;
            RequestExit();
        }

        private void Previous()
        {
            if (progress.step <= 0) return;
            progress.step--;
            ShowStep();
        }

        private void Next()
        {
            OnboardingStep step = steps[progress.step];
            if (!string.IsNullOrEmpty(step.RequiredAction)
                && !(step.RequiredAction == "wheel" && wheelCompleted)) return;
            if (progress.step >= steps.Length - 1)
            {
                Complete(false);
                return;
            }
            progress.step++;
            ShowStep();
        }

        private void AdvanceAfterRequiredAction()
        {
            if (!active) return;
            if (progress.step >= steps.Length - 1)
            {
                Complete(false);
                return;
            }
            progress.step++;
            ShowStep();
        }

        private void OnWheelUsed()
        {
            if (!active || progress == null || progress.step != 0) return;
            wheelCompleted = true;
            if (presentation != null) presentation.MarkActionComplete(
                "已完成滚轮切换。现在可以进入下一步。");
        }

        internal bool HandleHomeSettingsRequested()
        {
            if (!active || progress == null) return false;
            OnboardingStep step = steps[progress.step];
            if (!string.Equals(step.RequiredAction, "open_settings", StringComparison.Ordinal))
                return true;
            ClosePresentation();
            EnsureSettings();
            AdvanceAfterRequiredAction();
            return true;
        }

        private void OnSettingsTabSelected(string title)
        {
            if (!active || progress == null) return;
            string expected = "settings_tab:" + title;
            if (!string.Equals(steps[progress.step].RequiredAction, expected,
                StringComparison.Ordinal)) return;
            AdvanceAfterRequiredAction();
        }

        private bool OnSettingsActionRequested(string key)
        {
            if (!active || progress == null) return false;
            string expected = "settings_action:" + key;
            if (!string.Equals(steps[progress.step].RequiredAction, expected,
                StringComparison.Ordinal)) return true;
            ClosePresentation();
            if (string.Equals(key, "appearance", StringComparison.Ordinal))
                EnsureAppearance();
            else
                EnsureUtility(key);
            AdvanceAfterRequiredAction();
            return true;
        }

        private void OnAppearanceTabSelected(string title)
        {
            if (!active || progress == null) return;
            string expected = "appearance_tab:" + title;
            if (!string.Equals(steps[progress.step].RequiredAction, expected,
                StringComparison.Ordinal)) return;
            AdvanceAfterRequiredAction();
        }

        private void SkipOrExitReplay()
        {
            ClosePresentation();
            using (OnboardingSkipForm skip = new OnboardingSkipForm(
                masteryShortcut == null ? (Keys.Alt | Keys.J)
                    : masteryShortcut(), replay))
            {
                skip.ShowDialog(CurrentOwner());
                if (skip.SkipSucceeded)
                {
                    ShowSkipSuccess(CurrentOwner());
                    if (replay) End(false);
                    else Complete(true);
                    return;
                }
            }
            if (active) ShowStep();
        }

        private void ShowSkipSuccess(Form owner)
        {
            using (OnboardingSkipSuccessForm success = new OnboardingSkipSuccessForm(replay))
                success.ShowDialog(owner ?? main);
        }

        private Form CurrentOwner()
        {
            if (utility != null && !utility.IsDisposed && utility.Visible) return utility;
            if (appearance != null && !appearance.IsDisposed && appearance.Visible) return appearance;
            if (settings != null && !settings.IsDisposed && settings.Visible) return settings;
            return main;
        }

        private void RequestExit()
        {
            if (replay)
            {
                MessageBox.Show(CurrentOwner(),
                    "本次新手引导回放将结束。\n\n以后仍可通过主界面右上角“帮助”中的“新手引导与功能导览”重新回放。",
                    "退出新手引导回放", MessageBoxButtons.OK, MessageBoxIcon.Information);
                End(false);
                return;
            }
            MessageBox.Show(CurrentOwner(),
                "新手引导尚未完成。下次启动会从当前步骤继续。\n\n"
                + "你也可以随时通过主界面右上角“帮助”中的“新手引导与功能导览”重新查看。",
                "暂时退出新手引导", MessageBoxButtons.OK, MessageBoxIcon.Information);
            store.Save(progress);
            End(false);
        }

        public void NotifyApplicationClosing()
        {
            if (!active || replay) return;
            store.Save(progress);
            MessageBox.Show(main,
                "新手引导会在下次启动时继续。\n\n回放入口：主界面右上角“帮助” > “新手引导与功能导览”。",
                "新手引导未完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void Complete(bool skipped)
        {
            if (!replay)
            {
                progress.completed = true;
                progress.skipped = skipped;
                progress.step = steps.Length - 1;
                store.Save(progress);
            }
            End(!skipped);
        }

        private void End(bool showCompletion)
        {
            active = false;
            ClosePresentation();
            CloseUtility();
            CloseAppearance();
            CloseSettings();
            if (refreshApplication != null) refreshApplication();
            main.Show();
            main.Activate();
            if (showCompletion)
                MessageBox.Show(main,
                    "新手引导已完成。以后可从主界面右上角“帮助”再次回放。",
                    "引导完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ClosePresentation()
        {
            if (delayedShow != null)
            {
                delayedShow.Stop(); delayedShow.Dispose(); delayedShow = null;
            }
            if (presentation != null)
            {
                presentation.Dispose();
                presentation = null;
            }
        }

        private void CloseSettings()
        {
            if (settings == null) return;
            closingInternally = true;
            try
            {
                settings.GuideTabSelected -= OnSettingsTabSelected;
                settings.GuideActionRequested -= OnSettingsActionRequested;
                settings.FormClosing -= OnGuideWindowClosing;
                settings.Close();
                settings.Dispose();
            }
            finally { settings = null; closingInternally = false; }
        }

        private void CloseAppearance()
        {
            if (appearance == null) return;
            closingInternally = true;
            try
            {
                appearance.GuideTabSelected -= OnAppearanceTabSelected;
                appearance.FormClosing -= OnGuideWindowClosing;
                appearance.Close();
                appearance.Dispose();
            }
            finally { appearance = null; closingInternally = false; }
        }

        private void CloseUtility()
        {
            if (utility == null) return;
            closingInternally = true;
            try
            {
                utility.FormClosing -= OnGuideWindowClosing;
                utility.Close();
                utility.Dispose();
            }
            finally
            {
                utility = null;
                utilityKind = null;
                closingInternally = false;
            }
        }

        public void Dispose()
        {
            dashboard.WheelUsed -= OnWheelUsed;
            active = false;
            ClosePresentation();
            CloseUtility();
            CloseAppearance();
            CloseSettings();
        }
    }

    internal sealed class SpotlightPresentation : IDisposable
    {
        private readonly Form owner;
        private readonly Func<Rectangle> targetProvider;
        private readonly SpotlightMaskForm mask;
        private readonly SpotlightRingForm ring;
        private readonly GuideCalloutForm callout;
        private readonly System.Windows.Forms.Timer timer;
        private Rectangle lastOwner;
        private Rectangle lastTarget;

        public event Action PreviousRequested;
        public event Action NextRequested;
        public event Action SkipRequested;
        public event Action ExitRequested;

        public SpotlightPresentation(Form targetOwner, Func<Rectangle> provider,
            OnboardingStep step, int index, int count, int chapterCount,
            bool replay, bool nextEnabled)
        {
            owner = targetOwner;
            targetProvider = provider;
            mask = new SpotlightMaskForm();
            ring = new SpotlightRingForm();
            callout = new GuideCalloutForm(step, index, count, chapterCount,
                replay, nextEnabled);
            callout.PreviousRequested += delegate { if (PreviousRequested != null) PreviousRequested(); };
            callout.NextRequested += delegate { if (NextRequested != null) NextRequested(); };
            callout.SkipRequested += delegate { if (SkipRequested != null) SkipRequested(); };
            callout.ExitRequested += delegate { if (ExitRequested != null) ExitRequested(); };
            timer = new System.Windows.Forms.Timer { Interval = 90 };
            timer.Tick += delegate { LayoutWindows(); };
        }

        public void Show()
        {
            mask.Show(owner);
            ring.Show(owner);
            callout.Show(owner);
            LayoutWindows();
            timer.Start();
            owner.Activate();
        }

        public void MarkActionComplete(string message)
        {
            callout.MarkActionComplete(message);
        }

        private void LayoutWindows()
        {
            if (owner == null || owner.IsDisposed || !owner.Visible) return;
            Rectangle ownerClient = owner.RectangleToScreen(owner.ClientRectangle);
            Rectangle target = targetProvider == null ? Rectangle.Empty : targetProvider();
            target = Rectangle.Intersect(ownerClient, target);
            if (target.Width < 8 || target.Height < 8) return;
            target.Inflate(12, 12);
            target = Rectangle.Intersect(ownerClient, target);
            if (ownerClient == lastOwner && target == lastTarget) return;
            lastOwner = ownerClient;
            lastTarget = target;

            mask.Bounds = ownerClient;
            Rectangle local = new Rectangle(target.X - ownerClient.X,
                target.Y - ownerClient.Y, target.Width, target.Height);
            mask.SetHole(local);
            ring.SetTarget(target);
            callout.PlaceNear(target, Screen.FromRectangle(owner.Bounds).WorkingArea);
            mask.BringToFront();
            ring.BringToFront();
            callout.BringToFront();
        }

        public void Dispose()
        {
            timer.Stop(); timer.Dispose();
            callout.Close(); callout.Dispose();
            ring.Close(); ring.Dispose();
            mask.Close(); mask.Dispose();
        }
    }

    internal sealed class SpotlightMaskForm : Form
    {
        public SpotlightMaskForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black;
            Opacity = .86d;
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= 0x08000000 | 0x00000080;
                return parameters;
            }
        }

        public void SetHole(Rectangle hole)
        {
            Region next = new Region(new Rectangle(Point.Empty, ClientSize));
            using (GraphicsPath rounded = Rounded(hole, 22)) next.Exclude(rounded);
            Region previous = Region;
            Region = next;
            if (previous != null) previous.Dispose();
        }

        private static GraphicsPath Rounded(Rectangle rectangle, int radius)
        {
            int diameter = Math.Max(2, radius * 2);
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter,
                diameter, diameter, 0, 90);
            path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class SpotlightRingForm : Form
    {
        public SpotlightRingForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            // Use a neutral colour key. Anti-aliased pixels are blended with the
            // form background before Windows removes the key colour; a coloured
            // key therefore leaks a purple/cyan fringe around rounded corners.
            BackColor = Color.Black;
            TransparencyKey = Color.Black;
            DoubleBuffered = true;
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= 0x08000000 | 0x00000080 | 0x00000020;
                return parameters;
            }
        }

        public void SetTarget(Rectangle target)
        {
            Rectangle bounds = target;
            bounds.Inflate(17, 17);
            Bounds = bounds;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle frame = new Rectangle(17, 17,
                Math.Max(4, ClientSize.Width - 34), Math.Max(4, ClientSize.Height - 34));
            DrawGlow(e.Graphics, frame, 10, 12, Color.FromArgb(38, 38, 38));
            DrawGlow(e.Graphics, frame, 6, 8, Color.FromArgb(88, 88, 88));
            DrawGlow(e.Graphics, frame, 3, 5, Color.FromArgb(168, 168, 168));
            DrawGlow(e.Graphics, frame, 0, 2, Color.White);
        }

        private static void DrawGlow(Graphics graphics, Rectangle frame,
            int inflation, int width, Color color)
        {
            Rectangle rectangle = frame;
            rectangle.Inflate(inflation, inflation);
            using (GraphicsPath path = Rounded(rectangle, 22 + inflation))
            using (Pen pen = new Pen(color, width))
                graphics.DrawPath(pen, path);
        }

        private static GraphicsPath Rounded(Rectangle rectangle, int radius)
        {
            int diameter = Math.Max(4, Math.Min(radius * 2,
                Math.Min(rectangle.Width, rectangle.Height)));
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter,
                diameter, diameter, 0, 90);
            path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class GuideCalloutForm : Form
    {
        private readonly Button previous;
        private readonly Button next;
        private readonly Button skip;
        private readonly Button exit;
        private readonly Label status;

        public event Action PreviousRequested;
        public event Action NextRequested;
        public event Action SkipRequested;
        public event Action ExitRequested;

        public GuideCalloutForm(OnboardingStep step, int index, int count,
            int chapterCount, bool replay, bool nextEnabled)
        {
            ModernUI.ApplyAppIcon(this);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(24, 28, 35);
            ForeColor = Theme.Text;
            Font = Theme.UiFont;
            AutoScaleMode = AutoScaleMode.Dpi;

            int width = 600;
            int horizontal = 30;
            int contentWidth = width - horizontal * 2;
            Font titleFont = new Font("Microsoft YaHei UI", 16f, FontStyle.Bold);
            Font bodyFont = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Regular);
            Font pathFont = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular);
            int titleHeight = Math.Max(52, Measure(step.Title, titleFont, contentWidth) + 10);
            int pathHeight = Math.Max(36, Measure(step.Path, pathFont, contentWidth) + 8);
            int descriptionHeight = Math.Max(76,
                Measure(step.Description, bodyFont, contentWidth) + 18);
            int y = 22;

            Label stepLabel = MakeLabel(string.Format(
                "功能 {0:00} / {1:00}    步骤 {2:00} / {3:00}",
                step.Chapter, chapterCount, index + 1, count),
                Theme.MutedText, Theme.UiFont, horizontal, y, contentWidth, 28);
            Controls.Add(stepLabel); y += 34;
            Label title = MakeLabel(step.Title, Theme.Text, titleFont,
                horizontal, y, contentWidth, titleHeight);
            Controls.Add(title); y += titleHeight + 4;
            Label path = MakeLabel(step.Path, Color.FromArgb(191, 210, 225),
                pathFont, horizontal, y, contentWidth, pathHeight);
            Controls.Add(path); y += pathHeight + 6;
            Label description = MakeLabel(step.Description, Theme.Text, bodyFont,
                horizontal, y, contentWidth, descriptionHeight);
            Controls.Add(description); y += descriptionHeight + 8;
            status = MakeLabel(nextEnabled ? string.Empty : "请在高亮区域完成操作",
                Color.FromArgb(96, 211, 132), bodyFont,
                horizontal, y, contentWidth, 38);
            Controls.Add(status); y += 46;

            FlowLayoutPanel actions = new FlowLayoutPanel {
                Location = new Point(horizontal, y), Size = new Size(contentWidth, 52),
                FlowDirection = FlowDirection.RightToLeft, WrapContents = false,
                BackColor = BackColor, Padding = new Padding(0, 5, 0, 0) };
            next = Button("下一步", 112);
            if (index == count - 1) next.Text = "完成引导";
            next.Enabled = nextEnabled;
            previous = Button("上一步", 102);
            previous.Enabled = index > 0;
            skip = Button("跳过教程", 126);
            exit = Button("暂时退出", 126);
            actions.Controls.Add(next);
            actions.Controls.Add(previous);
            actions.Controls.Add(exit);
            actions.Controls.Add(skip);
            Controls.Add(actions);
            ClientSize = new Size(width, y + 70);
            next.Click += delegate { if (NextRequested != null) NextRequested(); };
            previous.Click += delegate { if (PreviousRequested != null) PreviousRequested(); };
            skip.Click += delegate { if (SkipRequested != null) SkipRequested(); };
            exit.Click += delegate { if (ExitRequested != null) ExitRequested(); };
            Theme.Apply(this);
            stepLabel.Font = Theme.UiFont;
            title.Font = titleFont;
            path.Font = pathFont;
            description.Font = bodyFont;
            status.Font = bodyFont;
            UpdateRoundedRegion();
        }

        private static int Measure(string text, Font font, int width)
        {
            return TextRenderer.MeasureText(text ?? string.Empty, font,
                new Size(width, int.MaxValue), TextFormatFlags.WordBreak
                | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding).Height;
        }

        private static Label MakeLabel(string text, Color color, Font font,
            int x, int y, int width, int height)
        {
            return new Label { Text = text ?? string.Empty,
                Location = new Point(x, y), Size = new Size(width, height),
                Font = font, ForeColor = color, BackColor = Color.Transparent,
                AutoEllipsis = false, TextAlign = ContentAlignment.MiddleLeft };
        }

        private static Button Button(string text, int width)
        {
            return new ModernButton { Text = text, Width = width, Height = 40,
                Margin = new Padding(6, 0, 0, 0), Subtle = true };
        }

        public void MarkActionComplete(string message)
        {
            status.Text = message;
            next.Enabled = true;
        }

        internal bool HasClippedTextForTests()
        {
            foreach (Control control in Controls)
            {
                Label label = control as Label;
                if (label == null || string.IsNullOrEmpty(label.Text)) continue;
                Size measured = TextRenderer.MeasureText(label.Text, label.Font,
                    new Size(Math.Max(1, label.ClientSize.Width), int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl
                    | TextFormatFlags.NoPadding);
                if (measured.Height > label.ClientSize.Height + 2) return true;
            }
            return false;
        }

        public void PlaceNear(Rectangle target, Rectangle working)
        {
            int gap = 24;
            Point[] choices = {
                new Point(target.Right + gap, target.Top + (target.Height - Height) / 2),
                new Point(target.Left - Width - gap, target.Top + (target.Height - Height) / 2),
                new Point(target.Left + (target.Width - Width) / 2, target.Bottom + gap),
                new Point(target.Left + (target.Width - Width) / 2, target.Top - Height - gap)
            };
            foreach (Point choice in choices)
            {
                Rectangle candidate = new Rectangle(choice, Size);
                if (working.Contains(candidate)) { Location = choice; return; }
            }
            int x = Math.Max(working.Left + 14, working.Right - Width - 24);
            int y = Math.Max(working.Top + 14, working.Bottom - Height - 24);
            Location = new Point(x, y);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateRoundedRegion();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = Rounded(new Rectangle(1, 1,
                Math.Max(2, ClientSize.Width - 3), Math.Max(2, ClientSize.Height - 3)), 20))
            using (Pen pen = new Pen(Color.FromArgb(210, 210, 210), 2f))
                e.Graphics.DrawPath(pen, path);
        }

        private void UpdateRoundedRegion()
        {
            if (ClientSize.Width < 4 || ClientSize.Height < 4) return;
            using (GraphicsPath path = Rounded(new Rectangle(Point.Empty, ClientSize), 22))
            {
                Region previous = Region;
                Region = new Region(path);
                if (previous != null) previous.Dispose();
            }
        }

        private static GraphicsPath Rounded(Rectangle rectangle, int radius)
        {
            int diameter = Math.Max(4, Math.Min(radius * 2,
                Math.Min(rectangle.Width, rectangle.Height)));
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter,
                diameter, diameter, 0, 90);
            path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal enum OnboardingWelcomeChoice
    {
        Cancel,
        Start,
        Skip
    }

    internal sealed class OnboardingWelcomeForm : Form
    {
        public OnboardingWelcomeChoice Choice { get; private set; }

        public OnboardingWelcomeForm(bool replay, int currentStep, int totalSteps)
        {
            ModernUI.ApplyAppIcon(this);
            Text = replay ? "回放新手引导" : "欢迎使用 KRY 增强版";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(740, 560);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = Theme.UiFont;

            Label heading = new Label {
                Text = replay ? "准备回放功能导览" :
                    (currentStep > 0 ? "继续完成新手引导" : "先花几分钟认识这款工具"),
                Location = new Point(42, 40), Size = new Size(656, 64),
                Font = new Font("Microsoft YaHei UI", 21f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter };
            string bodyText = "接下来会直接使用程序的真实界面。你需要亲自滚动学习模式、"
                + "点击设置、切换标签页并打开相应功能，这样引导结束后仍能沿同一路径找到它们。\n\n"
                + "深色遮罩会隐藏无关内容，圆角荧光边框表示当前需要操作的位置。"
                + "引导不会修改你的学习进度或设置。\n\n"
                + "预计用时约 3 分钟。中途退出后，下次会从当前进度继续；"
                + "也可从主界面右上角“帮助”中的“新手引导与功能导览”回放。";
            Font bodyFont = new Font("Microsoft YaHei UI", 11.5f, FontStyle.Regular);
            int bodyHeight = Math.Max(270, TextRenderer.MeasureText(bodyText, bodyFont,
                new Size(612, int.MaxValue), TextFormatFlags.WordBreak
                | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding).Height + 12);
            Label body = new Label { Text = bodyText,
                Location = new Point(64, 120), Size = new Size(612, bodyHeight),
                Font = bodyFont,
                ForeColor = Theme.Text, TextAlign = ContentAlignment.TopLeft,
                AutoEllipsis = false };
            Label progress = new Label {
                Text = replay || currentStep == 0 ? string.Empty : string.Format(
                    "上次进度  {0} / {1}", currentStep + 1, totalSteps),
                Location = new Point(64, 126 + bodyHeight), Size = new Size(612, 32),
                ForeColor = Theme.MutedText, TextAlign = ContentAlignment.MiddleCenter };
            Button start = new ModernButton {
                Text = replay ? "开始回放" : (currentStep > 0 ? "继续引导" : "开始引导"),
                Width = 150, Height = 48, Primary = true };
            Button skip = new ModernButton { Text = "跳过教程", Width = 130, Height = 48 };
            Button later = new ModernButton {
                Text = replay ? "取消" : "暂不开始", Width = 130, Height = 48 };
            FlowLayoutPanel actions = new FlowLayoutPanel {
                Location = new Point(64, 168 + bodyHeight), Size = new Size(612, 62),
                FlowDirection = FlowDirection.RightToLeft, WrapContents = false,
                BackColor = Theme.Background };
            actions.Controls.Add(start);
            actions.Controls.Add(later);
            actions.Controls.Add(skip);
            start.Click += delegate { Choice = OnboardingWelcomeChoice.Start; Close(); };
            skip.Click += delegate { Choice = OnboardingWelcomeChoice.Skip; Close(); };
            later.Click += delegate { Choice = OnboardingWelcomeChoice.Cancel; Close(); };
            Controls.Add(heading);
            Controls.Add(body);
            Controls.Add(progress);
            Controls.Add(actions);
            ClientSize = new Size(740, 250 + bodyHeight);
            Theme.Apply(this);
            heading.Font = new Font("Microsoft YaHei UI", 21f, FontStyle.Bold);
            body.Font = bodyFont;
            progress.Font = Theme.UiFont;
        }
    }

    internal sealed class OnboardingSkipForm : Form
    {
        private readonly Keys masteryShortcut;
        private readonly TextBox answer;
        private readonly Label feedback;
        private readonly Label shortcutHint;
        private System.Windows.Forms.Timer returnTimer;
        public bool SkipSucceeded { get; private set; }

        public OnboardingSkipForm(Keys shortcut, bool showShortcutHint)
        {
            masteryShortcut = shortcut;
            ModernUI.ApplyAppIcon(this);
            Text = "跳过教程";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false; MaximizeBox = false; ShowInTaskbar = false;
            ClientSize = new Size(620, 420);
            BackColor = Theme.Background; ForeColor = Theme.Text; Font = Theme.UiFont;

            Label caption = new Label { Text = "跳过教程验证", Location = new Point(42, 36),
                Width = 536, Height = 54, Font = new Font("Microsoft YaHei UI", 20f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter };
            Label meaning = new Label { Text = "中文释义\n跳过；略过", Location = new Point(42, 108),
                Width = 536, Height = 78, Font = new Font("Microsoft YaHei UI", 14f),
                TextAlign = ContentAlignment.MiddleCenter };
            shortcutHint = new Label {
                Text = showShortcutHint ? "按 "
                    + new KeysConverter().ConvertToString(masteryShortcut)
                    + " 斩去单词" : string.Empty,
                Location = new Point(42, 181), Width = 536, Height = 34,
                Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold),
                ForeColor = Theme.MutedText, TextAlign = ContentAlignment.MiddleCenter };
            answer = new TextBox { Location = new Point(110, 220), Width = 400,
                Height = 42, TextAlign = HorizontalAlignment.Center,
                Font = new Font("Segoe UI", 16f) };
            Button submit = new ModernButton { Text = "提交", Location = new Point(220, 282),
                Width = 180, Height = 46, Primary = true };
            feedback = new Label { Location = new Point(42, 342), Width = 536, Height = 44,
                ForeColor = Theme.Error, TextAlign = ContentAlignment.MiddleCenter };
            submit.Click += delegate { FailedAttempt(); };
            Controls.Add(caption); Controls.Add(meaning); Controls.Add(shortcutHint); Controls.Add(answer);
            Controls.Add(submit); Controls.Add(feedback);
            AcceptButton = submit;
            Theme.Apply(this);
            caption.Font = new Font("Microsoft YaHei UI", 20f, FontStyle.Bold);
            meaning.Font = new Font("Microsoft YaHei UI", 14f, FontStyle.Regular);
            shortcutHint.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
            shortcutHint.ForeColor = Theme.MutedText;
            answer.Font = new Font("Segoe UI", 16f, FontStyle.Regular);
            feedback.ForeColor = Theme.Error;
            Shown += delegate { answer.Focus(); };
        }

        internal string ShortcutHintForTests
        {
            get { return shortcutHint.Text; }
        }

        private void FailedAttempt()
        {
            feedback.Text = "未达成跳过条件喵";
            answer.Enabled = false;
            if (returnTimer != null) { returnTimer.Stop(); returnTimer.Dispose(); }
            returnTimer = new System.Windows.Forms.Timer { Interval = 1100 };
            returnTimer.Tick += delegate
            {
                returnTimer.Stop(); returnTimer.Dispose(); returnTimer = null;
                DialogResult = DialogResult.Cancel;
                Close();
            };
            returnTimer.Start();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == masteryShortcut)
            {
                SkipSucceeded = true;
                DialogResult = DialogResult.OK;
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && returnTimer != null)
            {
                returnTimer.Stop(); returnTimer.Dispose(); returnTimer = null;
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class OnboardingSkipSuccessForm : Form
    {
        private readonly Label heading;
        private readonly Label body;
        private readonly System.Windows.Forms.Timer animation;
        private int frame;

        public OnboardingSkipSuccessForm(bool replay)
        {
            ModernUI.ApplyAppIcon(this);
            Text = "验证成功";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(580, 390);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = Theme.UiFont;
            DoubleBuffered = true;

            heading = new Label {
                Text = "跳过验证成功",
                Location = new Point(42, 166), Size = new Size(496, 52),
                Font = new Font("Microsoft YaHei UI", 20f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent };
            body = new Label {
                Text = replay
                    ? "恭喜，你已经掌握了“斩”操作。\n本次新手引导回放已结束。"
                    : "恭喜，你已经掌握了“斩”操作。\n新手引导已成功跳过。",
                Location = new Point(54, 226), Size = new Size(472, 64),
                Font = new Font("Microsoft YaHei UI", 11.5f, FontStyle.Regular),
                ForeColor = Theme.MutedText,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent };
            Button finish = new ModernButton {
                Text = "完成", Location = new Point(200, 310),
                Width = 180, Height = 46, Primary = true };
            finish.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            Controls.Add(heading);
            Controls.Add(body);
            Controls.Add(finish);
            AcceptButton = finish;
            Theme.Apply(this);
            heading.Font = new Font("Microsoft YaHei UI", 20f, FontStyle.Bold);
            heading.BackColor = Color.Transparent;
            body.Font = new Font("Microsoft YaHei UI", 11.5f, FontStyle.Regular);
            body.ForeColor = Theme.MutedText;
            body.BackColor = Color.Transparent;

            animation = new System.Windows.Forms.Timer { Interval = 24 };
            animation.Tick += delegate
            {
                frame++;
                if (Opacity < 1d) Opacity = Math.Min(1d, Opacity + .12d);
                Invalidate(new Rectangle(180, 22, 220, 140));
                if (frame >= 42) animation.Stop();
            };
            Shown += delegate { Opacity = .18d; frame = 0; animation.Start(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Point center = new Point(ClientSize.Width / 2, 92);
            float phase = Math.Min(1f, frame / 30f);
            for (int index = 0; index < 3; index++)
            {
                float local = Math.Max(0f, Math.Min(1f, phase - index * .16f));
                int radius = 31 + (int)(local * 30f) + index * 9;
                int alpha = Math.Max(0, 105 - (int)(local * 82f) - index * 20);
                using (Pen glow = new Pen(Color.FromArgb(alpha, Color.White), 2f))
                    e.Graphics.DrawEllipse(glow, center.X - radius, center.Y - radius,
                        radius * 2, radius * 2);
            }
            using (Pen circle = new Pen(Color.White, 3f))
                e.Graphics.DrawEllipse(circle, center.X - 31, center.Y - 31, 62, 62);
            using (Pen check = new Pen(Color.White, 5f))
            {
                check.StartCap = LineCap.Round;
                check.EndCap = LineCap.Round;
                e.Graphics.DrawLines(check, new[] {
                    new Point(center.X - 15, center.Y),
                    new Point(center.X - 3, center.Y + 12),
                    new Point(center.X + 18, center.Y - 13) });
            }
        }

        internal bool HasClippedTextForTests()
        {
            Size headingSize = TextRenderer.MeasureText(heading.Text, heading.Font,
                heading.ClientSize, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            Size bodySize = TextRenderer.MeasureText(body.Text, body.Font,
                body.ClientSize, TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl
                | TextFormatFlags.NoPadding);
            return headingSize.Width > heading.ClientSize.Width
                || headingSize.Height > heading.ClientSize.Height
                || bodySize.Width > body.ClientSize.Width
                || bodySize.Height > body.ClientSize.Height;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && animation != null)
            {
                animation.Stop();
                animation.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class HelpCenterForm : Form
    {
        public bool ReplayRequested { get; private set; }

        public HelpCenterForm(string message)
        {
            ModernUI.ApplyAppIcon(this);
            Text = "帮助";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = ModernUI.FitWindow(760, 600);
            MinimumSize = new Size(640, 500);
            BackColor = Theme.Background; ForeColor = Theme.Text; Font = Theme.UiFont;

            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill,
                RowCount = 2, ColumnCount = 1, Padding = new Padding(22),
                BackColor = Theme.Background };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            Controls.Add(layout);
            RichTextBox content = new RichTextBox { Dock = DockStyle.Fill,
                ReadOnly = true, DetectUrls = true, BorderStyle = BorderStyle.None,
                BackColor = Theme.Background, ForeColor = Theme.Text,
                Text = message, ScrollBars = RichTextBoxScrollBars.Vertical };
            content.LinkClicked += delegate(object sender, LinkClickedEventArgs e)
            {
                try { System.Diagnostics.Process.Start(e.LinkText); }
                catch { }
            };
            layout.Controls.Add(content, 0, 0);
            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft, WrapContents = false,
                BackColor = Theme.Background, Padding = new Padding(0, 6, 0, 0) };
            Button close = new ModernButton { Text = "关闭", Width = 110, Height = 42 };
            Button replay = new ModernButton { Text = "新手引导与功能导览",
                Width = 240, Height = 42, Primary = true };
            close.Click += delegate { Close(); };
            replay.Click += delegate
            {
                ReplayRequested = true;
                Close();
            };
            actions.Controls.Add(close); actions.Controls.Add(replay);
            layout.Controls.Add(actions, 0, 1);
            Theme.Apply(this);
        }
    }
}
