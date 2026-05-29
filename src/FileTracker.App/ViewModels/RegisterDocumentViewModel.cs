using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using FileTracker.Core.Dtos;
using FileTracker.Core.Models;
using FileTracker.Core.Services;
using System.ComponentModel.DataAnnotations;

namespace FileTracker.App.ViewModels;

public partial class RegisterDocumentViewModel : ObservableValidator
{
    private readonly IDocumentService _docService;
    private readonly IAttachmentService _attachmentService;
    private bool _isClearing;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Subject is required")]
    private string _subject = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "File number is required")]
    private string _originalFileNumber = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "This field is required")]
    private string _senderOrRecipient = string.Empty;

    [ObservableProperty]
    private string _remarks = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Date is required")]
    private DateTime _documentDate = DateTime.Today;

    [ObservableProperty]
    private bool _isIncoming = true;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private int? _editingDocumentId;

    [ObservableProperty]
    private string _modeIndicator = "Register New Document";

    [ObservableProperty]
    private ObservableCollection<string> _pendingAttachmentPaths = new();

    public RegisterDocumentViewModel(IDocumentService docService, IAttachmentService attachmentService)
    {
        _docService = docService;
        _attachmentService = attachmentService;
    }

    partial void OnSubjectChanged(string value)
    {
        if (!_isClearing) HasUnsavedChanges = true;
        SubmitCommand.NotifyCanExecuteChanged();
    }

    partial void OnOriginalFileNumberChanged(string value)
    {
        if (!_isClearing) HasUnsavedChanges = true;
        SubmitCommand.NotifyCanExecuteChanged();
    }

    partial void OnSenderOrRecipientChanged(string value)
    {
        if (!_isClearing) HasUnsavedChanges = true;
        SubmitCommand.NotifyCanExecuteChanged();
    }

    partial void OnDocumentDateChanged(DateTime value)
    {
        if (!_isClearing) HasUnsavedChanges = true;
        SubmitCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void AddAttachment()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Attachment",
            Filter = "Document Files (*.pdf;*.jpg;*.jpeg;*.png)|*.pdf;*.jpg;*.jpeg;*.png",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var filePath in dialog.FileNames)
            {
                PendingAttachmentPaths.Add(filePath);
            }
            HasUnsavedChanges = true;
        }
    }

    [RelayCommand]
    private void RemovePendingAttachment(string? filePath)
    {
        if (filePath is not null)
        {
            PendingAttachmentPaths.Remove(filePath);
        }
    }

    /// <summary>
    /// Pre-populate the form for editing an existing document.
    /// Sets IsEditMode=true and populates all fields from the document.
    /// </summary>
    public void LoadForEdit(Document document)
    {
        _isClearing = true;

        IsEditMode = true;
        EditingDocumentId = document.Id;
        IsIncoming = document.Direction == DocumentDirection.Incoming;
        ModeIndicator = $"Edit Document #{document.TrackingId}";
        Subject = document.Subject;
        OriginalFileNumber = document.OriginalFileNumber;
        SenderOrRecipient = document.Direction == DocumentDirection.Incoming
            ? (document.Sender ?? string.Empty)
            : (document.Recipient ?? string.Empty);
        Remarks = document.Remarks ?? string.Empty;
        DocumentDate = document.DocumentDate;
        HasUnsavedChanges = false;
        ErrorMessage = string.Empty;
        ClearErrors();

        _isClearing = false;
    }

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync()
    {
        ErrorMessage = string.Empty;

        ValidateAllProperties();
        if (HasErrors) return;

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
            Document? document = null;
            if (IsEditMode && EditingDocumentId.HasValue)
            {
                await _docService.UpdateAsync(EditingDocumentId.Value, dto);
                document = await _docService.GetByIdAsync(EditingDocumentId.Value);
            }
            else
            {
                document = await _docService.RegisterAsync(dto);
            }

            // Process pending attachments for newly registered documents
            if (!IsEditMode && document is not null && PendingAttachmentPaths.Count > 0)
            {
                var attachmentErrors = new List<string>();
                foreach (var path in PendingAttachmentPaths.ToList())
                {
                    try
                    {
                        await _attachmentService.AddAttachmentAsync(document.Id, path);
                    }
                    catch (Exception ex)
                    {
                        attachmentErrors.Add($"{System.IO.Path.GetFileName(path)}: {ex.Message}");
                    }
                }
                if (attachmentErrors.Count > 0)
                {
                    ErrorMessage = "Document saved, but some attachments failed: " + string.Join("; ", attachmentErrors);
                }
            }

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

    private bool CanSubmit()
    {
        return !string.IsNullOrWhiteSpace(Subject)
            && !string.IsNullOrWhiteSpace(OriginalFileNumber)
            && !string.IsNullOrWhiteSpace(SenderOrRecipient);
    }

    private void ClearForm()
    {
        _isClearing = true;
        HasUnsavedChanges = false;
        IsEditMode = false;
        EditingDocumentId = null;
        ModeIndicator = "Register New Document";
        Subject = string.Empty;
        OriginalFileNumber = string.Empty;
        SenderOrRecipient = string.Empty;
        Remarks = string.Empty;
        DocumentDate = DateTime.Today;
        ErrorMessage = string.Empty;
        ClearErrors();
        PendingAttachmentPaths.Clear();
        _isClearing = false;
    }
}

public class DocumentRegisteredMessage : ValueChangedMessage<bool>
{
    public DocumentRegisteredMessage(bool value) : base(value) { }
}
