using api_test.Data;
using api_test.Middelware;
using api_test.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Text;

namespace api_test
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Controllers
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();

            // OpenAPI (»œÌ· Swagger)
            builder.Services.AddOpenApi();

            // DbContext
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
            );

            // JWT
            var key = Encoding.UTF8.GetBytes(builder.Configuration["AppSettings:Token"]!);

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = builder.Configuration["AppSettings:Issuer"],
                        ValidAudience = builder.Configuration["AppSettings:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(key)
                    };
                });

            builder.Services.AddAuthorization();
            builder.Services.AddScoped<IAuthService, AuthService>();

            var app = builder.Build();

            app.UseMiddleware<VisitorLoggingMiddleware>();

            // OpenAPI + Scalar
            app.MapOpenApi();            // /openapi/v1.json
            app.MapScalarApiReference(); // /scalar

            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();
            app.Run();
        }
    }
}
