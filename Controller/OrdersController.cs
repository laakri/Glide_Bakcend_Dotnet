using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using B2CPlatform.Models;
using B2CPlatform.Data;
using System.Linq;
using System.Threading.Tasks;
using System;
using B2CPlatform.Services;

namespace B2CPlatform.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notificationService;
        private readonly ISSEClientManager _sseClientManager;


        public OrdersController(
         ApplicationDbContext context,
         NotificationService notificationService,
         ISSEClientManager sseClientManager
         )
        {
            _context = context;
            _notificationService = notificationService;
    _sseClientManager = sseClientManager;

        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateOrder([FromBody] OrderRequest orderRequest)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userId");
            if (userIdClaim == null)
            {
                return Unauthorized("User not found. Claim 'userId' is missing.");
            }

            var userId = userIdClaim.Value;

            var newOrder = new Order
            {
                Date = DateTime.Now,
                Status = OrderStatus.Pending,
                Total = orderRequest.Total,
                UserId = userId, 
                FullName = orderRequest.FullName,
                Email = orderRequest.Email,
                Phone = orderRequest.Phone,
                Address = orderRequest.Address,
                City = orderRequest.City,
                PostalCode = orderRequest.PostalCode,
                OrderItems = orderRequest.Items.Select(item => new OrderItem
                {
                    ProductId = item.Product.Id,
                    Quantity = item.Quantity
                }).ToList()
            };

            _context.Orders.Add(newOrder);
            await _context.SaveChangesAsync();

            var userNotification = new Notification
            {
                UserId = userId,
                Title = "Order Created",
                Message = $"Your order with ID {newOrder.Id} has been created successfully.",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
            await _notificationService.CreateNotificationAsync(userNotification);

            var adminUsers = _context.Users.Where(u => u.Role == UserRole.Admin).ToList();

            var adminNotifications = adminUsers.Select(admin => new Notification
            {
                UserId = admin.Id,
                Title = "New Order Created",
                Message = $"A new order with ID {newOrder.Id} has been created.",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            foreach (var notification in adminNotifications)
            {
                await _notificationService.CreateNotificationAsync(notification);
            }

            return Ok(new { message = "Order created successfully." });
        }

        [HttpGet("user/{userId}")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrdersByUserId(string userId)
        {
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .Where(o => o.UserId == userId)
                .ToListAsync();

            if (orders == null || !orders.Any())
            {
                return NotFound("No orders found for the user.");
            }

            return Ok(orders);
        }

        [HttpGet("{orderId}")]
        [Authorize]
        public async Task<ActionResult<Order>> GetOrderById(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                return NotFound("Order not found.");
            }

            return Ok(order);
        }

        [HttpGet("admin/orders")]
        [Authorize(Roles = "Admin")]

        public async Task<ActionResult<IEnumerable<OrderAdminDto>>> GetOrdersForAdmin()
        {
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                .Select(o => new OrderAdminDto
                {
                    Id = o.Id,
                    Date = o.Date,
                    Status = o.Status.ToString(),
                    Total = o.OrderItems.Sum(oi => oi.Product.Price * oi.Quantity),
                    ItemCount = o.OrderItems.Count
                })
                .ToListAsync();

            if (orders == null || !orders.Any())
            {
                return NotFound("No orders found.");
            }

            return Ok(orders);
        }

        [HttpPut("{orderId}/status")]
        [Authorize(Roles = "Admin")]

        public async Task<IActionResult> UpdateOrderStatus(int orderId, [FromBody] UpdateOrderStatusRequest statusRequest)

        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
            {
                return NotFound("Order not found.");
            }
            var oldStatus = order.Status;

            order.Status = statusRequest.Status;
            _context.Orders.Update(order);
            var result = await _context.SaveChangesAsync();
            if (result == 0)
            {
                return StatusCode(500, "Failed to update the order status.");
            }

            if (oldStatus != statusRequest.Status)
            {
                var notification = new Notification
                {
                    UserId = order.UserId,
                    Title = "Order Status Changed",
                    Message = $"Your order with ID {order.Id} has been updated to {statusRequest.Status}.",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };
                await _notificationService.CreateNotificationAsync(notification);

                if (statusRequest.Status == OrderStatus.Processing || statusRequest.Status == OrderStatus.Delivered )
                {
                    if (statusRequest.Status == OrderStatus.Processing || statusRequest.Status == OrderStatus.Delivered)
                    {
                        var deliveryUsers = _context.Users.Where(u => u.Role == UserRole.Delivery).ToList();
                        var deliveryNotifications = deliveryUsers.Select(deliveryUser => new Notification
                        {
                            UserId = deliveryUser.Id,
                            Title = "Order Status Changed",
                            Message = $"An order with ID {order.Id} has been updated to {statusRequest.Status}.",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        }).ToList();

                        foreach (var deliveryNotification in deliveryNotifications)
                        {
                            await _notificationService.CreateNotificationAsync(deliveryNotification);
                        }
                    }

                    var adminUsers = _context.Users.Where(u => u.Role == UserRole.Admin).ToList();
                    var adminNotifications = adminUsers.Select(adminUser => new Notification
                    {
                        UserId = adminUser.Id,
                        Title = "Order Status Changed",
                        Message = $"An order with ID {order.Id} has been updated to {statusRequest.Status}.",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    }).ToList();

                    foreach (var adminNotification in adminNotifications)
                    {
                        await _notificationService.CreateNotificationAsync(adminNotification);
                    }
                }
            }
            return Ok(new { message = "Order status updated successfully." });
        }

        public class UpdateOrderStatusRequest
        {
            public OrderStatus Status { get; set; }
        }

        [HttpGet("delivery/orders")]
        [Authorize(Roles = "Delivery,Admin")]

        public async Task<ActionResult<IEnumerable<object>>> GetOrdersForDelivery()
        {
            var orders = await _context.Orders
                .Where(o => o.Status == OrderStatus.Processing || o.Status == OrderStatus.ReadyForPickup)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .ToListAsync();

            if (orders == null || !orders.Any())
            {
                return NotFound("No orders found.");
            }

            var deliveryOrders = orders.Select(o => new
            {
                o.Id,
                o.Status,
                o.FullName,
                o.Date,
                o.Address,
                o.City,
                o.PostalCode,
                o.UserId,
                TotalItems = o.OrderItems.Sum(oi => oi.Quantity),
                Total = o.OrderItems.Sum(oi => oi.Product.Price * oi.Quantity),
                OrderItems = o.OrderItems.Select(oi => new
                {
                    oi.Product.Name,
                    oi.Quantity,
                    oi.Product.Price,
                    Subtotal = oi.Quantity * oi.Product.Price
                })
            });

            return Ok(deliveryOrders);
        }



        [HttpPut("{orderId}/readyforpickup")]
        [Authorize]
        public async Task<IActionResult> MarkOrderAsReadyForPickup(int orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
            {
                return NotFound("Order not found.");
            }

            if (order.Status != OrderStatus.Processing)
            {
                return BadRequest("Order status is not valid for this operation.");
            }
            var oldStatus = order.Status; 

            order.Status = OrderStatus.ReadyForPickup;
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();

            if (oldStatus != order.Status)
            {
                var notification = new Notification
                {
                    UserId = order.UserId,
                    Title = "Order Status Changed",
                    Message = $"Order marked as ready for pickup successfully .",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };
                await _notificationService.CreateNotificationAsync(notification);
            }
            return Ok(new { message = "Order marked as ready for pickup successfully." });
        }

        [HttpPost]
        [Route("verify-qr")]
        [Authorize(Roles = "Client,Admin,Delivery")]
        public async Task<IActionResult> VerifyQrCode([FromBody] QrCodeVerificationRequest request)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userId");
            if (userIdClaim == null)
            {
                return Unauthorized("User not found. Claim 'userId' is missing.");
            }

            var tokenUserId = userIdClaim.Value;

            if (tokenUserId != request.UserId)
            {
                return BadRequest("User ID mismatch.");
            }

            var order = await _context.Orders.FindAsync(request.OrderId);
            if (order == null)
            {
                return NotFound("Order not found.");
            }

            if (order.UserId != request.UserId)
            {
                return BadRequest("Order does not belong to the user.");
            }

            if (order.Status != OrderStatus.ReadyForPickup)
            {
                return BadRequest("Order status is not valid for this operation.");
            }
            var oldStatus = order.Status; 

            order.Status = OrderStatus.Delivered;
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();
            if (oldStatus != order.Status)
            {
                var notification = new Notification
                {
                    UserId = order.UserId,
                    Title = "Order Status Changed",
                    Message = $"Order status updated to Delivered successfully.",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };
                await _notificationService.CreateNotificationAsync(notification);
            }
            return Ok(new { message = "Order status updated to Delivered successfully." });
        }
        public class QrCodeVerificationRequest
        {
            public string UserId { get; set; }
            public int OrderId { get; set; }
        }
        
    }

    public class OrderRequest
{
    public decimal Total { get; set; }
    public required string FullName { get; set; }
    public required string Email { get; set; }
    public required string Phone { get; set; }
    public required string Address { get; set; }
    public required string City { get; set; }
    public required string PostalCode { get; set; }
    public List<OrderItemRequest> Items { get; set; }
}

    public class OrderItemRequest
    {
        public required Product Product { get; set; }
        public int Quantity { get; set; }
    }
    public class OrderAdminDto
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; }
        public decimal Total { get; set; }
        public int ItemCount { get; set; }
    }

}
