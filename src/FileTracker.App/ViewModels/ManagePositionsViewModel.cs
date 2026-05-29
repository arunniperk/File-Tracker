using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileTracker.Core.Models;
using FileTracker.Core.Services;

namespace FileTracker.App.ViewModels;

public partial class ManagePositionsViewModel : ObservableObject
{
    private readonly IPositionService _positionService;

    [ObservableProperty]
    private ObservableCollection<Position> _positions = new();

    [ObservableProperty]
    private Position? _selectedPosition;

    [ObservableProperty]
    private string _newPositionName = string.Empty;

    [ObservableProperty]
    private string _renameText = string.Empty;

    [ObservableProperty]
    private bool _isEditingName;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public ManagePositionsViewModel(IPositionService positionService)
    {
        _positionService = positionService;
        _ = LoadPositionsAsync();
    }

    [RelayCommand]
    private async Task LoadPositionsAsync()
    {
        try
        {
            var positions = await _positionService.GetAllAsync();
            Positions = new ObservableCollection<Position>(positions);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task AddPositionAsync()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(NewPositionName))
        {
            ErrorMessage = "Position name is required.";
            return;
        }

        try
        {
            await _positionService.AddAsync(NewPositionName.Trim());
            NewPositionName = string.Empty;
            await LoadPositionsAsync();
        }
        catch (ArgumentException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void StartRename(Position position)
    {
        SelectedPosition = position;
        RenameText = position.Name;
        IsEditingName = true;
    }

    [RelayCommand]
    private async Task SaveRenameAsync()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(RenameText))
        {
            ErrorMessage = "Position name is required.";
            return;
        }

        if (SelectedPosition is null) return;

        try
        {
            await _positionService.RenameAsync(SelectedPosition.Id, RenameText.Trim());
            IsEditingName = false;
            await LoadPositionsAsync();
        }
        catch (ArgumentException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void CancelRename()
    {
        IsEditingName = false;
    }

    [RelayCommand]
    private async Task MoveUpAsync(Position position)
    {
        try
        {
            await _positionService.MoveUpAsync(position.Id);
            await LoadPositionsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task MoveDownAsync(Position position)
    {
        try
        {
            await _positionService.MoveDownAsync(position.Id);
            await LoadPositionsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DeactivateAsync(Position position)
    {
        try
        {
            await _positionService.DeactivateAsync(position.Id);
            await LoadPositionsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
