using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using B2CPlatform.Data;
using B2CPlatform.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.IO;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;

namespace B2CPlatform.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ProductsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Products
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
        {
            return await _context.Products.ToListAsync();
        }

        // GET: api/Products/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProduct(int id)
        {
            try
            {
                var product = await _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Ratings)
                        .ThenInclude(r => r.User) 
                    .FirstOrDefaultAsync(p => p.Id == id);

                return product != null ? Ok(product) : NotFound();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }


        // POST: api/Products
        [HttpPost]
        public async Task<ActionResult<Product>> PostProduct([FromForm] Product product, [FromForm] IFormFile image, [FromForm] string colors, [FromForm] string sizes)
        {
            try
            {
                if (!string.IsNullOrEmpty(colors))
                {
                    product.Colors = JsonConvert.DeserializeObject<List<string>>(colors);
                }

                if (!string.IsNullOrEmpty(sizes))
                {
                    product.Sizes = JsonConvert.DeserializeObject<List<string>>(sizes);
                }

                if (image != null)
                {
                    var uploadsFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                    if (!Directory.Exists(uploadsFolderPath))
                    {
                        Directory.CreateDirectory(uploadsFolderPath);
                    }

                    var fileName = $"{Guid.NewGuid()}_{image.FileName}";
                    var filePath = Path.Combine(uploadsFolderPath, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await image.CopyToAsync(stream);
                    }

                    product.ImageUrl = $"http://localhost:5152/uploads/{fileName}";
                }

                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // PUT: api/Products/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PutProduct(int id, [FromForm] Product product, [FromForm] IFormFile image)
        {
            if (id != product.Id)
            {
                return BadRequest();
            }

            var existingProduct = await _context.Products.FindAsync(id);
            if (existingProduct == null)
            {
                return NotFound();
            }

            // Update product fields
            existingProduct.Name = product.Name;
            existingProduct.Description = product.Description;
            existingProduct.ShortDescription = product.ShortDescription;
            existingProduct.Price = product.Price;
            existingProduct.Stock = product.Stock;
            existingProduct.CategoryId = product.CategoryId;
            existingProduct.Colors = product.Colors;
            existingProduct.Sizes = product.Sizes;

            if (image != null)
            {
                // Delete the old image if it exists
                if (!string.IsNullOrEmpty(existingProduct.ImageUrl))
                {
                    var oldImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", existingProduct.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldImagePath))
                    {
                        System.IO.File.Delete(oldImagePath);
                    }
                }

                var uploadsFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsFolderPath))
                {
                    Directory.CreateDirectory(uploadsFolderPath);
                }

                var fileName = $"{Guid.NewGuid()}_{image.FileName}";
                var filePath = Path.Combine(uploadsFolderPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }

                existingProduct.ImageUrl = $"/uploads/{fileName}";
            }

            _context.Entry(existingProduct).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // DELETE: api/Products/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            // Delete the image file from the server
            if (!string.IsNullOrEmpty(product.ImageUrl))
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", product.ImageUrl.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/Products/Search
        [HttpGet("search")]
        public async Task<ActionResult<PaginatedResult<ProductWithRating>>> SearchProducts(
            [FromQuery] string? nameOrDescription,
            [FromQuery] int[]? categories,
            [FromQuery] decimal? minPrice,
            [FromQuery] decimal? maxPrice,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 12)
        {
            var query = _context.Products
                .Include(p => p.Ratings)
                .AsQueryable();

            if (!string.IsNullOrEmpty(nameOrDescription))
            {
                query = query.Where(p => p.Name.Contains(nameOrDescription) || p.ShortDescription.Contains(nameOrDescription));
            }

            if (categories != null && categories.Any())
            {
                query = query.Where(p => categories.Contains(p.CategoryId));
            }

            if (minPrice.HasValue)
            {
                query = query.Where(p => p.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(p => p.Price <= maxPrice.Value);
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var products = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductWithRating
                {
                    Product = p,
                    AverageRating = p.Ratings.Any() ? p.Ratings.Average(r => r.Score) : 0,
                    TotalRatings = p.Ratings.Count
                })
                .ToListAsync();

            var result = new PaginatedResult<ProductWithRating>
            {
                Items = products,
                TotalItems = totalItems,
                TotalPages = totalPages,
                CurrentPage = page,
                PageSize = pageSize
            };

            return Ok(result);
        }

        public class ProductWithRating
        {
            public Product Product { get; set; }
            public double AverageRating { get; set; }
            public int TotalRatings { get; set; }
        }
        
        public class PaginatedResult<T>
        {
            public List<T> Items { get; set; }
            public int TotalItems { get; set; }
            public int TotalPages { get; set; }
            public int CurrentPage { get; set; }
            public int PageSize { get; set; }
        }

        
        // POST: api/Products/{id}/rate
       [HttpPost("{id}/rate")]
        [Authorize]
        public async Task<IActionResult> RateProduct(int id, [FromBody] RatingRequest ratingRequest)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var product = await _context.Products.Include(p => p.Ratings).FirstOrDefaultAsync(p => p.Id == id);
            if (product == null)
            {
                return NotFound();
            }

            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userId");
            if (userIdClaim == null)
            {
                return Unauthorized("User not found. Claim 'userId' is missing.");
            }
            var userId = userIdClaim.Value;

            // Check if the user has already rated this product
            var existingRating = product.Ratings.FirstOrDefault(r => r.UserId == userId);
            if (existingRating != null)
            {
                // Update the existing rating
                existingRating.Score = ratingRequest.score;
                existingRating.Comment = ratingRequest.comment;

                _context.Ratings.Update(existingRating);
            }
            else
            {
                // Add a new rating
                var newRating = new Rating
                {
                    UserId = userId,
                    ProductId = id,
                    Score = ratingRequest.score,
                    Comment = ratingRequest.comment,
                };

                _context.Ratings.Add(newRating);
            }

            await _context.SaveChangesAsync();

            return Ok();
        }

        public class RatingRequest
        {
            public required int score { get; set; }
            public required string comment { get; set; }
        }
       // PUT: api/Products/{productId}/rate/{ratingId}
        [HttpPut("{productId}/rate/{ratingId}")]
        [Authorize]
        public async Task<IActionResult> UpdateRating(int productId, int ratingId, [FromBody] RatingRequest ratingRequest)
        {
            var rating = await _context.Ratings.FindAsync(ratingId);
            if (rating == null)
            {
                return NotFound();
            }

            if (rating.ProductId != productId)
            {
                return BadRequest("Rating does not belong to the specified product.");
            }

            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userId");
            if (userIdClaim == null)
            {
                return Unauthorized("User not found. Claim 'userId' is missing.");
            }
            var userId = userIdClaim.Value;

            // Check if the user is the owner of the rating
            if (rating.UserId != userId)
            {
                return Forbid("You can only edit your own ratings.");
            }

            rating.Score = ratingRequest.score;
            rating.Comment = ratingRequest.comment;
            rating.CreatedAt = DateTime.UtcNow;

            _context.Entry(rating).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return Ok(rating);
        }

        // DELETE: api/Products/{productId}/rate/{ratingId}
        [HttpDelete("{productId}/rate/{ratingId}")]
        [Authorize]
        public async Task<IActionResult> DeleteRating(int productId, int ratingId)
        {
            var rating = await _context.Ratings.FindAsync(ratingId);
            if (rating == null)
            {
                return NotFound();
            }

            if (rating.ProductId != productId)
            {
                return BadRequest("Rating does not belong to the specified product.");
            }

            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userId");
            if (userIdClaim == null)
            {
                return Unauthorized("User not found. Claim 'userId' is missing.");
            }
            var userId = userIdClaim.Value;

            // Check if the user is the owner of the rating
            if (rating.UserId != userId)
            {
                return Forbid("You can only delete your own ratings.");
            }

            _context.Ratings.Remove(rating);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }
    }
}
