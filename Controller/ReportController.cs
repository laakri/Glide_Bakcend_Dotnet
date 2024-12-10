using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using B2CPlatform.Models;
using B2CPlatform.Data;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace B2CPlatform.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Authorize(Roles = "Client,Admin,Delivery")]
        public async Task<IActionResult> CreateReport([FromBody] ReportRequest reportRequest)
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

            var order = await _context.Orders.FindAsync(reportRequest.OrderId);
            if (order == null)
            {
                return NotFound("Order not found.");
            }

            // Check if a report for the same order by the same user already exists
            var existingReport = await _context.Reports
                .FirstOrDefaultAsync(r => r.OrderId == reportRequest.OrderId && r.UserId == userId);

            if (existingReport != null)
            {
                return BadRequest("You have already created a report for this order.");
            }

            var newReport = new Report
            {
                UserId = userId,
                OrderId = reportRequest.OrderId,
                Description = reportRequest.Description,
                Status = ReportStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _context.Reports.Add(newReport);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Report created successfully." });
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<ReportDto>>> GetReports()
        {
            var reports = await _context.Reports
                .Include(r => r.User)
                .Include(r => r.Order)
                .Select(r => new ReportDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    UserEmail = r.User.Email,
                    OrderId = r.OrderId,
                    Description = r.Description,
                    Status = (int)r.Status,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync();

            if (reports == null || !reports.Any())
            {
                return NotFound("No reports found.");
            }

            return Ok(reports);
        }

        public class ReportDto
        {
            public int Id { get; set; }
            public string UserId { get; set; }
            public string UserEmail { get; set; }
            public int OrderId { get; set; }
            public string Description { get; set; }
            public int Status { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        public class UserDto
        {
            public string Id { get; set; }
            public string Email { get; set; }
        }

        public class OrderDto
        {
            public int Id { get; set; }
            public DateTime Date { get; set; }
            public int Status { get; set; }
            public decimal Total { get; set; }
        }

        [HttpPut("{reportId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateReportStatus(int reportId, [FromBody] UpdateReportStatusRequest statusRequest)
        {
            var report = await _context.Reports.FindAsync(reportId);
            if (report == null)
            {
                return NotFound("Report not found.");
            }

            report.Status = statusRequest.Status;
            _context.Reports.Update(report);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Report status updated successfully." });
        }

        public class ReportRequest
        {
            public int OrderId { get; set; }
            public required string Description { get; set; }
        }

        public class UpdateReportStatusRequest
        {
            public ReportStatus Status { get; set; }
        }
    }
}
