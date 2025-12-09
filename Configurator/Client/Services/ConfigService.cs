using System.Net.Http.Json;
using Configurator.Shared;

namespace Configurator.Client.Services;

public interface IConfigService
{
    Task<List<ConfigGroup>> GetSchemaAsync();
    Task<bool> CheckPythonAsync();
    Task SaveConfigAsync(List<MarlinSetting> settings);
}

public class ConfigService : IConfigService
{
    private readonly HttpClient _httpClient;

    public ConfigService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<ConfigGroup>> GetSchemaAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<ConfigGroup>>("api/Schema") ?? new List<ConfigGroup>();
    }

    public async Task<bool> CheckPythonAsync()
    {
        return await _httpClient.GetFromJsonAsync<bool>("api/Schema/check-python");
    }

    public async Task SaveConfigAsync(List<MarlinSetting> settings)
    {
        var response = await _httpClient.PostAsJsonAsync("api/Config", settings);
        response.EnsureSuccessStatusCode();
    }
}
