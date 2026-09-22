using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace EnglishDictationTool
{
    internal sealed class StudySettingsForm : Form
    {
        private readonly StudyStore store;
        private readonly NotebookStore notebooks;
        private readonly DataLoader loader;
        private readonly BackupService backups;
        private readonly AppearanceStore appearance;
        private readonly PracticeStore practice;
        private readonly PronunciationStore pronunciation;
        private readonly Panel freePracticePanel;
        private TabControl tabs;
        private readonly StudySettings draft;
        private readonly PronunciationSettings pronunciationDraft;
        private readonly Dictionary<string, NumericUpDown> quotas = new Dictionary<string, NumericUpDown>();
        private NumericUpDown a, b, c, exampleTarget, dictationTarget, spellingTarget,
            undoLimit, backupMinutes, backupKeep;
        private CheckBox random, fuzzy, newExample, newDictation, newSpelling,
            listExample, listDictation, listSpelling, problemExample, problemDictation,
            problemSpelling, freeExample, freeDictation, freeSpelling, overlap, dueOnly,
            carryCount, carryPreview, exampleFirstLetter, dictationMeaningHint,
            reviewFirstLetter, backupBeforeEnd, timerEnabled, updateOnStartup;
        private TextBox days, previewKey, exampleHintKey, previousPageKey, nextPageKey,
            masteryKey, undoKey, backupKey, pauseKey;
        private ComboBox newQuestionOrder, listQuestionOrder, problemQuestionOrder,
            freeQuestionOrder, timerPrecision;
        private ListBox newTaskOrder, listTaskOrder, problemTaskOrder, freeTaskOrder;
        private TextBox speechReplayKey;
        private CheckBox speechEnabled, speechAutomatic;
        private ComboBox speechVoice;
        private NumericUpDown speechRate;
        private TrackBar speechVolume;
        private Label speechVolumeValue;
        private WordPronouncer speechPreview;
        private ListView backupList;
        private TextBox backupPath;
        private ComboBox managedBook;
        private TextBox renamedBook, importPath, importBook, importUnit;
        private Label managedBookSummary;

        public StudySettingsForm(StudyStore study, NotebookStore notebookStore,
            DataLoader dataLoader, BackupService backupService, AppearanceStore appearanceStore,
            Panel freeSettingsPanel = null)
        {
            ModernUI.ApplyAppIcon(this);
            store = study; notebooks = notebookStore; loader = dataLoader; backups = backupService;
            appearance = appearanceStore;
            practice = new PracticeStore(AppPaths.FindProjectRoot());
            freePracticePanel = freeSettingsPanel;
            draft = new JavaScriptSerializer().Deserialize<StudySettings>(
                new JavaScriptSerializer().Serialize(store.Settings));
            pronunciation = new PronunciationStore(AppPaths.FindProjectRoot());
            pronunciationDraft = new JavaScriptSerializer().Deserialize<PronunciationSettings>(
                new JavaScriptSerializer().Serialize(pronunciation.Settings));
            if (draft.defaultBookCounts == null) draft.defaultBookCounts = new Dictionary<string, int>();
            Text = "大英默写器 · 设置";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = ModernUI.FitWindow(1450, 900);
            MinimumSize = new Size(1080, 690);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = Theme.UiFont;

            tabs = new ModernTabControl { Dock = DockStyle.Fill };
            Controls.Add(tabs);
            if (freePracticePanel != null)
            {
                TabPage freePage = new TabPage("自由练习")
                { BackColor = Theme.Background, ForeColor = Theme.Text };
                tabs.TabPages.Add(freePage);
                ModernCard freeCard = new ModernCard { Dock = DockStyle.Fill,
                    Margin = new Padding(14), Padding = new Padding(12) };
                freePage.Controls.Add(freeCard);
                freePracticePanel.Dock = DockStyle.Fill;
                freeCard.Controls.Add(freePracticePanel);
            }
            BuildPlanTab(tabs);
            BuildLibraryTab(tabs);
            BuildAnswerTab(tabs);
            BuildShortcutTab(tabs);
            BuildPronunciationTab(tabs);
            BuildUpdateTab(tabs);
            BuildAppearanceTab(tabs);
            BuildCalendarTab(tabs);
            BuildBackupTab(tabs);
            Theme.Apply(this);
        }

        internal void SelectTab(string title)
        {
            TabPage page = tabs.TabPages.Cast<TabPage>().FirstOrDefault(x => x.Text.Contains(title));
            if (page != null) tabs.SelectedTab = page;
        }

        public void DetachFreeSettings()
        {
            if (freePracticePanel != null && freePracticePanel.Parent != null)
                freePracticePanel.Parent.Controls.Remove(freePracticePanel);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (speechPreview != null) { speechPreview.Dispose(); speechPreview = null; }
            DetachFreeSettings();
            base.OnFormClosing(e);
        }

        private static FlowLayoutPanel Page(TabControl tabs, string title)
        {
            TabPage page = new TabPage(title) { BackColor = Theme.Background, ForeColor = Theme.Text };
            tabs.TabPages.Add(page);
            ModernCard card = new ModernCard { Dock = DockStyle.Fill,
                Margin = new Padding(14), Padding = new Padding(12) };
            page.Controls.Add(card);
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true,
                BackColor = ModernUI.Card, Padding = new Padding(15) };
            card.Controls.Add(flow);
            return flow;
        }

        private static Label Label(string text, int width)
        {
            int measured = TextRenderer.MeasureText(text ?? string.Empty, Theme.UiFont,
                new Size(width, int.MaxValue), TextFormatFlags.WordBreak).Height;
            return new Label { Text = text, Width = width, Height = Math.Max(36, measured + 8),
                BackColor = Theme.Background, ForeColor = Theme.Text,
                TextAlign = ContentAlignment.MiddleLeft };
        }

        private static Panel Row(string label, Control input, int labelWidth)
        {
            labelWidth = Math.Max(420, labelWidth);
            Label title = Label(label, labelWidth);
            Panel row = new Panel { Width = Math.Max(900, labelWidth + input.Width + 20),
                Height = Math.Max(title.Height + 8, input.Height + 12), BackColor = Theme.Background };
            title.Location = new Point(0, 4);
            input.Location = new Point(labelWidth + 8, Math.Max(4, (row.Height - input.Height) / 2));
            row.Controls.Add(title);
            row.Controls.Add(input);
            return row;
        }

        private static NumericUpDown Number(int value, int min, int max)
        {
            return new NumericUpDown { Width = 90, Minimum = min, Maximum = max,
                Value = Math.Min(max, Math.Max(min, value)), BackColor = Theme.Surface, ForeColor = Theme.Text };
        }

        private static CheckBox Check(string text, bool value)
        {
            int measured = TextRenderer.MeasureText(text ?? string.Empty, Theme.UiFont,
                new Size(760, int.MaxValue), TextFormatFlags.WordBreak).Height;
            return new CheckBox { Text = text, Checked = value, Width = 800,
                Height = Math.Max(38, measured + 9),
                BackColor = Theme.Background, ForeColor = Theme.Text };
        }

        private static Button Button(string text, EventHandler click)
        {
            Button button = new ModernButton { Text = text, Width = 175, Height = 40 };
            button.Click += click;
            return button;
        }

        private void BuildPlanTab(TabControl tabs)
        {
            FlowLayoutPanel page = Page(tabs, "每日计划");
            page.Controls.Add(Label("三个部分可分别进入；完成后再次进入会抽取下一批。", 800));
            a = Number(draft.newCount, 0, 10000);
            b = Number(draft.listCount, 0, 10000);
            c = Number(draft.problemCount, 0, 10000);
            page.Controls.Add(Row("每日新学词数（建议 20）", a, 320));
            page.Controls.Add(Row("历史列表复习数量（建议 4）", b, 320));
            page.Controls.Add(Row("错题与易错词复习数量（建议 15）", c, 320));
            random = Check("随机跨单元抽取（关闭时按书本和单元顺序）", draft.randomExtraction);
            overlap = Check("同一词允许当天同时进入列表复习与错题复习", draft.allowOverlap);
            carryCount = Check("跨日优先词计入每日新学词数", draft.carryOverCountsInNewCount);
            carryPreview = Check("跨日优先词重新展示单词、例句和释义", draft.carryOverPreview);
            page.Controls.Add(random); page.Controls.Add(overlap);
            page.Controls.Add(carryCount); page.Controls.Add(carryPreview);
            page.Controls.Add(Label("默认按书本配额（抽取前仍可逐次调整；旁边显示未提取余量）：", 800));
            Dictionary<string, int> available = store.AvailableCounts();
            foreach (string book in loader.GetAvailableBooks())
            {
                int current = draft.defaultBookCounts.ContainsKey(book) ? draft.defaultBookCounts[book] : 0;
                NumericUpDown count = Number(current, 0, 10000);
                quotas.Add(book, count);
                page.Controls.Add(Row(book + " · 可抽 " + (available.ContainsKey(book) ? available[book] : 0), count, 320));
            }
            page.Controls.Add(Button("保存设置", delegate { SaveSettings(); }));
        }

        private void BuildAnswerTab(TabControl tabs)
        {
            FlowLayoutPanel page = Page(tabs, "答题与归档");
            page.Controls.Add(Label("例句填空、听写、普通拼写可以同时开启；每个学习部分可分别调整题型先后和单词顺序。", 1000));
            newExample = Check("新学：例句填空", draft.newExample);
            newDictation = Check("新学：听写", draft.newDictation);
            newSpelling = Check("新学：普通拼写", draft.newSpelling);
            listExample = Check("历史列表复习：例句填空", draft.listExample);
            listDictation = Check("历史列表复习：听写", draft.listDictation);
            listSpelling = Check("历史列表复习：普通拼写", draft.listSpelling);
            problemExample = Check("错题/易错词复习：例句填空", draft.problemExample);
            problemDictation = Check("错题/易错词复习：听写", draft.problemDictation);
            problemSpelling = Check("错题/易错词复习：普通拼写", draft.problemSpelling);
            freeExample = Check("自由练习：例句填空", draft.freeExample);
            freeDictation = Check("自由练习：听写", draft.freeDictation);
            freeSpelling = Check("自由练习：普通拼写", draft.freeSpelling);
            foreach (CheckBox item in new[] { newExample, newDictation, newSpelling,
                listExample, listDictation, listSpelling, problemExample, problemDictation,
                problemSpelling, freeExample, freeDictation, freeSpelling }) page.Controls.Add(item);

            newQuestionOrder = QuestionOrderCombo(draft.newQuestionOrder);
            listQuestionOrder = QuestionOrderCombo(draft.listQuestionOrder);
            problemQuestionOrder = QuestionOrderCombo(draft.problemQuestionOrder);
            freeQuestionOrder = QuestionOrderCombo(draft.freeQuestionOrder);
            page.Controls.Add(Row("新学单词顺序", newQuestionOrder, 320));
            page.Controls.Add(Row("历史列表复习单词顺序", listQuestionOrder, 320));
            page.Controls.Add(Row("错题复习单词顺序", problemQuestionOrder, 320));
            page.Controls.Add(Row("自由练习单词顺序", freeQuestionOrder, 320));
            newTaskOrder = AddTaskOrderEditor(page, "新学题型顺序", draft.newTaskOrder);
            listTaskOrder = AddTaskOrderEditor(page, "历史列表复习题型顺序", draft.listTaskOrder);
            problemTaskOrder = AddTaskOrderEditor(page, "错题复习题型顺序", draft.problemTaskOrder);
            freeTaskOrder = AddTaskOrderEditor(page, "自由练习题型顺序", draft.freeTaskOrder);
            exampleFirstLetter = Check("所有例句填空启用分阶段首字母提示（默认开启）",
                draft.exampleFirstLetterHints);
            page.Controls.Add(exampleFirstLetter);
            page.Controls.Add(Label("例句题开始时不会直接显示提示；按提示键后显示首字母，再按一次显示中文，最后提交。关闭首字母后，第一次按键直接显示中文。", 1000));
            dictationMeaningHint = Check("听写输入框为空时，按提示键显示中文释义",
                draft.dictationMeaningHint);
            page.Controls.Add(dictationMeaningHint);
            exampleTarget = Number(draft.exampleCorrectTarget, 0, 99);
            dictationTarget = Number(draft.dictationCorrectTarget, 0, 99);
            spellingTarget = Number(draft.spellingCorrectTarget, 0, 99);
            page.Controls.Add(Row("错题转易错：例句首次答对次数", exampleTarget, 320));
            page.Controls.Add(Row("错题转易错：听写首次答对次数", dictationTarget, 320));
            page.Controls.Add(Row("错题转易错：拼写首次答对次数", spellingTarget, 320));
            page.Controls.Add(Label("三个门槛需要同时满足；复现答对不计次数。单词仍只能通过手动“斩”进入已掌握。", 1000));
            fuzzy = Check("模糊作答：合并词库原文、斜线变体与手动可接受答案", draft.fuzzyAnswers);
            reviewFirstLetter = Check("自由错题拼写直接显示首字母", notebooks.ReviewFirstLetter);
            dueOnly = Check("仅从达到下方间隔天数的错题/易错词中抽取（默认关闭）", draft.reviewDueOnly);
            page.Controls.Add(fuzzy); page.Controls.Add(reviewFirstLetter); page.Controls.Add(dueOnly);
            days = new TextBox { Width = 400, Text = string.Join(",", draft.reviewDays ?? new[] { 1, 3, 7, 14 }) };
            page.Controls.Add(Row("复习间隔天数（逗号分隔）", days, 320));
            page.Controls.Add(Button("管理可接受答案", delegate { new AcceptedAnswersForm(store, loader).ShowDialog(this); }));
            page.Controls.Add(Button("管理全部单词本", delegate { new NotebookManagerForm(loader, notebooks).ShowDialog(this); }));
            page.Controls.Add(Button("保存设置", delegate { SaveSettings(); }));
        }

        private static ComboBox QuestionOrderCombo(string value)
        {
            ComboBox combo = new DarkComboBox { Width = 300 };
            combo.Items.AddRange(new object[] { "顺序", "单元内随机", "词书内随机" });
            combo.SelectedIndex = value == "unit_random" ? 1 : value == "book_random" ? 2 : 0;
            return combo;
        }

        private static string QuestionOrderValue(ComboBox combo)
        {
            return combo.SelectedIndex == 1 ? "unit_random" : combo.SelectedIndex == 2
                ? "book_random" : "sequential";
        }

        private static string ModeDisplay(string mode)
        {
            return mode == "example" ? "例句填空" : mode == "dictation" ? "听写" : "普通拼写";
        }

        private static string ModeValue(string display)
        {
            return display == "例句填空" ? "example" : display == "听写" ? "dictation" : "spelling";
        }

        private static ListBox AddTaskOrderEditor(FlowLayoutPanel page, string title,
            IEnumerable<string> order)
        {
            Panel panel = new Panel { Width = 1040, Height = 150, BackColor = Theme.Background };
            Label caption = Label(title + "（选中后上移/下移）", 360);
            caption.Location = new Point(0, 4);
            ListBox list = new ListBox { Location = new Point(370, 4), Width = 300, Height = 132,
                BackColor = Theme.Surface, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
            foreach (string mode in order ?? new[] { "example", "dictation", "spelling" })
                list.Items.Add(ModeDisplay(mode));
            foreach (string mode in new[] { "example", "dictation", "spelling" })
                if (!list.Items.Contains(ModeDisplay(mode))) list.Items.Add(ModeDisplay(mode));
            Button up = Button("上移", delegate { MoveSelected(list, -1); });
            Button down = Button("下移", delegate { MoveSelected(list, 1); });
            up.Width = down.Width = 120;
            up.Location = new Point(690, 16); down.Location = new Point(690, 72);
            panel.Controls.Add(caption); panel.Controls.Add(list); panel.Controls.Add(up); panel.Controls.Add(down);
            page.Controls.Add(panel);
            return list;
        }

        private static void MoveSelected(ListBox list, int delta)
        {
            int current = list.SelectedIndex;
            int target = current + delta;
            if (current < 0 || target < 0 || target >= list.Items.Count) return;
            object item = list.Items[current];
            list.Items.RemoveAt(current);
            list.Items.Insert(target, item);
            list.SelectedIndex = target;
        }

        private static List<string> TaskOrderValues(ListBox list)
        {
            return list.Items.Cast<object>().Select(x => ModeValue(x.ToString())).ToList();
        }

        private void BuildLibraryTab(TabControl tabs)
        {
            FlowLayoutPanel page = Page(tabs, "词书管理");
            page.Controls.Add(Label("可以把外部 CSV 导入现有词书或新建词书，也可以重命名整个词书。导入文件必须包含 english、chinese 两列，可另加 examples 和 part_of_speech（或 pos）列。", 1000));
            managedBook = new DarkComboBox { Width = 360 };
            managedBook.SelectedIndexChanged += delegate
            {
                string selected = managedBook.SelectedItem as string ?? string.Empty;
                renamedBook.Text = selected;
                importBook.Text = selected;
                RefreshManagedBookSummary();
            };
            page.Controls.Add(Row("当前词书", managedBook, 320));
            managedBookSummary = Label(string.Empty, 1000);
            page.Controls.Add(managedBookSummary);
            renamedBook = new TextBox { Width = 360 };
            page.Controls.Add(Row("重命名为", renamedBook, 320));
            page.Controls.Add(Button("重命名词书", delegate { RenameSelectedBook(); }));
            page.Controls.Add(Label("导入外部 CSV", 1000));
            importPath = new TextBox { Width = 620, ReadOnly = true };
            page.Controls.Add(Row("所选文件", importPath, 320));
            page.Controls.Add(Button("选择 CSV 文件", delegate { ChooseImportCsv(); }));
            importBook = new TextBox { Width = 360 };
            importUnit = new TextBox { Width = 360 };
            page.Controls.Add(Row("导入到词书（可输入新名称）", importBook, 320));
            page.Controls.Add(Row("单元名称", importUnit, 320));
            page.Controls.Add(Button("导入 CSV", delegate { ImportCsv(); }));
            RefreshManagedBooks(null);
        }

        private void RefreshManagedBooks(string select)
        {
            List<string> books = loader.GetAvailableBooks();
            managedBook.Items.Clear();
            managedBook.Items.AddRange(books.Cast<object>().ToArray());
            if (!string.IsNullOrWhiteSpace(select))
            {
                int index = books.FindIndex(x => string.Equals(x, select, StringComparison.OrdinalIgnoreCase));
                if (index >= 0) managedBook.SelectedIndex = index;
            }
            if (managedBook.SelectedIndex < 0 && managedBook.Items.Count > 0) managedBook.SelectedIndex = 0;
            RefreshManagedBookSummary();
        }

        private void RefreshManagedBookSummary()
        {
            string book = managedBook == null ? null : managedBook.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(book))
            {
                if (managedBookSummary != null) managedBookSummary.Text = "当前没有可用词书。可以在下方导入第一个 CSV。";
                return;
            }
            List<string> units = loader.GetUnitsForBook(book);
            int words = loader.LoadWordList(book, units).Count;
            managedBookSummary.Text = book + " · " + units.Count + " 个单元 · " + words + " 个词条";
        }

        private void ChooseImportCsv()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择要导入的词书 CSV";
                dialog.Filter = "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*";
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    int count = loader.ValidateCsvFile(dialog.FileName);
                    importPath.Text = dialog.FileName;
                    importUnit.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
                    MessageBox.Show(this, "文件格式有效，共检测到 " + count + " 个词条。", "CSV 检查完成");
                }
                catch (Exception error) { MessageBox.Show(this, error.Message, "无法导入 CSV"); }
            }
        }

        private void ImportCsv()
        {
            try
            {
                string book = DataLoader.ValidateDataName(importBook.Text, "词书名称");
                string unit = DataLoader.ValidateDataName(importUnit.Text, "单元名称");
                loader.ValidateCsvFile(importPath.Text);
                string destination = Path.Combine(loader.DataDirectory, book, unit + ".csv");
                bool overwrite = File.Exists(destination);
                if (overwrite && MessageBox.Show(this, "目标单元已经存在，是否覆盖？\n" + book + " / " + unit,
                    "确认覆盖", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                backups.Create(true, store.Settings.autoBackupKeep);
                int count = loader.ValidateCsvFile(importPath.Text);
                loader.ImportCsv(importPath.Text, book, unit, overwrite);
                RefreshManagedBooks(book);
                MessageBox.Show(this, "导入完成，共 " + count + " 个词条。重新打开设置后，每日配额中会显示新词书。", "导入完成");
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, "导入失败"); }
        }

        private void RenameSelectedBook()
        {
            string oldName = managedBook.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(oldName)) return;
            string newName;
            try { newName = DataLoader.ValidateDataName(renamedBook.Text, "新词书名称"); }
            catch (Exception error) { MessageBox.Show(this, error.Message, "名称无效"); return; }
            if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase)) return;
            if (MessageBox.Show(this, "确定将词书“" + oldName + "”重命名为“" + newName + "”吗？",
                "重命名词书", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                backups.Create(true, store.Settings.autoBackupKeep);
                loader.RenameBook(oldName, newName);
                try { store.RenameBookReferences(oldName, newName); }
                catch
                {
                    loader.RenameBook(newName, oldName);
                    throw;
                }
                RefreshManagedBooks(newName);
                MessageBox.Show(this, "词书已重命名。", "完成");
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, "重命名失败"); }
        }

        private TextBox Shortcut(int key, bool allowPlain)
        {
            TextBox capture = new TextBox { ReadOnly = true, Width = 240,
                Text = new KeysConverter().ConvertToString((Keys)key) };
            capture.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                e.SuppressKeyPress = true;
                if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey
                    || e.KeyCode == Keys.Menu || e.KeyCode == Keys.Escape) return;
                if (!allowPlain && (e.KeyData & Keys.Modifiers) == Keys.None)
                {
                    MessageBox.Show(this, "此快捷键需包含 Ctrl、Alt 或 Shift，以免误触发。", "快捷键");
                    return;
                }
                capture.Tag = (int)e.KeyData;
                capture.Text = new KeysConverter().ConvertToString(e.KeyData);
            };
            capture.Tag = key;
            return capture;
        }

        private void BuildShortcutTab(TabControl tabs)
        {
            FlowLayoutPanel page = Page(tabs, "快捷键");
            page.Controls.Add(Label("点击快捷键框后直接按键。冲突的组合不允许保存。", 800));
            previewKey = Shortcut(draft.previewKey, true);
            exampleHintKey = Shortcut(draft.exampleHintKey, true);
            previousPageKey = Shortcut(draft.previousPageKey, false);
            nextPageKey = Shortcut(draft.nextPageKey, false);
            masteryKey = Shortcut((int)notebooks.MasteryShortcut, false);
            undoKey = Shortcut(draft.undoKey, false);
            backupKey = Shortcut(draft.manualBackupKey, false);
            pauseKey = Shortcut(draft.pauseKey, false);
            page.Controls.Add(Row("展示下一层", previewKey, 320));
            page.Controls.Add(Row("例句提示 / 提交（默认 Enter）", exampleHintKey, 320));
            page.Controls.Add(Row("翻到上一页（默认 Shift+Q）", previousPageKey, 320));
            page.Controls.Add(Row("翻到下一页（默认 Shift+E）", nextPageKey, 320));
            page.Controls.Add(Row("斩当前词", masteryKey, 320));
            page.Controls.Add(Row("撤销上一个词", undoKey, 320));
            page.Controls.Add(Row("手动备份", backupKey, 320));
            page.Controls.Add(Row("暂停 / 继续（默认 Ctrl+Shift+P）", pauseKey, 320));
            undoLimit = Number(draft.undoLimit, 0, 5);
            backupMinutes = Number(draft.autoBackupMinutes, 1, 1440);
            backupKeep = Number(draft.autoBackupKeep, 1, 100);
            page.Controls.Add(Row("保留撤销步数（最多 5）", undoLimit, 320));
            page.Controls.Add(Row("自动备份间隔 · 分钟（默认 10）", backupMinutes, 320));
            page.Controls.Add(Row("自动备份最多保留份数（默认 6）", backupKeep, 320));
            backupBeforeEnd = Check("手动结束列表前自动创建一份手动备份（默认开启）",
                draft.backupBeforeManualEnd);
            timerEnabled = Check("学习页面显示并记录计时器（默认开启）", draft.timerEnabled);
            timerPrecision = new DarkComboBox { Width = 300 };
            timerPrecision.Items.AddRange(new object[] { "精确到分钟", "速通计时 · 毫秒" });
            timerPrecision.SelectedIndex = draft.timerPrecision == "millisecond" ? 1 : 0;
            page.Controls.Add(backupBeforeEnd);
            page.Controls.Add(timerEnabled);
            page.Controls.Add(Row("计时器显示精度", timerPrecision, 320));
            page.Controls.Add(Label("暂停、切换窗口、失去焦点或退出学习页面时不计时；重新进入同一模块后继续累计。统计看板统一显示到分钟。", 1000));
            page.Controls.Add(Button("保存设置", delegate { SaveSettings(); }));
        }

        private void BuildPronunciationTab(TabControl tabs)
        {
            FlowLayoutPanel page = Page(tabs, "单词朗读");
            page.Controls.Add(Label("使用本机英语语音离线朗读；展示阶段可自动朗读，听写题只播放语音、不显示英文。", 1000));
            speechEnabled = Check("启用单词朗读", pronunciationDraft.enabled);
            speechAutomatic = Check("展示新单词时自动朗读一次", pronunciationDraft.automatic);
            page.Controls.Add(speechEnabled);
            page.Controls.Add(speechAutomatic);
            speechVoice = new DarkComboBox { Width = 390 };
            speechVoice.Items.Add("自动选择英语语音");
            foreach (string name in WordPronouncer.GetEnglishVoices()) speechVoice.Items.Add(name);
            int selected = pronunciationDraft.voice == null ? -1
                : speechVoice.Items.IndexOf(pronunciationDraft.voice);
            speechVoice.SelectedIndex = selected < 1 ? 0 : selected;
            page.Controls.Add(Row("朗读音色", speechVoice, 420));
            if (speechVoice.Items.Count == 1)
                page.Controls.Add(Label("当前电脑没有可用的英语语音。安装系统英语语音后，此功能会自动可用。", 1000));
            speechRate = Number(pronunciationDraft.rate, -10, 10);
            page.Controls.Add(Row("语速（-10 最慢，10 最快）", speechRate, 420));
            speechVolume = new TrackBar { Minimum = 0, Maximum = 100,
                Value = Math.Min(100, Math.Max(0, pronunciationDraft.volume)),
                Width = 350, Height = 38, AutoSize = false, TickStyle = TickStyle.None,
                SmallChange = 1, LargeChange = 10, BackColor = Theme.Background };
            speechVolumeValue = new Label { Width = 70, Height = 38,
                TextAlign = ContentAlignment.MiddleLeft, BackColor = Theme.Background,
                ForeColor = Theme.Text };
            Panel volumeControl = new Panel { Width = 440, Height = 42,
                BackColor = Theme.Background };
            speechVolume.Location = new Point(0, 2);
            speechVolumeValue.Location = new Point(362, 1);
            volumeControl.Controls.Add(speechVolume);
            volumeControl.Controls.Add(speechVolumeValue);
            Action updateVolume = delegate { speechVolumeValue.Text = speechVolume.Value + "%"; };
            speechVolume.ValueChanged += delegate { updateVolume(); };
            updateVolume();
            page.Controls.Add(Row("学习音频总音量（朗读与后续提示音共用）", volumeControl, 420));
            speechReplayKey = Shortcut(pronunciationDraft.replayKey, false);
            page.Controls.Add(Row("展示阶段重播快捷键", speechReplayKey, 420));
            page.Controls.Add(Label("默认 Ctrl+R；也可点击学习窗口中的“重播单词”。回看已学单词时可手动重播。", 1000));
            page.Controls.Add(Button("试听 example", delegate { PreviewPronunciation(); }));
            page.Controls.Add(Button("保存设置", delegate { SaveSettings(); }));
        }

        private void BuildUpdateTab(TabControl tabs)
        {
            FlowLayoutPanel page = Page(tabs, "程序更新");
            page.Controls.Add(Label("程序从项目 GitHub Releases 获取稳定版本。自动安装前必须同时找到升级包 ZIP 和 SHA256SUMS.txt，并通过校验。", 1050));
            page.Controls.Add(Label("安装前会创建完整手动备份；更新只替换程序文件，不会覆盖 data、学习进度、单词本、外观、导入词书、备份或用户设置。", 1050));
            updateOnStartup = Check("每次启动程序时自动检查更新（网络失败时不打扰）",
                draft.checkUpdatesOnStartup);
            page.Controls.Add(updateOnStartup);
            Button check = Button("立即检查更新", delegate
            {
                new UpdateService(AppPaths.FindProjectRoot(), backups).CheckAsync(this, false);
            });
            check.Width = 220; check.Height = 48;
            page.Controls.Add(check);
            page.Controls.Add(Button("保存设置", delegate { SaveSettings(); }));
        }

        private void CollectPronunciation()
        {
            pronunciationDraft.enabled = speechEnabled.Checked;
            pronunciationDraft.automatic = speechAutomatic.Checked;
            pronunciationDraft.voice = speechVoice.SelectedIndex <= 0 ? string.Empty
                : speechVoice.SelectedItem.ToString();
            pronunciationDraft.rate = (int)speechRate.Value;
            pronunciationDraft.volume = (int)speechVolume.Value;
            pronunciationDraft.replayKey = (int)speechReplayKey.Tag;
        }

        private void PreviewPronunciation()
        {
            CollectPronunciation();
            if (speechPreview != null) speechPreview.Dispose();
            speechPreview = new WordPronouncer(pronunciationDraft);
            string error;
            if (!speechPreview.Speak("example", out error))
                MessageBox.Show(this, error ?? "请先启用单词朗读。", "朗读试听");
        }

        private void SaveSettings()
        {
            try
            {
                int previewValue = (int)previewKey.Tag;
                int exampleValue = (int)exampleHintKey.Tag;
                int previousPageValue = (int)previousPageKey.Tag;
                int nextPageValue = (int)nextPageKey.Tag;
                int masteryValue = (int)masteryKey.Tag;
                int undoValue = (int)undoKey.Tag;
                int backupValue = (int)backupKey.Tag;
                int pauseValue = (int)pauseKey.Tag;
                int speechValue = (int)speechReplayKey.Tag;
                int[] exclusiveKeys = { previousPageValue, nextPageValue, masteryValue,
                    undoValue, backupValue, pauseValue, speechValue };
                if (exclusiveKeys.Distinct().Count() != exclusiveKeys.Length
                    || exclusiveKeys.Contains(previewValue) || exclusiveKeys.Contains(exampleValue))
                    throw new InvalidOperationException("快捷键有冲突，请为不同操作设置不同的按键。");
                int[] intervals = days.Text.Split(new[] { ',', '，', ';', '；' },
                    StringSplitOptions.RemoveEmptyEntries).Select(x => int.Parse(x.Trim())).ToArray();
                if (intervals.Length == 0 || intervals.Any(x => x < 0 || x > 3650))
                    throw new InvalidOperationException("复习间隔天数需为 0 到 3650 的整数。");
                draft.newCount = (int)a.Value; draft.listCount = (int)b.Value;
                draft.problemCount = (int)c.Value;
                draft.randomExtraction = random.Checked; draft.allowOverlap = overlap.Checked;
                draft.carryOverCountsInNewCount = carryCount.Checked;
                draft.carryOverPreview = carryPreview.Checked;
                draft.newExample = newExample.Checked; draft.newDictation = newDictation.Checked;
                draft.newSpelling = newSpelling.Checked;
                draft.listExample = listExample.Checked; draft.listDictation = listDictation.Checked;
                draft.listSpelling = listSpelling.Checked;
                draft.problemExample = problemExample.Checked;
                draft.problemDictation = problemDictation.Checked;
                draft.problemSpelling = problemSpelling.Checked;
                draft.freeExample = freeExample.Checked; draft.freeDictation = freeDictation.Checked;
                draft.freeSpelling = freeSpelling.Checked;
                draft.newQuestionOrder = QuestionOrderValue(newQuestionOrder);
                draft.listQuestionOrder = QuestionOrderValue(listQuestionOrder);
                draft.problemQuestionOrder = QuestionOrderValue(problemQuestionOrder);
                draft.freeQuestionOrder = QuestionOrderValue(freeQuestionOrder);
                draft.newTaskOrder = TaskOrderValues(newTaskOrder);
                draft.listTaskOrder = TaskOrderValues(listTaskOrder);
                draft.problemTaskOrder = TaskOrderValues(problemTaskOrder);
                draft.freeTaskOrder = TaskOrderValues(freeTaskOrder);
                draft.exampleFirstLetterHints = exampleFirstLetter.Checked;
                draft.dictationMeaningHint = dictationMeaningHint.Checked;
                draft.exampleCorrectTarget = (int)exampleTarget.Value;
                draft.dictationCorrectTarget = (int)dictationTarget.Value;
                draft.spellingCorrectTarget = (int)spellingTarget.Value;
                draft.fuzzyAnswers = fuzzy.Checked; draft.reviewDays = intervals;
                draft.reviewDueOnly = dueOnly.Checked;
                draft.previewKey = previewValue; draft.exampleHintKey = exampleValue;
                draft.previousPageKey = previousPageValue;
                draft.nextPageKey = nextPageValue;
                draft.undoKey = undoValue; draft.manualBackupKey = backupValue;
                draft.pauseKey = pauseValue;
                draft.undoLimit = (int)undoLimit.Value;
                draft.autoBackupMinutes = (int)backupMinutes.Value;
                draft.autoBackupKeep = (int)backupKeep.Value;
                draft.backupBeforeManualEnd = backupBeforeEnd.Checked;
                draft.timerEnabled = timerEnabled.Checked;
                draft.timerPrecision = timerPrecision.SelectedIndex == 1 ? "millisecond" : "minute";
                draft.checkUpdatesOnStartup = updateOnStartup.Checked;
                draft.defaultBookCounts = quotas.ToDictionary(x => x.Key, x => (int)x.Value.Value);
                store.UpdateSettings(draft);
                notebooks.MasteryShortcut = (Keys)masteryValue;
                notebooks.ReviewFirstLetter = reviewFirstLetter.Checked;
                if (draft.spellingCorrectTarget > 0) notebooks.ReviewCorrectTarget = draft.spellingCorrectTarget;
                CollectPronunciation();
                pronunciation.Save(pronunciationDraft);
                MessageBox.Show(this, "设置已保存。", "每日学习");
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "设置未保存");
            }
        }

        private void BuildCalendarTab(TabControl tabs)
        {
            FlowLayoutPanel page = Page(tabs, "学习统计");
            page.Controls.Add(Label("统计看板已升级为独立大窗口，包含今日、近 7 天、近 30 天和累计数据，以及正确率、首次正确率、复现次数、有效学习时长和趋势。", 1050));
            page.Controls.Add(Label("大日历仍按凌晨 4:01 划分学习日；选择日期可查看三类学习数量，双击列表可直接打开对应记录。", 1050));
            Button open = Button("打开学习统计", delegate
            {
                using (StatisticsForm form = new StatisticsForm(store, practice, appearance))
                    form.ShowDialog(this);
            });
            open.Width = 240; open.Height = 50;
            page.Controls.Add(open);
        }

        private void BuildAppearanceTab(TabControl tabs)
        {
            FlowLayoutPanel page = Page(tabs, "外观");
            page.Controls.Add(Label("单词、例句、释义及三种反馈可分别设字体、字号和颜色。", 850));
            page.Controls.Add(Label("四类练习和日历的背景各自独立，可选纯色或导入图片。", 850));
            page.Controls.Add(Button("打开外观编辑器", delegate
            {
                using (AppearanceSettingsForm form = new AppearanceSettingsForm(appearance))
                    form.ShowDialog(this);
            }));
        }

        private void BuildBackupTab(TabControl tabs)
        {
            FlowLayoutPanel page = Page(tabs, "备份");
            page.Controls.Add(Label("手动备份独立存放、无数量限制；恢复前会再保存一份当前完整数据。", 800));
            backupList = new ListView { Width = 1100, Height = 430, View = View.Details,
                FullRowSelect = true, MultiSelect = false, BackColor = Theme.Surface,
                ForeColor = Theme.Text, ShowItemToolTips = true };
            backupList.Columns.Add("类型", 100); backupList.Columns.Add("创建时间", 230);
            backupList.Columns.Add("文件数", 95); backupList.Columns.Add("备份编号", 300);
            backupList.SelectedIndexChanged += delegate
            {
                BackupInfo selected = SelectedBackup();
                backupPath.Text = selected == null ? "选择一份备份以查看完整目录。" : selected.path;
            };
            page.Controls.Add(backupList);
            page.Controls.Add(Label("所选备份的完整目录", 1100));
            backupPath = new TextBox { Width = 1100, Height = 70, Multiline = true,
                ReadOnly = true, WordWrap = true, ScrollBars = ScrollBars.Vertical,
                Text = "选择一份备份以查看完整目录。" };
            page.Controls.Add(backupPath);
            FlowLayoutPanel actions = new FlowLayoutPanel { Width = 1100, Height = 58,
                BackColor = Theme.Background, WrapContents = false };
            actions.Controls.Add(Button("立即手动备份", delegate { CreateManualBackup(); }));
            actions.Controls.Add(Button("刷新列表", delegate { RefreshBackups(); }));
            actions.Controls.Add(Button("恢复所选备份", delegate { RestoreSelected(); }));
            actions.Controls.Add(Button("清理所选手动备份", delegate { DeleteSelected(); }));
            page.Controls.Add(actions);
            RefreshBackups();
        }

        private void RefreshBackups()
        {
            backupList.Items.Clear();
            foreach (BackupInfo info in backups.List("manual").Concat(backups.List("auto"))
                .OrderByDescending(x => x.createdAt))
            {
                ListViewItem row = new ListViewItem(info.kind == "manual" ? "手动" : "自动");
                row.SubItems.Add(info.createdAt.ToString("yyyy-MM-dd HH:mm:ss"));
                row.SubItems.Add(info.fileCount.ToString());
                row.SubItems.Add(Path.GetFileName(info.path));
                row.ToolTipText = info.path;
                row.Tag = info;
                backupList.Items.Add(row);
            }
            if (backupList.Items.Count > 0)
            {
                backupList.Items[0].Selected = true;
                backupPath.Text = ((BackupInfo)backupList.Items[0].Tag).path;
            }
        }

        private BackupInfo SelectedBackup()
        {
            return backupList.SelectedItems.Count == 0 ? null : backupList.SelectedItems[0].Tag as BackupInfo;
        }

        private void CreateManualBackup()
        {
            try
            {
                BackupInfo info = backups.Create(true, store.Settings.autoBackupKeep);
                RefreshBackups();
                MessageBox.Show(this, "手动备份已完成：\n" + info.path, "备份成功");
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, "备份失败"); }
        }

        private void RestoreSelected()
        {
            BackupInfo selected = SelectedBackup();
            if (selected == null) return;
            if (MessageBox.Show(this, "将关闭程序，并把所选备份恢复到项目；恢复前会另存当前状态。继续吗？",
                "确认恢复", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try { backups.BeginRestore(selected.path); }
            catch (Exception error) { MessageBox.Show(this, error.Message, "恢复失败"); }
        }

        private void DeleteSelected()
        {
            BackupInfo selected = SelectedBackup();
            if (selected == null) return;
            if (selected.kind != "manual")
            {
                MessageBox.Show(this, "只能在此手动清理手动备份。", "提示"); return;
            }
            if (MessageBox.Show(this, "确定删除这份手动备份？此操作无法从程序内撤销。",
                "清理备份", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try { backups.DeleteManual(selected.path); RefreshBackups(); }
            catch (Exception error) { MessageBox.Show(this, error.Message, "删除失败"); }
        }
    }

    internal sealed class StudyListViewerForm : Form
    {
        public StudyListViewerForm(StudyList list)
        {
            ModernUI.ApplyAppIcon(this);
            Text = "学习列表 " + list.id;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = ModernUI.FitWindow(1000, 650);
            MinimumSize = new Size(760, 500);
            TextBox text = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
                ScrollBars = ScrollBars.Vertical, BackColor = Theme.Background, ForeColor = Theme.Text };
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("创建：" + list.createdAt.ToString("yyyy-MM-dd HH:mm:ss"));
            builder.AppendLine("学习日：" + list.studyDate + "    状态：" + list.status);
            builder.AppendLine("来源列表：" + string.Join(", ", list.sourceListIds ?? new List<string>()));
            builder.AppendLine();
            foreach (StudyItem item in list.items)
                builder.AppendLine(GameEngine.CleanEnglish(item.word) + "  [" +
                    (item.mastered ? "已斩" : item.released ? "跨日返池" :
                    item.exampleComplete && item.spellingComplete ? "已完成" : "未完成") + "]");
            text.Text = builder.ToString();
            Controls.Add(text);
            Theme.Apply(this);
        }
    }

    internal sealed class AcceptedAnswersForm : Form
    {
        private readonly StudyStore store;
        private readonly List<WordEntry> all;
        private readonly ListBox words;
        private readonly TextBox search;
        private readonly TextBox aliases;

        public AcceptedAnswersForm(StudyStore study, DataLoader loader)
        {
            ModernUI.ApplyAppIcon(this);
            store = study;
            all = store.AllWords().Select(x => x.word).ToList();
            Text = "手动可接受答案";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = ModernUI.FitWindow(1180, 760);
            MinimumSize = new Size(900, 620);
            BackColor = Theme.Background; ForeColor = Theme.Text;
            search = new TextBox { Location = new Point(24, 24), Width = 400,
                Anchor = AnchorStyles.Top | AnchorStyles.Left };
            search.TextChanged += delegate { RefreshWords(); };
            Controls.Add(search);
            words = new ListBox { Location = new Point(24, 68), Width = 400, Height = 630,
                BackColor = Theme.Surface, ForeColor = Theme.Text, DisplayMember = "english",
                HorizontalScrollbar = true, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left };
            words.SelectedIndexChanged += delegate { ShowWord(); };
            Controls.Add(words);
            Label help = new Label { Text = "每行一个可接受答案；只有开启模糊作答时生效。",
                Location = new Point(455, 24), Width = 690, Height = 56,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(help);
            aliases = new TextBox { Location = new Point(455, 92), Width = 690, Height = 545,
                Multiline = true, ScrollBars = ScrollBars.Vertical,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(aliases);
            Controls.Add(new Label { Text = "单词本原文和斜线展开结果无需重复填写。",
                Location = new Point(455, 646), Width = 690, Height = 34,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right });
            Button save = Button("保存此词答案", delegate { Save(); });
            save.Location = new Point(945, 690);
            save.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            Controls.Add(save);
            Theme.Apply(this);
            RefreshWords();
        }

        private static Button Button(string text, EventHandler click)
        {
            Button button = new ModernButton { Text = text, Width = 200, Height = 44 };
            button.Click += click;
            return button;
        }

        private void RefreshWords()
        {
            words.Items.Clear();
            int widest = 0;
            foreach (WordEntry word in all.Where(x => x.english.IndexOf(search.Text,
                StringComparison.CurrentCultureIgnoreCase) >= 0))
            {
                words.Items.Add(word);
                widest = Math.Max(widest, TextRenderer.MeasureText(word.english ?? string.Empty, words.Font).Width);
            }
            words.HorizontalExtent = widest + 24;
        }

        private void ShowWord()
        {
            WordEntry word = words.SelectedItem as WordEntry;
            StudyWord record = word == null ? null : store.FindWord(word);
            aliases.Text = record == null || record.acceptedAnswers == null ? string.Empty :
                string.Join(Environment.NewLine, record.acceptedAnswers);
        }

        private void Save()
        {
            WordEntry word = words.SelectedItem as WordEntry;
            if (word == null) return;
            store.SetAcceptedAnswers(word, aliases.Lines);
            MessageBox.Show(this, "已保存 " + GameEngine.CleanEnglish(word) + " 的可接受答案。", "完成");
        }
    }
}
