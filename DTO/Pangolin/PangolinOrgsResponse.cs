using System.Text.Json.Serialization;

namespace PangolinWatchdog.DTO.Pangolin;

public class PangolinOrgsResponse
{
    [JsonPropertyName("data")]
    public PangolinOrgsData? Data { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public bool Error { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }
}

public class PangolinOrgsData
{
    [JsonPropertyName("orgs")]
    public List<PangolinOrgEntry> Orgs { get; set; } = new();

    [JsonPropertyName("pagination")]
    public PangolinPagination? Pagination { get; set; }
}

public class PangolinOrgEntry
{
    [JsonPropertyName("orgId")]
    public string OrgId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("subnet")]
    public string? Subnet { get; set; }

    [JsonPropertyName("utilitySubnet")]
    public string? UtilitySubnet { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public class PangolinPagination
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }
}
