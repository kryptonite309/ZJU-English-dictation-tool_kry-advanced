using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EnglishDictationTool
{
    internal sealed class NotebookManagerForm : Form
    {
        private readonly NotebookStore notebooks;
        private readonly List<WordEntry> allWords;
        private readonly DarkComboBox filter;
        private readonly TextBox search;
        private readonly ListView words;
        private readonly DarkComboBox destination;
        private readonly TextBox details;
        private readonly Label summary;

        public NotebookManagerForm(DataLoader loader, NotebookStore store)
        {
            notebooks = store;
            allWords = LoadAllWords(loader, store);
            Text = "单词本管理";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = ModernUI.FitWindow(1300, 810);
            MinimumSize = new Size(900, 620);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = Theme.UiFont;

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(12);
            layout.ColumnCount = 1;
            layout.RowCount = 4;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 160));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            Controls.Add(layout);

            FlowLayoutPanel controls = new FlowLayoutPanel();
            controls.Dock = DockStyle.Fill;
            controls.BackColor = Theme.Background;
            controls.WrapContents = false;
            controls.Controls.Add(MakeLabel("查看"));
            filter = new DarkComboBox();
            filter.Width = 165;
            filter.Items.Add("全部词目");
            foreach (string book in Notebooks.All) filter.Items.Add(Notebooks.DisplayName(book));
            filter.Items.Add("最近 5 次操作");
            filter.SelectedIndex = 0;
            filter.SelectedIndexChanged += delegate { RefreshWords(null); };
            controls.Controls.Add(filter);
            controls.Controls.Add(MakeLabel("搜索"));
            search = new TextBox();
            search.Width = 330;
            search.BackColor = Theme.Surface;
            search.ForeColor = Theme.Text;
            search.TextChanged += delegate { RefreshWords(null); };
            controls.Controls.Add(search);
            summary = MakeLabel(string.Empty);
            summary.AutoSize = true;
            controls.Controls.Add(summary);
            layout.Controls.Add(controls, 0, 0);

            words = new ListView();
            words.Dock = DockStyle.Fill;
            words.View = View.Details;
            words.FullRowSelect = true;
            words.HideSelection = false;
            words.MultiSelect = false;
            words.ShowItemToolTips = true;
            words.BackColor = Theme.Surface;
            words.ForeColor = Theme.Text;
            words.Columns.Add("英文", 300);
            words.Columns.Add("所属单词本", 150);
            words.Columns.Add("连续答对", 115);
            words.Columns.Add("释义", 680);
            words.SelectedIndexChanged += delegate { ShowSelection(); };
            layout.Controls.Add(words, 0, 1);

            details = new TextBox();
            details.Dock = DockStyle.Fill;
            details.Multiline = true;
            details.ReadOnly = true;
            details.ScrollBars = ScrollBars.Vertical;
            details.BackColor = Theme.Surface;
            details.ForeColor = Theme.Text;
            layout.Controls.Add(details, 0, 2);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.BackColor = Theme.Background;
            actions.WrapContents = false;
            actions.Controls.Add(MakeLabel("将所选词移入"));
            destination = new DarkComboBox();
            destination.Width = 180;
            destination.Items.AddRange(Notebooks.All.Select(Notebooks.DisplayName).Cast<object>().ToArray());
            destination.SelectedIndex = 0;
            actions.Controls.Add(destination);
            Button apply = MakeButton("应用归属");
            apply.Click += delegate { ApplyNotebook(); };
            actions.Controls.Add(apply);
            Button close = MakeButton("关闭");
            close.Click += delegate { Close(); };
            actions.Controls.Add(close);
            layout.Controls.Add(actions, 0, 3);

            Theme.Apply(this);
            RefreshWords(null);
        }

        private static List<WordEntry> LoadAllWords(DataLoader loader, NotebookStore notebooks)
        {
            List<WordEntry> items = new List<WordEntry>();
            foreach (string book in loader.GetAvailableBooks())
            {
                items.AddRange(loader.LoadWordList(book, loader.GetUnitsForBook(book)));
            }
            items.AddRange(notebooks.Records.Select(record => record.word));
            items.AddRange(notebooks.RecentWords);
            return items.Where(item => item != null).Distinct()
                .OrderBy(item => GameEngine.CleanEnglish(item), StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static Label MakeLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Padding = new Padding(0, 10, 8, 0);
            label.BackColor = Theme.Background;
            label.ForeColor = Theme.Text;
            return label;
        }

        private static Button MakeButton(string text)
        {
            Button button = new ModernButton();
            button.Text = text;
            button.Width = 130;
            button.Height = 42;
            return button;
        }

        private void RefreshWords(WordEntry selected)
        {
            if (words == null || filter == null || search == null) return;
            string query = search.Text.Trim();
            IEnumerable<WordEntry> candidates = filter.SelectedIndex == 5
                ? notebooks.RecentWords
                : allWords;
            if (filter.SelectedIndex >= 1 && filter.SelectedIndex <= 4)
            {
                string notebook = Notebooks.All[filter.SelectedIndex - 1];
                candidates = candidates.Where(item => notebooks.GetNotebook(item) == notebook);
            }
            if (!string.IsNullOrEmpty(query))
            {
                candidates = candidates.Where(item =>
                    (item.english ?? string.Empty).IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0
                    || (item.chinese ?? string.Empty).IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0);
            }

            words.BeginUpdate();
            try
            {
                words.Items.Clear();
                foreach (WordEntry word in candidates)
                {
                    string notebook = notebooks.GetNotebook(word);
                    ListViewItem row = new ListViewItem(GameEngine.CleanEnglish(word));
                    row.SubItems.Add(Notebooks.DisplayName(notebook));
                    row.SubItems.Add(notebooks.GetCorrectCount(word).ToString());
                    row.SubItems.Add(word.chinese ?? string.Empty);
                    row.ToolTipText = (word.english ?? string.Empty) + Environment.NewLine
                        + (word.chinese ?? string.Empty);
                    row.Tag = word;
                    words.Items.Add(row);
                    if (selected != null && selected.Equals(word)) row.Selected = true;
                }
            }
            finally
            {
                words.EndUpdate();
            }

            summary.Text = string.Format("当前 {0} 条；错题 {1} / 易错 {2} / 已掌握 {3}",
                words.Items.Count,
                notebooks.Count(Notebooks.Wrong),
                notebooks.Count(Notebooks.ErrorProne),
                notebooks.Count(Notebooks.Mastered));
            if (words.SelectedItems.Count == 0) details.Clear();
        }

        private void ShowSelection()
        {
            if (words.SelectedItems.Count == 0) return;
            WordEntry word = (WordEntry)words.SelectedItems[0].Tag;
            destination.SelectedIndex = Array.IndexOf(Notebooks.All, notebooks.GetNotebook(word));
            details.Text = (word.english ?? string.Empty) + Environment.NewLine
                + (word.chinese ?? string.Empty) + Environment.NewLine
                + (word.examples ?? string.Empty);
        }

        private void ApplyNotebook()
        {
            if (words.SelectedItems.Count == 0 || destination.SelectedIndex < 0) return;
            WordEntry word = (WordEntry)words.SelectedItems[0].Tag;
            notebooks.MoveToNotebook(word, Notebooks.All[destination.SelectedIndex]);
            RefreshWords(word);
        }
    }
}
