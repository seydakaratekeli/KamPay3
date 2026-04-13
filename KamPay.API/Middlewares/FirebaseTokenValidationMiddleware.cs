using FirebaseAdmin.Auth;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace KamPay.API.Middlewares
{
    public class FirebaseTokenValidationMiddleware
    {
        private readonly RequestDelegate _next;

        public FirebaseTokenValidationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

            if (authHeader != null && authHeader.StartsWith("Bearer "))
            {
                var token = authHeader.Substring("Bearer ".Length).Trim();
                
                try
                {
                    // Validate Firebase ID token using FirebaseAdmin SDK
                    FirebaseToken decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(token);

                    // Extract uid and custom claims
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, decodedToken.Uid),
                        new Claim("uid", decodedToken.Uid)
                    };

                    // Add custom claims from Firebase if they exist
                    foreach (var claim in decodedToken.Claims)
                    {
                        claims.Add(new Claim(claim.Key, claim.Value.ToString()));
                    }

                    // Inject into HttpContext (Set the User principal)
                    var identity = new ClaimsIdentity(claims, "Firebase");
                    context.User = new ClaimsPrincipal(identity);
                }
                catch (FirebaseAuthException)
                {
                    // If validation fails, it might not be a Firebase token.
                    // It could be the "Custom API JWT" we generate later! 
                    // So we do not immediately return Unauthorized here if they are using both.
                    // We just let the pipeline continue to the standard JWT Bearer validation.
                }
                catch (Exception)
                {
                    // Catch other generic exceptions
                }
            }

            // Continue the request pipeline
            await _next(context);
        }
    }
}
