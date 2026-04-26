using System;
using System.Text;

namespace sqlSense.Services.Ai
{
    public static class PromptBuilder
    {
        public static string BuildPrompt(
            string userMessage,
            bool isFastMode,
            string dbType = "MSSQL",
            string? schemaContext = null,
            string? userContext = null)
        {
            var sb = new StringBuilder();

            var systemInstruction = new SystemInstruction();
            sb.AppendLine(systemInstruction.GetSystemInstruction());
            sb.AppendLine();
            sb.AppendLine("# User Request");
            sb.AppendLine(userMessage);

            return sb.ToString();
        }
    }
}