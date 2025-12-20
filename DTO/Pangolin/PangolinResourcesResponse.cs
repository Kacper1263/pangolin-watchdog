using System.Text.Json.Serialization;

namespace PangolinWatchdog.DTO.Pangolin;

public class PangolinResourcesResponse
{
    [JsonPropertyName("data")]
    public PangolinResourcesData? Data { get; set; }
    
    [JsonPropertyName("success")]
    public bool Success { get; set; }
}

public class PangolinResourcesData
{
    [JsonPropertyName("resources")]
    public List<PangolinResourceEntry> Resources { get; set; } = new();
}

public class PangolinResourceEntry
{
    [JsonPropertyName("resourceId")]
    public long ResourceId { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("fullDomain")]
    public string FullDomain { get; set; } = string.Empty;
}
