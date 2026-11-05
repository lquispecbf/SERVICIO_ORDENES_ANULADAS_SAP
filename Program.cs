using CBF_ORDENES_ANULADAS.Configuration;
using CBF_ORDENES_ANULADAS.Repositories;
using CBF_ORDENES_ANULADAS.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.IO;

namespace CBF_ORDENES_ANULADAS
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            string logDir = config.GetValue<string>("LogDirectory") ?? @"C:\LogOrdenesAnuladas\";
            if (!Directory.Exists(logDir))
            {
                try { Directory.CreateDirectory(logDir); } catch { }
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    path: Path.Combine(logDir, "log-.txt"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
                .CreateLogger();

            try
            {
                Log.Information("Iniciando host del servicio CBF_ORDENES_ANULADAS...");
                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "El servicio CBF_ORDENES_ANULADAS falló al iniciar.");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .UseSerilog()
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure<SmtpSettings>(hostContext.Configuration.GetSection("SmtpSettings"));
                    services.AddScoped<IOrdenesAnuladasRepo, OrdenesAnuladasRepo>();
                    services.AddScoped<IEmailSenderAnulaciones, EmailSenderAnulaciones>();
                    services.AddHostedService<Worker>();
                });
    }
}

