using System.Text.Json.Serialization;

namespace PangolinWatchdog.Services;

// LOGS
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


// --- RULES (Management) ---

// Response from GET /resource/{id}/rules
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

public class PangolinExistingRule
{
    [JsonPropertyName("ruleId")]
    public long RuleId { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;
}

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
    public int Priority { get; set; } // must be unique within the resource

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}