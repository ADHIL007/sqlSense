using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace sqlSense.Services.Ai
{
    public class ChatMessage
    {
        public string Role { get; set; } // "user", "assistant", "tool"
        public string Content { get; set; }
        public string Thinking { get; set; }
        public string ToolName { get; set; }
        public JArray ToolCalls { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ChatSession
    {
        public string SessionId { get; set; }
        public string Title { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        [JsonIgnore]
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }

    public static class ChatSessionManager
    {
        private static readonly string SessionDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "sqlSense", "sessions");

        public static ChatSession CurrentSession { get; set; }

        static ChatSessionManager()
        {
            if (!Directory.Exists(SessionDir))
                Directory.CreateDirectory(SessionDir);
        }

        public static ChatSession CreateNewSession()
        {
            CurrentSession = new ChatSession
            {
                SessionId = "session-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "-" + Guid.NewGuid().ToString().Substring(0, 4),
                Title = "New Chat",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            return CurrentSession;
        }

        public static void AddMessage(string role, string content, string thinking = null, JArray toolCalls = null, string toolName = null)
        {
            if (CurrentSession == null)
            {
                CreateNewSession();
            }

            var msg = new ChatMessage
            {
                Role = role,
                Content = content,
                Thinking = thinking,
                ToolCalls = toolCalls,
                ToolName = toolName,
                Timestamp = DateTime.UtcNow
            };
            
            CurrentSession.Messages.Add(msg);
            CurrentSession.UpdatedAt = DateTime.UtcNow;

            if (CurrentSession.Title == "New Chat" && role == "user")
            {
                CurrentSession.Title = content.Length > 45 ? content.Substring(0, 45) + "..." : content;
            }

            AppendMessageToFile(CurrentSession.SessionId, msg);
            SaveSessionMeta(CurrentSession);
        }

        private static void AppendMessageToFile(string sessionId, ChatMessage msg)
        {
            try
            {
                string path = Path.Combine(SessionDir, $"{sessionId}.jsonl");
                var line = JsonConvert.SerializeObject(new { type = "message", message = msg });
                File.AppendAllText(path, line + Environment.NewLine);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ChatSessionManager] Error appending message: {ex.Message}");
            }
        }

        private static void SaveSessionMeta(ChatSession session)
        {
            try
            {
                string tempPath = Path.Combine(SessionDir, $"{session.SessionId}.meta.tmp");
                string path = Path.Combine(SessionDir, $"{session.SessionId}.meta");
                File.WriteAllText(tempPath, JsonConvert.SerializeObject(session));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tempPath, path);
            }
            catch { }
        }

        public static List<ChatSession> GetRecentSessions()
        {
            var sessions = new List<ChatSession>();
            try
            {
                foreach (var file in Directory.GetFiles(SessionDir, "*.meta"))
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var session = JsonConvert.DeserializeObject<ChatSession>(json);
                        if (session != null) sessions.Add(session);
                    }
                    catch { }
                }
            }
            catch { }
            
            return sessions.OrderByDescending(s => s.UpdatedAt).ToList();
        }

        public static ChatSession LoadSession(string sessionId)
        {
            try
            {
                string metaPath = Path.Combine(SessionDir, $"{sessionId}.meta");
                string path = Path.Combine(SessionDir, $"{sessionId}.jsonl");
                
                if (File.Exists(metaPath) && File.Exists(path))
                {
                    var session = JsonConvert.DeserializeObject<ChatSession>(File.ReadAllText(metaPath));
                    session.Messages = new List<ChatMessage>();
                    
                    var lines = File.ReadAllLines(path);
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var obj = JObject.Parse(line);
                        if (obj["type"]?.ToString() == "message")
                        {
                            var msg = obj["message"].ToObject<ChatMessage>();
                            session.Messages.Add(msg);
                        }
                    }
                    CurrentSession = session;
                    return session;
                }
            }
            catch { }
            return null;
        }
        
        public static void DeleteSession(string sessionId)
        {
            try
            {
                string metaPath = Path.Combine(SessionDir, $"{sessionId}.meta");
                string path = Path.Combine(SessionDir, $"{sessionId}.jsonl");
                
                if (File.Exists(metaPath)) File.Delete(metaPath);
                if (File.Exists(path)) File.Delete(path);
                
                if (CurrentSession?.SessionId == sessionId) CurrentSession = null;
            }
            catch { }
        }
        
        public static string GetRelativeTime(DateTime time)
        {
            var span = DateTime.UtcNow - time;
            if (span.TotalDays >= 7) return $"{(int)(span.TotalDays / 7)} wk{(span.TotalDays >= 14 ? "s" : "")} ago";
            if (span.TotalDays >= 1) return $"{(int)span.TotalDays} day{((int)span.TotalDays > 1 ? "s" : "")} ago";
            if (span.TotalHours >= 1) return $"{(int)span.TotalHours} hr{((int)span.TotalHours > 1 ? "s" : "")} ago";
            if (span.TotalMinutes >= 1) return $"{(int)span.TotalMinutes} min{((int)span.TotalMinutes > 1 ? "s" : "")} ago";
            return "Just now";
        }
    }
}
