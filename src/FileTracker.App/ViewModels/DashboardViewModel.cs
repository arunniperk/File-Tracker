using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using FileTracker.Core.Dtos;
using FileTracker.Core.Models;
using FileTracker.Data;
using FileTracker.App.Views;
using Microsoft.Extensions.DependencyInjection;

namespace FileTracker.App.ViewModels;

public partial class DashboardViewModel : ObservableObject,
    IRecipient<DocumentRegisteredMessage>,
    IRecipient<DocumentMovedMessage>
{
    private readonly IDocumentRepository _docRepo;
    private readonly IServiceProvider _services;

    // ── Collections ──────────────────────────────────────────────

    [ObservableProperty]
    private ObservableCollection<OfficerPendingCountDto> _pendingByOfficer = new();

    [ObservableProperty]
    private ObservableCollection<Document> _recentDocuments = new();

    [ObservableProperty]
    private ObservableCollection<Document> _overdueDocuments = new();

    [ObservableProperty]
    private DateTime _lastUpdated;

    // ── Computed properties ──────────────────────────────────────

    public bool HasPending => PendingByOfficer.Count > 0;
    public bool HasRecent => RecentDocuments.Count > 0;
    public bool HasOverdue => OverdueDocuments.Count > 0;
    public int TotalPending => PendingByOfficer.Sum(o => o.DocumentCount);

    public DashboardViewModel(IDocumentRepository docRepo, IServiceProvider services)
    {
        _docRepo = docRepo;
        _services = services;

        WeakReferenceMessenger.Default.Register<DocumentRegisteredMessage>(this);
        WeakReferenceMessenger.Default.Register<DocumentMovedMessage>(this);

        _ = RefreshAsync();
    }

    // ── Message handlers ─────────────────────────────────────────

    public void Receive(DocumentRegisteredMessage message)
    {
        _ = RefreshAsync();
    }

    public void Receive(DocumentMovedMessage message)
    {
        _ = RefreshAsync();
    }

    // ── Commands ─────────────────────────────────────────────────

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var pendingTask = _docRepo.GetPendingByOfficerAsync();
        var recentTask = _docRepo.GetRecentAsync(7);
        var overdueTask = _docRepo.GetOverdueAsync(7);

        await Task.WhenAll(pendingTask, recentTask, overdueTask);

        PendingByOfficer = new ObservableCollection<OfficerPendingCountDto>(pendingTask.Result);
        RecentDocuments = new ObservableCollection<Document>(recentTask.Result);
        OverdueDocuments = new ObservableCollection<Document>(overdueTask.Result);
        LastUpdated = DateTime.Now;

        // Notify computed properties
        OnPropertyChanged(nameof(HasPending));
        OnPropertyChanged(nameof(HasRecent));
        OnPropertyChanged(nameof(HasOverdue));
        OnPropertyChanged(nameof(TotalPending));
    }

    [RelayCommand]
    private async Task NavigateToOfficerAsync(OfficerPendingCountDto officer)
    {
        // Resolve SearchViewModel and set filter to this officer's name
        var searchVm = _services.GetRequiredService<SearchViewModel>();
        searchVm.SearchSenderRecipient = officer.OfficerName;
        await searchVm.SearchCommand.ExecuteAsync(null);

        // Switch to the Documents tab
        WeakReferenceMessenger.Default.Send(new SwitchToDocumentsTabMessage(true));
    }

    [RelayCommand]
    private async Task OpenDocumentAsync(Document document)
    {
        var app = (App)System.Windows.Application.Current;
        var detailVm = app.Services.GetRequiredService<DocumentDetailViewModel>();
        if (detailVm is null) return;

        await detailVm.LoadDocumentAsync(document.Id);

        var window = new DocumentDetailWindow
        {
            DataContext = detailVm,
            Owner = System.Windows.Application.Current.MainWindow
        };
        window.Show();
    }
}

/// <summary>
/// Message sent when the dashboard requests navigation to the Documents tab.
/// </summary>
public class SwitchToDocumentsTabMessage : ValueChangedMessage<bool>
{
    public SwitchToDocumentsTabMessage(bool value) : base(value) { }
}
