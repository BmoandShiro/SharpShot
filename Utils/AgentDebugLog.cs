using System;
using System.IO;
using System.Text.Json;

namespace SharpShot.Utils
{
    /// <summary>Session debug NDJSON logger for clipboard lag investigation.</summary>
    internal static class AgentDebugLog
    {
        private const string SessionId = "e73b6b";
        private static readonly string LogPath = Path.Combine(
            @"c:\Users\BMO\source\repos\Screenshot Tool",
            "debug-e73b6b.log");

        public static void Write(string hypothesisId, string location, string message, object? data = null, string? runId = null)
        {
            try
            {
                var payload = new
                {
                    sessionId = SessionId,
                    hypothesisId,
                    location,
                    message,
                    data,
                    runId = runId ?? "pre-fix",
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                File.AppendAllText(LogPath, JsonSerializer.Serialize(payload) + "\n");
            }
            catch
            {
                // Ignore logging errors during debug
            }
        }
    }
}
