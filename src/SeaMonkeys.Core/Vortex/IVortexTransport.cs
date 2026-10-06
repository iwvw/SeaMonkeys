using System.Net;
using SeaMonkeys.Core.Settings;

namespace SeaMonkeys.Core.Vortex;

public interface IVortexTransport
{
    string BuildUrl(string host, string path);

    Task<string> GetAsync(string url, CancellationToken cancellationToken);

    /// <summary>Returns null for 404 (e.g. a player without a clan) instead of throwing.</summary>
    Task<string?> GetOptionalAsync(string url, CancellationToken cancellationToken);
}

public sealed class HttpVortexTransport : IVortexTransport, IDisposable
{
    private readonly HttpClient http;
    private readonly bool ownsClient;
    private readonly SemaphoreSlim gate;
    private readonly int delayMs;

    public HttpVortexTransport(SeaMonkeysSettings settings, HttpClient? client = null)
    {
        Settings = settings;
        ownsClient = client is null;
        http = client ?? new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds);
        if (!http.DefaultRequestHeaders.Contains("X-Requested-With"))
        {
            http.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        }
        if (!http.DefaultRequestHeaders.Contains("User-Agent"))
        {
            http.DefaultRequestHeaders.Add(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0 Safari/537.36");
        }

        gate = new SemaphoreSlim(Math.Max(1, settings.MaximumParallelRequests));
        delayMs = Math.Max(0, settings.RequestDelayMs);
    }

    public SeaMonkeysSettings Settings { get; }

    /// <summary>请求级全局并发门限：所有端点共用，避免玩家级并发叠加后打爆上游。</summary>
    private async Task<HttpResponseMessage> SendAsync(string url, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            HttpResponseMessage response = await http.GetAsync(url, HttpCompletionOption.ResponseContentRead, cancellationToken);
            try
            {
                if (delayMs > 0)
                {
                    await Task.Delay(delayMs, cancellationToken);
                }
            }
            catch
            {
                response.Dispose();
                throw;
            }

            return response;
        }
        finally
        {
            gate.Release();
        }
    }

    public string BuildUrl(string host, string path)
    {
        if (!string.IsNullOrWhiteSpace(Settings.ProxyBaseUrl))
        {
            return $"{Settings.ProxyBaseUrl.TrimEnd('/')}/{host}{path}";
        }

        return $"https://{host}{path}";
    }

    public async Task<string> GetAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await SendAsync(url, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized ||
                response.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new HttpRequestException("HttpRequestFailed", null, response.StatusCode);
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException("HttpRequestFailed", ex, ex.StatusCode);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new HttpRequestException("HttpRequestFailed", ex);
        }
    }

    public async Task<string?> GetOptionalAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await SendAsync(url, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized ||
                response.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new HttpRequestException("HttpRequestFailed", null, response.StatusCode);
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException("HttpRequestFailed", ex, ex.StatusCode);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new HttpRequestException("HttpRequestFailed", ex);
        }
    }

    public void Dispose()
    {
        if (ownsClient)
        {
            http.Dispose();
        }

        gate.Dispose();
    }
}
