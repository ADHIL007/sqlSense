using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using ScintillaNET;
using ScintillaNET.WPF;
using sqlSense.ViewModels;
using sqlSense.Controllers;
using sqlSense.Services;

namespace sqlSense.UI.Controls.Sql
{
    public partial class SqlEditorPanel : UserControl
    {
        private MainViewModel? _viewModel;
        private bool _isUpdatingText = false;
        private SqlAutocompleteController? _autocompleteController;

        public SqlEditorPanel()
        {
            InitializeComponent();

            this.Loaded += SqlEditorPanel_Loaded;
            this.DataContextChanged += SqlEditorPanel_DataContextChanged;

            SqlEditor.UpdateUI += SqlEditor_UpdateUI;
            SqlEditor.CharAdded += SqlEditor_CharAdded;
            SqlEditor.MarginClick += SqlEditor_MarginClick;
            SqlEditor.KeyDown += SqlEditor_KeyDown;

            SqlEditorService.Configure(SqlEditor);
        }

        private void UpdateViewModelReference()
        {
            if (_viewModel != null)
                _viewModel.SqlEditor.PropertyChanged -= SqlEditorViewModel_PropertyChanged;

            _viewModel = DataContext as MainViewModel;

            if (_viewModel != null)
            {
                _viewModel.SqlEditor.PropertyChanged += SqlEditorViewModel_PropertyChanged;
                _autocompleteController = new SqlAutocompleteController(_viewModel);
                _autocompleteController.RequestShowAutoComplete = () => 
                {
                    if (SqlEditor.AutoCActive) ShowAutoComplete();
                };
            }
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
            if (_autocompleteController == null) return;

            if (char.IsLetter((char)e.Char) || e.Char == '_' || e.Char == '.')
            {
                string contextStr = GetContextString();
                _autocompleteController.TriggerDynamicFetch(contextStr, (char)e.Char);
                ShowAutoComplete();
            }
        }

        private string GetContextString()
        {
            int pos = SqlEditor.CurrentPosition;
            int startPos = pos;
            while (startPos > 0)
            {
                char c = (char)SqlEditor.GetCharAt(startPos - 1);
                if (char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '[' || c == ']')
                    startPos--;
                else
                    break;
            }
            return SqlEditor.GetTextRange(startPos, pos - startPos);
        }

        private void ShowAutoComplete()
        {
            if (_autocompleteController == null) return;
            
            string contextStr = GetContextString();
            var result = _autocompleteController.GetAutoCompleteList(contextStr);
            
            if (result != null)
            {
                if (SqlEditor.AutoCActive) SqlEditor.AutoCCancel();
                SqlEditor.AutoCShow(result.Item1, result.Item2);
            }
        }

        // ─── Key bindings ─────────────────────────────────────────────
        private void SqlEditor_KeyDown(object? sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (e.KeyCode == System.Windows.Forms.Keys.S && e.Control)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                return;
            }

            if (e.KeyCode == System.Windows.Forms.Keys.Space && e.Control)
            {
                ShowAutoComplete();
                e.SuppressKeyPress = true;
                e.Handled = true;
                return;
            }

            if (e.KeyCode == System.Windows.Forms.Keys.F5 || (e.KeyCode == System.Windows.Forms.Keys.E && e.Control))
            {
                if (_viewModel?.RunQueryCommand.CanExecute(null) == true)
                    _viewModel.RunQueryCommand.Execute(null);
                e.SuppressKeyPress = true;
                e.Handled = true;
                return;
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
