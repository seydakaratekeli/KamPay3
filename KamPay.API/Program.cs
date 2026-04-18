using Firebase.Database;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;               // OpenApiInfo, OpenApiSecurityScheme vs. (v2+ namespace)
using KamPay.API.Repositories;
using KamPay.API.Middlewares;
using System.Text;
using KamPay.API.Services.Auth;
using KamPay.API.Services.Products;

var builder = WebApplication.CreateBuilder(args);

// API'nin tüm ağ arayüzlerinden ve belirttiğimiz porttan yanıt vermesini zorluyoruz:
builder.WebHost.UseUrls("http://0.0.0.0:5011");

// 1. Firebase Admin SDK'yı Başlat (Güvenlik ve Auth için)
FirebaseApp.Create(new AppOptions()
{
    Credential = GoogleCredential.FromJson(File.ReadAllText("firebase-admin.json"))
});

// 2. Firebase Database Bağlantısını Servis Olarak Ekle (Dependency Injection)
builder.Services.AddSingleton(new FirebaseClient(
    "https://kampay-b006d-default-rtdb.europe-west1.firebasedatabase.app/",
    new FirebaseOptions
    {
        // "Database secrets" sekmesinden kopyaladığınız kodu buraya yapıştırın.
        // Bu kod API'nize tam yetki (admin) verir, kurallara takılmazsınız.
        AuthTokenAsyncFactory = () => Task.FromResult("7t7wMzquCV96p0v2zu0eLd14hMTWHoO1iRYI2Nkm")
    }));

// --- YENİ EKLENEN KISIM: JWT Güvenlik Duvarı ---
var jwtSecret = builder.Configuration["JwtSettings:Secret"] ?? "YOUR_VERY_SECURE_SECRET_KEY_HERE_MIN_16_CHARS";
var issuer = builder.Configuration["JwtSettings:Issuer"] ?? "KamPayAPI";
var audience = builder.Configuration["JwtSettings:Audience"] ?? "KamPayApp";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.IncludeErrorDetails = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero
        };
    });

// Add services to the container.
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddEndpointsApiExplorer();

// builder.Services.AddSwaggerGen(); yerine bu bloğu ekle:
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "KamPay API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Token gir"
    });

    c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", doc),
            new List<string>()
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();       // Swashbuckle JSON endpoint
    app.UseSwaggerUI();
}

// Development'ta HTTP ile de çalışabilsin (mobil cihaz testi için)
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseMiddleware<FirebaseTokenValidationMiddleware>(); // Firebase ID Token Doğrulama Middleware'i
app.UseAuthentication(); // Kimlik Kontrolü (Sen kimsin?)
app.UseAuthorization();  // Yetki Kontrolü (Buraya girmeye yetkin var mı?)

app.MapControllers();

app.Run();
