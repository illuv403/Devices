using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Devices.API;
using Devices.API.Helpers.Middleware;
using Devices.API.Helpers.Options;
using Devices.API.Services.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var JwtConfig = builder.Configuration.GetSection("Jwt");    

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? throw new InvalidOperationException("Could not find connection string.");

builder.Services.AddDbContext<DevicesDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.Configure<JwtOptions>(JwtConfig);

builder.Services.AddScoped<ITokenService, TokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidIssuer = JwtConfig["Issuer"],
            ValidAudience = JwtConfig["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtConfig["Key"])),
            ClockSkew = TimeSpan.FromMinutes(int.Parse(JwtConfig["ValidityInMinutes"]))
        };
    });

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();    
app.UseAuthorization();

app.MapControllers();

app.UseMiddleware<AdditionalPropertiesMiddleware>();

app.Run();