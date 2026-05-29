using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using FileTracker.Core.Dtos;
using FileTracker.Core.Models;
using FileTracker.Core.Services;
using System.ComponentModel.DataAnnotations;

namespace FileTracker.App.ViewModels;

public partial class RecordMovementViewModel : ObservableValidator
{
    private readonly IMovementService _movementService;
    private readonly IPositionService _positionService;

    [ObservableProperty]
    private Document? _document;

    [ObservableProperty]
    private ObservableCollection<Position> _positions = new();

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "To Position is required")]
    private Position? _selectedToPosition;

    [ObservableProperty]
    private Position? _selectedFromPosition;

    [ObservableProperty]
    private string _directionText = "Sent";

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Date is required")]
    private DateTime _movementDate = DateTime.Today;

    [ObservableProperty]
    private string _remarks = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _windowTitle = "Record Movement";

    [ObservableProperty]
    private bool _shouldClose;

    public event Action? RequestClose;

    public RecordMovementViewModel(IMovementService movementService, IPositionService positionService)
    {
        _movementService = movementService;
        _positionService = positionService;
    }

    partial void OnDirectionTextChanged(string value)
    {
        // Triggers Direction recalculation via partial method
    }

    private MovementDirection Direction =>
        DirectionText == "Received" ? MovementDirection.Received : MovementDirection.Sent;

    [RelayCommand]
    public async Task LoadAsync(Document document)
    {
        Document = document;
        WindowTitle = $"Record Movement — {document.TrackingId} — {document.Subject}";

        var activePositions = await _positionService.GetActiveAsync();
        Positions = new ObservableCollection<Position>(activePositions);

        // Pre-set MovementDate to today
        MovementDate = DateTime.Today;
        DirectionText = "Sent";
        Remarks = string.Empty;
        SelectedFromPosition = null;
        SelectedToPosition = null;
        ErrorMessage = string.Empty;
        ClearErrors();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = string.Empty;
        ValidateAllProperties();

        if (HasErrors) return;

        if (SelectedToPosition is null)
        {
            ErrorMessage = "Please select a To Position.";
            return;
        }

        try
        {
            var dto = new RecordMovementDto
            {
                DocumentId = Document!.Id,
                FromPositionId = SelectedFromPosition?.Id,
                ToPositionId = SelectedToPosition.Id,
                Direction = Direction,
                MovementDate = MovementDate,
                Remarks = Remarks
            };

            await _movementService.RecordMovementAsync(dto);
            WeakReferenceMessenger.Default.Send(new DocumentMovedMessage(Document.Id));

            RequestClose?.Invoke();
        }
        catch (ArgumentException ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}

public class DocumentMovedMessage : ValueChangedMessage<int>
{
    public DocumentMovedMessage(int documentId) : base(documentId) { }
}
