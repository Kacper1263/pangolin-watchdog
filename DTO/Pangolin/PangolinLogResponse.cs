using System.Text.Json.Serialization;

namespace PangolinWatchdog.DTO.Pangolin;

public class PangolinLogResponse
{
    [JsonPropertyName("data")]
    public PangolinLogData? Data { get; set; }
    
    [JsonPropertyName("success")]
    public bool Success { get; set; }
}

public class PangolinLogData
{
    [JsonPropertyName("log")]
    public List<PangolinLogEntry> Logs { get; set; } = new();
}

// log response entry
public class PangolinLogEntry
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("ip")]
    public string Ip { get; set; } = string.Empty;

    [JsonPropertyName("host")]
    public string Host { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty; 

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("resourceId")]
    public long ResourceId { get; set; }
    
    [JsonPropertyName("resourceName")]
    public string ResourceName { get; set; } = string.Empty;
}