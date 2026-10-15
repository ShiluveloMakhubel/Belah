using System.Text;
using Belah_Backend.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.FileProviders;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Allow local operators to provide the SQL password separately from the
// connection string. SqlConnectionStringBuilder safely encodes special
// characters without placing the password in source or command arguments.
var sqlPassword = Environment.GetEnvironmentVariable("BELAH_SQL_PASSWORD");
if (sqlPassword is not null)
{
    if (string.IsNullOrEmpty(sqlPassword))
        throw new InvalidOperationException("BELAH_SQL_PASSWORD was set but is empty.");

    var configuredConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(configuredConnectionString))
        throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection before setting BELAH_SQL_PASSWORD.");

    var sqlConnection = new SqlConnectionStringBuilder(configuredConnectionString)
    {
        UserID = Environment.GetEnvironmentVariable("BELAH_SQL_USER") ?? "sa",
        Password = sqlPassword,
        IntegratedSecurity = false
    };
    builder.Configuration["ConnectionStrings:DefaultConnection"] = sqlConnection.ConnectionString;
}

if (args.Contains("--seed-mock-products", StringComparer.Ordinal))
{
    Belah_Backend.Services.DevelopmentMockProductSeeder.EnsureDevelopmentEnvironment(builder.Environment.EnvironmentName);
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection before running the mock product seeder.");

    var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connectionString).Options;
    await using var context = new ApplicationDbContext(options);
    var seedResult = await new Belah_Backend.Services.DevelopmentMockProductSeeder().SeedAsync(context);
    Console.WriteLine($"Development catalogue refresh complete: {seedResult.Added} added, {seedResult.Updated} refreshed, {seedResult.SkippedConflicts} reserved-SKU conflicts skipped.");
    return;
}

if (args.Contains("--seed-fragrance-catalogue", StringComparer.Ordinal))
{
    Belah_Backend.Services.FragranceCatalogueSeeder.EnsureDevelopmentEnvironment(builder.Environment.EnvironmentName);
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection before running the fragrance catalogue seeder.");

    var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connectionString).Options;
    await using var context = new ApplicationDbContext(options);
    var seedResult = await new Belah_Backend.Services.FragranceCatalogueSeeder().SeedAsync(context);
    Console.WriteLine($"Fragrance intake complete: {seedResult.Added} added, {seedResult.SkippedExisting} existing products skipped. All seeded records remain price-unapproved and stock-unconfirmed.");
    return;
}

// Require a secure JWT signing key from configuration.
var jwtKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtKey) ||
    Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "Configure Jwt:Key with a secret of at least 32 bytes.");
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "BelahApi";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "BelahWebsite";

// CORS
var allowedCorsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
if (allowedCorsOrigins is null || allowedCorsOrigins.Length == 0)
{
    allowedCorsOrigins = new[] { "http://localhost:3000", "https://simplyluxe.co.za" };
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins(allowedCorsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT authentication
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,

            ValidateAudience = true,
            ValidAudience = jwtAudience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

builder.Services.AddHttpClient<Belah_Backend.Services.IPaystackService, Belah_Backend.Services.PaystackService>();
builder.Services.Configure<Belah_Backend.Services.SmtpEmailOptions>(
    builder.Configuration.GetSection(Belah_Backend.Services.SmtpEmailOptions.SectionName));
builder.Services.Configure<Belah_Backend.Services.PurchaseConfirmationOptions>(
    builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<Belah_Backend.Services.IEmailSender, Belah_Backend.Services.MailKitEmailSender>();
builder.Services.AddScoped<Belah_Backend.Services.EmailOutboxProcessor>();
builder.Services.AddHostedService<Belah_Backend.Services.EmailOutboxWorker>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

var configuredUploadsPath = app.Configuration["Uploads:Path"];
var uploadsPath = string.IsNullOrWhiteSpace(configuredUploadsPath)
    ? null
    : Path.GetFullPath(configuredUploadsPath);
if (uploadsPath is not null)
    Directory.CreateDirectory(uploadsPath);

app.UseRouting();
app.UseCors("AllowReactApp");
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();
if (uploadsPath is not null)
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadsPath),
        RequestPath = "/uploads"
    });
}
app.MapControllers();

app.Run();
