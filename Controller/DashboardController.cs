using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using B2CPlatform.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetDashboardStats()
    {
        var totalOrders = await _context.Orders.CountAsync();
        var totalProducts = await _context.Products.CountAsync();
        var totalUsers = await _context.Users.CountAsync();
        var totalRevenue = await _context.Orders.SumAsync(o => o.Total);

        var ordersByStatus = await _context.Orders
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var topProducts = await _context.OrderItems
            .GroupBy(oi => oi.ProductId)
            .Select(g => new { ProductId = g.Key, TotalSold = g.Sum(oi => oi.Quantity) })
            .OrderByDescending(x => x.TotalSold)
            .Take(5)
            .Join(_context.Products, 
                  tp => tp.ProductId, 
                  p => p.Id, 
                  (tp, p) => new { p.Name, tp.TotalSold })
            .ToListAsync();

        var recentOrders = await _context.Orders
            .OrderByDescending(o => o.Date)
            .Take(5)
            .Select(o => new { o.Id, o.Date, o.Total, o.Status })
            .ToListAsync();

        var productsByCategory = await _context.Products
            .GroupBy(p => p.Category.Name)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToListAsync();

        // Modified monthly sales query
        var monthlySales = await _context.Orders
            .GroupBy(o => new { o.Date.Year, o.Date.Month })
            .Select(g => new { 
                Year = g.Key.Year,
                Month = g.Key.Month,
                Total = g.Sum(o => o.Total) 
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .Take(12)
            .ToListAsync();

        var processedMonthlySales = monthlySales.Select(ms => new {
            Date = new DateTime(ms.Year, ms.Month, 1),
            ms.Total
        }).ToList();

        return Ok(new
        {
            TotalOrders = totalOrders,
            TotalProducts = totalProducts,
            TotalUsers = totalUsers,
            TotalRevenue = totalRevenue,
            
            OrdersByStatus = ordersByStatus,
            TopProducts = topProducts,
            RecentOrders = recentOrders,
            ProductsByCategory = productsByCategory,
            MonthlySales = processedMonthlySales
        });
    }
}