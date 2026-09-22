using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace StoryboardDesigner.App.Services;

internal sealed class HostReadinessProbe : IHostReadinessProbe
{
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _pollInterval;

    public HostReadinessProbe(HttpClient? httpClient = null, TimeSpan? pollInterval = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(150);
    }

    public async Task<HostReadinessResult> WaitUntilReadyAsync(
        Uri hostUri,
        Func<bool> isProcessExited,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var healthUri = new Uri(hostUri, "/health");
        var deadline = DateTime.UtcNow + timeout;
        var lastMessage = "GameHost readiness endpoint did not respond.";

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (isProcessExited())
            {
                return new HostReadinessResult
                {
                    IsReady = false,
                    Message = "GameHost exited before reporting readiness."
                };
            }

            try
            {
                using var response = await _httpClient.GetAsync(healthUri, cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var payload = await response.Content.ReadFromJsonAsync<HealthPayload>(cancellationToken: cancellationToken).ConfigureAwait(false);
                    if (payload?.RuntimeRegistrationReady == true)
                    {
                        return new HostReadinessResult
                        {
                            IsReady = true,
                            Message = "GameHost reported a ready runtime registration source."
                        };
                    }

                    lastMessage = payload?.RuntimeRegistrationFailure
                        ?? "GameHost is running but its runtime registration source is not ready.";
                }
                else
                {
                    lastMessage = $"GameHost readiness returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).";
                }
            }
            catch (HttpRequestException ex)
            {
                lastMessage = ex.Message;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lastMessage = "GameHost readiness request timed out.";
            }

            var remaining = deadline - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining < _pollInterval ? remaining : _pollInterval, cancellationToken).ConfigureAwait(false);
            }
        }

        return new HostReadinessResult
        {
            IsReady = false,
            Message = $"Timed out waiting for GameHost readiness. {lastMessage}"
        };
    }

    private sealed class HealthPayload
    {
        public bool RuntimeRegistrationReady { get; init; }
        public string? RuntimeRegistrationFailure { get; init; }
    }
}
