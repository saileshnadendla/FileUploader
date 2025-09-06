using Microsoft.AspNetCore.Http.Features;
using StackExchange.Redis;

namespace FileUploader.API
{
    public class Startup
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ----------------------------
            // 1. Configure Services
            // ----------------------------

            // Allow large file uploads (up to 1 GB)
            builder.Services.Configure<FormOptions>(o =>
            {
                o.MultipartBodyLengthLimit = 1024L * 1024L * 1024L;
            });

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Limits.MaxRequestBodySize = 1024L * 1024L * 1024L; // 1 GB
            });

            builder.Services.AddEndpointsApiExplorer();


            // Add Controllers
            builder.Services.AddControllers();

            var app = builder.Build();


            app.UseHttpsRedirection();
            app.MapControllers();

            app.Run();
        }
    }
}
