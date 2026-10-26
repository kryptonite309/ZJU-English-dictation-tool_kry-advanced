using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EnglishDictationTool
{
    // Scoped to the learning window: no system-wide hotkeys, and no handling in a modal child.
    // This runs before focused controls classify or consume a key. Preview has no enabled input,
    // and the custom buttons deliberately cannot take keyboard focus.
    internal sealed class FavoriteShortcutFilter : IMessageFilter, IDisposable
    {
        private readonly Form owner;
        private readonly Func<Keys> shortcut;
        private readonly Action action;
        private bool disposed;
        public FavoriteShortcutFilter(Form owner, Func<Keys> shortcut, Action action)
        {
            this.owner = owner; this.shortcut = shortcut; this.action = action;
            Application.AddMessageFilter(this);
            owner.Disposed += delegate { Dispose(); };
        }
        internal bool HandleTarget(Control target, Keys keyData, bool repeated)
        {
            if (disposed || target == null || target.FindForm() != owner || !owner.Visible
                || owner.OwnedForms.Any(x => x.Visible && x.Modal) || keyData != shortcut()) return false;
            if (!repeated) action();
            return true;
        }
        public bool PreFilterMessage(ref Message message)
        {
            if (message.Msg != 0x100 && message.Msg != 0x104) return false;
            Keys key = ((Keys)message.WParam.ToInt32() & Keys.KeyCode) | Control.ModifierKeys;
            return HandleTarget(Control.FromChildHandle(message.HWnd), key,
                (message.LParam.ToInt64() & (1L << 30)) != 0);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; Application.RemoveMessageFilter(this);
        }
    }

    internal static class FavoriteUi
    {
        internal static Button Button(string text, EventHandler clicked, int width = 160)
        {
            Button button = new ModernButton { Text = text, Width = width, Height = 44,
                Margin = new Padding(4), MinimumSize = new Size(110, 44) };
            button.Click += clicked; return button;
        }
        internal static Label Label(string text)
        { return new Label { Text = text, AutoSize = true, Margin = new Padding(6, 12, 6, 8) }; }
        internal static string ButtonText(NotebookStore store, WordEntry word)
        {
            int count = store.FavoriteFolderIds(word).Count;
            return count == 0 ? "收藏到…" : "收藏 (" + count + ")";
        }
        internal static void EditWord(IWin32Window owner, NotebookStore store, WordEntry word)
        {
            if (word == null) return;
            using (WordFavoriteForm form = new WordFavoriteForm(store, word)) form.ShowDialog(owner);
        }
    }

    internal sealed class WordFavoriteForm : Form
    {
        private readonly NotebookStore store;
        private readonly WordEntry word;
        private readonly CheckedListBox folders;
        private readonly TextBox newName;
        internal IList<string> CheckedFolderIds
        { get { return folders.CheckedItems.Cast<FavoriteFolder>().Select(x => x.id).ToList(); } }

        public WordFavoriteForm(NotebookStore store, WordEntry word)
        {
            this.store = store; this.word = word;
            ModernUI.ApplyAppIcon(this);
            Text = "收藏归属  " + GameEngine.CleanEnglish(word);
            StartPosition = FormStartPosition.CenterParent; ClientSize = ModernUI.FitWindow(600, 550);
            MinimumSize = new Size(500, 420); Font = Theme.UiFont; BackColor = Theme.Background;
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16),
                RowCount = 4, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            Controls.Add(layout);
            layout.Controls.Add(new Label { Dock = DockStyle.Fill, AutoEllipsis = false,
                Text = GameEngine.CleanEnglish(word) + "\r\n可同时勾选多个收藏夹。快捷收藏的目标可在“管理收藏夹”中更改。",
                TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            folders = new DarkCheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true,
                IntegralHeight = false };
            layout.Controls.Add(folders, 0, 1);
            FlowLayoutPanel create = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true };
            newName = new TextBox { Width = 270, Margin = new Padding(4, 10, 4, 0), MaxLength = 60 };
            create.Controls.Add(newName);
            create.Controls.Add(FavoriteUi.Button("新建并勾选", delegate { CreateFolder(); }));
            layout.Controls.Add(create, 0, 2);
            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            Button save = FavoriteUi.Button("保存归属", delegate { SaveMembership(); });
            actions.Controls.Add(save);
            actions.Controls.Add(FavoriteUi.Button("取消", delegate { DialogResult = DialogResult.Cancel; Close(); }));
            layout.Controls.Add(actions, 0, 3);
            Theme.Apply(this); RefreshFolders(store.FavoriteFolderIds(word));
            AcceptButton = save;
        }
        private void RefreshFolders(IEnumerable<string> checkedIds)
        {
            HashSet<string> selected = new HashSet<string>(checkedIds);
            folders.Items.Clear();
            foreach (FavoriteFolder folder in store.FavoriteFolders)
                folders.Items.Add(folder, selected.Contains(folder.id));
        }
        private void CreateFolder()
        {
            try
            {
                List<string> ids = CheckedFolderIds.ToList();
                FavoriteFolder folder = store.CreateFavoriteFolder(newName.Text);
                ids.Add(folder.id); newName.Clear(); RefreshFolders(ids);
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, "未新建收藏夹"); }
        }
        private void SaveMembership()
        {
            try { store.SetFavoriteFolders(word, CheckedFolderIds); DialogResult = DialogResult.OK; Close(); }
            catch (Exception error) { MessageBox.Show(this, error.Message, "未保存收藏归属"); }
        }
    }

    internal sealed class FavoriteManagerForm : Form
    {
        private sealed class Choice
        {
            public string Id; public string Text;
            public override string ToString() { return Text; }
        }
        private readonly NotebookStore store;
        private readonly List<WordEntry> vocabulary;
        private readonly ListBox folders;
        private readonly TextBox folderName, search, details;
        private readonly CheckBox onlyCurrent;
        private readonly ListView words;
        private readonly Label summary;
        private readonly Button delete, setDefault;
        private readonly Keys favoriteKey;
        private bool refreshing;
        private string SelectedId { get { Choice choice = folders.SelectedItem as Choice;
            return choice == null ? store.DefaultFavoriteFolderId : choice.Id; } }

        public FavoriteManagerForm(DataLoader loader, NotebookStore store, Keys favoriteKey)
        {
            this.store = store; this.favoriteKey = favoriteKey;
            vocabulary = NotebookManagerForm.LoadAllWords(loader, store);
            ModernUI.ApplyAppIcon(this); Text = "收藏夹管理";
            StartPosition = FormStartPosition.CenterParent; ClientSize = ModernUI.FitWindow(1240, 820);
            MinimumSize = new Size(950, 650); Font = Theme.UiFont; BackColor = Theme.Background;
            TableLayoutPanel shell = new TableLayoutPanel { Dock = DockStyle.Fill,
                Padding = new Padding(16), ColumnCount = 2, RowCount = 1 };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 265));
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); Controls.Add(shell);
            TableLayoutPanel left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4,
                Margin = new Padding(0, 0, 16, 0) };
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 240));
            shell.Controls.Add(left, 0, 0);
            left.Controls.Add(FavoriteUi.Label("我的收藏夹"), 0, 0);
            folders = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
            folders.SelectedIndexChanged += delegate { if (!refreshing) ShowFolder(); };
            left.Controls.Add(folders, 0, 1);
            folderName = new TextBox { Dock = DockStyle.Fill, MaxLength = 60, Margin = new Padding(0, 8, 0, 4) };
            left.Controls.Add(folderName, 0, 2);
            TableLayoutPanel folderActions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            folderActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int row = 0; row < 4; row++) folderActions.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            folderActions.Controls.Add(FavoriteUi.Button("新建收藏夹", delegate { EditFolder(true); }, 228));
            folderActions.Controls.Add(FavoriteUi.Button("重命名", delegate { EditFolder(false); }, 228));
            setDefault = FavoriteUi.Button("设为快捷收藏夹", delegate { store.SetDefaultFavoriteFolder(SelectedId); }, 228);
            folderActions.Controls.Add(setDefault);
            delete = FavoriteUi.Button("删除收藏夹", delegate { DeleteFolder(); }, 228);
            folderActions.Controls.Add(delete);
            foreach (Control button in folderActions.Controls)
            { button.Dock = DockStyle.Fill; button.MinimumSize = Size.Empty; button.Margin = new Padding(2, 3, 2, 3); }
            left.Controls.Add(folderActions, 0, 3);
            TableLayoutPanel right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 138));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 58)); shell.Controls.Add(right, 1, 0);
            TableLayoutPanel header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            FlowLayoutPanel filters = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
            filters.Controls.Add(FavoriteUi.Label("搜索单词 / 释义"));
            search = new TextBox { Width = 245, Margin = new Padding(4, 8, 4, 4) };
            search.TextChanged += delegate { ShowFolder(); }; filters.Controls.Add(search);
            onlyCurrent = new CheckBox { Text = "仅显示当前收藏夹", AutoSize = true, Checked = true,
                Margin = new Padding(8, 10, 4, 4) };
            onlyCurrent.CheckedChanged += delegate { ShowFolder(); }; filters.Controls.Add(onlyCurrent);
            summary = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            header.Controls.Add(filters, 0, 0); header.Controls.Add(summary, 0, 1); right.Controls.Add(header, 0, 0);
            words = new ModernListView { Dock = DockStyle.Fill, View = View.Details,
                MultiSelect = true, FullRowSelect = true, HideSelection = false, ShowItemToolTips = true };
            words.Columns.Add("单词", 180); words.Columns.Add("释义", 270); words.Columns.Add("所属收藏夹", 310);
            words.Resize += delegate { LayoutColumns(); };
            words.SelectedIndexChanged += delegate { ShowSelection(); }; right.Controls.Add(words, 0, 1);
            details = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
                ScrollBars = ScrollBars.Vertical, Margin = new Padding(0, 10, 0, 4) }; right.Controls.Add(details, 0, 2);
            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true };
            actions.Controls.Add(FavoriteUi.Button("加入当前收藏夹", delegate { SetMembership(true); }, 185));
            actions.Controls.Add(FavoriteUi.Button("移出当前收藏夹", delegate { SetMembership(false); }, 185));
            actions.Controls.Add(FavoriteUi.Button("多选收藏归属…", delegate { EditMembership(); }, 180));
            right.Controls.Add(actions, 0, 3);
            FlowLayoutPanel bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            bottom.Controls.Add(FavoriteUi.Button("关闭", delegate { Close(); }));
            right.Controls.Add(bottom, 0, 4);
            Theme.Apply(this); store.Changed += StoreChanged; RefreshFolders(null); LayoutColumns();
        }
        private void LayoutColumns()
        {
            if (words == null || words.Columns.Count != 3 || words.ClientSize.Width < 100) return;
            int width = words.ClientSize.Width;
            words.Columns[0].Width = Math.Max(140, Math.Min(200, width * 22 / 100));
            words.Columns[1].Width = Math.Max(180, Math.Min(280, width * 30 / 100));
            words.Columns[2].Width = Math.Max(230, width - words.Columns[0].Width - words.Columns[1].Width - 1);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) store.Changed -= StoreChanged;
            base.Dispose(disposing);
        }
        private void StoreChanged(object sender, EventArgs args) { RefreshFolders(SelectedId); }
        private void RefreshFolders(string selectedId)
        {
            refreshing = true;
            try
            {
                folders.Items.Clear();
                foreach (FavoriteFolder folder in store.FavoriteFolders)
                    folders.Items.Add(new Choice { Id = folder.id, Text = folder.name + " ("
                        + store.GetFavoriteWords(folder.id).Count + ")"
                        + (folder.id == store.DefaultFavoriteFolderId ? "  快捷" : "") });
                int selected = folders.Items.Cast<Choice>().ToList().FindIndex(x => x.Id == selectedId);
                if (selected < 0) selected = folders.Items.Cast<Choice>().ToList()
                    .FindIndex(x => x.Id == store.DefaultFavoriteFolderId);
                folders.SelectedIndex = Math.Max(0, selected);
            }
            finally { refreshing = false; }
            ShowFolder();
        }
        private void ShowFolder()
        {
            if (words == null || refreshing) return;
            FavoriteFolder folder = store.GetFavoriteFolder(SelectedId); if (folder == null) return;
            folderName.Text = folder.name;
            string key = words.SelectedItems.Count == 0 ? null : DataLoader.WordKey((WordEntry)words.SelectedItems[0].Tag);
            IEnumerable<WordEntry> source = DataLoader.MergeWordEntries(vocabulary.Concat(store.Records.Select(x => x.word)));
            if (onlyCurrent.Checked) source = source.Where(x => store.FavoriteFolderIds(x).Contains(folder.id));
            source = source.Where(x => NotebookManagerForm.MatchesSearch(x, search.Text, false))
                .OrderBy(GameEngine.CleanEnglish, StringComparer.CurrentCultureIgnoreCase);
            words.BeginUpdate();
            try
            {
                words.Items.Clear();
                foreach (WordEntry word in source)
                {
                    ListViewItem row = new ListViewItem(GameEngine.CleanEnglish(word)) { Tag = word };
                    row.SubItems.Add(PartOfSpeech.DisplayChinese(word)); row.SubItems.Add(store.FavoriteFolderNames(word));
                    row.ToolTipText = row.Text + "\n" + PartOfSpeech.DisplayChinese(word) + "\n" + store.FavoriteFolderNames(word);
                    words.Items.Add(row); if (DataLoader.WordKey(word) == key) row.Selected = true;
                }
            }
            finally { words.EndUpdate(); }
            delete.Enabled = store.FavoriteFolders.Count > 1;
            setDefault.Enabled = folder.id != store.DefaultFavoriteFolderId;
            summary.Text = folder.name + "：共 " + store.GetFavoriteWords(folder.id).Count + " 词；当前显示 " + words.Items.Count + " 词";
            ShowSelection();
        }
        private void ShowSelection()
        {
            if (details == null) return;
            if (words.SelectedItems.Count == 0) { details.Clear(); return; }
            WordEntry word = (WordEntry)words.SelectedItems[0].Tag;
            details.Text = GameEngine.CleanEnglish(word) + "\r\n" + PartOfSpeech.DisplayChinese(word)
                + "\r\n" + ExampleCloze.PlainText(word.examples ?? string.Empty)
                + "\r\n收藏归属：" + store.FavoriteFolderNames(word);
        }
        private void EditFolder(bool create)
        {
            try
            {
                if (create) { FavoriteFolder folder = store.CreateFavoriteFolder(folderName.Text); RefreshFolders(folder.id); }
                else store.RenameFavoriteFolder(SelectedId, folderName.Text);
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, "收藏夹未更改"); }
        }
        private void DeleteFolder()
        {
            FavoriteFolder folder = store.GetFavoriteFolder(SelectedId); if (folder == null) return;
            if (MessageBox.Show(this, "删除“" + folder.name + "”？仅移除这个收藏夹及其归属，"
                + "不会删除单词、其他收藏夹归属或学习进度。", "删除收藏夹", MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes) return;
            store.DeleteFavoriteFolder(folder.id);
        }
        private void SetMembership(bool include)
        {
            List<WordEntry> selected = words.SelectedItems.Cast<ListViewItem>().Select(x => (WordEntry)x.Tag).ToList();
            string id = SelectedId;
            foreach (WordEntry word in selected) store.SetFavoriteMembership(word, id, include);
        }
        private void EditMembership()
        {
            if (words.SelectedItems.Count != 1) { MessageBox.Show(this, "请先选择一个单词。", "收藏归属"); return; }
            FavoriteUi.EditWord(this, store, (WordEntry)words.SelectedItems[0].Tag);
        }
        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (keyData == favoriteKey)
            {
                foreach (WordEntry word in words.SelectedItems.Cast<ListViewItem>().Select(x => (WordEntry)x.Tag).ToList())
                    store.ToggleFavorite(word);
                return true;
            }
            return base.ProcessCmdKey(ref message, keyData);
        }
    }
}
