using System.Collections.ObjectModel;
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

    public SearchViewModel SearchVm { get; }

    [ObservableProperty]
    private Document? _selectedDocument;

    public bool HasUnsavedChanges => _registerVm.HasUnsavedChanges;

    public MainViewModel(
        IDocumentService docService,
        RegisterDocumentViewModel registerVm,
        SearchViewModel searchVm,
        IMovementService movementService)
    {
        _docService = docService;
        _registerVm = registerVm;
        _movementService = movementService;
        SearchVm = searchVm;
        WeakReferenceMessenger.Default.Register(this);
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

    /// <summary>
    /// Look up the current location name for a document.
    /// Returns the ToPositionName of the most recent movement, or "—" if none.
    /// </summary>
    public async Task<string> GetCurrentLocationForDocumentAsync(int documentId)
    {
        var location = await _movementService.GetCurrentLocationAsync(documentId);
        return location?.ToPositionName ?? "\u2014";
    }
}
