using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using FileTracker.Core.Models;
using FileTracker.Core.Services;

namespace FileTracker.App.ViewModels;

public partial class MainViewModel : ObservableObject, IRecipient<DocumentRegisteredMessage>
{
    private readonly IDocumentService _docService;
    private readonly RegisterDocumentViewModel _registerVm;

    [ObservableProperty]
    private ObservableCollection<Document> _documents = new();

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
}
