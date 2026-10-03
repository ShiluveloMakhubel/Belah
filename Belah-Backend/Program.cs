using Belah_Backend.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    
    // Policy for production frontend
    options.AddPolicy("AllowProduction", policy =>
    {
        policy.WithOrigins("http://simplyluxe.co.za") // live frontend
              .AllowAnyHeader()
              .AllowAnyMethod();
    });

    options.AddPolicy("AllowProduction", policy =>
    {
        policy.WithOrigins("http://localhost") // live frontend
              .AllowAnyHeader()
              .AllowAnyMethod();
    });

    options.AddPolicy("AllowProduction", policy =>
    {
        policy.WithOrigins("http://localhost:5000") // live frontend
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});


// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseCors("AllowReactApp");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.UseStaticFiles();

app.Run();
