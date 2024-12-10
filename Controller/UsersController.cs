using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using B2CPlatform.Data;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Google.Apis.Auth;
using Google.Apis.Auth.OAuth2;

namespace B2CPlatform.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;

        public UsersController(ApplicationDbContext context, IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        [HttpPost("Register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register(User userRequest)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Bad Request.");
            }

            if (EmailExists(userRequest.Email))
            {
                return BadRequest("Email already registered.");
            }

            var newUser = new User
            {
                Id = Guid.NewGuid().ToString(),
                Username = userRequest.Username,
                Password = userRequest.Password,
                Email = userRequest.Email,
                Role = UserRole.Client
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();
        
            return Ok(new { message = "User registered successfully." });

        }

        private bool EmailExists(string email)
        {
            return _context.Users.Any(u => u.Email == email);
        }

        [HttpPost("Login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginRequest userRequest)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == userRequest.Email && u.Password == userRequest.Password);

            if (user == null)
            {
                return Unauthorized("Invalid email or password.");
            }

            var jwtIssuer = _config.GetSection("Jwt:Issuer").Get<string>();
            var jwtKey = _config.GetSection("Jwt:Key").Get<string>();

            if (string.IsNullOrEmpty(jwtIssuer) || string.IsNullOrEmpty(jwtKey))
            {
                return StatusCode(500, "JWT configuration error.");
            }

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("userId", user.Id.ToString()),
                new Claim("userName", user.Username),
                new Claim("userEmail", user.Email),
                new Claim("role", user.Role.ToString())
            };

            var token = new JwtSecurityToken(
                jwtIssuer,
                jwtIssuer,
                claims,
                expires: DateTime.Now.AddMinutes(120),
                signingCredentials: credentials);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new { token = tokenString });
        }
        public class LoginRequest
        {
            public required string Email { get; set; }
            public required string Password { get; set; }
        }



        [HttpPost("GoogleLogin")]
        [AllowAnonymous]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            var payload = await VerifyGoogleToken(request.IdToken);
            if (payload == null)
            {
                return BadRequest("Invalid Google token.");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.GoogleId == payload.Subject);
            if (user == null)
            {
                user = new User
                {
                    Id = Guid.NewGuid().ToString(),
                    Username = payload.GivenName ,
                    Email = payload.Email,
                    Password = payload.Email,
                    GoogleId = payload.Subject,
                    Role = UserRole.Client
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }

            var token = GenerateJwtToken(user);
            return Ok(new { token });
        }

        private async Task<GoogleJsonWebSignature.Payload> VerifyGoogleToken(string idToken)
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings()
            {
                Audience = new List<string>() { _config["GoogleAuthSettings:ClientId"] }
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            return payload;
        }

        private string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("userId", user.Id.ToString()),
                new Claim("userName", user.Username),
                new Claim("userEmail", user.Email),
                new Claim("role", user.Role.ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                _config["Jwt:Issuer"],
                _config["Jwt:Issuer"],
                claims,
                expires: DateTime.Now.AddMinutes(120),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public class GoogleLoginRequest
        {
            public required string IdToken { get; set; }
        }

        
    }


       
}
