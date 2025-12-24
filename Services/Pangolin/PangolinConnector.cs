using System.Diagnostics;
using PangolinWatchdog.Data;
using PangolinWatchdog.DTO.Pangolin;
using PangolinWatchdog.Helpers.Exceptions;

namespace PangolinWatchdog.Services.Pangolin;

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

    public async Task BanIpAsync(AppConfig config, string ip, long resourceId, WatchdogRule rule)
    {
        try 
        {
            // Get existing rules to find the next priority
            var nextPriority = await GetNextPriorityAsync(config, resourceId, rule.MaxPriority);
            
            // Check if the nextPriority exceeds maxPriority. If so, we will disable the rule and add a new problem.
            if(nextPriority >= rule.MaxPriority)
            {
                throw new MaxPriorityReachedException(
                    $"Next priority exceeds or equals MaxPriority {rule.MaxPriority.Value} for rule \"{rule.Name}\" (ID: {rule.Id}). " +
                    $"Cannot ban IP {ip} on resource  \"{rule.TargetResource?.Name ?? "N/A"}\". Rule has been disabled.");
            }

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

            if(Debugger.IsAttached)
                Debugger.Break();
            
            var response = await _http.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to ban IP {Ip}. Status: {Status}. Response: {Body}", ip, response.StatusCode, errorBody);
                throw new Exception($"API Error: {response.StatusCode}");
            }

            _logger.LogInformation("Successfully banned IP {Ip} on Resource {ResId} with Priority {Prio}", ip, resourceId, nextPriority);
        }
        catch (MaxPriorityReachedException)
        {
            throw; // Re-throw to be handled by caller
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during BanIpAsync execution");
            throw; // Re-throw so the worker knows it failed
        }
    }

    public async Task<List<PangolinExistingRule>> GetRulesAsync(AppConfig config, long resourceId)
    {
        var allRules = new List<PangolinExistingRule>();
        var limit = 1000;
        var offset = 0;
        var morePagesAvailable = true;

        try
        {
            while (morePagesAvailable)
            {
                var url = $"{config.PangolinApiUrl.TrimEnd('/')}/resource/{resourceId}/rules?limit={limit}&offset={offset}";
                
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                AddAuthHeader(request, config);

                var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<PangolinRulesResponse>();
                var batch = result?.Data?.Rules ?? new List<PangolinExistingRule>();

                if (batch.Count > 0)
                {
                    allRules.AddRange(batch);
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

            return allRules;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch rules from Pangolin API (Offset: {Offset})", offset);
            return allRules;
        }
    }

    public async Task<bool> DeleteRuleAsync(AppConfig config, long resourceId, long ruleId)
    {
        try
        {
            var url = $"{config.PangolinApiUrl.TrimEnd('/')}/resource/{resourceId}/rule/{ruleId}";
            
            var request = new HttpRequestMessage(HttpMethod.Delete, url);
            AddAuthHeader(request, config);

            var response = await _http.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to delete rule {RuleId}. Status: {Status}. Response: {Body}", ruleId, response.StatusCode, errorBody);
                return false;
            }

            _logger.LogInformation("Successfully deleted rule {RuleId} from Resource {ResId}", ruleId, resourceId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during DeleteRuleAsync execution");
            return false;
        }
    }

    private async Task<long> GetNextPriorityAsync(AppConfig config, long resourceId, long? maxRulePriority)
    {
        var rules = await GetRulesAsync(config, resourceId);
        
        if (!rules.Any())
        {
            return 10;
        }
        
        // If maxRulePriority is set, ensure we do not exceed it. We take numbers lower than maxRulePriority.
        // We ignore everything equal or higher than maxRulePriority. Then we add 1 and if we are equal or higher than maxRulePriority,
        // we disable the rule.
        if (maxRulePriority.HasValue)
        {
            var filteredRules = rules.Where(r => r.Priority < maxRulePriority.Value).ToList();
            if (filteredRules.Count == 0)
            {
                return 10;
            }
            var maxPriorityFiltered = filteredRules.Max(r => r.Priority);
            return maxPriorityFiltered + 1;
        }

        var maxPriority = rules.Max(r => r.Priority);
        return maxPriority + 1;
    }

    public async Task<List<PangolinResourceEntry>> GetResourcesAsync(AppConfig config)
    {
        var allResources = new List<PangolinResourceEntry>();
        var limit = 1000;
        var offset = 0;
        var morePagesAvailable = true;

        try
        {
            while (morePagesAvailable)
            {
                var url = $"{config.PangolinApiUrl.TrimEnd('/')}/org/{config.PangolinOrgId}/resources?limit={limit}&offset={offset}";
                
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                AddAuthHeader(request, config);

                var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<PangolinResourcesResponse>();
                var batch = result?.Data?.Resources ?? new List<PangolinResourceEntry>();

                if (batch.Count > 0)
                {
                    allResources.AddRange(batch);
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

            return allResources;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch resources from Pangolin API (Offset: {Offset})", offset);
            throw;
        }
    }

    public async Task<List<PangolinOrgEntry>> GetOrgsAsync(AppConfig config)
    {
        var allOrgs = new List<PangolinOrgEntry>();
        var limit = 1000;
        var offset = 0;
        var morePagesAvailable = true;

        try
        {
            while (morePagesAvailable)
            {
                var url = $"{config.PangolinApiUrl.TrimEnd('/')}/orgs?limit={limit}&offset={offset}";
                
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                AddAuthHeader(request, config);

                var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<PangolinOrgsResponse>();
                var batch = result?.Data?.Orgs ?? new List<PangolinOrgEntry>();

                if (batch.Count > 0)
                {
                    allOrgs.AddRange(batch);
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

            return allOrgs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch organizations from Pangolin API (Offset: {Offset})", offset);
            throw;
        }
    }

    private void AddAuthHeader(HttpRequestMessage request, AppConfig config)
    {
        if (!string.IsNullOrEmpty(config.PangolinApiToken))
        {
            request.Headers.Add("Authorization", $"Bearer {config.PangolinApiToken}");
        }
    }
}