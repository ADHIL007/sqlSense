using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using sqlSense.Models;

namespace sqlSense.Services
{
    /// <summary>
    /// Parses raw SQL text into a ViewDefinitionInfo model (tables, joins, columns).
    /// Works offline — no database connection needed.
    /// </summary>
    public static class SqlParserService
    {
        /// <summary>
        /// Parses a SQL SELECT statement (with optional ALTER/CREATE VIEW wrapper)
        /// and returns a fully populated ViewDefinitionInfo.
        /// Preserves the existing workbook's metadata (name, database, zoom, etc.)
        /// while replacing structural data (tables, joins, columns).
        /// </summary>
        public static bool TrySyncSqlToModel(string sql, ViewDefinitionInfo target)
        {
            if (string.IsNullOrWhiteSpace(sql)) return false;

            try
            {
                var parser = new TSql160Parser(false);
                using var reader = new StringReader(sql);
                var tree = parser.Parse(reader, out var errors);

                if (errors != null && errors.Count > 0) return false;

                // Visit for tables, joins, columns, WHERE
                var visitor = new FullSqlVisitor();
                tree.Accept(visitor);

                // ── Clear old structural data ──
                // Keep node positions for tables that still exist
                var oldPositions = new Dictionary<string, (double X, double Y)>(target.NodePositions);

                target.ReferencedTables.Clear();
                target.Joins.Clear();
                target.Columns.Clear();

                // ── Populate tables ──
                foreach (var tbl in visitor.Tables)
                {
                    target.ReferencedTables.Add(tbl);
                }

                // Ensure every table has an alias
                foreach (var table in target.ReferencedTables)
                {
                    if (string.IsNullOrEmpty(table.Alias))
                        table.Alias = table.Name;
                }

                // ── Populate joins ──
                foreach (var join in visitor.Joins)
                {
                    target.Joins.Add(join);
                }

                // ── Populate columns ──
                foreach (var col in visitor.Columns)
                {
                    target.Columns.Add(col);
                }

                // ── Populate clauses ──
                target.WhereClause = visitor.WhereClause;
                target.GroupByClause = visitor.GroupByClause;
                target.HavingClause = visitor.HavingClause;
                target.OrderByClause = visitor.OrderByClause;

                // ── Restore node positions for tables that still exist ──
                target.NodePositions.Clear();
                foreach (var table in target.ReferencedTables)
                {
                    var key = table.FullName;
                    var keyByAlias = table.Alias;
                    if (oldPositions.TryGetValue(key, out var pos))
                        target.NodePositions[key] = pos;
                    else if (oldPositions.TryGetValue(keyByAlias, out var posByAlias))
                        target.NodePositions[key] = posByAlias;
                }

                // ── Detect if this wraps a CREATE/ALTER VIEW ──
                target.IsView = visitor.IsViewStatement;
                if (visitor.IsViewStatement)
                {
                    if (!string.IsNullOrEmpty(visitor.ViewSchema))
                        target.SchemaName = visitor.ViewSchema;
                    if (!string.IsNullOrEmpty(visitor.ViewName))
                        target.ViewName = visitor.ViewName;
                }

                // Store the raw SQL definition
                target.SqlDefinition = sql;

                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Full SQL visitor that extracts tables, joins, columns, and clauses.
    /// </summary>
    internal class FullSqlVisitor : TSqlFragmentVisitor
    {
        public List<ReferencedTable> Tables { get; } = new();
        public List<JoinRelationship> Joins { get; } = new();
        public List<ViewColumnInfo> Columns { get; } = new();
        public string WhereClause { get; set; } = "";
        public string GroupByClause { get; set; } = "";
        public string HavingClause { get; set; } = "";
        public string OrderByClause { get; set; } = "";
        public bool IsViewStatement { get; set; } = false;
        public string ViewName { get; set; } = "";
        public string ViewSchema { get; set; } = "";

        private readonly Dictionary<string, string> _aliasToTable = new(StringComparer.OrdinalIgnoreCase);

        // ── Detect CREATE/ALTER VIEW ──
        public override void Visit(CreateViewStatement node)
        {
            IsViewStatement = true;
            ViewName = node.SchemaObjectName?.BaseIdentifier?.Value ?? "";
            ViewSchema = node.SchemaObjectName?.SchemaIdentifier?.Value ?? "dbo";
            base.Visit(node);
        }

        public override void Visit(AlterViewStatement node)
        {
            IsViewStatement = true;
            ViewName = node.SchemaObjectName?.BaseIdentifier?.Value ?? "";
            ViewSchema = node.SchemaObjectName?.SchemaIdentifier?.Value ?? "dbo";
            base.Visit(node);
        }

        // ── Extract SELECT columns ──
        public override void Visit(QuerySpecification node)
        {
            base.Visit(node);

            // SELECT columns
            foreach (var elem in node.SelectElements)
            {
                if (elem is SelectStarExpression star)
                {
                    // SELECT * or SELECT alias.*
                    var table = star.Qualifier?.Identifiers?.LastOrDefault()?.Value ?? "";
                    Columns.Add(new ViewColumnInfo
                    {
                        ColumnName = "*",
                        SourceTable = table,
                        SourceColumn = "*",
                        Expression = string.IsNullOrEmpty(table) ? "*" : $"{table}.*"
                    });
                }
                else if (elem is SelectScalarExpression scalar)
                {
                    string alias = scalar.ColumnName?.Value ?? "";
                    string expression = GetFragmentText(scalar.Expression);
                    string sourceTable = "";
                    string sourceColumn = "";

                    if (scalar.Expression is ColumnReferenceExpression colRef)
                    {
                        var parts = colRef.MultiPartIdentifier?.Identifiers;
                        if (parts != null && parts.Count >= 2)
                        {
                            sourceTable = parts[parts.Count - 2].Value;
                            sourceColumn = parts[parts.Count - 1].Value;
                        }
                        else if (parts != null && parts.Count == 1)
                        {
                            sourceColumn = parts[0].Value;
                        }

                        if (string.IsNullOrEmpty(alias))
                            alias = sourceColumn;
                    }

                    Columns.Add(new ViewColumnInfo
                    {
                        ColumnName = alias,
                        SourceTable = sourceTable,
                        SourceColumn = sourceColumn,
                        Alias = alias,
                        Expression = (sourceTable != "" && sourceColumn != "") ? "" : expression
                    });
                }
            }

            // WHERE
            if (node.WhereClause != null)
            {
                WhereClause = GetFragmentText(node.WhereClause.SearchCondition);
            }

            // GROUP BY
            if (node.GroupByClause != null)
            {
                WhereClause = WhereClause; // already set
                var groupItems = node.GroupByClause.GroupingSpecifications
                    .Select(g => GetFragmentText(g)).ToList();
                GroupByClause = string.Join(", ", groupItems);
            }

            // HAVING
            if (node.HavingClause != null)
            {
                HavingClause = GetFragmentText(node.HavingClause.SearchCondition);
            }

            // ORDER BY
            if (node.OrderByClause != null)
            {
                var orderItems = node.OrderByClause.OrderByElements
                    .Select(o => GetFragmentText(o)).ToList();
                OrderByClause = string.Join(", ", orderItems);
            }
        }

        // ── Extract tables ──
        public override void Visit(NamedTableReference node)
        {
            var tableName = node.SchemaObject.BaseIdentifier?.Value ?? "";
            var schemaName = node.SchemaObject.SchemaIdentifier?.Value ?? "dbo";
            var alias = node.Alias?.Value ?? tableName;

            Tables.Add(new ReferencedTable
            {
                Schema = schemaName,
                Name = tableName,
                Alias = alias
            });

            _aliasToTable[alias] = $"{schemaName}.{tableName}";
        }

        // ── Extract joins ──
        public override void Visit(QualifiedJoin node)
        {
            base.Visit(node);

            string joinType = node.QualifiedJoinType switch
            {
                QualifiedJoinType.Inner => "INNER",
                QualifiedJoinType.LeftOuter => "LEFT",
                QualifiedJoinType.RightOuter => "RIGHT",
                QualifiedJoinType.FullOuter => "FULL",
                _ => "INNER"
            };

            if (node.SearchCondition is BooleanComparisonExpression comparison)
            {
                ExtractJoinColumns(comparison, joinType);
            }
            else if (node.SearchCondition is BooleanBinaryExpression binaryExpr)
            {
                ExtractJoinColumnsRecursive(binaryExpr, joinType);
            }
        }

        private void ExtractJoinColumnsRecursive(BooleanExpression expr, string joinType)
        {
            if (expr is BooleanComparisonExpression comp)
            {
                ExtractJoinColumns(comp, joinType);
            }
            else if (expr is BooleanBinaryExpression bin)
            {
                ExtractJoinColumnsRecursive(bin.FirstExpression, joinType);
                ExtractJoinColumnsRecursive(bin.SecondExpression, joinType);
            }
        }

        private void ExtractJoinColumns(BooleanComparisonExpression comparison, string joinType)
        {
            if (comparison.FirstExpression is ColumnReferenceExpression leftCol &&
                comparison.SecondExpression is ColumnReferenceExpression rightCol)
            {
                var leftParts = leftCol.MultiPartIdentifier?.Identifiers;
                var rightParts = rightCol.MultiPartIdentifier?.Identifiers;

                if (leftParts != null && leftParts.Count >= 2 &&
                    rightParts != null && rightParts.Count >= 2)
                {
                    var leftTable = leftParts[leftParts.Count - 2].Value;
                    var leftColumn = leftParts[leftParts.Count - 1].Value;
                    var rightTable = rightParts[rightParts.Count - 2].Value;
                    var rightColumn = rightParts[rightParts.Count - 1].Value;

                    string leftSchema = "dbo", rightSchema = "dbo";
                    string leftTbl = leftTable, rightTbl = rightTable;

                    if (_aliasToTable.TryGetValue(leftTable, out var resL))
                    {
                        var parts = resL.Split('.');
                        leftSchema = parts[0]; leftTbl = parts[1];
                    }
                    if (_aliasToTable.TryGetValue(rightTable, out var resR))
                    {
                        var parts = resR.Split('.');
                        rightSchema = parts[0]; rightTbl = parts[1];
                    }

                    Joins.Add(new JoinRelationship
                    {
                        LeftTableAlias = leftTable,
                        LeftTableSchema = leftSchema,
                        LeftTableName = leftTbl,
                        LeftColumn = leftColumn,
                        RightTableAlias = rightTable,
                        RightTableSchema = rightSchema,
                        RightTableName = rightTbl,
                        RightColumn = rightColumn,
                        JoinType = joinType
                    });
                }
            }
        }

        /// <summary>
        /// Gets the original SQL text for a fragment by reading its token stream.
        /// </summary>
        private static string GetFragmentText(TSqlFragment fragment)
        {
            if (fragment == null) return "";
            var sb = new System.Text.StringBuilder();
            for (int i = fragment.FirstTokenIndex; i <= fragment.LastTokenIndex; i++)
            {
                sb.Append(fragment.ScriptTokenStream[i].Text);
            }
            return sb.ToString().Trim();
        }
    }
}
