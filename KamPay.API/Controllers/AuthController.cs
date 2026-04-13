using FirebaseAdmin.Auth;
using Microsoft.AspNetCore.Mvc;
using KamPay.API.Services;

namespace KamPay.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        public class LoginRequest
        {
            public string IdToken { get; set; } = string.Empty;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrEmpty(request.IdToken))
            {
                return BadRequest(new { Message = "ID token is missing." });
            }

            try
            {
                var result = await _authService.LoginWithFirebaseAsync(request.IdToken);

                return Ok(new
                {
                    token = result.Token,
                    uid = result.Uid,
                    expiration = result.Expiration
                });
            }
            catch (FirebaseAuthException)
            {
                return Unauthorized(new { Message = "Invalid Firebase ID token." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }
    }
}
