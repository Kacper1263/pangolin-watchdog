using System.Text.Json.Serialization;

namespace PangolinWatchdog.DTO.Pangolin;

// Request body for PUT /resource/{id}/rule
public class PangolinCreateRuleRequest
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = "DROP"; // Enum: ACCEPT, DROP, PASS

    [JsonPropertyName("match")]
    public string Match { get; set; } = "IP"; // Enum: CIDR, IP, PATH, COUNTRY

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty; // eg. The IP address

    [JsonPropertyName("priority")]
    public long Priority { get; set; } // must be unique within the resource

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}