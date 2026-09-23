using System.Net;
using System.Net.Http.Json;
using Storyboard.Shared.HostContracts.Transport;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class HostReadinessProbeTests
{
    [Fact]
    public async Task WaitUntilReadyAsync_UsesVersionedHealthRoute()
    {
        Uri? requestedUri = null;
        using var httpClient = new HttpClient(new RecordingHandler(request =>
        {
            requestedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { runtimeRegistrationReady = true })
            };
        }));
        var probe = new HostReadinessProbe(httpClient, TimeSpan.Zero);

        var result = await probe.WaitUntilReadyAsync(
            new Uri("http://127.0.0.1:5199"),
            () => false,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.Equal(HostTransportRoutes.Health, requestedUri?.AbsolutePath);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
