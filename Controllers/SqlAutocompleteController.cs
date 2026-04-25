using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using sqlSense.ViewModels;

namespace sqlSense.Controllers
{
    public class SqlAutocompleteController
    {
        private readonly MainViewModel _viewModel;
        private readonly Dictionary<string, List<string>> _dbKeywordCache = new(StringComparer.OrdinalIgnoreCase);
        private bool _isFetchingKeywords = false;

        public Action? RequestShowAutoComplete { get; set; }

        private const string SqlKeywords1 =
            "add all alter and any as asc authorization backup begin between break browse bulk by cascade case check checkpoint close clustered coalesce collate column commit compute constraint contains containstable continue convert create cross current current_date current_time current_timestamp current_user cursor database dbcc deallocate declare default delete deny desc disk distinct distributed double drop dump else end errlvl escape except exec execute exists exit external fetch file fillfactor for foreign freetext freetexttable from full function goto grant group having holdlock identity identitycol identity_insert if in index inner insert intersect into is join key kill left like lineno load merge national nocheck nonclustered not null nullif of off offsets on open opendatasource openquery openrowset openxml option or order outer over percent pivot plan precision primary print proc procedure public raiserror read readtext reconfigure references replication restore restrict return revert revoke right rollback rowcount rowguidcol rule save schema securityaudit select semantickeyphrasetable semanticsimilaritydetailstable semanticsimilaritytable session_user set setuser shutdown some statistics system_user table tablesample textsize then to top tran transaction trigger truncate try_convert tsequal union unique unpivot update updatetext use user values varying view waitfor when where while with within group writetext";

        private const string SqlKeywords2 =
            "abs ascii cast ceiling char charindex coalesce concat convert count datename datepart dateadd datediff day getdate getutcdate iif isnull isdate isnumeric left len lower ltrim max min month newid newsequentialid nullif object_id object_name parsename patindex replace right rtrim scope_identity sign space sqrt str stuff substring sum sysdatetime sysutcdatetime trim upper year avg count_big stdev stdevp var varp row_number rank dense_rank ntile lag lead first_value last_value percent_rank cume_dist";

        public SqlAutocompleteController(MainViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public void TriggerDynamicFetch(string contextStr, char addedChar)
        {
            string[] parts = contextStr.Split('.');
            bool isContextual = parts.Length > 1;

            if (isContextual && addedChar == '.')
            {
                string schemaOrDb = parts[parts.Length - 2].Trim('[', ']');
                if (!string.IsNullOrEmpty(schemaOrDb))
                {
                    string cacheKey = $"CTX:{schemaOrDb.ToUpperInvariant()}";
                    if (!_dbKeywordCache.ContainsKey(cacheKey) && !_isFetchingKeywords && _viewModel.DbService != null && _viewModel.Explorer.SelectedDatabaseName != null)
                    {
                        _isFetchingKeywords = true;
                        _ = FetchContextualKeywordsAsync(cacheKey, _viewModel.Explorer.SelectedDatabaseName, schemaOrDb);
                    }
                }
            }
            else
            {
                var word = parts.Length > 0 ? parts[parts.Length - 1] : "";
                if (!string.IsNullOrEmpty(word) && char.IsLetter(word[0]))
                {
                    string prefix = word.ToUpperInvariant();
                    int requiredCount = 20;

                    var localMatches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kvp in _dbKeywordCache)
                    {
                        if (kvp.Key.StartsWith("CTX:")) continue;
                        foreach (var item in kvp.Value)
                        {
                            var namePart = item.Split('?')[0];
                            if (namePart.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                                localMatches.Add(namePart);
                        }
                    }

                    if (localMatches.Count < requiredCount && !_dbKeywordCache.ContainsKey(prefix) && !_isFetchingKeywords && _viewModel.DbService != null && _viewModel.Explorer.SelectedDatabaseName != null)
                    {
                        _isFetchingKeywords = true;
                        _ = FetchDbKeywordsAsync(prefix, _viewModel.Explorer.SelectedDatabaseName);
                    }
                }
            }
        }

        private async Task FetchContextualKeywordsAsync(string cacheKey, string dbName, string schema)
        {
            try 
            {
                if (_viewModel.DbService != null) 
                {
                    var items = await _viewModel.DbService.GetContextualSuggestionsAsync(dbName, schema);
                    _dbKeywordCache[cacheKey] = items;
                    
                    App.Current.Dispatcher.InvokeAsync(() => {
                        RequestShowAutoComplete?.Invoke();
                    });
                }
            } 
            finally 
            {
                _isFetchingKeywords = false;
            }
        }

        private async Task FetchDbKeywordsAsync(string prefix, string dbName)
        {
            try 
            {
                if (_viewModel.DbService != null) 
                {
                    var items = await _viewModel.DbService.GetAutocompleteSuggestionsAsync(dbName, prefix);
                    _dbKeywordCache[prefix] = items;
                    
                    App.Current.Dispatcher.InvokeAsync(() => {
                        RequestShowAutoComplete?.Invoke();
                    });
                }
            } 
            finally 
            {
                _isFetchingKeywords = false;
            }
        }

        public Tuple<int, string>? GetAutoCompleteList(string contextStr)
        {
            string[] parts = contextStr.Split('.');
            string word = parts.Length > 0 ? parts[parts.Length - 1] : "";
            bool isContextual = parts.Length > 1;

            var keywordsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (isContextual)
            {
                string prevPart1 = parts.Length > 1 ? parts[parts.Length - 2].Trim('[', ']') : "";
                string prevPart2 = parts.Length > 2 ? parts[parts.Length - 3].Trim('[', ']') : "";

                // 1. Check if prevPart1 is a table alias in the active workbook -> Show columns
                if (_viewModel.ActiveWorkbook != null)
                {
                    var targetTable = _viewModel.ActiveWorkbook.ReferencedTables.Find(t => 
                        string.Equals(t.Alias, prevPart1, StringComparison.OrdinalIgnoreCase) || 
                        string.Equals(t.Name, prevPart1, StringComparison.OrdinalIgnoreCase));

                    if (targetTable != null && _viewModel.ActiveWorkbook.SourceTableAllColumns.TryGetValue(targetTable.FullName, out var cols))
                    {
                        foreach (var col in cols) keywordsSet.Add(col);
                    }
                }

                // 2. Check Database Explorer Tree for schemas or tables
                foreach (var dbNode in _viewModel.Explorer.TreeItems)
                {
                    bool matchesDb = string.Equals(dbNode.DatabaseName, prevPart1, StringComparison.OrdinalIgnoreCase) || 
                                     string.Equals(dbNode.DatabaseName, prevPart2, StringComparison.OrdinalIgnoreCase);
                    bool isCurrentDb = string.Equals(dbNode.DatabaseName, _viewModel.Explorer.SelectedDatabaseName, StringComparison.OrdinalIgnoreCase);

                    if (matchesDb || isCurrentDb)
                    {
                        ExtractContextualTreeNames(dbNode.Children, keywordsSet, prevPart1, prevPart2);
                    }
                }
                
                // Always suggest common schemas if typing directly after a DB
                if (parts.Length == 2 && prevPart1.Length > 0 && keywordsSet.Count == 0)
                {
                    keywordsSet.Add("dbo");
                    keywordsSet.Add("guest");
                    keywordsSet.Add("sys");
                    keywordsSet.Add("INFORMATION_SCHEMA");
                }

                // 3. Dynamic Contextual Cache (If tree was not expanded)
                string cacheKey = $"CTX:{prevPart1.ToUpperInvariant()}";
                if (_dbKeywordCache.TryGetValue(cacheKey, out var ctxItems))
                {
                    foreach (var w in ctxItems)
                    {
                        keywordsSet.Add(w);
                    }
                }
            }
            else
            {
                // 1. SQL Keywords
                foreach (var kw in SqlKeywords1.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    keywordsSet.Add(kw.ToUpper());
                foreach (var kw in SqlKeywords2.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    keywordsSet.Add(kw.ToUpper());

                // 2. DB metadata from explorer tree
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

                // 4. Dynamic Cache from DB (limit to 5 visual suggestions)
                if (!string.IsNullOrEmpty(word) && char.IsLetter(word[0]))
                {
                    string prefix = word;
                    int addedDynamicItems = 0;
                    foreach (var kvp in _dbKeywordCache)
                    {
                        if (kvp.Key.StartsWith("CTX:")) continue;
                        foreach (var w in kvp.Value)
                        {
                            var namePart = w.Split('?')[0];
                            if (namePart.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            {
                                if (addedDynamicItems < 5)
                                {
                                    keywordsSet.Add(w);
                                    addedDynamicItems++;
                                }
                            }
                        }
                    }
                }
            }

            if (keywordsSet.Count == 0) return null;

            // Priority sort: prefix-match first, then contains
            if (string.IsNullOrEmpty(word))
            {
                var all = new List<string>(keywordsSet);
                all.Sort(StringComparer.OrdinalIgnoreCase);
                return new Tuple<int, string>(0, string.Join("|", all));
            }

            var startsWith = new List<string>();
            var contains = new List<string>();
            string lowerWord = word.ToLower();

            foreach (var kw in keywordsSet)
            {
                string lkw = kw.ToLower();
                if (lkw.StartsWith(lowerWord))       startsWith.Add(kw);
                else if (lkw.Contains(lowerWord))    contains.Add(kw);
            }

            startsWith.Sort(StringComparer.OrdinalIgnoreCase);
            contains.Sort(StringComparer.OrdinalIgnoreCase);

            var finalList = new List<string>(startsWith);
            finalList.AddRange(contains);

            if (finalList.Count > 0)
            {
                return new Tuple<int, string>(word.Length, string.Join("|", finalList));
            }

            return null;
        }

        private void ExtractContextualTreeNames(IEnumerable<Models.DatabaseTreeItem>? items, 
                                                HashSet<string> keywords, 
                                                string prevPart1, string prevPart2)
        {
            if (items == null) return;
            
            foreach (var item in items)
            {
                if (item.NodeType == Models.TreeNodeType.Table ||
                    item.NodeType == Models.TreeNodeType.View)
                {
                    if (string.IsNullOrEmpty(prevPart1) || 
                        string.Equals(item.SchemaName, prevPart1, StringComparison.OrdinalIgnoreCase)) 
                    {
                        if (!string.IsNullOrEmpty(item.Tag)) keywords.Add(item.Tag);
                    }
                    
                    if (!string.IsNullOrEmpty(prevPart1) && string.IsNullOrEmpty(prevPart2))
                    {
                        if (!string.IsNullOrEmpty(item.SchemaName)) keywords.Add(item.SchemaName);
                    }
                }
                
                if (item.Children?.Count > 0)
                    ExtractContextualTreeNames(item.Children, keywords, prevPart1, prevPart2);
            }
        }

        private void ExtractTreeNames(IEnumerable<Models.DatabaseTreeItem> items, HashSet<string> keywords)
        {
            foreach (var item in items)
            {
                if (item.NodeType == Models.TreeNodeType.Database)
                {
                    if (!string.IsNullOrEmpty(item.DatabaseName)) keywords.Add(item.DatabaseName);
                }
                else if (item.NodeType == Models.TreeNodeType.Table ||
                         item.NodeType == Models.TreeNodeType.View)
                {
                    if (!string.IsNullOrEmpty(item.Tag)) keywords.Add(item.Tag);
                    if (!string.IsNullOrEmpty(item.SchemaName)) keywords.Add(item.SchemaName);
                }
                else if (item.NodeType == Models.TreeNodeType.Column)
                {
                    var colName = item.Name.Split(' ')[0];
                    if (!string.IsNullOrEmpty(colName) && colName != "__dummy__")
                        keywords.Add(colName);
                }

                if (item.Children?.Count > 0)
                    ExtractTreeNames(item.Children, keywords);
            }
        }

        public HashSet<string> ExtractEditorWords(string text)
        {
            var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(text) && text.Length < 100_000)
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(text, @"\b[a-zA-Z_][a-zA-Z0-9_]{2,}\b");
                foreach (System.Text.RegularExpressions.Match m in matches)
                    words.Add(m.Value);
            }
            return words;
        }
    }
}
