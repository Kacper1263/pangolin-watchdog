using System.Text.Json.Serialization;

namespace PangolinWatchdog.DTO.Pangolin;

public class PangolinRulesResponse
{
    [JsonPropertyName("data")]
    public PangolinRulesData? Data { get; set; }
    
    [JsonPropertyName("success")]
    public bool Success { get; set; }
}

public class PangolinRulesData
{
    // Assuming the API returns a list called "rules" or similar inside data. 
    // Based on "log" pattern, it's likely "rules".
    [JsonPropertyName("rules")]
    public List<PangolinExistingRule> Rules { get; set; } = new();
}

// Response from GET /resource/{id}/rules
public class PangolinExistingRule
{
    [JsonPropertyName("ruleId")]
    public long RuleId { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;
    
    [JsonPropertyName("match")]
    public string Match { get; set; } = string.Empty;
    
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}