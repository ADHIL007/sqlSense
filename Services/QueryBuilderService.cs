using System;
using System.Collections.Generic;
using System.Linq;
using sqlSense.Models;

namespace sqlSense.Services
{
    public class QueryBuilderService
    {
        public static string BuildSqlForNode(NodeCard node, ViewDefinitionInfo viewDef)
        {
            if (node.IsViewNode)
            {
                return $"SELECT TOP 50 * FROM [{viewDef.SchemaName}].[{viewDef.ViewName}]";
            }

            if (node.SourceTable != null)
            {
                var tbl = node.SourceTable;
                return $"SELECT TOP 50 * FROM [{tbl.Schema}].[{tbl.Name}]";
            }

            if (node.JoinData != null)
            {
                // To show "Joined Data" at this level, we need to build a query that 
                // includes all tables contributing to THIS join.
                return BuildJoinQuery(node, viewDef);
            }

            return "";
        }

        private static string BuildJoinQuery(NodeCard node, ViewDefinitionInfo viewDef)
        {
            var participating = node.ParticipatingTables;
            if (participating.Count == 0) return "";

            LoggerService.Log($"Building Join Query for {participating.Count} participating tables: {string.Join(", ", participating)}");

            // Find joins that involve these participating tables
            var relevantJoins = viewDef.Joins.Where(j => 
                participating.Contains(j.LeftTableAlias) && 
                participating.Contains(j.RightTableAlias)).ToList();

            var firstTable = viewDef.ReferencedTables.FirstOrDefault(t => participating.Contains(t.Alias));
            if (firstTable == null) return "";

            string sql = $"SELECT TOP 50 * \nFROM [{firstTable.Schema}].[{firstTable.Name}] AS [{firstTable.Alias}]";
            var joinedAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { firstTable.Alias };
            
            bool added = true;
            while (added)
            {
                added = false;
                foreach (var join in relevantJoins)
                {
                    if (joinedAliases.Contains(join.LeftTableAlias) && !joinedAliases.Contains(join.RightTableAlias))
                    {
                        // Always resolve actual table info from global ReferencedTables if available
                        var rightTable = viewDef.ReferencedTables.FirstOrDefault(t => string.Equals(t.Alias, join.RightTableAlias, StringComparison.OrdinalIgnoreCase));
                        string schema = rightTable?.Schema ?? join.RightTableSchema;
                        string table = rightTable?.Name ?? join.RightTableName;
                        
                        sql += $"\n{join.JoinType} JOIN [{schema}].[{table}] AS [{join.RightTableAlias}] " +
                               $"ON [{join.LeftTableAlias}].[{join.LeftColumn}] = [{join.RightTableAlias}].[{join.RightColumn}]";
                        joinedAliases.Add(join.RightTableAlias);
                        added = true;
                    }
                    else if (joinedAliases.Contains(join.RightTableAlias) && !joinedAliases.Contains(join.LeftTableAlias))
                    {
                        // Resolve the actual table for the left alias
                        var leftTable = viewDef.ReferencedTables.FirstOrDefault(t => string.Equals(t.Alias, join.LeftTableAlias, StringComparison.OrdinalIgnoreCase));
                        if (leftTable != null)
                        {
                            sql += $"\n{join.JoinType} JOIN [{leftTable.Schema}].[{leftTable.Name}] AS [{leftTable.Alias}] " +
                                   $"ON [{join.LeftTableAlias}].[{join.LeftColumn}] = [{join.RightTableAlias}].[{join.RightColumn}]";
                            joinedAliases.Add(join.LeftTableAlias);
                            added = true;
                        }
                    }
                }
            }
            if (!string.IsNullOrEmpty(viewDef.WhereClause))
            {
                sql += $"\nWHERE {viewDef.WhereClause}";
            }

            return sql;
        }
    }
}
