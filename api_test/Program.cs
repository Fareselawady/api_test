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
            builder.Services.AddOpenApi();

            // ======================
            // DbContext
            // ======================
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });


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
            builder.Services.AddScoped<EmailService>();
            builder.Services.AddScoped<OtpService>();
            builder.Services.AddScoped<IScheduleService, ScheduleService>();
            builder.Services.AddScoped<IInteractionService, InteractionService>();
            builder.Services.AddScoped<IAlertService, AlertService>();
            builder.Services.AddSingleton<ITranslationService, TranslationService>();
            builder.Services.AddHostedService<NotificationBackgroundService>();

            builder.Services.Configure<AiChatbotOptions>(
                builder.Configuration.GetSection(AiChatbotOptions.SectionName));
            builder.Services.AddHttpClient<IAiChatbotService, AiChatbotService>((services, client) =>
            {
                var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiChatbotOptions>>().Value;
                if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
                    throw new InvalidOperationException("AiChatbot:BaseUrl must be a valid absolute URL.");

                client.BaseAddress = new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 5, 120));
                client.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "true");
            });

            // ======================
            // Medicine Image Scanning
            // ======================
            builder.Services.AddHttpClient<IAIMedicineRecognitionService, AIMedicineRecognitionService>();

            var app = builder.Build();

            app.UseDeveloperExceptionPage();

            app.UseMiddleware<VisitorLoggingMiddleware>();

            app.UseHttpsRedirection();

            app.UseCors("AllowAll");

            app.UseAuthentication();
            app.UseAuthorization();

            // OpenAPI + Scalar
            app.MapOpenApi();            // /openapi/v1.json
            app.MapScalarApiReference(); // /scalar

            app.MapControllers();

            app.Run();
        }
    }
}
