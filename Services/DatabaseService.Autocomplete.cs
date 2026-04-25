using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace sqlSense.Services
{
    public partial class DatabaseService
    {
        public async Task<List<string>> GetAutocompleteSuggestionsAsync(string database, string prefix)
        {
            var suggestions = new List<string>();
            var connStr = ChangeDatabaseInConnectionString(_connectionString, database);
            using var conn = new SqlConnection(connStr);
            try
            {
                await conn.OpenAsync();
                
                string sql = @"
                    SELECT TOP 20 name, '2' as icon FROM sys.schemas WHERE name LIKE @prefix + '%'
                    UNION
                    SELECT TOP 20 name, 
                           CASE 
                               WHEN type = 'U' THEN '3'
                               WHEN type = 'V' THEN '4'
                               ELSE '5' 
                           END as icon 
                    FROM sys.objects WHERE type IN ('U','V','P','FN','IF','TF') AND name LIKE @prefix + '%' AND is_ms_shipped = 0
                    UNION
                    SELECT TOP 20 name, '1' as icon FROM sys.databases WHERE name LIKE @prefix + '%'
                ";
                
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@prefix", prefix);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    suggestions.Add($"{reader.GetString(0)}?{reader.GetString(1)}");
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"GetAutocompleteSuggestionsAsync failed for {database} prefix {prefix}", ex);
            }
            return suggestions;
        }

        public async Task<List<string>> GetContextualSuggestionsAsync(string database, string schema)
        {
            var suggestions = new List<string>();
            var connStr = ChangeDatabaseInConnectionString(_connectionString, database);
            using var conn = new SqlConnection(connStr);
            try
            {
                await conn.OpenAsync();
                
                string sql = @"
                    SELECT TOP 200 name, 
                           CASE 
                               WHEN type = 'U' THEN '3'
                               WHEN type = 'V' THEN '4'
                               ELSE '5' 
                           END as icon 
                    FROM sys.objects 
                    WHERE type IN ('U','V','P','FN','IF','TF') 
                      AND schema_id = SCHEMA_ID(@schema) 
                      AND is_ms_shipped = 0
                ";
                
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@schema", schema);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    suggestions.Add($"{reader.GetString(0)}?{reader.GetString(1)}");
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"GetContextualSuggestionsAsync failed for {database}.{schema}", ex);
            }
            return suggestions;
        }
    }
}
