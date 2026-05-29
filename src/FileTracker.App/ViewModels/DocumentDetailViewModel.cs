using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;
using FileTracker.Core.Dtos;
using FileTracker.Core.Models;
using FileTracker.Core.Services;
using FileTracker.Data;

namespace FileTracker.App.ViewModels;

public partial class DocumentDetailViewModel : ObservableObject
{
    private readonly IDocumentService _docService;
    private readonly IDocumentRepository _repository;
    private readonly IMovementService _movementService;
    private readonly IAttachmentService _attachmentService;

    [ObservableProperty]
    private Document? _selectedDocument;

    [ObservableProperty]
    private ObservableCollection<DocumentAudit> _auditEntries = new();

    [ObservableProperty]
    private ObservableCollection<Movement> _movementHistory = new();

    [ObservableProperty]
    private ObservableCollection<Attachment> _attachments = new();

    [ObservableProperty]
    private bool _isEditMode;

    // Edit fields bound to TextBoxes in edit mode
    [ObservableProperty]
    private string _editSubject = string.Empty;

    [ObservableProperty]
    private string _editOriginalFileNumber = string.Empty;

    [ObservableProperty]
    private string _editSender = string.Empty;

    [ObservableProperty]
    private string _editRecipient = string.Empty;

    [ObservableProperty]
    private string _editRemarks = string.Empty;

    [ObservableProperty]
    private DateTime _editDocumentDate = DateTime.Today;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public DocumentDetailViewModel(IDocumentService docService, IDocumentRepository repository, IMovementService movementService, IAttachmentService attachmentService)
    {
        _docService = docService;
        _repository = repository;
        _movementService = movementService;
        _attachmentService = attachmentService;
    }

    [RelayCommand]
    public async Task LoadDocumentAsync(int documentId)
    {
        ErrorMessage = string.Empty;
        IsEditMode = false;

        SelectedDocument = await _docService.GetByIdAsync(documentId);
        if (SelectedDocument is null) return;

        var entries = await _repository.GetAuditEntriesAsync(documentId);
        AuditEntries = new ObservableCollection<DocumentAudit>(entries);

        var movements = await _movementService.GetMovementHistoryAsync(documentId);
        MovementHistory = new ObservableCollection<Movement>(movements);

        var attachments = await _attachmentService.GetAttachmentsAsync(documentId);
        Attachments = new ObservableCollection<Attachment>(attachments);
    }

    [RelayCommand]
    public void EnterEditMode()
    {
        if (SelectedDocument is null) return;

        EditSubject = SelectedDocument.Subject;
        EditOriginalFileNumber = SelectedDocument.OriginalFileNumber;
        EditSender = SelectedDocument.Sender ?? string.Empty;
        EditRecipient = SelectedDocument.Recipient ?? string.Empty;
        EditRemarks = SelectedDocument.Remarks ?? string.Empty;
        EditDocumentDate = SelectedDocument.DocumentDate;
        ErrorMessage = string.Empty;

        IsEditMode = true;
    }

    [RelayCommand]
    public async Task SaveEditAsync()
    {
        if (SelectedDocument is null) return;

        try
        {
            var dto = new RegisterDocumentDto
            {
                Direction = SelectedDocument.Direction,
                Sender = SelectedDocument.Direction == DocumentDirection.Incoming ? EditSender : null,
                Recipient = SelectedDocument.Direction == DocumentDirection.Outgoing ? EditRecipient : null,
                Subject = EditSubject,
                DocumentDate = EditDocumentDate,
                OriginalFileNumber = EditOriginalFileNumber,
                Remarks = EditRemarks
            };

            await _docService.UpdateAsync(SelectedDocument.Id, dto);

            // Reload document and audit trail
            await LoadDocumentAsync(SelectedDocument.Id);
            IsEditMode = false;
        }
        catch (ArgumentException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    public async Task CancelEdit()
    {
        if (SelectedDocument is null) return;

        await LoadDocumentAsync(SelectedDocument.Id);
        IsEditMode = false;
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private async Task AddAttachment()
    {
        if (SelectedDocument is null) return;

        var dialog = new OpenFileDialog
        {
            Title = "Select Attachment",
            Filter = "Document Files (*.pdf;*.jpg;*.jpeg;*.png)|*.pdf;*.jpg;*.jpeg;*.png",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _attachmentService.AddAttachmentAsync(SelectedDocument.Id, dialog.FileName);
                await LoadDocumentAsync(SelectedDocument.Id);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
    }

    [RelayCommand]
    private async Task RemoveAttachment(Attachment? attachment)
    {
        if (attachment is null) return;

        var result = MessageBox.Show(
            $"Remove attachment \"{attachment.FileName}\"?",
            "Confirm Removal",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            await _attachmentService.RemoveAttachmentAsync(attachment.Id);
            Attachments.Remove(attachment);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task OpenAttachment(Attachment? attachment)
    {
        if (attachment is null) return;

        try
        {
            await _attachmentService.OpenAttachmentAsync(attachment.Id);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
