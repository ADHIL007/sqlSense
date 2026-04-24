using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using ScintillaNET;
using ScintillaNET.WPF;
using sqlSense.ViewModels;

namespace sqlSense.UI
{
    public partial class SqlEditorPanel : UserControl
    {
        private MainViewModel? _viewModel;
        private bool _isUpdatingText = false;

        // ─── Keyword Sets ──────────────────────────────────────────────
        private const string SqlKeywords1 =
            "add all alter and any as asc authorization backup begin between break browse bulk by cascade case check checkpoint close clustered coalesce collate column commit compute constraint contains containstable continue convert create cross current current_date current_time current_timestamp current_user cursor database dbcc deallocate declare default delete deny desc disk distinct distributed double drop dump else end errlvl escape except exec execute exists exit external fetch file fillfactor for foreign freetext freetexttable from full function goto grant group having holdlock identity identitycol identity_insert if in index inner insert intersect into is join key kill left like lineno load merge national nocheck nonclustered not null nullif of off offsets on open opendatasource openquery openrowset openxml option or order outer over percent pivot plan precision primary print proc procedure public raiserror read readtext reconfigure references replication restore restrict return revert revoke right rollback rowcount rowguidcol rule save schema securityaudit select semantickeyphrasetable semanticsimilaritydetailstable semanticsimilaritytable session_user set setuser shutdown some statistics system_user table tablesample textsize then to top tran transaction trigger truncate try_convert tsequal union unique unpivot update updatetext use user values varying view waitfor when where while with within group writetext";

        private const string SqlKeywords2 =
            "abs ascii cast ceiling char charindex coalesce concat convert count datename datepart dateadd datediff day getdate getutcdate iif isnull isdate isnumeric left len lower ltrim max min month newid newsequentialid nullif object_id object_name parsename patindex replace right rtrim scope_identity sign space sqrt str stuff substring sum sysdatetime sysutcdatetime trim upper year avg count_big stdev stdevp var varp row_number rank dense_rank ntile lag lead first_value last_value percent_rank cume_dist";

        public SqlEditorPanel()
        {
            InitializeComponent();

            this.Loaded += SqlEditorPanel_Loaded;
            this.DataContextChanged += SqlEditorPanel_DataContextChanged;

            SqlEditor.UpdateUI += SqlEditor_UpdateUI;
            SqlEditor.CharAdded += SqlEditor_CharAdded;

            ConfigureScintilla();
        }

        private void ConfigureScintilla()
        {
            // ─── General ──────────────────────────────────────────────
            SqlEditor.WrapMode = WrapMode.None;
            SqlEditor.IndentationGuides = IndentView.LookBoth;
            SqlEditor.ScrollWidthTracking = true;
            SqlEditor.ScrollWidth = 1;
            SqlEditor.AutoCIgnoreCase = true;
            SqlEditor.AutoCDropRestOfWord = false;
            SqlEditor.CaretForeColor = System.Windows.Media.Color.FromRgb(174, 175, 173);
            SqlEditor.CaretWidth = 2;

            // ─── AutoComplete Popup Theming (via DirectMessage) ────────
            // SCI_AUTOCSETORDER - Custom ordering
            SqlEditor.AutoCOrder = Order.Custom;
            SqlEditor.AutoCSeparator = '|';
            SqlEditor.AutoCMaxHeight = 10;
            SqlEditor.AutoCMaxWidth = 50;

            // Background color (#252526) — SCI_AUTOCSETBACK=2205, FORE=2206, SELBG=2208, SELF=2209
            SqlEditor.DirectMessage(2205,
                new IntPtr(System.Drawing.ColorTranslator.ToWin32(System.Drawing.Color.FromArgb(37, 37, 38))),
                IntPtr.Zero);
            SqlEditor.DirectMessage(2206,
                new IntPtr(System.Drawing.ColorTranslator.ToWin32(System.Drawing.Color.FromArgb(212, 212, 212))),
                IntPtr.Zero);
            SqlEditor.DirectMessage(2208,
                new IntPtr(System.Drawing.ColorTranslator.ToWin32(System.Drawing.Color.FromArgb(9, 71, 113))),
                IntPtr.Zero);
            SqlEditor.DirectMessage(2209,
                new IntPtr(System.Drawing.ColorTranslator.ToWin32(System.Drawing.Color.White)),
                IntPtr.Zero);

            // ─── Margins ────────────────────────────────────────────
            // Line numbers
            SqlEditor.Margins[0].Width = 44;
            SqlEditor.Margins[0].Type = MarginType.Number;
            // Fold margin
            SqlEditor.Margins[1].Width = 0;
            SqlEditor.Margins[2].Type = MarginType.Symbol;
            SqlEditor.Margins[2].Mask = Marker.MaskFolders;
            SqlEditor.Margins[2].Sensitive = true;
            SqlEditor.Margins[2].Width = 18;

            // ─── Lexer ──────────────────────────────────────────────
            SqlEditor.Lexer = Lexer.Sql;

            // ─── Default Style ──────────────────────────────────────
            SqlEditor.Styles[ScintillaNET.Style.Default].Font = "Consolas";
            SqlEditor.Styles[ScintillaNET.Style.Default].Size = 13;
            SqlEditor.Styles[ScintillaNET.Style.Default].BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            SqlEditor.Styles[ScintillaNET.Style.Default].ForeColor = System.Drawing.Color.FromArgb(212, 212, 212);
            SqlEditor.StyleClearAll();

            // ─── SQL Syntax Colors (VS Code Dark+ palette) ───────────
            // 0 = Default / Identifier
            SqlEditor.Styles[0].ForeColor = System.Drawing.Color.FromArgb(212, 212, 212);
            SqlEditor.Styles[0].BackColor = System.Drawing.Color.FromArgb(30, 30, 30);

            // 1 = Block comment  /* … */
            SqlEditor.Styles[1].ForeColor = System.Drawing.Color.FromArgb(106, 153, 85);
            SqlEditor.Styles[1].BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            SqlEditor.Styles[1].Italic = true;

            // 2 = Line comment   -- …
            SqlEditor.Styles[2].ForeColor = System.Drawing.Color.FromArgb(106, 153, 85);
            SqlEditor.Styles[2].BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            SqlEditor.Styles[2].Italic = true;

            // 3 = Doc comment
            SqlEditor.Styles[3].ForeColor = System.Drawing.Color.FromArgb(106, 153, 85);
            SqlEditor.Styles[3].BackColor = System.Drawing.Color.FromArgb(30, 30, 30);

            // 4 = Number
            SqlEditor.Styles[4].ForeColor = System.Drawing.Color.FromArgb(181, 206, 168);
            SqlEditor.Styles[4].BackColor = System.Drawing.Color.FromArgb(30, 30, 30);

            // 5 = Keyword (SELECT, FROM, WHERE…)
            SqlEditor.Styles[5].ForeColor = System.Drawing.Color.FromArgb(86, 156, 214);
            SqlEditor.Styles[5].BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            SqlEditor.Styles[5].Bold = true;

            // 6 = String 'text'
            SqlEditor.Styles[6].ForeColor = System.Drawing.Color.FromArgb(206, 145, 120);
            SqlEditor.Styles[6].BackColor = System.Drawing.Color.FromArgb(30, 30, 30);

            // 7 = Character / Quoted identifier
            SqlEditor.Styles[7].ForeColor = System.Drawing.Color.FromArgb(206, 145, 120);
            SqlEditor.Styles[7].BackColor = System.Drawing.Color.FromArgb(30, 30, 30);

            // 8 = SQL Plus (unused but set)
            SqlEditor.Styles[8].ForeColor = System.Drawing.Color.FromArgb(212, 212, 212);
            SqlEditor.Styles[8].BackColor = System.Drawing.Color.FromArgb(30, 30, 30);

            // 9 = SQL Plus Prompt
            SqlEditor.Styles[9].ForeColor = System.Drawing.Color.FromArgb(212, 212, 212);
            SqlEditor.Styles[9].BackColor = System.Drawing.Color.FromArgb(30, 30, 30);

            // 10 = Operator  +  -  *  /  =  <  >  …
            SqlEditor.Styles[10].ForeColor = System.Drawing.Color.FromArgb(212, 212, 212);
            SqlEditor.Styles[10].BackColor = System.Drawing.Color.FromArgb(30, 30, 30);

            // 11 = Identifier
            SqlEditor.Styles[11].ForeColor = System.Drawing.Color.FromArgb(212, 212, 212);
            SqlEditor.Styles[11].BackColor = System.Drawing.Color.FromArgb(30, 30, 30);

            // 12 = Quoted identifier  [...]
            SqlEditor.Styles[12].ForeColor = System.Drawing.Color.FromArgb(156, 220, 254);
            SqlEditor.Styles[12].BackColor = System.Drawing.Color.FromArgb(30, 30, 30);

            // 13 = DB2 delimiter
            SqlEditor.Styles[13].ForeColor = System.Drawing.Color.FromArgb(212, 212, 212);
            SqlEditor.Styles[13].BackColor = System.Drawing.Color.FromArgb(30, 30, 30);

            // 14 = @variable
            SqlEditor.Styles[14].ForeColor = System.Drawing.Color.FromArgb(156, 220, 254);
            SqlEditor.Styles[14].BackColor = System.Drawing.Color.FromArgb(30, 30, 30);

            // 15 = #temp table
            SqlEditor.Styles[15].ForeColor = System.Drawing.Color.FromArgb(156, 220, 254);
            SqlEditor.Styles[15].BackColor = System.Drawing.Color.FromArgb(30, 30, 30);

            // 16 = Word2 (functions / builtins)
            SqlEditor.Styles[16].ForeColor = System.Drawing.Color.FromArgb(220, 220, 170);
            SqlEditor.Styles[16].BackColor = System.Drawing.Color.FromArgb(30, 30, 30);

            // ─── Line Number Margin Style ────────────────────────────
            SqlEditor.Styles[ScintillaNET.Style.LineNumber].BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            SqlEditor.Styles[ScintillaNET.Style.LineNumber].ForeColor = System.Drawing.Color.FromArgb(133, 133, 133);

            // ─── Fold Margin ─────────────────────────────────────────
            SqlEditor.SetFoldMarginColor(true, System.Windows.Media.Color.FromArgb(255, 30, 30, 30));
            SqlEditor.SetFoldMarginHighlightColor(true, System.Windows.Media.Color.FromArgb(255, 30, 30, 30));

            // ─── Fold Markers ────────────────────────────────────────
            SqlEditor.Markers[Marker.FolderEnd].Symbol = MarkerSymbol.BoxPlusConnected;
            SqlEditor.Markers[Marker.FolderOpenMid].Symbol = MarkerSymbol.BoxMinusConnected;
            SqlEditor.Markers[Marker.FolderMidTail].Symbol = MarkerSymbol.TCorner;
            SqlEditor.Markers[Marker.FolderTail].Symbol = MarkerSymbol.LCorner;
            SqlEditor.Markers[Marker.FolderSub].Symbol = MarkerSymbol.VLine;
            SqlEditor.Markers[Marker.Folder].Symbol = MarkerSymbol.BoxPlus;
            SqlEditor.Markers[Marker.FolderOpen].Symbol = MarkerSymbol.BoxMinus;

            for (int i = Marker.FolderEnd; i <= Marker.FolderOpen; i++)
            {
                SqlEditor.Markers[i].SetForeColor(System.Drawing.Color.FromArgb(30, 30, 30));
                SqlEditor.Markers[i].SetBackColor(System.Drawing.Color.FromArgb(100, 100, 100));
            }

            // ─── Folding ─────────────────────────────────────────────
            SqlEditor.SetProperty("fold", "1");
            SqlEditor.SetProperty("fold.compact", "0");
            SqlEditor.SetProperty("fold.comment", "1");
            SqlEditor.SetProperty("fold.sql.only.begin", "1");

            // ─── Indentation ─────────────────────────────────────────
            SqlEditor.IndentWidth = 4;
            SqlEditor.UseTabs = false;
            SqlEditor.TabWidth = 4;

            // ─── Selection ───────────────────────────────────────────
            SqlEditor.SetSelectionBackColor(true, System.Windows.Media.Color.FromArgb(255, 38, 79, 120));

            // ─── Current Line Highlight ───────────────────────────────
            SqlEditor.CaretLineVisible = true;
            SqlEditor.CaretLineBackColor = System.Windows.Media.Color.FromArgb(255, 40, 40, 40);

            // ─── Keyword Registration ──────────────────────────────────
            SqlEditor.SetKeywords(0, SqlKeywords1.ToLower());
            SqlEditor.SetKeywords(1, SqlKeywords2.ToLower());

            // ─── Events ──────────────────────────────────────────────
            SqlEditor.MarginClick += SqlEditor_MarginClick;
            SqlEditor.KeyDown += SqlEditor_KeyDown;
        }

        // ─── Margin: fold toggle ──────────────────────────────────────
        private void SqlEditor_MarginClick(object? sender, MarginClickEventArgs e)
        {
            if (e.Margin == 2)
            {
                int line = SqlEditor.LineFromPosition(e.Position);
                SqlEditor.Lines[line].ToggleFold();
            }
        }

        // ─── Auto-complete trigger ────────────────────────────────────
        private void SqlEditor_CharAdded(object? sender, CharAddedEventArgs e)
        {
            if (char.IsLetter((char)e.Char) || e.Char == '_')
            {
                ShowAutoComplete();
            }
        }

        // ─── Key bindings ─────────────────────────────────────────────
        private void SqlEditor_KeyDown(object? sender, System.Windows.Forms.KeyEventArgs e)
        {
            // Ctrl+Space = Force autocomplete
            if (e.KeyCode == System.Windows.Forms.Keys.Space && e.Control)
            {
                ShowAutoComplete();
                e.Handled = true;
                return;
            }

            // F5 or Ctrl+E = Execute Query (SSMS convention)
            if (e.KeyCode == System.Windows.Forms.Keys.F5 ||
                (e.KeyCode == System.Windows.Forms.Keys.E && e.Control))
            {
                if (_viewModel?.RunQueryCommand.CanExecute(null) == true)
                    _viewModel.RunQueryCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        // ─── Auto-complete logic ──────────────────────────────────────
        private void ShowAutoComplete()
        {
            var word = SqlEditor.GetWordFromPosition(SqlEditor.CurrentPosition);
            var keywordsSet = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. SQL Keywords
            foreach (var kw in SqlKeywords1.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                keywordsSet.Add(kw.ToUpper());
            foreach (var kw in SqlKeywords2.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                keywordsSet.Add(kw.ToUpper());

            // 2. DB metadata from explorer tree
            if (_viewModel != null)
            {
                ExtractTreeNames(_viewModel.Explorer.TreeItems, keywordsSet);
                foreach (var w in _viewModel.Explorer.GetCachedAutocompleteWords())
                    keywordsSet.Add(w);

                // 3. Active workbook tables/columns
                if (_viewModel.ActiveWorkbook != null)
                {
                    foreach (var t in _viewModel.ActiveWorkbook.ReferencedTables)
                    {
                        if (!string.IsNullOrEmpty(t.Name)) keywordsSet.Add(t.Name);
                        if (!string.IsNullOrEmpty(t.Alias)) keywordsSet.Add(t.Alias);
                    }
                    foreach (var colList in _viewModel.ActiveWorkbook.SourceTableAllColumns.Values)
                        foreach (var col in colList)
                            keywordsSet.Add(col);
                }
            }

            // 4. Words already in the editor
            var text = SqlEditor.Text;
            if (!string.IsNullOrWhiteSpace(text) && text.Length < 100_000)
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(text, @"\b[a-zA-Z_][a-zA-Z0-9_]{2,}\b");
                foreach (System.Text.RegularExpressions.Match m in matches)
                    keywordsSet.Add(m.Value);
            }

            // Nothing typed yet → show all sorted
            if (string.IsNullOrEmpty(word))
            {
                var all = new System.Collections.Generic.List<string>(keywordsSet);
                all.Sort(StringComparer.OrdinalIgnoreCase);
                SqlEditor.AutoCShow(0, string.Join("|", all));
                return;
            }

            // Priority sort: prefix-match first, then contains
            var startsWith = new System.Collections.Generic.List<string>();
            var contains = new System.Collections.Generic.List<string>();
            string lowerWord = word.ToLower();

            foreach (var kw in keywordsSet)
            {
                string lkw = kw.ToLower();
                if (lkw.StartsWith(lowerWord))       startsWith.Add(kw);
                else if (lkw.Contains(lowerWord))    contains.Add(kw);
            }

            startsWith.Sort(StringComparer.OrdinalIgnoreCase);
            contains.Sort(StringComparer.OrdinalIgnoreCase);

            var finalList = new System.Collections.Generic.List<string>(startsWith);
            finalList.AddRange(contains);

            if (finalList.Count > 0)
            {
                if (SqlEditor.AutoCActive) SqlEditor.AutoCCancel();
                SqlEditor.AutoCShow(word.Length, string.Join("|", finalList));
            }
        }

        private void ExtractTreeNames(System.Collections.Generic.IEnumerable<sqlSense.Models.DatabaseTreeItem> items,
                                      System.Collections.Generic.HashSet<string> keywords)
        {
            foreach (var item in items)
            {
                if (item.NodeType == sqlSense.Models.TreeNodeType.Table ||
                    item.NodeType == sqlSense.Models.TreeNodeType.View)
                {
                    if (!string.IsNullOrEmpty(item.Tag)) keywords.Add(item.Tag);
                }
                else if (item.NodeType == sqlSense.Models.TreeNodeType.Column)
                {
                    var colName = item.Name.Split(' ')[0];
                    if (!string.IsNullOrEmpty(colName) && colName != "__dummy__")
                        keywords.Add(colName);
                }

                if (item.Children?.Count > 0)
                    ExtractTreeNames(item.Children, keywords);
            }
        }

        // ─── Cursor position display ──────────────────────────────────
        private void SqlEditor_UpdateUI(object? sender, UpdateUIEventArgs e)
        {
            if (_viewModel == null) return;
            int line = SqlEditor.CurrentLine;
            int col  = SqlEditor.GetColumn(SqlEditor.CurrentPosition);
            _viewModel.SqlEditor.CursorPosition = $"Ln {line + 1}, Col {col + 1}";
            SyncTextToViewModel();
        }

        // ─── DataContext wiring ───────────────────────────────────────
        private void SqlEditorPanel_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateViewModelReference();
            SyncTextFromViewModel();
        }

        private void SqlEditorPanel_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateViewModelReference();
            SyncTextFromViewModel();
        }

        private void UpdateViewModelReference()
        {
            if (_viewModel != null)
                _viewModel.SqlEditor.PropertyChanged -= SqlEditorViewModel_PropertyChanged;

            _viewModel = DataContext as MainViewModel;

            if (_viewModel != null)
                _viewModel.SqlEditor.PropertyChanged += SqlEditorViewModel_PropertyChanged;
        }

        private void SqlEditorViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "SqlText")
                SyncTextFromViewModel();
        }

        private void SyncTextFromViewModel()
        {
            if (_viewModel == null || _isUpdatingText) return;
            string newText = _viewModel.SqlEditor.SqlText ?? string.Empty;
            if (SqlEditor.Text != newText)
            {
                _isUpdatingText = true;
                SqlEditor.Text = newText;
                _isUpdatingText = false;
            }
        }

        private void SyncTextToViewModel()
        {
            if (_viewModel == null || _isUpdatingText) return;
            if (_viewModel.SqlEditor.SqlText != SqlEditor.Text)
            {
                _isUpdatingText = true;
                _viewModel.SqlEditor.SqlText = SqlEditor.Text;
                _isUpdatingText = false;
            }
        }
    }
}
