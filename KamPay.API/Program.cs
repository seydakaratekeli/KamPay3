using Firebase.Database;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;               // OpenApiInfo, OpenApiSecurityScheme vs. (v2+ namespace)

var builder = WebApplication.CreateBuilder(args);

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
var firebaseProjectId = "kampay-b006d"; // Örn: kampay-12345

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://securetoken.google.com/{firebaseProjectId}";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"https://securetoken.google.com/{firebaseProjectId}",
            ValidateAudience = true,
            ValidAudience = firebaseProjectId,
            ValidateLifetime = true
        };
    });

// Add services to the container.

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

app.UseHttpsRedirection();

app.UseAuthentication(); // Kimlik Kontrolü (Sen kimsin?)
app.UseAuthorization();  // Yetki Kontrolü (Buraya girmeye yetkin var mı?)

app.MapControllers();

app.Run();
