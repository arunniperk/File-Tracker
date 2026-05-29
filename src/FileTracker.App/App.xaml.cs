using System.IO;
using System.Windows;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using FileTracker.App.ViewModels;
using FileTracker.App.Views;

using FileTracker.Core.Services;
using FileTracker.Data;
using FileTracker.App.Services;

namespace FileTracker.App;

public partial class App : Application
{
    private IHost? _host;

    /// <summary>
    /// Expose the DI service provider for ViewModels that need to resolve services at runtime.
    /// </summary>
    public IServiceProvider Services => _host?.Services
        ?? throw new InvalidOperationException("Host not initialized");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder();

        // Logging — Serilog replaces default providers
        builder.Logging.ClearProviders();
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FileTracker", "logs", "filetracker-.log");
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();
        builder.Logging.AddSerilog();

        // Database — single connection, app-lifetime scoped
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FileTracker", "filetracker.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        builder.Services.AddSingleton<SqliteConnection>(_ =>
        {
            var conn = new SqliteConnection(connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL;";
            cmd.ExecuteNonQuery();
            return conn;
        });

        // Data layer
        builder.Services.AddSingleton<IDocumentRepository, DocumentRepository>();
        builder.Services.AddSingleton<IPositionRepository, PositionRepository>();
        builder.Services.AddSingleton<IMovementRepository, MovementRepository>();

        // Services
        builder.Services.AddSingleton<IDocumentService, DocumentService>();
        builder.Services.AddSingleton<IPositionService, PositionService>();
        builder.Services.AddSingleton<IMovementService, MovementService>();
        builder.Services.AddSingleton<IAttachmentRepository, AttachmentRepository>();
        builder.Services.AddSingleton<IAttachmentService, AttachmentService>();

        // ViewModels — transient so each window gets fresh state
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<RegisterDocumentViewModel>();
        builder.Services.AddTransient<DocumentDetailViewModel>();
        builder.Services.AddTransient<SearchViewModel>();
        builder.Services.AddTransient<ManagePositionsViewModel>();
        builder.Services.AddTransient<RecordMovementViewModel>();

        // Views
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();
        await _host.StartAsync();

        // Initialize database schema
        var initializer = ActivatorUtilities
            .CreateInstance<DatabaseInitializer>(_host.Services);
        await initializer.InitializeAsync();

        // Show main window
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
