using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using System.Data.SqlClient;

namespace api_test.Middelware
{
    public class VisitorLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _connectionString;

        public VisitorLoggingMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // تجاهل requests للملفات الثابتة (مثل favicon.ico)
            if (context.Request.Path.Value!.EndsWith(".ico") || context.Request.Path.Value!.Contains("/css") || context.Request.Path.Value!.Contains("/js"))
            {
                await _next(context);
                return;
            }

            // الحصول على IP الحقيقي من Azure
            string ipAddress = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (string.IsNullOrEmpty(ipAddress))
            {
                ipAddress = context.Connection.RemoteIpAddress?.ToString();
            }

            string path = context.Request.Path;
            DateTime visitedAt = DateTime.UtcNow;

            // سجل البيانات في SQL Database
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string query = "INSERT INTO VisitorLogs (IpAddress, Path, VisitedAt) VALUES (@IpAddress, @Path, @VisitedAt)";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@IpAddress", ipAddress);
                        cmd.Parameters.AddWithValue("@Path", path);
                        cmd.Parameters.AddWithValue("@VisitedAt", visitedAt);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                // أي خطأ أثناء تسجيل الـ log مش هيوقف الـ API
                Console.WriteLine($"Error logging visitor: {ex.Message}");
            }

            // استمر في pipeline
            await _next(context);
        }
    }
}
