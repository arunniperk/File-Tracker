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

    [ObservableProperty]
    private ObservableCollection<Document> _documents = new();

    [ObservableProperty]
    private Document? _selectedDocument;

    public bool HasUnsavedChanges => _registerVm.HasUnsavedChanges;

    public MainViewModel(IDocumentService docService, RegisterDocumentViewModel registerVm)
    {
        _docService = docService;
        _registerVm = registerVm;
        WeakReferenceMessenger.Default.Register(this);
        _ = LoadDocumentsAsync();
    }

    public void Receive(DocumentRegisteredMessage message)
    {
        _ = LoadDocumentsAsync();
    }

    private async Task LoadDocumentsAsync()
    {
        try
        {
            var docs = await _docService.GetAllAsync();
            Documents = new ObservableCollection<Document>(docs);
        }
        catch (Exception)
        {
            // Logged by service layer
        }
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
}
