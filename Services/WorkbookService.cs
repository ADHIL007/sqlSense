using System;
using System.IO;
using System.Text.Json;
using sqlSense.Models;

namespace sqlSense.Services
{
    /// <summary>
    /// Service responsible for persisting and loading SQLSense workbooks.
    /// handles serialization and file system operations.
    /// </summary>
    public class WorkbookService
    {
        private readonly JsonSerializerOptions _jsonOptions;

        public WorkbookService()
        {
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
        }

        /// <summary>
        /// Persists a view definition to the specified file path.
        /// </summary>
        public void SaveWorkbook(ViewDefinitionInfo workbook, string filePath)
        {
            try
            {
                var json = JsonSerializer.Serialize(workbook, _jsonOptions);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to save workbook: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Loads a view definition from the specified file path.
        /// </summary>
        public ViewDefinitionInfo LoadWorkbook(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("Workbook file not found.", filePath);

                var json = File.ReadAllText(filePath);
                var workbook = JsonSerializer.Deserialize<ViewDefinitionInfo>(json, _jsonOptions);

                if (workbook == null)
                    throw new InvalidDataException("Loaded workbook is null or invalid.");

                return workbook;
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to load workbook: {ex.Message}", ex);
            }
        }
    }
}
