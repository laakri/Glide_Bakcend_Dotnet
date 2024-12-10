using Microsoft.AspNetCore.Http;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace B2CPlatform
{
    public class SSEClient
    {
        private readonly HttpResponse _response;

        public SSEClient(HttpResponse response)
        {
            _response = response;
        }

        
        public async Task Listen(CancellationToken cancellationToken)
        {
            try
            {
                await _response.Body.FlushAsync(cancellationToken);
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                // Client disconnected
            }
        }

        public async void Send(Notification notification)
        {
            try
            {
                if (!_response.HasStarted)
                {
                    _response.ContentType = "text/event-stream";
                }

                await _response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(notification)}\n\n");
                await _response.Body.FlushAsync();
            }
            catch
            {
                // Ignore errors if the client disconnected
            }
        }
    }
}
