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
        private WordEntry shown;
        private string shownMode;
        private int lastAutoSpokenCursor = -1;
        private string lastAutoSpokenListId;
        private bool speechWarningShown;
        private int previewReviewIndex = -1;
        private int quizReviewIndex = -1;
        internal string VisibleText { get { return display.ContentText; } }

        public StudySessionForm(StudyStore study, NotebookStore notebookStore,
            Func<bool> backupAction, AppearanceStore appearanceStore)
        {
            store = study;
            notebooks = notebookStore;
            manualBackup = backupAction;
            appearance = appearanceStore;
            pronunciationSettings = new PronunciationStore(AppPaths.FindProjectRoot()).Settings;
            pronouncer = new WordPronouncer(pronunciationSettings);
            openedListId = study.Active == null ? null : study.Active.id;
            Text = "每日学习";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = ModernUI.FitWindow(1280, 820);
            MinimumSize = new Size(850, 610);
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
                Padding = new Padding(20), ColumnCount = 1, RowCount = 4 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            Controls.Add(layout);
            ModernCard headingCard = new ModernCard { Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 12), Padding = new Padding(22, 8, 20, 8) };
            heading = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft YaHei UI", 13, FontStyle.Bold) };
            headingCard.Controls.Add(heading);
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
                else Submit();
            };
            layout.Controls.Add(answer, 0, 2);
            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 7, 0, 0) };
            previous = ActionButton("上一页", delegate { Previous(); });
            advance = ActionButton("下一步", delegate { Advance(); });
            master = ActionButton("斩当前词", delegate { Master(); });
            undo = ActionButton("撤销上词", delegate { Undo(); });
            backup = ActionButton("手动备份", delegate
            {
                if (manualBackup != null && manualBackup()) Append("手动备份已完成。", Theme.Correct);
            });
            replay = ActionButton("重播单词", delegate { ReplayWord(); });
            actions.Controls.Add(previous);
            actions.Controls.Add(advance);
            actions.Controls.Add(master);
            actions.Controls.Add(undo);
            actions.Controls.Add(backup);
            actions.Controls.Add(replay);
            layout.Controls.Add(actions, 0, 3);
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
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
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

        private static Button ActionButton(string text, EventHandler action)
        {
            Button button = new ModernButton { Text = text, Width = 140, Height = 44,
                Margin = new Padding(0, 0, 8, 0) };
            button.Click += action;
            return button;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == notebooks.MasteryShortcut) { Master(); return true; }
            if (keyData == (Keys)store.Settings.undoKey) { Undo(); return true; }
            if (keyData == (Keys)store.Settings.manualBackupKey)
            {
                if (manualBackup != null && manualBackup()) Append("手动备份已完成。", Theme.Correct);
                return true;
            }
            if (keyData == (Keys)pronunciationSettings.replayKey && store.Active != null
                && store.Active.phase == "preview" && pronunciationSettings.enabled)
            { ReplayWord(); return true; }
            if (store.Active != null && store.Active.phase == "preview"
                && keyData == (Keys)store.Settings.previewKey)
            { Advance(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ShowStep()
        {
            StudyList list = store.Active;
            if (list == null)
            {
                pronouncer.Stop(); replay.Enabled = false;
                StudyList opened = store.Lists.FirstOrDefault(x => x.id == openedListId);
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
                    display.Add("释义：\n" + earlier.word.chinese, "meaning");
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
                if (list.previewStage >= 2) display.Add("释义：\n" + item.word.chinese, "meaning");
                answer.Clear(); answer.Enabled = false;
                previous.Enabled = list.previewCursor > 0;
                advance.Enabled = true;
                advance.Text = list.previewStage == 2 ? "下一个词" : "显示下一层";
                advance.Focus();
                master.Enabled = true;
                undo.Enabled = store.UndoCount > 0;
                replay.Enabled = pronunciationSettings.enabled;
                if (Visible && pronunciationSettings.enabled && pronunciationSettings.automatic
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
            shown = task.word; shownMode = task.mode;
            heading.Text = kind + " · " + list.id + " · " +
                Math.Min(list.taskCursor + 1, list.tasks.Count) + "/" + list.tasks.Count
                + (task.replay ? " · 错题复现" : "");
            display.ClearContent();
            if (task.mode == "example")
            {
                string sentence = task.word.examples.Split('；').FirstOrDefault(x => x.Contains("[["));
                if (sentence == null) sentence = task.word.examples;
                display.Add("例句填空：\n\n" + Regex.Replace(sentence, @"\[\[(.*?)\]\]", "________"), "example");
            }
            else
            {
                display.Add("请根据释义拼写英文：", "normal");
                display.Add(task.word.chinese, "meaning");
                if (list.kind == "problem_review" && notebooks.ReviewFirstLetter
                    && GameEngine.CleanEnglish(task.word).Length > 0)
                    Append("\n首字母提示：" + GameEngine.CleanEnglish(task.word).Substring(0, 1), Theme.Text);
            }
            if (missingExamples > 0)
                Append("\n该单词暂时没有例句（已跳过 " + missingExamples + " 题）", Theme.Text);
            answer.Enabled = true; answer.Clear(); answer.Focus();
            previous.Enabled = list.history != null && list.history.Count > 0;
            advance.Enabled = false; master.Enabled = true;
            undo.Enabled = store.UndoCount > 0;
        }

        private void Append(string text, Color color)
        {
            display.Add(text, color == Theme.Correct ? "correct" :
                color == Theme.Error ? "error" : "normal");
        }

        private void ReplayWord()
        {
            if (store.Active == null || store.Active.phase != "preview"
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
                string sentence = (task.word.examples ?? string.Empty).Split('；')
                    .FirstOrDefault(x => x.Contains("[[")) ?? task.word.examples ?? string.Empty;
                display.Add("例句填空：\n" + Regex.Replace(sentence, @"\[\[(.*?)\]\]", "________"), "example");
            }
            else { display.Add("根据释义拼写英文：", "normal"); display.Add(task.word.chinese, "meaning"); }
            if (task.mastered) Append("本题已斩。", Theme.Text);
            else if (task.skipped) Append("该单词暂时没有例句，已跳过。", Theme.Text);
            else
            {
                Append("当时作答：" + (task.submittedAnswer ?? "（旧记录未保存作答文本）"), Theme.Text);
                Append(task.correct ? "判定：正确" : "判定：错误", task.correct ? Theme.Correct : Theme.Error);
            }
            display.Add("正确答案：" + GameEngine.CleanEnglish(task.word), "word");
            answer.Clear(); answer.Enabled = false;
            previous.Enabled = index > 0;
            advance.Enabled = true; advance.Text = "后一题";
            master.Enabled = false; undo.Enabled = false;
        }

        private void Submit()
        {
            if (previewReviewIndex >= 0 || quizReviewIndex >= 0
                || shown == null || shownMode == "preview") return;
            string entered = answer.Text;
            WordEntry answeredWord = shown;
            try
            {
                AnswerOutcome result = store.Submit(entered, DateTime.Now);
                ShowStep();
                if (result.Correct)
                    display.Add(appearance.Prompt("correct", answeredWord, entered), "correct");
                else
                    display.Add(appearance.Prompt("error", answeredWord, entered), "error");
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
