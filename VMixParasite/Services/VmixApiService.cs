namespace VMixParasite.Services;

public class VmixApiService
{
    private readonly HttpClient _httpClient;

    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 8088;

    private string BaseUrl => $"http://{Host}:{Port}/api/";

    public VmixApiService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    }

    public async Task<(bool Success, string Message)> TestConnectionAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(BaseUrl);
            if (response.IsSuccessStatusCode)
                return (true, $"Conectado a vMix en {Host}:{Port}");
            else
                return (false, $"vMix respondió con {response.StatusCode}");
        }
        catch (HttpRequestException ex)
        {
            return (false, $"No se pudo conectar a {Host}:{Port} — {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return (false, $"Timeout conectando a {Host}:{Port} (¿está vMix abierto con Web API activa?)");
        }
        catch (Exception ex)
        {
            return (false, $"Error: {ex.Message}");
        }
    }

    public async Task<string> SetTitleTextAsync(string inputKey, string fieldName, string value)
    {
        var url = $"{BaseUrl}?Function=SetText&Input={Uri.EscapeDataString(inputKey)}&SelectedName={Uri.EscapeDataString(fieldName)}&Value={Uri.EscapeDataString(value)}";
        var response = await _httpClient.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        return $"[{response.StatusCode}] {fieldName}=\"{value}\" → {(response.IsSuccessStatusCode ? "OK" : body)}";
    }

    public async Task StartRecordingAsync()
    {
        await _httpClient.GetAsync($"{BaseUrl}?Function=StartRecording");
    }

    public async Task StopRecordingAsync()
    {
        await _httpClient.GetAsync($"{BaseUrl}?Function=StopRecording");
    }
}
