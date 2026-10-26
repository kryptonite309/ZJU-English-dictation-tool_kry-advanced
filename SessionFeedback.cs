using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EnglishDictationTool
{
    internal sealed class AnswerFeedback
    {
        public WordEntry word { get; set; }
        public string answer { get; set; }
        public string input { get; set; }
        public string mode { get; set; }
        public string difference { get; set; }
        public bool correct { get; set; }

        public void Render(StyledCanvas canvas, AppearanceStore appearance)
        {
            canvas.ClearContent();
            canvas.Add(appearance.Prompt(correct ? "correct" : "error", word, input, answer), correct ? "correct" : "error");
            canvas.Add(GameEngine.CleanEnglish(word), "word");
            canvas.Add(PartOfSpeech.DisplayChinese(word), "meaning");
            if (!correct) canvas.AddExample(word);
            canvas.Add("你的答案：" + input, "normal");
            if (mode == "example") canvas.Add("本题正确形式：" + answer, "correct");
            if (!correct && !string.IsNullOrWhiteSpace(difference)) canvas.Add(difference, "error");
        }
    }

    internal sealed class SessionSummary
    {
        public string title;
        public string module;
        public int words;
        public int total;
        public int firstCorrect;
        public bool manualEnd;
        public long milliseconds;
        public string Accuracy { get { return total == 0 ? "未进行有效作答" : (100d * firstCorrect / total).ToString("0.#") + "%"; } }
        private static string Key(StudyTask task)
        {
            return DataLoader.WordKey(task.word) + "\u001f" + task.mode + "\u001f" + (task.examplePrompt ?? "");
        }
        public static SessionSummary FromStudy(StudyList list)
        {
            var history = (list.history ?? new List<StudyTask>()).Where(x => x.word != null && !x.skipped && !x.mastered).ToList();
            var all = history.Concat(list.tasks ?? new List<StudyTask>()).Concat(list.retries ?? new List<StudyTask>())
                .Where(x => x.word != null && !x.skipped && !x.mastered).GroupBy(Key).ToList();
            return new SessionSummary { title = list.status == "ended" ? "本次学习已结束" : "本次学习完成",
                module = list.kind == "new" ? "新学" : list.kind == "list_review" ? "历史列表复习" : "错题与易错词复习",
                manualEnd = list.status == "ended",
                words = list.items.Select(x => DataLoader.WordKey(x.word)).Distinct().Count(),
                total = list.status == "ended" ? history.GroupBy(Key).Count() : all.Count,
                firstCorrect = list.status == "ended" ? history.GroupBy(Key).Count(g => g.Any(x => x.correct))
                    : history.GroupBy(Key).Count(g => g.OrderBy(x => x.answeredAt).First().correct && !g.OrderBy(x => x.answeredAt).First().replay),
                milliseconds = list.activeMilliseconds };
        }
        public static SessionSummary FromPractice(PracticeSessionState session, IEnumerable<PracticeAttempt> attempts, bool manual)
        {
            var answered = attempts.Where(x => x.sessionId == session.id).ToList();
            var first = answered.GroupBy(x => x.taskIndex)
                .Select(g => g.OrderBy(x => x.answeredAt).First()).ToList();
            return new SessionSummary { title = manual ? "本次练习已结束" : "本次学习完成",
                module = session.kind == "problem_free" ? "错题自由模式"
                    : session.kind == "favorites" ? "收藏夹练习" : "自由练习",
                manualEnd = manual,
                words = session.engine.deck.Select(DataLoader.WordKey).Distinct().Count(),
                total = manual ? first.Count : session.engine.deck.Count,
                firstCorrect = manual ? answered.GroupBy(x => x.taskIndex).Count(g => g.Any(x => x.correct))
                    : first.Count(x => x.correct && !x.retry), milliseconds = session.activeMilliseconds };
        }
    }

    internal sealed class SessionSummaryView : AppearanceBackgroundPanel
    {
        private readonly SessionSummary summary;
        private readonly bool showTime;
        private readonly string precision;
        public SessionSummaryView(SessionSummary summary, bool showTime, string precision)
        {
            this.summary = summary; this.showTime = showTime; this.precision = precision;
            Dock = DockStyle.Fill; Resize += delegate { Invalidate(); };
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            int width = Math.Max(100, ClientSize.Width - 80);
            using (Font title = new Font("Segoe UI", 30, FontStyle.Bold))
            using (Font label = new Font("Segoe UI", 13))
            using (Font value = new Font("Segoe UI", 25, FontStyle.Bold))
            {
                TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding;
                int titleHeight = TextRenderer.MeasureText(e.Graphics, summary.title, title, new Size(width, int.MaxValue), flags).Height + 20;
                int labelHeight = TextRenderer.MeasureText(e.Graphics, "首次作答正确率", label, new Size(width / 3, int.MaxValue), flags).Height + 16;
                int valueHeight = TextRenderer.MeasureText(e.Graphics, "00:00:00.000", value).Height + 20;
                int totalHeight = titleHeight + labelHeight * 3 + valueHeight + 70;
                int top = Math.Max(20, (ClientSize.Height - totalHeight) / 2);
                TextRenderer.DrawText(e.Graphics, summary.title, title, new Rectangle(40, top, width, titleHeight), Theme.Text, flags);
                top += titleHeight + 8;
                TextRenderer.DrawText(e.Graphics, summary.module, label, new Rectangle(40, top, width, labelHeight), Theme.MutedText, flags);
                top += labelHeight + 30;
                string accuracyLabel = summary.manualEnd ? "已作答题目正确率" : "首次作答正确率";
                string[] labels = showTime ? new[] { "单词数", accuracyLabel, "有效用时" } : new[] { "单词数", accuracyLabel };
                string[] values = { summary.words.ToString(), summary.Accuracy, LearningTimer.Format(summary.milliseconds, precision) };
                int cell = width / labels.Length;
                for (int i = 0; i < labels.Length; i++)
                {
                    TextRenderer.DrawText(e.Graphics, labels[i], label, new Rectangle(40 + i * cell, top, cell, labelHeight), Theme.MutedText, flags);
                    float size = 25;
                    while (size > 10)
                    {
                        using (Font candidate = new Font("Segoe UI", size, FontStyle.Bold))
                            if (TextRenderer.MeasureText(e.Graphics, values[i], candidate).Width <= cell - 16) break;
                        size -= 1;
                    }
                    using (Font fitted = new Font("Segoe UI", size, FontStyle.Bold))
                        TextRenderer.DrawText(e.Graphics, values[i], fitted, new Rectangle(40 + i * cell, top + labelHeight, cell, valueHeight), Theme.Text, flags);
                }
                top += labelHeight + valueHeight + 32;
                TextRenderer.DrawText(e.Graphics, "Esc  关闭当前学习窗口", label, new Rectangle(40, top, width, labelHeight), Theme.MutedText, flags);
            }
        }
    }
}
