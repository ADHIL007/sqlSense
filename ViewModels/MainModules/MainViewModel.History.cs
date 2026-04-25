using CommunityToolkit.Mvvm.Input;
using sqlSense.Models;
using System.Collections.Generic;
using System.Text.Json;

namespace sqlSense.ViewModels
{
    public partial class MainViewModel
    {
        public void NotifyModification(bool pushUndo = true)
        {
            if (!_isPerformingUndoRedo && pushUndo)
            {
                _undoStack.Push(JsonSerializer.Serialize(ActiveWorkbook));
                _redoStack.Clear();
                UndoCommand.NotifyCanExecuteChanged();
                RedoCommand.NotifyCanExecuteChanged();
            }

            HasUnsavedChanges = true;
            GenerateViewSqlCommand.Execute(null);
        }

        [RelayCommand(CanExecute = nameof(CanUndo))]
        private void Undo()
        {
            if (_undoStack.Count == 0) return;

            _isPerformingUndoRedo = true;
            _redoStack.Push(JsonSerializer.Serialize(ActiveWorkbook));
            
            var state = _undoStack.Pop();
            var workbook = JsonSerializer.Deserialize<ViewDefinitionInfo>(state);
            
            if (workbook != null)
            {
                RestoreWorkbookState(workbook);
                OnPropertyChanged(nameof(ActiveWorkbook));
            }
            
            _isPerformingUndoRedo = false;
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        }

        private bool CanUndo() => _undoStack.Count > 0;

        [RelayCommand(CanExecute = nameof(CanRedo))]
        private void Redo()
        {
            if (_redoStack.Count == 0) return;

            _isPerformingUndoRedo = true;
            _undoStack.Push(JsonSerializer.Serialize(ActiveWorkbook));

            var state = _redoStack.Pop();
            var workbook = JsonSerializer.Deserialize<ViewDefinitionInfo>(state);

            if (workbook != null)
            {
                RestoreWorkbookState(workbook);
                OnPropertyChanged(nameof(ActiveWorkbook));
            }

            _isPerformingUndoRedo = false;
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        }

        private bool CanRedo() => _redoStack.Count > 0;

        private void RestoreWorkbookState(ViewDefinitionInfo state)
        {
            if (ActiveWorkbook == null) return;
            ActiveWorkbook.ReferencedTables = state.ReferencedTables;
            ActiveWorkbook.Joins = state.Joins;
            ActiveWorkbook.Columns = state.Columns;
            ActiveWorkbook.NodePositions = state.NodePositions;
            ActiveWorkbook.WhereClause = state.WhereClause;
            ActiveWorkbook.GroupByClause = state.GroupByClause;
            ActiveWorkbook.OrderByClause = state.OrderByClause;
            ActiveWorkbook.ViewName = state.ViewName;
        }
    }
}
