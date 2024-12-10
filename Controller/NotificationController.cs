using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using B2CPlatform.Models;
using B2CPlatform.Services;

namespace B2CPlatform.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationController : ControllerBase
    {
        private readonly NotificationService _notificationService;
        private static readonly Dictionary<string, List<SSEClient>> _clients = new Dictionary<string, List<SSEClient>>();
        private readonly ISSEClientManager _sseClientManager;
        public NotificationController(NotificationService notificationService,ISSEClientManager sseClientManager)
        {
            _notificationService = notificationService;
            _sseClientManager = sseClientManager;
        }

        [HttpGet("subscribe/{userId}")]
        public async Task Subscribe(string userId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return;
            }

            var client = new SSEClient(Response);
            lock (_clients)
            {
                if (!_clients.ContainsKey(userId))
                {
                    _clients[userId] = new List<SSEClient>();
                }
                _clients[userId].Add(client);
            }

            Response.ContentType = "text/event-stream";

            var notifications = await _notificationService.GetNotificationsForUserAsync(userId);
            foreach (var notification in notifications)
            {
                client.Send(notification);
            }

            int lastNotificationId = (int)(notifications.Any() ? notifications.Max(n => n.Id) : 0);

            while (!cancellationToken.IsCancellationRequested) 
            {
                var allNotifications = await _notificationService.GetNotificationsForUserAsync(userId);
                
                var newNotifications = allNotifications.Where(n => n.Id > lastNotificationId);

                foreach (var notification in newNotifications)
                {
                    client.Send(notification);
                    lastNotificationId = (int)notification.Id; // Update the last notification ID
                }

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }

            lock (_clients)
            {
                _clients[userId].Remove(client);
                if (_clients[userId].Count == 0)
                {
                    _clients.Remove(userId);
                }
            }
        }


        [HttpPost]
        [Authorize]
        public async Task<ActionResult<Notification>> CreateNotification(Notification notification)
        {
            await _notificationService.CreateNotificationAsync(notification);
            await _sseClientManager.SendNotificationToUser(notification.UserId, notification);
            return CreatedAtAction(nameof(GetNotifications), new { id = notification.Id }, notification);
        }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult<IEnumerable<Notification>>> GetNotifications()
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value;
            if (userId == null)
            {
                return Unauthorized("User not found. Claim 'userId' is missing.");
            }

            var notifications = await _notificationService.GetNotificationsForUserAsync(userId);
            return Ok(notifications);
        }

        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkNotificationAsRead(int id)
        {
            await _notificationService.MarkNotificationAsReadAsync(id);
            return NoContent();
        }
        
    }
}
