using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FileTracker.Core.Models;
using FileTracker.Core.Services;
using FileTracker.App.Views;
using Microsoft.Extensions.DependencyInjection;

namespace FileTracker.App.ViewModels;

public partial class MainViewModel : ObservableObject, IRecipient<DocumentRegisteredMessage>
{
    private readonly IDocumentService _docService;
    private readonly RegisterDocumentViewModel _registerVm;
    private readonly IMovementService _movementService;
    private readonly IBackupService _backupService;

    public SearchViewModel SearchVm { get; }
    public DashboardViewModel DashboardVm { get; }

    [ObservableProperty]
    private Document? _selectedDocument;

    [ObservableProperty]
    private int _selectedTabIndex;

    public bool HasUnsavedChanges => _registerVm.HasUnsavedChanges;

    public MainViewModel(
        IDocumentService docService,
        RegisterDocumentViewModel registerVm,
        SearchViewModel searchVm,
        IMovementService movementService,
        DashboardViewModel dashboardVm,
        IBackupService backupService)
    {
        _docService = docService;
        _registerVm = registerVm;
        _movementService = movementService;
        _backupService = backupService;
        SearchVm = searchVm;
        DashboardVm = dashboardVm;
        WeakReferenceMessenger.Default.Register(this);
        WeakReferenceMessenger.Default.Register<SwitchToDocumentsTabMessage>(this, (_, _) => SelectedTabIndex = 1);
        WeakReferenceMessenger.Default.Register<DocumentMovedMessage>(this, (_, _) => SearchVm.SearchCommand.Execute(null));
        SearchVm.SearchCommand.Execute(null);
    }

    public void Receive(DocumentRegisteredMessage message)
    {
        SearchVm.SearchCommand.Execute(null);
    }

    [RelayCommand]
    private async Task OpenDocumentDetailAsync(Document document)
    {
        // Set selected document so the detail view can bind to it
        SelectedDocument = document;

        // Resolve DocumentDetailViewModel and load the document
        var app = (App)System.Windows.Application.Current;
        var detailVm = app.Services.GetRequiredService<DocumentDetailViewModel>();
        if (detailVm is null) return;

        await detailVm.LoadDocumentAsync(document.Id);

        // Show DocumentDetailView in a new window
        var window = new DocumentDetailWindow
        {
            DataContext = detailVm,
            Owner = System.Windows.Application.Current.MainWindow
        };
        window.Show();
    }

    [RelayCommand]
    private void EditDocument(Document document)
    {
        _registerVm.LoadForEdit(document);
    }

    [RelayCommand]
    private void OpenManagePositions()
    {
        var app = (App)System.Windows.Application.Current;
        var vm = app.Services.GetRequiredService<ManagePositionsViewModel>();
        var window = new ManagePositionsWindow
        {
            DataContext = vm,
            Owner = System.Windows.Application.Current.MainWindow
        };
        window.ShowDialog();
    }

    [RelayCommand]
    private async Task RecordMovementAsync(Document document)
    {
        var app = (App)System.Windows.Application.Current;
        var vm = app.Services.GetRequiredService<RecordMovementViewModel>();
        var window = new RecordMovementWindow
        {
            DataContext = vm,
            Owner = System.Windows.Application.Current.MainWindow
        };
        await window.LoadDocumentAsync(document);
        window.ShowDialog();
    }

    [RelayCommand]
    private void OpenReportWindow()
    {
        var app = (App)System.Windows.Application.Current;
        var vm = app.Services.GetRequiredService<ReportViewModel>();
        var window = new ReportWindow
        {
            DataContext = vm,
            Owner = System.Windows.Application.Current.MainWindow
        };
        window.ShowDialog();
    }

    /// <summary>
    /// Look up the current location name for a document.
    /// Returns the ToPositionName of the most recent movement, or "—" if none.
    /// </summary>
    public async Task<string> GetCurrentLocationForDocumentAsync(int documentId)
    {
        var location = await _movementService.GetCurrentLocationAsync(documentId);
        return location?.ToPositionName ?? "\u2014";
    }

    [RelayCommand]
    private async Task BackupAsync()
    {
        try
        {
            using var dialog = new FolderBrowserDialog
            {
                ShowNewFolderButton = true,
                Description = "Select folder to save backup"
            };

            if (dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
            {
                var backupPath = await _backupService.CreateBackupAsync(dialog.SelectedPath);
                System.Windows.MessageBox.Show(
                    $"Backup created successfully:\n{backupPath}",
                    "Backup Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Backup failed: {ex.Message}",
                "Backup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Restores database and attachments from a backup .zip file.
    /// Shows file picker → destructive warning (D-04) → restore → restart (D-05).
    /// </summary>
    [RelayCommand]
    private async Task RestoreAsync()
    {
        try
        {
            // Step 1: File picker for .zip backup files
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Backup Files (*.zip)|*.zip",
                Title = "Select Backup File to Restore",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FileName))
            {
                return; // User cancelled
            }

            // Step 2: Destructive warning per D-04
            var warningResult = System.Windows.MessageBox.Show(
                "This will replace ALL current data with the backup contents.\n\nThe application will restart after restore.\n\nThis cannot be undone. Continue?",
                "Confirm Restore",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (warningResult != MessageBoxResult.Yes)
            {
                return; // User declined
            }

            // Step 3: Execute restore
            await _backupService.RestoreFromBackupAsync(dialog.FileName);

            // Step 4: Success + restart per D-05
            System.Windows.MessageBox.Show(
                "Restore complete. The application will now restart.",
                "Restore Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            System.Windows.Application.Current.Shutdown();
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(processPath))
            {
                System.Diagnostics.Process.Start(processPath);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Restore failed: {ex.Message}",
                "Restore Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
