using FirebaseAdmin.Auth;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace KamPay.API.Services
{
    public class AuthService : IAuthService
    {
        private readonly IConfiguration _configuration;

        public AuthService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<(string Token, string Uid, DateTime Expiration)> LoginWithFirebaseAsync(string firebaseIdToken)
        {
            if (string.IsNullOrEmpty(firebaseIdToken))
                throw new ArgumentNullException(nameof(firebaseIdToken), "Firebase ID token is missing.");

            // 1. Validate Firebase ID token using FirebaseAdmin SDK
            // This implicitly calls Google's servers to verify signature and lifetime
            FirebaseToken decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(firebaseIdToken);

            // 2. Prepare user claims
            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, decodedToken.Uid),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // Opsiyonel: Veritabanına kullanıcının var olup olmadığını kontrol edip
            // yoksa User (AppUser) kaydı oluşturma işlemini burada AuthService içinde veya 
            // IUserRepository aracılığıyla yapabilirsiniz. (Şu anki kapsamda sadece Auth yapılıyor)

            // 3. Generate Custom JWT
            var jwtSecret = _configuration["JwtSettings:Secret"] ?? "YOUR_VERY_SECURE_SECRET_KEY_HERE_MIN_16_CHARS";
            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

            var expiration = DateTime.UtcNow.AddHours(3);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Issuer = _configuration["JwtSettings:Issuer"] ?? "KamPayAPI",
                Audience = _configuration["JwtSettings:Audience"] ?? "KamPayApp",
                Expires = expiration,
                Subject = new ClaimsIdentity(authClaims),
                SigningCredentials = new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var createdToken = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(createdToken);

            return (tokenString, decodedToken.Uid, expiration);
        }
    }
}
