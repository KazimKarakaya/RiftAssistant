using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Net.Http;

namespace RiftAssistant.Core;

public sealed class LcuClient : IDisposable
{
    private HttpClient? _httpClient;

    public bool IsConnected => _httpClient != null;

    public void Connect(int port, string password, string protocol = "https")
    {
        Disconnect();

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

        _httpClient = new HttpClient(handler)
        {
            BaseAddress =
                new Uri($"{protocol}://127.0.0.1:{port}"),

            Timeout = TimeSpan.FromSeconds(2)
        };

        string authValue = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"riot:{password}")
        );

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", authValue);
    }

    public async Task<string> GetStringAsync(string endpoint)
    {
        EnsureConnected();

        using var response = await _httpClient!.GetAsync(endpoint);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
    public async Task<HttpResponseMessage> PatchJsonAsync(
    string endpoint,
    string json)
    {
        EnsureConnected();

        var content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json"
        );

        var request = new HttpRequestMessage(
            HttpMethod.Patch,
            endpoint
        )
        {
            Content = content
        };

        return await _httpClient!.SendAsync(request);
    }
    public async Task<HttpResponseMessage> PostAsync(string endpoint)
    {
        EnsureConnected();

        return await _httpClient!.PostAsync(endpoint, null);
    }

    private void EnsureConnected()
    {
        if (_httpClient == null)
            throw new InvalidOperationException(
                "LCU bağlantısı kurulmamış."
            );
    }

    public void Disconnect()
    {
        _httpClient?.Dispose();
        _httpClient = null;
    }

    public void Dispose()
    {
        Disconnect();
    }
}
