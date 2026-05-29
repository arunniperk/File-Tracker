using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.Data.Sqlite;
using FileTracker.Core.Dtos;
using FileTracker.Core.Models;
using FileTracker.Core.Services;

namespace FileTracker.App.ViewModels;

public partial class RegisterDocumentViewModel : ObservableObject
{
    private readonly IDocumentService _docService;

    [ObservableProperty]
    private string _subject = string.Empty;

    [ObservableProperty]
    private string _originalFileNumber = string.Empty;

    [ObservableProperty]
    private string _senderOrRecipient = string.Empty;

    [ObservableProperty]
    private string _remarks = string.Empty;

    [ObservableProperty]
    private DateTime _documentDate = DateTime.Today;

    [ObservableProperty]
    private bool _isIncoming = true;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public RegisterDocumentViewModel(IDocumentService docService)
    {
        _docService = docService;
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        ErrorMessage = string.Empty;

        var dto = new RegisterDocumentDto
        {
            Direction = IsIncoming ? DocumentDirection.Incoming : DocumentDirection.Outgoing,
            Sender = IsIncoming ? SenderOrRecipient : null,
            Recipient = IsIncoming ? null : SenderOrRecipient,
            Subject = Subject,
            DocumentDate = DocumentDate,
            OriginalFileNumber = OriginalFileNumber,
            Remarks = Remarks
        };

        try
        {
            await _docService.RegisterAsync(dto);
            ClearForm();
            WeakReferenceMessenger.Default.Send(new DocumentRegisteredMessage(true));
        }
        catch (ArgumentException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (SqliteException ex) when (ex.Message.Contains("UNIQUE"))
        {
            ErrorMessage = "This file number already exists. Please enter a different file number.";
        }
    }

    private void ClearForm()
    {
        Subject = string.Empty;
        OriginalFileNumber = string.Empty;
        SenderOrRecipient = string.Empty;
        Remarks = string.Empty;
        DocumentDate = DateTime.Today;
        ErrorMessage = string.Empty;
    }
}

public class DocumentRegisteredMessage : ValueChangedMessage<bool>
{
    public DocumentRegisteredMessage(bool value) : base(value) { }
}
