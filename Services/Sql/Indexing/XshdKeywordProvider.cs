using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace sqlSense.Services.Sql.Indexing
{
    public class XshdKeywordProvider : IXshdKeywordProvider
    {
        private readonly string _xshdFilePath;
        private HashSet<string> _keywordsCache = new(StringComparer.OrdinalIgnoreCase);
        private bool _isLoaded;

        public XshdKeywordProvider(string xshdFilePath = null)
        {
            _xshdFilePath = xshdFilePath;
        }

        public IReadOnlySet<string> GetKeywords()
        {
            if (!_isLoaded)
            {
                LoadKeywords();
            }
            return _keywordsCache;
        }

        private void LoadKeywords()
        {
            _keywordsCache.Clear();

            if (!string.IsNullOrEmpty(_xshdFilePath) && File.Exists(_xshdFilePath))
            {
                try
                {
                    var doc = XDocument.Load(_xshdFilePath);
                    var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
                    
                    // XSHD uses <Word> elements inside <Keywords> ruleset
                    var words = doc.Descendants(ns + "Word");
                    foreach (var word in words)
                    {
                        var text = word.Value?.Trim();
                        if (!string.IsNullOrEmpty(text))
                        {
                            _keywordsCache.Add(text);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load XSHD file: {ex.Message}");
                }
            }

            // Fallback default keywords if none loaded
            if (_keywordsCache.Count == 0)
            {
                var defaults = new[]
                {
                    "SELECT", "UPDATE", "INSERT", "DELETE", "MERGE", "JOIN", "INNER", "LEFT", "RIGHT", "GROUP", "ORDER", "HAVING", "WHERE", "FROM", "INTO", "VALUES", "SET"
                };
                foreach (var kw in defaults)
                {
                    _keywordsCache.Add(kw);
                }
            }

            _isLoaded = true;
        }

        public void Reload()
        {
            _isLoaded = false;
        }
    }
}
