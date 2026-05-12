namespace sqlSense.Services.Ai.Tools
{
    public class ToolResult
    {
        public bool IsSuccess { get; set; }
        public string ResultData { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
        
        public static ToolResult Success(string data) => new ToolResult { IsSuccess = true, ResultData = data };
        public static ToolResult Error(string error) => new ToolResult { IsSuccess = false, ErrorMessage = error };
    }
}
