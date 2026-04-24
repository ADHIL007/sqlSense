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
        private const string SqlKeywords = "ALTER AND AS BEGIN BY CREATE DELETE DROP END EXEC FROM GO GROUP HAVING INNER INSERT INTO IS JOIN LEFT NOT NULL ON OR ORDER OUTER RIGHT SELECT SET TABLE UPDATE WHERE";

        public SqlEditorPanel()
        {
            InitializeComponent();
            
            this.Loaded += SqlEditorPanel_Loaded;
            this.DataContextChanged += SqlEditorPanel_DataContextChanged;
            
            SqlEditor.UpdateUI += SqlEditor_UpdateUI;
            SqlEditor.CharAdded += SqlEditor_CharAdded;
            
            // Wire text changed events using UpdateUI instead
            // SqlEditor.UpdateUI will catch text modifications
            
            ConfigureScintilla();
        }

        private void ConfigureScintilla()
        {
            // Initial styling
            SqlEditor.WrapMode = WrapMode.None;
            SqlEditor.IndentationGuides = IndentView.LookBoth;
            SqlEditor.ScrollWidthTracking = true;
            SqlEditor.ScrollWidth = 100;
            SqlEditor.AutoCIgnoreCase = true;
            SqlEditor.CaretForeColor = System.Windows.Media.Colors.Gray;

            // Configure Autocomplete UI
            SqlEditor.AutoCOrder = Order.Custom;
            SqlEditor.AutoCSeparator = '|';
            SqlEditor.DirectMessage(2205, new IntPtr(System.Drawing.ColorTranslator.ToWin32(System.Drawing.Color.FromArgb(30, 30, 30))), IntPtr.Zero); // Back
            SqlEditor.DirectMessage(2206, new IntPtr(System.Drawing.ColorTranslator.ToWin32(System.Drawing.Color.FromArgb(212, 212, 212))), IntPtr.Zero); // Fore
            SqlEditor.DirectMessage(2207, new IntPtr(System.Drawing.ColorTranslator.ToWin32(System.Drawing.Color.FromArgb(86, 156, 214))), IntPtr.Zero); // Highlight
            SqlEditor.DirectMessage(2208, new IntPtr(System.Drawing.ColorTranslator.ToWin32(System.Drawing.Color.FromArgb(51, 51, 51))), IntPtr.Zero); // SelBack
            SqlEditor.DirectMessage(2209, new IntPtr(System.Drawing.ColorTranslator.ToWin32(System.Drawing.Color.White)), IntPtr.Zero); // SelFore

            // Stying margins (line numbers, folding)
            SqlEditor.Margins[0].Width = 30; // Line numbers margin
            SqlEditor.Margins[0].Type = MarginType.Number;
            
            // Syntax Highlighting for SQL
            SqlEditor.Lexer = Lexer.Sql;

            // Set default font
            SqlEditor.Styles[ScintillaNET.Style.Default].Font = "Consolas";
            SqlEditor.Styles[ScintillaNET.Style.Default].Size = 13;
            SqlEditor.Styles[ScintillaNET.Style.Default].BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            SqlEditor.Styles[ScintillaNET.Style.Default].ForeColor = System.Drawing.Color.FromArgb(212, 212, 212);
            SqlEditor.StyleClearAll(); // Apply default to all styles

            // Line number margin styling
            SqlEditor.Styles[ScintillaNET.Style.LineNumber].BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            SqlEditor.Styles[ScintillaNET.Style.LineNumber].ForeColor = System.Drawing.Color.FromArgb(120, 120, 120);

            // Set fold margin colors
            SqlEditor.SetFoldMarginColor(true, System.Windows.Media.Color.FromArgb(255, 30, 30, 30));
            SqlEditor.SetFoldMarginHighlightColor(true, System.Windows.Media.Color.FromArgb(255, 30, 30, 30));

            // SQL Keyword styles (using standard Scintilla SQL Lexer indices)
            SqlEditor.Styles[0].ForeColor = System.Drawing.Color.Silver; // Default
            SqlEditor.Styles[1].ForeColor = System.Drawing.Color.Green;  // Comment
            SqlEditor.Styles[2].ForeColor = System.Drawing.Color.Green;  // CommentLine
            SqlEditor.Styles[3].ForeColor = System.Drawing.Color.Green;  // CommentDoc
            SqlEditor.Styles[4].ForeColor = System.Drawing.Color.Olive;  // Number
            SqlEditor.Styles[5].ForeColor = System.Drawing.Color.DeepSkyBlue; // Word
            SqlEditor.Styles[6].ForeColor = System.Drawing.Color.Red;    // String
            SqlEditor.Styles[7].ForeColor = System.Drawing.Color.Red;    // Character
            SqlEditor.Styles[10].ForeColor = System.Drawing.Color.Silver; // Operator
            SqlEditor.Styles[11].ForeColor = System.Drawing.Color.Silver; // Identifier
            SqlEditor.Styles[16].ForeColor = System.Drawing.Color.LightBlue; // Word2

            SqlEditor.SetKeywords(0, SqlKeywords.ToLower());

            // Folding
            SqlEditor.SetProperty("fold", "1");
            SqlEditor.SetProperty("fold.compact", "1");
            
            SqlEditor.Margins[2].Type = MarginType.Symbol;
            SqlEditor.Margins[2].Mask = Marker.MaskFolders;
            SqlEditor.Margins[2].Sensitive = true;
            SqlEditor.Margins[2].Width = 20;
            
            // Folding markers
            SqlEditor.Markers[Marker.FolderEnd].Symbol = MarkerSymbol.BoxPlusConnected;
            SqlEditor.Markers[Marker.FolderOpenMid].Symbol = MarkerSymbol.BoxMinusConnected;
            SqlEditor.Markers[Marker.FolderMidTail].Symbol = MarkerSymbol.TCorner;
            SqlEditor.Markers[Marker.FolderTail].Symbol = MarkerSymbol.LCorner;
            SqlEditor.Markers[Marker.FolderSub].Symbol = MarkerSymbol.VLine;
            SqlEditor.Markers[Marker.Folder].Symbol = MarkerSymbol.BoxPlus;
            SqlEditor.Markers[Marker.FolderOpen].Symbol = MarkerSymbol.BoxMinus;

            // Fold styling
            for (int i = Marker.FolderEnd; i <= Marker.FolderOpen; i++)
            {
                SqlEditor.Markers[i].SetForeColor(System.Drawing.Color.FromArgb(30, 30, 30));
                SqlEditor.Markers[i].SetBackColor(System.Drawing.Color.FromArgb(120, 120, 120));
            }
            
            SqlEditor.MarginClick += SqlEditor_MarginClick;
            SqlEditor.KeyDown += SqlEditor_KeyDown;
        }

        private void SqlEditor_MarginClick(object? sender, MarginClickEventArgs e)
        {
            if (e.Margin == 2)
            {
                // Toggle fold
                int line = SqlEditor.LineFromPosition(e.Position);
                SqlEditor.Lines[line].ToggleFold();
            }
        }

        private void SqlEditor_CharAdded(object? sender, CharAddedEventArgs e)
        {
            // Trigger auto-complete on letters or underscore
            if (char.IsLetter((char)e.Char) || e.Char == '_')
            {
                ShowAutoComplete();
            }
        }

        private void SqlEditor_KeyDown(object? sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (e.KeyCode == System.Windows.Forms.Keys.Space && e.Control)
            {
                ShowAutoComplete();
                e.Handled = true;
            }
        }

        private void ShowAutoComplete()
        {
            var word = SqlEditor.GetWordFromPosition(SqlEditor.CurrentPosition);
            
            var keywordsSet = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            // 1. Add SQL Keywords
            foreach (var kw in SqlKeywords.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                keywordsSet.Add(kw);
            }

            // 2. Add dynamic metadata from Explorer tree
            if (_viewModel != null)
            {
                ExtractTreeNames(_viewModel.Explorer.TreeItems, keywordsSet);

                foreach (var cachedWord in _viewModel.Explorer.GetCachedAutocompleteWords())
                {
                    keywordsSet.Add(cachedWord);
                }

                // 3. Add tables/columns from Active Workbook
                if (_viewModel.ActiveWorkbook != null)
                {
                    foreach (var t in _viewModel.ActiveWorkbook.ReferencedTables)
                    {
                        if (!string.IsNullOrEmpty(t.Name)) keywordsSet.Add(t.Name);
                        if (!string.IsNullOrEmpty(t.Alias)) keywordsSet.Add(t.Alias);
                    }

                    foreach (var colList in _viewModel.ActiveWorkbook.SourceTableAllColumns.Values)
                    {
                        foreach (var col in colList)
                        {
                            keywordsSet.Add(col);
                        }
                    }
                }
            }

            // 4. Words from current document
            var text = SqlEditor.Text;
            if (!string.IsNullOrWhiteSpace(text) && text.Length < 100000) // basic safety limit
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(text, @"\b[a-zA-Z_][a-zA-Z0-9_]*\b");
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (match.Value.Length >= 3)
                        keywordsSet.Add(match.Value);
                }
            }

            if (string.IsNullOrEmpty(word))
            {
                var allKeywords = new System.Collections.Generic.List<string>(keywordsSet);
                allKeywords.Sort();
                SqlEditor.AutoCShow(0, string.Join("|", allKeywords));
                return;
            }

            var startsWith = new System.Collections.Generic.List<string>();
            var contains = new System.Collections.Generic.List<string>();

            string lowerWord = word.ToLower();

            foreach (var kw in keywordsSet)
            {
                string lowerKw = kw.ToLower();
                if (lowerKw.StartsWith(lowerWord))
                {
                    startsWith.Add(kw);
                }
                else if (lowerKw.Contains(lowerWord))
                {
                    contains.Add(kw);
                }
            }

            startsWith.Sort();
            contains.Sort();

            var finalList = new System.Collections.Generic.List<string>();
            finalList.AddRange(startsWith);
            finalList.AddRange(contains);

            if (finalList.Count > 0)
            {
                if (SqlEditor.AutoCActive)
                    SqlEditor.AutoCCancel();
                
                SqlEditor.AutoCShow(word.Length, string.Join("|", finalList));
            }
        }

        private void ExtractTreeNames(System.Collections.Generic.IEnumerable<sqlSense.Models.DatabaseTreeItem> items, System.Collections.Generic.HashSet<string> keywords)
        {
            foreach (var item in items)
            {
                if (item.NodeType == sqlSense.Models.TreeNodeType.Table || item.NodeType == sqlSense.Models.TreeNodeType.View)
                {
                    if (!string.IsNullOrEmpty(item.Tag)) keywords.Add(item.Tag);
                }
                else if (item.NodeType == sqlSense.Models.TreeNodeType.Column)
                {
                    var colName = item.Name.Split(' ')[0];
                    if (!string.IsNullOrEmpty(colName) && colName != "__dummy__") keywords.Add(colName);
                }

                if (item.Children != null && item.Children.Count > 0)
                {
                    ExtractTreeNames(item.Children, keywords);
                }
            }
        }

        private void SqlEditor_UpdateUI(object? sender, UpdateUIEventArgs e)
        {
            if (_viewModel == null) return;
            int line = SqlEditor.CurrentLine;
            int col = SqlEditor.GetColumn(SqlEditor.CurrentPosition);
            _viewModel.SqlEditor.CursorPosition = $"Ln {line + 1}, Col {col + 1}";
            
            // Fallback for text changed sync
            SyncTextToViewModel();
        }

        private void SqlEditor_TextModified(object? sender, EventArgs e)
        {
            SyncTextToViewModel();
        }

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
            {
                _viewModel.SqlEditor.PropertyChanged -= SqlEditorViewModel_PropertyChanged;
            }

            _viewModel = DataContext as MainViewModel;

            if (_viewModel != null)
            {
                _viewModel.SqlEditor.PropertyChanged += SqlEditorViewModel_PropertyChanged;
            }
        }

        private void SqlEditorViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_viewModel.SqlEditor.SqlText))
            {
                SyncTextFromViewModel();
            }
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
