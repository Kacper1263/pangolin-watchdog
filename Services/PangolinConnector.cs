using System.Net.Http.Json;
using PangolinWatchdog.Data;

namespace PangolinWatchdog.Services;

public class PangolinConnector
{
    private readonly HttpClient _http;
    private readonly ILogger<PangolinConnector> _logger;

    public PangolinConnector(HttpClient http, ILogger<PangolinConnector> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<List<PangolinLogEntry>> FetchLogsAsync(AppConfig config, DateTime start, DateTime end)
    {
        var allLogs = new List<PangolinLogEntry>();
        var limit = 1000;
        var offset = 0;
        var morePagesAvailable = true;

        try
        {
            while (morePagesAvailable)
            {
                var baseUrl = $"{config.PangolinApiUrl.TrimEnd('/')}/org/{config.PangolinOrgId}/logs/request";
                var query = $"?timeStart={start:o}&timeEnd={end:o}&limit={limit}&offset={offset}";

                var request = new HttpRequestMessage(HttpMethod.Get, baseUrl + query);
                AddAuthHeader(request, config);

                var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<PangolinLogResponse>();
                var batch = result?.Data?.Logs ?? new List<PangolinLogEntry>();

                if (batch.Count > 0)
                {
                    allLogs.AddRange(batch);
                }

                if (batch.Count < limit)
                {
                    morePagesAvailable = false;
                }
                else
                {
                    offset += limit;
                }
            }

            return allLogs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch logs from Pangolin API (Offset: {Offset})", offset);
            return allLogs; 
        }
    }

    public async Task BanIpAsync(AppConfig config, string ip, long resourceId, int durationMinutes, string reason)
    {
        try 
        {
            // Get existing rules to find the next priority
            var nextPriority = await GetNextPriorityAsync(config, resourceId);

            var ruleRequest = new PangolinCreateRuleRequest
            {
                Action = "DROP",
                Match = "IP",
                Value = ip,
                Priority = nextPriority,
                Enabled = true
            };

            // Send PUT request
            var url = $"{config.PangolinApiUrl.TrimEnd('/')}/resource/{resourceId}/rule";
            
            var request = new HttpRequestMessage(HttpMethod.Put, url);
            AddAuthHeader(request, config);
            request.Content = JsonContent.Create(ruleRequest);

            var response = await _http.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to ban IP {Ip}. Status: {Status}. Response: {Body}", ip, response.StatusCode, errorBody);
                throw new Exception($"API Error: {response.StatusCode}");
            }

            _logger.LogInformation("Successfully banned IP {Ip} on Resource {ResId} with Priority {Prio}", ip, resourceId, nextPriority);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during BanIpAsync execution");
            throw; // Re-throw so the worker knows it failed
        }
    }

    private async Task<int> GetNextPriorityAsync(AppConfig config, long resourceId)
    {
        var currentPriorityMax = 0;
        var limit = 1000;
        var offset = 0;
        var morePagesAvailable = true;

        while (morePagesAvailable)
        {
            var url = $"{config.PangolinApiUrl.TrimEnd('/')}/resource/{resourceId}/rules?limit={limit}&offset={offset}";
            
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            AddAuthHeader(request, config);

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<PangolinRulesResponse>();
            var rules = result?.Data?.Rules ?? new List<PangolinExistingRule>();

            if (rules.Any())
            {
                // Check max in current batch
                var batchMax = rules.Max(r => r.Priority);
                if (batchMax > currentPriorityMax)
                {
                    currentPriorityMax = batchMax;
                }
            }

            // Logic to decide if we fetch next page
            if (rules.Count < limit)
            {
                // We received fewer items than the limit, so this is the last page
                morePagesAvailable = false;
            }
            else
            {
                // Full page received, likely there is more data
                offset += limit;
            }
        }

        // If no rules were ever found (currentPriorityMax still 0), start at 10.
        // Otherwise, increment the highest found priority.
        return currentPriorityMax == 0 ? 10 : currentPriorityMax + 1;
    }

    private void AddAuthHeader(HttpRequestMessage request, AppConfig config)
    {
        if (!string.IsNullOrEmpty(config.PangolinApiToken))
        {
            request.Headers.Add("Authorization", $"Bearer {config.PangolinApiToken}");
        }
    }
}