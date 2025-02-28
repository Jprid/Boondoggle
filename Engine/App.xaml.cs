using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AutoMapper;
using Engine.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Engine;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public IServiceProvider ServiceProvider { get; private set; }
    public IConfiguration Configuration { get; private set; }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    public App()
    {

        // Configure DI
        var services = new ServiceCollection();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console() // Log to console
            .WriteTo.File("logs/app.log", rollingInterval: RollingInterval.Day) // Log to file
            .CreateLogger();
        // Register AutoMapper
        services.AddSingleton<IMapper>(sp =>
        {
            var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
            return config.CreateMapper();
        });
        services.AddLogging(logging =>
        {
            logging.AddConsole(); // Built-in console logger
            logging.AddSerilog(dispose: true); // Integrate Serilog
        });
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory()) // Looks in bin directory
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        Configuration = builder.Build();
        // Register services
        services.AddSingleton<MainWindow>();
        services.AddSingleton<DbContext, DbContext>();
        services.AddSingleton<ICognitiveService, CognitiveService>();
        services.AddSingleton<IAzureMapsService, AzureMapsService>();
        services.AddSingleton(Configuration);
        // Build service provider
        ServiceProvider = services.BuildServiceProvider();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Create and show MainWindow with DI
        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}