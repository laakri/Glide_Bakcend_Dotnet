using System.Collections.Generic;
using System.Threading.Tasks;
using B2CPlatform.Models;

namespace B2CPlatform.Services
{
    public interface ISSEClientManager
    {
        void AddClient(string userId, SSEClient client);
        void RemoveClient(string userId, SSEClient client);
        Task SendNotificationToUser(string userId, Notification notification);
    }

    public class SSEClientManager : ISSEClientManager
    {
        private readonly Dictionary<string, List<SSEClient>> _clients = new Dictionary<string, List<SSEClient>>();

        public void AddClient(string userId, SSEClient client)
        {
            lock (_clients)
            {
                if (!_clients.ContainsKey(userId))
                {
                    _clients[userId] = new List<SSEClient>();
                }
                _clients[userId].Add(client);
            }
        }

        public void RemoveClient(string userId, SSEClient client)
        {
            lock (_clients)
            {
                if (_clients.ContainsKey(userId))
                {
                    _clients[userId].Remove(client);
                    if (_clients[userId].Count == 0)
                    {
                        _clients.Remove(userId);
                    }
                }
            }
        }

        public async Task SendNotificationToUser(string userId, Notification notification)
        {
            lock (_clients)
            {
                if (_clients.ContainsKey(userId))
                {
                    foreach (var client in _clients[userId])
                    {
                        client.Send(notification);
                    }
                }
            }
            await Task.CompletedTask;
        }
    }
}
