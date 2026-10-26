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
        private readonly DarkComboBox sort;
        private readonly TextBox search;
        private readonly ListView words;
        private readonly DarkComboBox destination;
        private readonly TextBox details;
        private readonly Label summary;
        private readonly CheckBox exactSearch;
        private readonly Button favoriteButton;
        private readonly Keys favoriteShortcut;
        private readonly Dictionary<string, int> unitOrder = new Dictionary<string, int>();
        private readonly Dictionary<string, string> wordLocations = new Dictionary<string, string>();

        public NotebookManagerForm(DataLoader loader, NotebookStore store,
            Keys favoriteKey = Keys.Control | Keys.L)
        {
            ModernUI.ApplyAppIcon(this);
            notebooks = store;
            favoriteShortcut = favoriteKey;
            allWords = LoadAllWords(loader, store);
            int position = 0;
            foreach (string book in loader.GetAvailableBooks())
                foreach (string unit in loader.GetUnitsForBook(book))
                    foreach (WordEntry word in loader.LoadWordList(book, new[] { unit }))
                    {
                        string key = DataLoader.WordKey(word);
                        string location = book + " / " + unit;
                        if (!unitOrder.ContainsKey(key))
                        { unitOrder[key] = position++; wordLocations[key] = location; }
                        else wordLocations[key] += "; " + location;
                    }
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
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 122));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 160));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            Controls.Add(layout);

            FlowLayoutPanel controls = new FlowLayoutPanel();
            controls.Dock = DockStyle.Fill;
            controls.BackColor = Theme.Background;
            controls.WrapContents = true;
            controls.AutoScroll = true;
            controls.Controls.Add(MakeLabel("查看"));
            filter = new DarkComboBox();
            filter.Width = 165;
            filter.Items.Add("全部词目");
            foreach (string book in Notebooks.All) filter.Items.Add(Notebooks.DisplayName(book));
            filter.Items.Add("最近 5 次操作");
            filter.Items.Add("收藏夹");
            filter.SelectedIndex = 0;
            filter.SelectedIndexChanged += delegate { RefreshWords(null); };
            controls.Controls.Add(filter);
            controls.Controls.Add(MakeLabel("搜索"));
            search = new TextBox();
            search.Width = 240;
            search.BackColor = Theme.Surface;
            search.ForeColor = Theme.Text;
            search.TextChanged += delegate { RefreshWords(null); };
            controls.Controls.Add(search);
            exactSearch = new CheckBox { Text = "精确匹配英文", AutoSize = true,
                Margin = new Padding(6, 10, 12, 0) };
            exactSearch.CheckedChanged += delegate { RefreshWords(null); };
            controls.Controls.Add(exactSearch);
            controls.Controls.Add(MakeLabel("排序"));
            sort = new DarkComboBox();
            sort.Width = 170;
            sort.Items.Add("按英文排序");
            sort.Items.Add("最近编辑优先");
            sort.Items.Add("词书 / 单元顺序");
            sort.SelectedIndex = 0;
            sort.SelectedIndexChanged += delegate { RefreshWords(null); };
            controls.Controls.Add(sort);
            summary = MakeLabel(string.Empty);
            summary.AutoSize = true;
            controls.Controls.Add(summary);
            layout.Controls.Add(controls, 0, 0);

            words = new ModernListView();
            words.Dock = DockStyle.Fill;
            words.View = View.Details;
            words.FullRowSelect = true;
            words.HideSelection = false;
            words.MultiSelect = false;
            words.ShowItemToolTips = true;
            words.BackColor = ModernUI.Card;
            words.ForeColor = Theme.Text;
            words.Columns.Add("英文", 220);
            words.Columns.Add("收藏", 100);
            words.Columns.Add("词书 / 单元", 240);
            words.Columns.Add("所属单词本", 140);
            words.Columns.Add("连续答对", 140);
            words.Columns.Add("最后编辑", 210);
            words.Columns.Add("释义", 505);
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
            actions.WrapContents = true;
            actions.AutoScroll = true;
            actions.Controls.Add(MakeLabel("将所选词移入"));
            destination = new DarkComboBox();
            destination.Width = 180;
            destination.Items.AddRange(Notebooks.All.Select(Notebooks.DisplayName).Cast<object>().ToArray());
            destination.SelectedIndex = 0;
            actions.Controls.Add(destination);
            Button apply = MakeButton("应用归属");
            apply.Click += delegate { ApplyNotebook(); };
            actions.Controls.Add(apply);
            favoriteButton = MakeButton("收藏所选词");
            favoriteButton.Click += delegate { EditSelectedFavorites(); };
            actions.Controls.Add(favoriteButton);
            Button manageFavorites = MakeButton("管理收藏夹");
            manageFavorites.Click += delegate
            {
                using (FavoriteManagerForm manager = new FavoriteManagerForm(loader, notebooks, favoriteShortcut))
                    manager.ShowDialog(this);
                RefreshWords(null);
            };
            actions.Controls.Add(manageFavorites);
            Button close = MakeButton("关闭");
            close.Click += delegate { Close(); };
            actions.Controls.Add(close);
            layout.Controls.Add(actions, 0, 3);

            Theme.Apply(this);
            RefreshWords(null);
        }

        internal static List<WordEntry> LoadAllWords(DataLoader loader, NotebookStore notebooks)
        {
            List<WordEntry> items = new List<WordEntry>();
            foreach (string book in loader.GetAvailableBooks())
            {
                items.AddRange(loader.LoadWordList(book, loader.GetUnitsForBook(book)));
            }
            items.AddRange(notebooks.Records.Select(record => record.word));
            items.AddRange(notebooks.RecentWords);
            return DataLoader.MergeWordEntries(items.Where(item => item != null))
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
            button.Width = Math.Max(160, TextRenderer.MeasureText(text, button.Font).Width + 44);
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
            if (filter.SelectedIndex == 6) candidates = candidates.Where(notebooks.IsFavorite);
            if (!string.IsNullOrEmpty(query))
            {
                candidates = candidates.Where(item => MatchesSearch(item, query,
                    exactSearch != null && exactSearch.Checked));
            }
            if (sort.SelectedIndex == 1)
                candidates = candidates.OrderByDescending(item => notebooks.GetLastEditedAt(item))
                    .ThenBy(item => GameEngine.CleanEnglish(item),
                        StringComparer.CurrentCultureIgnoreCase);
            else if (sort.SelectedIndex == 2)
                candidates = candidates.OrderBy(item => UnitPosition(item))
                    .ThenBy(item => GameEngine.CleanEnglish(item), StringComparer.CurrentCultureIgnoreCase);
            else if (filter.SelectedIndex != 5)
                candidates = candidates.OrderBy(item => GameEngine.CleanEnglish(item),
                    StringComparer.CurrentCultureIgnoreCase);

            words.BeginUpdate();
            try
            {
                words.Items.Clear();
                foreach (WordEntry word in candidates)
                {
                    string notebook = notebooks.GetNotebook(word);
                    ListViewItem row = new ListViewItem(GameEngine.CleanEnglish(word));
                    row.SubItems.Add(notebooks.IsFavorite(word) ? notebooks.FavoriteFolderIds(word).Count + " 个" : "");
                    string location;
                    row.SubItems.Add(wordLocations.TryGetValue(DataLoader.WordKey(word), out location)
                        ? location : "外部 / 历史词目");
                    row.SubItems.Add(Notebooks.DisplayName(notebook));
                    row.SubItems.Add(notebooks.GetCorrectCount(word).ToString());
                    DateTime edited = notebooks.GetLastEditedAt(word);
                    row.SubItems.Add(edited == DateTime.MinValue ? "—"
                        : edited.ToString("yyyy-MM-dd HH:mm"));
                    row.SubItems.Add(PartOfSpeech.DisplayChinese(word));
                    row.ToolTipText = (word.english ?? string.Empty) + Environment.NewLine
                        + (location ?? "外部 / 历史词目") + Environment.NewLine
                        + "收藏归属：" + notebooks.FavoriteFolderNames(word) + Environment.NewLine
                        + PartOfSpeech.DisplayChinese(word);
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
            if (words.SelectedItems.Count == 0) { details.Clear(); favoriteButton.Enabled = false; }
        }

        internal static bool MatchesSearch(WordEntry word, string query, bool exact)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            if (exact) return string.Equals(GameEngine.CleanEnglish(word), query.Trim(),
                StringComparison.OrdinalIgnoreCase) || DataLoader.WordKey(word) == query.Trim().ToLowerInvariant();
            return (word.english ?? string.Empty).IndexOf(query.Trim(),
                StringComparison.CurrentCultureIgnoreCase) >= 0
                || PartOfSpeech.DisplayChinese(word).IndexOf(query.Trim(),
                    StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private int UnitPosition(WordEntry word)
        {
            int position;
            return unitOrder.TryGetValue(DataLoader.WordKey(word), out position) ? position : int.MaxValue;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == favoriteShortcut) { ToggleSelectedFavorite(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ToggleSelectedFavorite()
        {
            if (words.SelectedItems.Count == 0) return;
            WordEntry word = (WordEntry)words.SelectedItems[0].Tag;
            notebooks.ToggleFavorite(word);
            RefreshWords(word);
        }

        private void EditSelectedFavorites()
        {
            if (words.SelectedItems.Count == 0) return;
            WordEntry word = (WordEntry)words.SelectedItems[0].Tag;
            FavoriteUi.EditWord(this, notebooks, word); RefreshWords(word);
        }

        private void ShowSelection()
        {
            if (words.SelectedItems.Count == 0) return;
            WordEntry word = (WordEntry)words.SelectedItems[0].Tag;
            favoriteButton.Enabled = true;
            favoriteButton.Text = "设置收藏归属…";
            destination.SelectedIndex = Array.IndexOf(Notebooks.All, notebooks.GetNotebook(word));
            details.Text = (word.english ?? string.Empty) + Environment.NewLine
                + PartOfSpeech.DisplayChinese(word) + Environment.NewLine
                + (word.examples ?? string.Empty);
            details.Text += Environment.NewLine + "收藏归属：" + notebooks.FavoriteFolderNames(word);
            string location;
            if (wordLocations.TryGetValue(DataLoader.WordKey(word), out location))
                details.Text += Environment.NewLine + "来源：" + location;
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
