using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace EnglishDictationTool
{
    internal sealed class QuotaForm : Form
    {
        private readonly StudyStore store;
        private readonly Dictionary<string, NumericUpDown> inputs = new Dictionary<string, NumericUpDown>();
        private readonly CheckBox remember;
        public Dictionary<string, int> Quotas { get; private set; }

        public QuotaForm(StudyStore study, DataLoader loader)
        {
            ModernUI.ApplyAppIcon(this);
            store = study;
            Text = "抽取今日新词";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(680, 300 + loader.GetAvailableBooks().Count * 52);
            MinimumSize = new Size(560, 300);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = Theme.UiFont;
            AutoScroll = true;
            Dictionary<string, int> remaining = store.AvailableCounts();
            bool hasDefaultQuotas = store.Settings.defaultBookCounts != null
                && store.Settings.defaultBookCounts.Values.Sum() > 0;
            int y = 20;
            Label intro = new Label { Text = "按书本分配数量；不足时只抽实际余量，不跨书补足。",
                Location = new Point(24, y), Width = 620, Height = 40 };
            Controls.Add(intro);
            y += 48;
            bool first = true;
            foreach (string book in loader.GetAvailableBooks())
            {
                int available = remaining.ContainsKey(book) ? remaining[book] : 0;
                string description = book + "  ·  未提取 " + available;
                int labelHeight = Math.Max(36, TextRenderer.MeasureText(description,
                    Theme.UiFont, new Size(400, int.MaxValue), TextFormatFlags.WordBreak).Height + 6);
                Label label = new Label { Text = description,
                    Location = new Point(24, y + 4), Width = 400, Height = labelHeight };
                NumericUpDown input = new NumericUpDown
                {
                    Location = new Point(455, y + 4), Width = 145, Minimum = 0, Maximum = 10000,
                    Value = hasDefaultQuotas && store.Settings.defaultBookCounts.ContainsKey(book)
                        ? Math.Min(10000, Math.Max(0, store.Settings.defaultBookCounts[book]))
                        : first ? Math.Min(10000, Math.Max(0, store.Settings.newCount)) : 0
                };
                first = false;
                inputs.Add(book, input);
                Controls.Add(label);
                Controls.Add(input);
                y += Math.Max(52, labelHeight + 10);
            }
            remember = new CheckBox { Text = "记住本次配额作为默认", Location = new Point(24, y), Width = 320,
                Height = 34, Checked = true };
            Controls.Add(remember);
            y += 45;
            Button accept = new ModernButton { Text = "开始新学", Location = new Point(455, y), Width = 145,
                Height = 44, DialogResult = DialogResult.None };
            accept.Click += delegate
            {
                Quotas = inputs.ToDictionary(x => x.Key, x => (int)x.Value.Value);
                if (Quotas.Values.Sum() <= 0)
                {
                    MessageBox.Show(this, "请至少选择一个单词。", "抽取数量");
                    return;
                }
                if (remember.Checked)
                {
                    store.Settings.defaultBookCounts = new Dictionary<string, int>(Quotas);
                    store.SaveSettings();
                }
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(accept);
            Rectangle workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
            ClientSize = new Size(Math.Min(680, workArea.Width - 72),
                Math.Min(Math.Max(300, y + 65), workArea.Height - 96));
            Theme.Apply(this);
        }
    }

    internal sealed class StudySessionForm : Form
    {
        private readonly StudyStore store;
        private readonly NotebookStore notebooks;
        private readonly AppearanceStore appearance;
        private readonly PronunciationSettings pronunciationSettings;
        private readonly WordPronouncer pronouncer;
        private readonly Func<bool> manualBackup;
        private readonly string openedListId;
        private readonly Timer rolloverTimer;
        private readonly Label heading;
        private readonly StyledCanvas display;
        private readonly TextBox answer;
        private readonly Button previous;
        private readonly Button advance;
        private readonly Button master;
        private readonly Button undo;
        private readonly Button backup;
        private readonly Button replay;
        private readonly Button pause;
        private readonly ModernProgressBar progress;
        private readonly Label timerLabel;
        private readonly PauseOverlay pauseOverlay;
        private readonly LearningTimer learningTimer;
        private WordEntry shown;
        private string shownMode;
        private int lastAutoSpokenCursor = -1;
        private string lastAutoSpokenListId;
        private bool speechWarningShown;
        private int previewReviewIndex = -1;
        private int quizReviewIndex = -1;
        private StudyTask stagedExampleTask;
        private StudyTask lastAutoSpokenTask;
        private int exampleRevealStage;
        private bool isPaused;
        internal string VisibleText { get { return display.ContentText; } }
        internal void RevealExampleForPreview()
        {
            if (!IsExampleQuizActive()) return;
            bool firstLetter = store.Settings.exampleFirstLetterHints;
            if (ExampleRevealFlow.ReadyToSubmit(exampleRevealStage, firstLetter)) return;
            exampleRevealStage = ExampleRevealFlow.NextStage(exampleRevealStage, firstLetter);
            ShowStep();
        }

        public StudySessionForm(StudyStore study, NotebookStore notebookStore,
            Func<bool> backupAction, AppearanceStore appearanceStore)
        {
            ModernUI.ApplyAppIcon(this);
            store = study;
            notebooks = notebookStore;
            manualBackup = backupAction;
            appearance = appearanceStore;
            pronunciationSettings = new PronunciationStore(AppPaths.FindProjectRoot()).Settings;
            pronouncer = new WordPronouncer(pronunciationSettings);
            openedListId = study.Active == null ? null : study.Active.id;
            StudyList restored = study.Active;
            if (restored != null)
            {
                previewReviewIndex = restored.pausedPreviewReviewIndex;
                quizReviewIndex = restored.pausedQuizReviewIndex;
                exampleRevealStage = restored.pausedRevealStage;
                isPaused = restored.paused;
            }
            Text = "每日学习";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = ModernUI.FitWindow(1280, 820);
            MinimumSize = new Size(1000, 650);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = Theme.UiFont;
            KeyPreview = true;
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (store.Active != null && store.Active.phase == "preview"
                    && e.KeyData == (Keys)store.Settings.previewKey)
                {
                    e.SuppressKeyPress = true;
                    Advance();
                }
            };

            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill,
                Padding = new Padding(20), ColumnCount = 1, RowCount = 5 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            Controls.Add(layout);
            ModernCard headingCard = new ModernCard { Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 12), Padding = new Padding(22, 8, 20, 8) };
            TableLayoutPanel headingLayout = new TableLayoutPanel { Dock = DockStyle.Fill,
                ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent };
            headingLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            headingLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
            heading = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft YaHei UI", 13, FontStyle.Bold) };
            timerLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Microsoft YaHei UI", 10, FontStyle.Regular), ForeColor = Theme.MutedText };
            headingLayout.Controls.Add(heading, 0, 0);
            headingLayout.Controls.Add(timerLabel, 1, 0);
            headingCard.Controls.Add(headingLayout);
            layout.Controls.Add(headingCard, 0, 0);
            ModernCard displayCard = new ModernCard { Dock = DockStyle.Fill,
                Padding = new Padding(14), Margin = new Padding(0, 0, 0, 10) };
            display = new StyledCanvas { Dock = DockStyle.Fill,
                BackColor = Theme.Background, ForeColor = Theme.Text };
            displayCard.Controls.Add(display);
            layout.Controls.Add(displayCard, 0, 1);
            answer = new TextBox { Dock = DockStyle.Fill, Font = Theme.LogFont,
                AutoSize = false,
                Margin = new Padding(0, 8, 0, 8) };
            answer.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                if (store.Active != null && store.Active.phase == "preview") Advance();
                else if (IsExampleQuizActive())
                {
                    if (ExampleRevealFlow.HasTypedAnswer(answer.Text))
                    {
                        exampleRevealStage = ExampleRevealFlow.LastRevealStage(
                            store.Settings.exampleFirstLetterHints);
                        Submit();
                    }
                    return;
                }
                else if (IsDictationQuizActive() && !ExampleRevealFlow.HasTypedAnswer(answer.Text))
                {
                    RevealDictationMeaning();
                    return;
                }
                else Submit();
            };
            layout.Controls.Add(answer, 0, 2);
            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 7, 0, 0) };
            previous = ActionButton("上一页", delegate { Previous(); });
            advance = ActionButton("下一步", delegate { AdvanceOrExample(); });
            master = ActionButton("斩当前词", delegate { Master(); });
            undo = ActionButton("撤销上词", delegate { Undo(); });
            backup = ActionButton("手动备份", delegate
            {
                if (manualBackup != null && manualBackup()) Append("手动备份已完成。", Theme.Correct);
            });
            replay = ActionButton("重播单词", delegate { ReplayWord(); });
            pause = ActionButton("暂停", delegate { TogglePause(); });
            actions.Controls.Add(previous);
            actions.Controls.Add(advance);
            actions.Controls.Add(master);
            actions.Controls.Add(undo);
            actions.Controls.Add(backup);
            actions.Controls.Add(replay);
            actions.Controls.Add(pause);
            layout.Controls.Add(actions, 0, 3);
            progress = new ModernProgressBar { Dock = DockStyle.Fill,
                Margin = new Padding(0, 6, 0, 0) };
            layout.Controls.Add(progress, 0, 4);
            Theme.Apply(this);
            display.SetAppearance(appearance, study.Active == null ? "new" : study.Active.kind);
            TextAppearance inputStyle = appearance.Settings.text["normal"];
            answer.Font = AppearanceStore.CreateFont(inputStyle);
            answer.ForeColor = Color.FromArgb(inputStyle.color);
            layout.RowStyles[2].Height = Math.Max(64,
                TextRenderer.MeasureText("Ag", answer.Font).Height + 24);
            rolloverTimer = new Timer { Interval = 30000 };
            rolloverTimer.Tick += delegate
            {
                bool wasActive = store.Active != null;
                store.SettleCrossDay(DateTime.Now);
                if (wasActive && store.Active == null) ShowStep();
            };
            rolloverTimer.Start();
            ShowStep();
            if (restored != null && restored.paused)
            {
                answer.Text = restored.pausedInput ?? string.Empty;
                exampleRevealStage = restored.pausedRevealStage;
                ShowStep();
                answer.Text = restored.pausedInput ?? string.Empty;
                answer.SelectionStart = answer.TextLength;
            }
            learningTimer = new LearningTimer(this, timerLabel,
                restored == null ? 0 : restored.activeMilliseconds,
                store.Settings.timerEnabled, store.Settings.timerPrecision,
                delegate { return !isPaused && store.Active != null
                    && store.Active.id == openedListId; },
                delegate(long delta) { store.AddActiveMilliseconds(openedListId, delta); });
            pauseOverlay = new PauseOverlay { Visible = false };
            pauseOverlay.ResumeRequested += delegate { ResumeSession(); };
            Controls.Add(pauseOverlay);
            if (isPaused)
                Shown += delegate { BeginPauseOverlay(false); };
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (store.Active != null && store.Active.id == openedListId)
                store.SaveSessionState(openedListId, answer.Text, exampleRevealStage,
                    previewReviewIndex, quizReviewIndex, true);
            learningTimer.Dispose();
            rolloverTimer.Stop();
            rolloverTimer.Dispose();
            pronouncer.Dispose();
            base.OnFormClosed(e);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ShowStep();
        }

        protected override void OnDeactivate(EventArgs e)
        {
            learningTimer.Refresh();
            base.OnDeactivate(e);
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            learningTimer.Refresh();
        }

        private static Button ActionButton(string text, EventHandler action)
        {
            Button button = new ModernButton { Text = text, Width = 125, Height = 44,
                Margin = new Padding(0, 0, 7, 0) };
            button.Click += action;
            return button;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys)store.Settings.pauseKey)
            {
                TogglePause();
                return true;
            }
            if (isPaused) return true;
            if (IsExampleQuizActive() && keyData == Keys.Enter
                && ExampleRevealFlow.HasTypedAnswer(answer.Text))
            {
                exampleRevealStage = ExampleRevealFlow.LastRevealStage(
                    store.Settings.exampleFirstLetterHints);
                Submit();
                return true;
            }
            if (keyData == (Keys)store.Settings.previousPageKey)
            { Previous(); return true; }
            if (keyData == (Keys)store.Settings.nextPageKey)
            { NextViewedPage(); return true; }
            if (IsExampleQuizActive() && keyData == (Keys)store.Settings.exampleHintKey)
            { AdvanceExampleOrSubmit(); return true; }
            if (IsDictationQuizActive() && keyData == (Keys)store.Settings.exampleHintKey
                && !ExampleRevealFlow.HasTypedAnswer(answer.Text))
            { RevealDictationMeaning(); return true; }
            if (keyData == notebooks.MasteryShortcut) { Master(); return true; }
            if (keyData == (Keys)store.Settings.undoKey) { Undo(); return true; }
            if (keyData == (Keys)store.Settings.manualBackupKey)
            {
                if (manualBackup != null && manualBackup()) Append("手动备份已完成。", Theme.Correct);
                return true;
            }
            if (keyData == (Keys)pronunciationSettings.replayKey && store.Active != null
                && (store.Active.phase == "preview" || IsDictationQuizActive())
                && pronunciationSettings.enabled)
            { ReplayWord(); return true; }
            if (store.Active != null && store.Active.phase == "preview"
                && keyData == (Keys)store.Settings.previewKey)
            { Advance(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void TogglePause()
        {
            if (isPaused) ResumeSession();
            else BeginPauseOverlay(true);
        }

        private void BeginPauseOverlay(bool persist)
        {
            if (store.Active == null || store.Active.id != openedListId) return;
            if (persist)
                store.SaveSessionState(openedListId, answer.Text, exampleRevealStage,
                    previewReviewIndex, quizReviewIndex, true);
            learningTimer.StopAndCommit();
            isPaused = true;
            pauseOverlay.Visible = false;
            pauseOverlay.Prepare(this, appearance, store.Active.kind);
            pauseOverlay.Visible = true;
            pauseOverlay.BringToFront();
            pause.Text = "继续";
            learningTimer.Refresh();
        }

        private void ResumeSession()
        {
            if (!isPaused) return;
            isPaused = false;
            pauseOverlay.Visible = false;
            pause.Text = "暂停";
            store.ClearPause(openedListId);
            ShowStep();
            learningTimer.Refresh();
            answer.Focus();
        }

        private void ShowStep()
        {
            StudyList list = store.Active;
            StudyList opened = list ?? store.Lists.FirstOrDefault(x => x.id == openedListId);
            UpdateProgress(opened);
            if (list == null)
            {
                pronouncer.Stop(); replay.Enabled = false;
                stagedExampleTask = null; exampleRevealStage = 0;
                if (quizReviewIndex >= 0 && opened != null && opened.history != null
                    && quizReviewIndex < opened.history.Count)
                {
                    ShowHistory(opened, quizReviewIndex);
                    return;
                }
                bool settled = opened != null && opened.status == "settled";
                heading.Text = settled ? "已跨日结算" : "本轮完成";
                display.ClearContent();
                Append(settled ? "本轮未完成的词已回到新学词池顶端。关闭窗口后可从新学入口优先抽取。"
                    : "本轮已完成。需要时可关闭窗口，继续选择其他学习部分。",
                    settled ? Theme.Text : Theme.Correct);
                answer.Enabled = false; advance.Enabled = false; master.Enabled = false;
                previous.Enabled = opened != null && opened.history != null && opened.history.Count > 0;
                undo.Enabled = store.UndoCount > 0;
                shown = null; shownMode = null;
                return;
            }
            string kind = list.kind == "new" ? "新学" :
                list.kind == "list_review" ? "历史列表复习" : "错题与易错词复习";
            if (list.phase == "preview")
            {
                stagedExampleTask = null; exampleRevealStage = 0;
                quizReviewIndex = -1;
                if (previewReviewIndex >= 0 && previewReviewIndex < list.previewCursor)
                {
                    pronouncer.Stop();
                    StudyItem earlier = list.items[previewReviewIndex];
                    shown = earlier.word; shownMode = "preview-history";
                    heading.Text = kind + " · 已看单词 " + (previewReviewIndex + 1) + "/" + list.items.Count;
                    display.ClearContent();
                    display.Add(GameEngine.CleanEnglish(earlier.word), "word");
                    display.Add("例句：\n" + (string.IsNullOrWhiteSpace(earlier.word.examples)
                        ? "该单词暂时没有例句" : Regex.Replace(earlier.word.examples, @"\[\[(.*?)\]\]", "$1")), "example");
                    display.Add("释义：\n" + PartOfSpeech.DisplayChinese(earlier.word), "meaning");
                    answer.Clear(); answer.Enabled = false;
                    previous.Enabled = previewReviewIndex > 0;
                    advance.Enabled = true; advance.Text = "后一词";
                    master.Enabled = false; undo.Enabled = false;
                    replay.Enabled = pronunciationSettings.enabled;
                    return;
                }
                previewReviewIndex = -1;
                StudyItem item = store.CurrentPreview();
                if (item == null)
                {
                    store.AdvancePreview();
                    ShowStep();
                    return;
                }
                shown = item.word; shownMode = "preview";
                heading.Text = kind + " · " + list.id + " · " + (list.previewCursor + 1) + "/" + list.items.Count;
                display.ClearContent();
                display.Add(GameEngine.CleanEnglish(item.word), "word");
                if (list.previewStage >= 1)
                {
                    display.Add("例句：\n" + (string.IsNullOrWhiteSpace(item.word.examples)
                        ? "该单词暂时没有例句" : Regex.Replace(item.word.examples, @"\[\[(.*?)\]\]", "$1")), "example");
                }
                if (list.previewStage >= 2)
                    display.Add("释义：\n" + PartOfSpeech.DisplayChinese(item.word), "meaning");
                answer.Clear(); answer.Enabled = false;
                previous.Enabled = list.previewCursor > 0;
                advance.Enabled = true;
                advance.Text = list.previewStage == 2 ? "下一个词" : "显示下一层";
                advance.Focus();
                master.Enabled = true;
                undo.Enabled = store.UndoCount > 0;
                replay.Enabled = pronunciationSettings.enabled;
                if (!isPaused && Visible && pronunciationSettings.enabled && pronunciationSettings.automatic
                    && list.previewStage == 0
                    && (lastAutoSpokenListId != list.id || lastAutoSpokenCursor != list.previewCursor))
                {
                    lastAutoSpokenListId = list.id;
                    lastAutoSpokenCursor = list.previewCursor;
                    SpeakShownWord();
                }
                return;
            }
            pronouncer.Stop(); replay.Enabled = false;
            previewReviewIndex = -1;
            if (quizReviewIndex >= 0 && list.history != null && quizReviewIndex < list.history.Count)
            {
                ShowHistory(list, quizReviewIndex);
                return;
            }
            quizReviewIndex = -1;
            StudyTask task = store.CurrentTask();
            int missingExamples = 0;
            while (task != null && task.mode == "example")
            {
                string missing = store.SkipMissingExample();
                if (missing == null) break;
                missingExamples++;
                task = store.CurrentTask();
            }
            if (task == null)
            {
                ShowStep();
                if (missingExamples > 0) Append("该单词暂时没有例句（已跳过 "
                    + missingExamples + " 题）", Theme.Text);
                return;
            }
            bool newStagedQuestion = false;
            string retainedAnswer = answer.Text;
            if (task.mode == "example" || task.mode == "dictation")
            {
                if (!object.ReferenceEquals(stagedExampleTask, task))
                {
                    stagedExampleTask = task;
                    exampleRevealStage = 0;
                    newStagedQuestion = true;
                }
            }
            else
            {
                stagedExampleTask = null;
                exampleRevealStage = 0;
            }
            shown = task.word; shownMode = task.mode;
            heading.Text = kind + " · " + list.id + " · " +
                Math.Min(list.taskCursor + 1, list.tasks.Count) + "/" + list.tasks.Count
                + (task.replay ? " · 错题复现" : "");
            display.ClearContent();
            if (task.mode == "example")
            {
                bool firstLetter = store.Settings.exampleFirstLetterHints;
                string prompt = RenderExamplePrompt(task,
                    ExampleRevealFlow.ShowsFirstLetter(exampleRevealStage, firstLetter));
                display.Add("例句填空：\n\n" + prompt, "example");
                if (ExampleRevealFlow.ShowsMeaning(exampleRevealStage, firstLetter))
                    display.Add("中文释义：\n" + GameEngine.ChineseHint(task.word), "meaning");
                display.Add("按 " + new KeysConverter().ConvertToString((Keys)store.Settings.exampleHintKey)
                    + (ExampleRevealFlow.ReadyToSubmit(exampleRevealStage, firstLetter)
                        ? " 提交答案。" : ExampleRevealFlow.ShowsFirstLetter(
                            ExampleRevealFlow.NextStage(exampleRevealStage, firstLetter), firstLetter)
                            && !ExampleRevealFlow.ShowsMeaning(
                                ExampleRevealFlow.NextStage(exampleRevealStage, firstLetter), firstLetter)
                            ? " 显示首字母提示。" : " 显示中文释义。"), "normal");
            }
            else if (task.mode == "dictation")
            {
                display.Add("听写：请听发音后输入英文。", "normal");
                display.Add("可点击“重播单词”或使用朗读快捷键再次播放。", "normal");
                if (exampleRevealStage > 0 && store.Settings.dictationMeaningHint)
                    display.Add("中文释义：\n" + PartOfSpeech.DisplayChinese(task.word), "meaning");
                else if (store.Settings.dictationMeaningHint)
                    display.Add("输入框为空时按 "
                        + new KeysConverter().ConvertToString((Keys)store.Settings.exampleHintKey)
                        + " 可显示中文释义。", "normal");
                replay.Enabled = pronunciationSettings.enabled;
                if (!isPaused && Visible && pronunciationSettings.enabled && !object.ReferenceEquals(
                    lastAutoSpokenTask, task))
                {
                    lastAutoSpokenTask = task;
                    SpeakShownWord();
                }
            }
            else if (task.mode == "dictation")
            {
                display.Add("听写题", "normal");
                display.Add("中文释义：\n" + PartOfSpeech.DisplayChinese(task.word), "meaning");
            }
            else
            {
                display.Add("请根据释义拼写英文：", "normal");
                display.Add(PartOfSpeech.DisplayChinese(task.word), "meaning");
                if (list.kind == "problem_review" && notebooks.ReviewFirstLetter
                    && GameEngine.CleanEnglish(task.word).Length > 0)
                    Append("\n首字母提示：" + GameEngine.CleanEnglish(task.word).Substring(0, 1), Theme.Text);
            }
            if (missingExamples > 0)
                Append("\n该单词暂时没有例句（已跳过 " + missingExamples + " 题）", Theme.Text);
            answer.Enabled = true;
            if ((task.mode != "example" && task.mode != "dictation") || newStagedQuestion) answer.Clear();
            else
            {
                answer.Text = retainedAnswer;
                answer.SelectionStart = answer.TextLength;
            }
            answer.Focus();
            previous.Enabled = list.history != null && list.history.Count > 0;
            advance.Enabled = task.mode == "example"
                || (task.mode == "dictation" && store.Settings.dictationMeaningHint
                    && exampleRevealStage == 0);
            advance.Text = task.mode == "example" ? NextExampleActionText()
                : task.mode == "dictation" ? "显示中文" : "下一步";
            master.Enabled = true;
            undo.Enabled = store.UndoCount > 0;
        }

        private void Append(string text, Color color)
        {
            display.Add(text, color == Theme.Correct ? "correct" :
                color == Theme.Error ? "error" : "normal");
        }

        private void ReplayWord()
        {
            if (store.Active == null || (store.Active.phase != "preview" && !IsDictationQuizActive())
                || !pronunciationSettings.enabled || shown == null) return;
            SpeakShownWord();
        }

        private void SpeakShownWord()
        {
            string error;
            if (pronouncer.Speak(GameEngine.CleanEnglish(shown), out error) || speechWarningShown
                || string.IsNullOrWhiteSpace(error)) return;
            speechWarningShown = true;
            Append("朗读暂不可用：" + error, Theme.Text);
        }

        private void Advance()
        {
            StudyList list = store.Active;
            if (quizReviewIndex >= 0)
            {
                StudyList opened = list ?? store.Lists.FirstOrDefault(x => x.id == openedListId);
                quizReviewIndex = opened != null && quizReviewIndex + 1 < opened.history.Count
                    ? quizReviewIndex + 1 : -1;
                ShowStep();
                return;
            }
            if (list == null || list.phase != "preview") return;
            if (previewReviewIndex >= 0)
            {
                previewReviewIndex = previewReviewIndex + 1 < list.previewCursor
                    ? previewReviewIndex + 1 : -1;
                ShowStep();
                return;
            }
            pronouncer.Stop();
            store.AdvancePreview();
            ShowStep();
        }

        private void AdvanceOrExample()
        {
            if (IsExampleQuizActive()) AdvanceExampleOrSubmit();
            else if (IsDictationQuizActive()) RevealDictationMeaning();
            else Advance();
        }

        private void NextViewedPage()
        {
            if (previewReviewIndex < 0 && quizReviewIndex < 0) return;
            Advance();
        }

        private void UpdateProgress(StudyList list)
        {
            progress.Visible = list != null;
            if (list == null) return;
            StudyProgress current = store.Progress(list);
            progress.Total = current.total;
            progress.Completed = current.completed;
        }

        private bool IsExampleQuizActive()
        {
            return previewReviewIndex < 0 && quizReviewIndex < 0 && store.Active != null
                && store.Active.phase == "quiz" && store.CurrentTask() != null
                && store.CurrentTask().mode == "example";
        }

        private bool IsDictationQuizActive()
        {
            return previewReviewIndex < 0 && quizReviewIndex < 0 && store.Active != null
                && store.Active.phase == "quiz" && store.CurrentTask() != null
                && store.CurrentTask().mode == "dictation";
        }

        private void RevealDictationMeaning()
        {
            if (!IsDictationQuizActive() || !store.Settings.dictationMeaningHint
                || ExampleRevealFlow.HasTypedAnswer(answer.Text)) return;
            exampleRevealStage = 1;
            ShowStep();
        }

        private void AdvanceExampleOrSubmit()
        {
            if (!IsExampleQuizActive()) return;
            bool firstLetter = store.Settings.exampleFirstLetterHints;
            if (ExampleRevealFlow.ReadyToSubmit(exampleRevealStage, firstLetter))
            {
                Submit();
                return;
            }
            exampleRevealStage = ExampleRevealFlow.NextStage(exampleRevealStage, firstLetter);
            ShowStep();
        }

        private string NextExampleActionText()
        {
            bool firstLetter = store.Settings.exampleFirstLetterHints;
            if (ExampleRevealFlow.ReadyToSubmit(exampleRevealStage, firstLetter)) return "提交答案";
            int next = ExampleRevealFlow.NextStage(exampleRevealStage, firstLetter);
            return ExampleRevealFlow.ShowsFirstLetter(next, firstLetter)
                && !ExampleRevealFlow.ShowsMeaning(next, firstLetter)
                ? "显示首字母" : "显示中文";
        }

        private static string RenderExamplePrompt(StudyTask task, bool showFirstLetter)
        {
            string prompt = task.examplePrompt;
            string expected = task.exampleAnswer;
            if (string.IsNullOrWhiteSpace(prompt) || string.IsNullOrWhiteSpace(expected))
            {
                ExampleQuestion fallback = ExampleCloze.First(task.word);
                if (fallback != null)
                {
                    prompt = fallback.prompt;
                    expected = fallback.answer;
                }
            }
            return new ExampleQuestion { prompt = prompt ?? string.Empty,
                answer = expected ?? string.Empty }.Render(showFirstLetter);
        }

        private void Previous()
        {
            StudyList list = store.Active;
            if (list != null && list.phase == "preview")
            {
                int target = previewReviewIndex < 0 ? list.previewCursor - 1 : previewReviewIndex - 1;
                if (target < 0) return;
                previewReviewIndex = target;
            }
            else
            {
                StudyList opened = list ?? store.Lists.FirstOrDefault(x => x.id == openedListId);
                if (opened == null || opened.history == null) return;
                int target = quizReviewIndex < 0 ? opened.history.Count - 1 : quizReviewIndex - 1;
                if (target < 0) return;
                quizReviewIndex = target;
            }
            ShowStep();
        }

        private void ShowHistory(StudyList list, int index)
        {
            pronouncer.Stop(); replay.Enabled = false;
            StudyTask task = list.history[index];
            shown = null; shownMode = "quiz-history";
            heading.Text = "答题回看 · " + (index + 1) + "/" + list.history.Count
                + (task.replay ? " · 错题复现" : "");
            display.ClearContent();
            if (task.mode == "example")
            {
                string prompt = task.examplePrompt;
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    ExampleQuestion fallback = ExampleCloze.First(task.word);
                    prompt = fallback == null ? string.Empty : fallback.prompt;
                }
                display.Add("例句填空：\n" + prompt, "example");
            }
            else
            {
                display.Add("根据释义拼写英文：", "normal");
                display.Add(PartOfSpeech.DisplayChinese(task.word), "meaning");
            }
            if (task.mastered) Append("本题已斩。", Theme.Text);
            else if (task.skipped) Append("该单词暂时没有例句，已跳过。", Theme.Text);
            else
            {
                Append("当时作答：" + (task.submittedAnswer ?? "（旧记录未保存作答文本）"), Theme.Text);
                Append(task.correct ? "判定：正确" : "判定：错误", task.correct ? Theme.Correct : Theme.Error);
            }
            display.Add("正确答案：" + store.ExpectedAnswer(task), "word");
            if (task.mode == "dictation")
                display.Add("中文释义：" + PartOfSpeech.DisplayChinese(task.word), "meaning");
            answer.Clear(); answer.Enabled = false;
            previous.Enabled = index > 0;
            advance.Enabled = true; advance.Text = "后一题";
            master.Enabled = false; undo.Enabled = false;
        }

        private void Submit()
        {
            if (previewReviewIndex >= 0 || quizReviewIndex >= 0
                || shown == null || shownMode == "preview") return;
            if (IsExampleQuizActive() && !ExampleRevealFlow.ReadyToSubmit(exampleRevealStage,
                store.Settings.exampleFirstLetterHints)) return;
            string entered = answer.Text;
            WordEntry answeredWord = shown;
            StudyTask answeredTask = store.CurrentTask();
            List<string> accepted = store.AcceptedAnswersForTask(answeredTask);
            try
            {
                AnswerOutcome result = store.Submit(entered, DateTime.Now);
                ShowStep();
                if (result.Correct)
                    display.Add(appearance.Prompt("correct", answeredWord, entered,
                        result.CorrectAnswer), "correct");
                else
                {
                    display.Add(appearance.Prompt("error", answeredWord, entered,
                        result.CorrectAnswer), "error");
                    string difference = SpellingDifference.Report(entered, accepted);
                    if (!string.IsNullOrWhiteSpace(difference)) display.Add(difference, "error");
                }
                if (answeredTask != null && answeredTask.mode == "dictation")
                {
                    display.Add("正确英文：" + result.CorrectAnswer, "word");
                    display.Add("中文释义：" + PartOfSpeech.DisplayChinese(answeredWord), "meaning");
                }
                if (result.MovedToErrorProne) Append("已达到门槛，移入易错本。", Theme.Text);
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "作答失败");
                ShowStep();
            }
        }

        private void Master()
        {
            if (previewReviewIndex >= 0 || quizReviewIndex >= 0) return;
            pronouncer.Stop();
            WordEntry mastered = store.MasterCurrent(DateTime.Now);
            if (mastered == null) return;
            ShowStep();
            display.Add(appearance.Prompt("mastered", mastered, null), "mastered");
        }

        private void Undo()
        {
            if (previewReviewIndex >= 0 || quizReviewIndex >= 0) return;
            pronouncer.Stop();
            WordEntry word = store.Undo();
            if (word == null) return;
            ShowStep();
            Append("已撤销：" + GameEngine.CleanEnglish(word) + "；当前题不变，下题重做该词。", Theme.Text);
        }
    }
}
