using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace EnglishDictationTool
{
    internal sealed class StudyStatisticsSnapshot
    {
        public int newWords;
        public int listReviewWords;
        public int problemReviewWords;
        public int freeAttempts;
        public int attempts;
        public int correct;
        public int firstAttempts;
        public int firstCorrect;
        public int retries;
        public long milliseconds;
        public SortedDictionary<DateTime, int[]> trend = new SortedDictionary<DateTime, int[]>();
        public int Accuracy { get { return attempts == 0 ? 0 : correct * 100 / attempts; } }
        public int FirstAccuracy { get { return firstAttempts == 0 ? 0 : firstCorrect * 100 / firstAttempts; } }
    }

    internal static class StudyStatistics
    {
        public static StudyStatisticsSnapshot Calculate(StudyStore store, PracticeStore practice,
            DateTime? from, DateTime? through)
        {
            StudyStatisticsSnapshot result = new StudyStatisticsSnapshot();
            Func<DateTime, bool> include = value => value != DateTime.MinValue
                && (!from.HasValue || StudyStore.StudyDay(value) >= from.Value.Date)
                && (!through.HasValue || StudyStore.StudyDay(value) <= through.Value.Date);
            Func<string, bool> includeDay = value =>
            {
                DateTime parsed;
                return DateTime.TryParse(value, out parsed)
                    && (!from.HasValue || parsed.Date >= from.Value.Date)
                    && (!through.HasValue || parsed.Date <= through.Value.Date);
            };
            foreach (StudyList list in store.Lists)
            {
                int words = (list.items ?? new List<StudyItem>()).Count(x =>
                    include(x.firstAnsweredAt));
                if (list.kind == "new") result.newWords += words;
                else if (list.kind == "list_review") result.listReviewWords += words;
                else if (list.kind == "problem_review") result.problemReviewWords += words;
                foreach (StudyTask task in (list.history ?? new List<StudyTask>()).Where(x =>
                    x != null && !x.skipped && !x.mastered && include(x.answeredAt)))
                {
                    result.attempts++;
                    if (task.correct) result.correct++;
                    bool first = !task.replay && task.attempt <= 1;
                    if (first)
                    {
                        result.firstAttempts++;
                        if (task.correct) result.firstCorrect++;
                    }
                    else result.retries++;
                    AddTrend(result, StudyStore.StudyDay(task.answeredAt), task.correct);
                }
                if (includeDay(list.studyDate)) result.milliseconds += Math.Max(0, list.activeMilliseconds);
            }
            foreach (PracticeAttempt attempt in practice.Attempts.Where(x => include(x.answeredAt)))
            {
                result.freeAttempts++;
                result.attempts++;
                if (attempt.correct) result.correct++;
                if (!attempt.retry)
                {
                    result.firstAttempts++;
                    if (attempt.correct) result.firstCorrect++;
                }
                else result.retries++;
                AddTrend(result, StudyStore.StudyDay(attempt.answeredAt), attempt.correct);
            }
            foreach (PracticeSessionState session in practice.Completed)
                if (include(session.createdAt)) result.milliseconds += Math.Max(0, session.activeMilliseconds);
            if (practice.Current != null && practice.Current.active && include(practice.Current.createdAt))
                result.milliseconds += Math.Max(0, practice.Current.activeMilliseconds);
            return result;
        }

        private static void AddTrend(StudyStatisticsSnapshot snapshot, DateTime day, bool correct)
        {
            int[] value;
            if (!snapshot.trend.TryGetValue(day.Date, out value))
            { value = new int[2]; snapshot.trend[day.Date] = value; }
            value[0]++;
            if (correct) value[1]++;
        }
    }

    internal sealed class StatisticsChart : Control
    {
        private SortedDictionary<DateTime, int[]> data = new SortedDictionary<DateTime, int[]>();
        public StatisticsChart() { DoubleBuffered = true; Height = 250; BackColor = ModernUI.Card; }
        public void SetData(SortedDictionary<DateTime, int[]> value)
        { data = value ?? new SortedDictionary<DateTime, int[]>(); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle plot = new Rectangle(58, 24, Math.Max(10, Width - 86), Math.Max(10, Height - 64));
            using (Pen grid = new Pen(Color.FromArgb(65, 76, 92)))
                for (int i = 0; i <= 4; i++) e.Graphics.DrawLine(grid, plot.Left,
                    plot.Top + plot.Height * i / 4, plot.Right, plot.Top + plot.Height * i / 4);
            List<KeyValuePair<DateTime, int[]>> points = data.OrderBy(x => x.Key).TakeLastCompat(30).ToList();
            if (points.Count == 0)
            {
                TextRenderer.DrawText(e.Graphics, "当前区间还没有作答数据", Theme.UiFont, plot,
                    Theme.MutedText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }
            int max = Math.Max(1, points.Max(x => x.Value[0]));
            PointF[] attempts = new PointF[points.Count];
            PointF[] correct = new PointF[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                float x = points.Count == 1 ? plot.Left + plot.Width / 2f
                    : plot.Left + plot.Width * i / (float)(points.Count - 1);
                attempts[i] = new PointF(x, plot.Bottom - plot.Height * points[i].Value[0] / (float)max);
                correct[i] = new PointF(x, plot.Bottom - plot.Height * points[i].Value[1] / (float)max);
            }
            if (attempts.Length > 1)
            {
                using (Pen total = new Pen(Color.FromArgb(115, 190, 255), 3f)) e.Graphics.DrawLines(total, attempts);
                using (Pen right = new Pen(Color.FromArgb(94, 226, 163), 3f)) e.Graphics.DrawLines(right, correct);
            }
            foreach (PointF point in attempts) e.Graphics.FillEllipse(Brushes.LightSkyBlue,
                point.X - 3, point.Y - 3, 6, 6);
            foreach (PointF point in correct) e.Graphics.FillEllipse(Brushes.LightGreen,
                point.X - 3, point.Y - 3, 6, 6);
            TextRenderer.DrawText(e.Graphics, "蓝色：作答  绿色：正确", Theme.UiFont,
                new Rectangle(plot.Left, plot.Bottom + 8, plot.Width, 28), Theme.MutedText,
                TextFormatFlags.HorizontalCenter);
        }
    }

    internal static class EnumerableCompatibility
    {
        public static IEnumerable<T> TakeLastCompat<T>(this IEnumerable<T> source, int count)
        {
            Queue<T> queue = new Queue<T>();
            foreach (T item in source)
            {
                queue.Enqueue(item);
                if (queue.Count > count) queue.Dequeue();
            }
            return queue;
        }
    }

    internal sealed class StatisticsForm : Form
    {
        private readonly StudyStore store;
        private readonly PracticeStore practice;
        private readonly AppearanceStore appearance;
        private readonly FlowLayoutPanel cards;
        private readonly StatisticsChart chart;
        private readonly StudyCalendarView calendar;
        private readonly Label dailySummary;
        private readonly ListView dailyLists;
        private readonly List<ModernButton> rangeButtons = new List<ModernButton>();
        private string range = "today";

        public StatisticsForm(StudyStore store, PracticeStore practice, AppearanceStore appearance)
        {
            ModernUI.ApplyAppIcon(this);
            this.store = store; this.practice = practice; this.appearance = appearance;
            Text = "学习统计";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = ModernUI.FitWindow(1500, 940);
            MinimumSize = new Size(1120, 760);
            BackColor = Theme.Background; ForeColor = Theme.Text; Font = Theme.UiFont;
            TableLayoutPanel shell = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2,
                ColumnCount = 1, Padding = new Padding(18), BackColor = Theme.Background };
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(shell);
            FlowLayoutPanel ranges = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false,
                BackColor = Theme.Background, Padding = new Padding(0, 6, 0, 0) };
            foreach (KeyValuePair<string, string> item in new[] {
                new KeyValuePair<string,string>("today", "今日"),
                new KeyValuePair<string,string>("7", "近 7 天"),
                new KeyValuePair<string,string>("30", "近 30 天"),
                new KeyValuePair<string,string>("all", "累计") })
            {
                ModernButton button = new ModernButton { Text = item.Value, Width = 132, Height = 42,
                    Primary = item.Key == range, Tag = item.Key };
                button.Click += delegate(object sender, EventArgs e)
                {
                    range = (string)((Button)sender).Tag;
                    foreach (ModernButton choice in rangeButtons)
                    { choice.Primary = (string)choice.Tag == range; choice.Invalidate(); }
                    RefreshOverview();
                };
                rangeButtons.Add(button);
                ranges.Controls.Add(button);
            }
            shell.Controls.Add(ranges, 0, 0);
            TabControl tabs = new ModernTabControl { Dock = DockStyle.Fill };
            shell.Controls.Add(tabs, 0, 1);

            TabPage overview = new TabPage("概览与趋势") { BackColor = Theme.Background };
            tabs.TabPages.Add(overview);
            TableLayoutPanel overviewLayout = new TableLayoutPanel { Dock = DockStyle.Fill,
                RowCount = 2, ColumnCount = 1, Padding = new Padding(12), BackColor = Theme.Background };
            overviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 360));
            overviewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            overview.Controls.Add(overviewLayout);
            cards = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true,
                BackColor = Theme.Background, Padding = new Padding(4) };
            overviewLayout.Controls.Add(cards, 0, 0);
            chart = new StatisticsChart { Dock = DockStyle.Fill };
            overviewLayout.Controls.Add(chart, 0, 1);

            TabPage calendarPage = new TabPage("学习日历") { BackColor = Theme.Background };
            tabs.TabPages.Add(calendarPage);
            TableLayoutPanel calendarLayout = new TableLayoutPanel { Dock = DockStyle.Fill,
                RowCount = 3, ColumnCount = 1, Padding = new Padding(12), BackColor = Theme.Background };
            calendarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            calendarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
            calendarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
            calendarPage.Controls.Add(calendarLayout);
            calendar = new StudyCalendarView(store, appearance) { Dock = DockStyle.Fill };
            calendar.SelectedDateChanged += delegate { RefreshDay(); };
            calendarLayout.Controls.Add(calendar, 0, 0);
            dailySummary = new Label { Dock = DockStyle.Fill, Font = new Font("Microsoft YaHei UI", 11,
                FontStyle.Bold), ForeColor = Theme.Text, TextAlign = ContentAlignment.MiddleLeft };
            calendarLayout.Controls.Add(dailySummary, 0, 1);
            dailyLists = new ListView { Dock = DockStyle.Fill, BackColor = Theme.Surface,
                ForeColor = Theme.Text, View = View.Details, FullRowSelect = true, MultiSelect = false };
            dailyLists.Columns.Add("提取时间", 185); dailyLists.Columns.Add("学习部分", 210);
            dailyLists.Columns.Add("词数", 80); dailyLists.Columns.Add("用时", 90);
            dailyLists.Columns.Add("状态", 110); dailyLists.Columns.Add("列表编号", 440);
            dailyLists.DoubleClick += delegate
            {
                StudyList selected = dailyLists.SelectedItems.Count == 0 ? null
                    : dailyLists.SelectedItems[0].Tag as StudyList;
                if (selected != null) new StudyListViewerForm(selected).ShowDialog(this);
            };
            calendarLayout.Controls.Add(dailyLists, 0, 2);
            Theme.Apply(this);
            RefreshOverview(); RefreshDay();
        }

        private void RefreshOverview()
        {
            DateTime today = StudyStore.StudyDay(DateTime.Now);
            DateTime? from = range == "today" ? today : range == "7" ? today.AddDays(-6)
                : range == "30" ? today.AddDays(-29) : (DateTime?)null;
            StudyStatisticsSnapshot value = StudyStatistics.Calculate(store, practice, from, today);
            cards.Controls.Clear();
            AddCard("新学", value.newWords.ToString(), "完成首次作答的单词");
            AddCard("列表复习", value.listReviewWords.ToString(), "历史列表作答单词");
            AddCard("错题复习", value.problemReviewWords.ToString(), "错题与易错词作答");
            AddCard("自由练习", value.freeAttempts.ToString(), "自由练习作答次数");
            AddCard("总体正确率", value.Accuracy + "%", value.correct + " / " + value.attempts);
            AddCard("首次正确率", value.FirstAccuracy + "%", value.firstCorrect + " / " + value.firstAttempts);
            AddCard("复现次数", value.retries.ToString(), "重复作答与错题复现");
            AddCard("学习时长", Math.Floor(TimeSpan.FromMilliseconds(value.milliseconds).TotalMinutes) + " 分钟",
                "仅计入有效学习时间");
            chart.SetData(value.trend);
        }

        private void AddCard(string title, string value, string detail)
        {
            ModernCard card = new ModernCard { Width = 330, Height = 150, Margin = new Padding(8),
                Eyebrow = title, Title = value, Detail = detail };
            cards.Controls.Add(card);
        }

        private void RefreshDay()
        {
            string date = calendar.SelectedDate.ToString("yyyy-MM-dd");
            List<StudyList> lists = store.Lists.Where(x => x.studyDate == date).OrderBy(x => x.createdAt).ToList();
            Func<string, int> count = kind => lists.Where(x => x.kind == kind)
                .Sum(x => x.items.Count(y => y.firstAnsweredAt != DateTime.MinValue
                    && StudyStore.StudyDayKey(y.firstAnsweredAt) == date));
            dailySummary.Text = date + "    新学 " + count("new") + "  ·  列表复习 "
                + count("list_review") + "  ·  错题复习 " + count("problem_review")
                + "  ·  新学列表 " + lists.Count(x => x.kind == "new") + " 个";
            dailyLists.Items.Clear();
            foreach (StudyList list in lists)
            {
                string kind = list.kind == "new" ? "新学" : list.kind == "list_review"
                    ? "历史列表复习" : "错题与易错词复习";
                ListViewItem row = new ListViewItem(list.createdAt.ToString("yyyy-MM-dd HH:mm:ss"));
                row.SubItems.Add(kind); row.SubItems.Add(list.items.Count.ToString());
                row.SubItems.Add(Math.Floor(TimeSpan.FromMilliseconds(list.activeMilliseconds).TotalMinutes) + " 分");
                row.SubItems.Add(list.status == "completed" ? "已完成" : list.status == "active" ? "进行中" : "已结算");
                row.SubItems.Add(list.id); row.Tag = list; dailyLists.Items.Add(row);
            }
        }
    }
}
