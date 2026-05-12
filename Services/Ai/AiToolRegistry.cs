using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using sqlSense.Services.Ai.Tools;

namespace sqlSense.Services.Ai
{
    public static class AiToolRegistry
    {
        private static readonly ToolRouter _router = new();

        static AiToolRegistry()
        {
            // Register AI Workflow Tools
            _router.RegisterTool(new GetSoftwareInformationTool());
            _router.RegisterTool(new GetActiveDocumentTool(() => {
                return System.Windows.Application.Current?.Dispatcher.Invoke(() => {
                    if (System.Windows.Application.Current.MainWindow?.DataContext is sqlSense.ViewModels.MainViewModel vm)
                    {
                        return vm.SqlEditor?.SqlText;
                    }
                    return string.Empty;
                });
            }));
            _router.RegisterTool(new ParseQueryAstTool());
            
            // Wire up the indexer tools to the actual MainWindow DataContext safely
            _router.RegisterTool(new SearchIndexTool(() => {
                return System.Windows.Application.Current?.Dispatcher.Invoke(() => {
                    if (System.Windows.Application.Current.MainWindow?.DataContext is sqlSense.ViewModels.MainViewModel vm)
                    {
                        return vm.SqlEditor.CurrentIndex;
                    }
                    return null;
                });
            }));

            _router.RegisterTool(new LoadSpanTool(() => {
                return System.Windows.Application.Current?.Dispatcher.Invoke(() => {
                    if (System.Windows.Application.Current.MainWindow?.DataContext is sqlSense.ViewModels.MainViewModel vm)
                    {
                        return vm.ActiveWorkbook?.FilePath ?? "unsaved_document";
                    }
                    return null;
                });
            }, () => {
                return System.Windows.Application.Current?.Dispatcher.Invoke(() => {
                    if (System.Windows.Application.Current.MainWindow?.DataContext is sqlSense.ViewModels.MainViewModel vm)
                    {
                        return vm.SqlEditor.SqlText;
                    }
                    return string.Empty;
                });
            }));
        }

        public static JArray GetAvailableTools()
        {
            return _router.GetAvailableToolsSchema();
        }

        // Keep synchronous fallback for compatibility if needed, but prefer async
        public static string ExecuteTool(string toolName, JObject arguments = null)
        {
            var result = _router.RouteAndExecuteAsync(toolName, arguments, CancellationToken.None).GetAwaiter().GetResult();
            if (result.IsSuccess)
                return result.ResultData;
            return $"Tool error: {result.ErrorMessage}";
        }

        public static async Task<string> ExecuteToolAsync(string toolName, JObject arguments = null, CancellationToken cancellationToken = default)
        {
            var result = await _router.RouteAndExecuteAsync(toolName, arguments, cancellationToken);
            if (result.IsSuccess)
                return result.ResultData;
            return $"Tool error: {result.ErrorMessage}";
        }
    }
}
