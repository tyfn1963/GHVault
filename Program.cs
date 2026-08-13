using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims; // Rol atamaları için bu kütüphane şart
using System.IdentityModel.Tokens.Jwt;
using InventoryAPI.Data;
using InventoryAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Veritabanı Bağlantısı
builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Şifreleme Servisimizi Ekliyoruz (AES-256)
builder.Services.AddSingleton<EncryptionService>();

// 3. Controller'lar, Swagger ve SIEM Loglama Servisi
builder.Services.AddControllers();
builder.Services.AddScoped<ISyslogService, SyslogService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 4. JWT Kimlik Doğrulama Ayarları
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(jwtKey)) throw new Exception("JWT Key appsettings.json içinde bulunamadı!");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(5), // Saat farkı esnekliği
            // --------------------------------------------------------
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"JWT Hatası: {context.Exception.Message}");
                return Task.CompletedTask;
            }
        };
    });

// 5. CORS AYARI (ŞAH MAT HAMLESİ: Şirket içinden gelen her isteğe kapı açık)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy
            .AllowAnyOrigin()    // Herhangi bir IP veya laptoptan gelen isteği kabul et
            .AllowAnyMethod()    // GET, POST, PUT, DELETE hepsine izin ver
            .AllowAnyHeader());  // Bütün header'ları (Token dahil) geçir
});

var app = builder.Build();

// --- 2. KALKAN: EVRENSEL HATA YAKALAYICI (Global Exception Handler) ---
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            
            // HATA DEDEKTİFİ: C#'ın arka planda yediği asıl tokadı yakalıyoruz!
            var exceptionHandlerPathFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
            var errorMsg = exceptionHandlerPathFeature?.Error?.Message ?? "Bilinmeyen Hata";
            var innerMsg = exceptionHandlerPathFeature?.Error?.InnerException?.Message ?? "";

            // Hatayı maskelemeden direkt ekrana basıyoruz:
            var errorJson = $"{{\"message\": \"GERÇEK HATA: {errorMsg} | İÇ DETAY: {innerMsg}\"}}";
            await context.Response.WriteAsync(errorJson);
        });
    });
}
// -----------------------------------------------------------------------

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// KİLİT NOKTA 1: CORS Her şeyden önce gelmeli
app.UseCors("AllowAll");

// KİLİT NOKTA 2: Auth Sıralaması Asla Değişmemeli
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// --- 6. OTOMATİK VERİTABANI KURULUM MİMARİSİ ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<InventoryDbContext>();
        context.Database.Migrate();
        Console.WriteLine("Veritabanı kontrolü başarılı: Sistem canlıya hazır!");
    }
    catch (Exception ex)
    {
        Console.WriteLine("Veritabanı oluşturulurken hata: " + ex.Message);
    }
}

app.Run();