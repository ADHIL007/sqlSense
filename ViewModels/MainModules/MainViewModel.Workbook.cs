using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using sqlSense.Models;
using System;
using System.IO;

namespace sqlSense.ViewModels
{
    public partial class MainViewModel
    {
        [RelayCommand]
        public void NewWorkspace()
        {
            TablePreview.Reset();
            var newView = new ViewDefinitionInfo { ViewName = "New Workbook", DatabaseName = Explorer.SelectedDatabaseName ?? "master" };
            OpenWorkbooks.Add(newView);
            ActiveWorkbook = newView;
            SqlEditor.SqlText = "";
            SqlEditor.IsChartDisabled = false;
            SqlEditor.LanguageMode = "T-SQL";
            StatusMessage = "New workbook started.";
        }

        [RelayCommand]
        public void CloseWorkbook(ViewDefinitionInfo workbook)
        {
            if (workbook == null) return;
            
            int index = OpenWorkbooks.IndexOf(workbook);
            bool wasActive = (ActiveWorkbook == workbook);
            
            OpenWorkbooks.Remove(workbook);
            
            if (wasActive && OpenWorkbooks.Count > 0)
            {
                int nextIndex = Math.Clamp(index - 1, 0, OpenWorkbooks.Count - 1);
                ActiveWorkbook = OpenWorkbooks[nextIndex];
            }
            
            if (OpenWorkbooks.Count == 0)
            {
                NewWorkspace();
            }
        }

        [RelayCommand(CanExecute = nameof(CanModifyView))]
        private void SaveWorkbook()
        {
            if (ActiveWorkbook == null) return;

            SyncSqlToChart();

            string? targetFilePath = ActiveWorkbook.FilePath;

            if (string.IsNullOrEmpty(targetFilePath))
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "SqlSense Workbook (*.sqv)|*.sqv",
                    FileName = ActiveWorkbook.ViewName ?? "Workbook1"
                };

                if (saveDialog.ShowDialog() != true)
                    return;

                targetFilePath = saveDialog.FileName;
                ActiveWorkbook.ViewName = Path.GetFileNameWithoutExtension(targetFilePath);
                ActiveWorkbook.FilePath = targetFilePath;
            }

            try
            {
                ActiveWorkbook.CanvasZoom = Canvas.Zoom;
                _workbookService.SaveWorkbook(ActiveWorkbook, targetFilePath);
                HasUnsavedChanges = false;
                StatusMessage = $"✓ Workbook saved as {ActiveWorkbook.ViewName}.sqv";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Save Error: {ex.Message}";
            }
        }

        [RelayCommand]
        private void OpenWorkbook()
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "SqlSense Workbook (*.sqv)|*.sqv"
            };

            if (openDialog.ShowDialog() == true)
            {
                LoadWorkbookFromFile(openDialog.FileName);
            }
        }

        public void LoadWorkbookFromFile(string path)
        {
            try
            {
                var workbook = _workbookService.LoadWorkbook(path);
                if (workbook != null)
                {
                    workbook.FilePath = path;
                    if (string.IsNullOrEmpty(workbook.ViewName)) 
                        workbook.ViewName = Path.GetFileNameWithoutExtension(path);
                    
                    OpenWorkbooks.Add(workbook);
                    ActiveWorkbook = workbook;
                    HasUnsavedChanges = false;
                    StatusMessage = $"✓ Loaded workbook: {workbook.ViewName}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Open Error: {ex.Message}";
            }
        }
    }
}
