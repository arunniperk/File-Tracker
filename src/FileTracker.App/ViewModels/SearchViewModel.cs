using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileTracker.Core.Dtos;
using FileTracker.Core.Models;
using FileTracker.Core.Services;

namespace FileTracker.App.ViewModels;

public partial class SearchViewModel : ObservableObject
{
    private readonly IDocumentService _docService;

    // ── Filter properties ──────────────────────────────────────

    [ObservableProperty]
    private string _searchFileNumber = string.Empty;

    [ObservableProperty]
    private string _searchTrackingId = string.Empty;

    [ObservableProperty]
    private string _searchSubject = string.Empty;

    [ObservableProperty]
    private string _searchSenderRecipient = string.Empty;

    [ObservableProperty]
    private DateTime? _searchFromDate;

    [ObservableProperty]
    private DateTime? _searchToDate;

    // ── Results and pagination ──────────────────────────────────

    [ObservableProperty]
    private ObservableCollection<Document> _searchResults = new();

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _pageSize = 20;

    [ObservableProperty]
    private bool _hasResults;

    [ObservableProperty]
    private bool _isSearching;

    // ── Computed properties ─────────────────────────────────────

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    public bool HasPreviousPage => CurrentPage > 1;

    public bool HasNextPage => CurrentPage < TotalPages;

    public string PageIndicator => TotalCount > 0
        ? $"Page {CurrentPage} of {TotalPages} ({TotalCount} results)"
        : "No results";

    public SearchViewModel(IDocumentService docService)
    {
        _docService = docService;
    }

    // ── Commands ─────────────────────────────────────────────────

    [RelayCommand]
    private async Task SearchAsync()
    {
        IsSearching = true;
        CurrentPage = 1;

        await ExecuteSearchAsync();

        IsSearching = false;
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (!HasNextPage) return;

        IsSearching = true;
        CurrentPage++;
        await ExecuteSearchAsync();
        IsSearching = false;
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (!HasPreviousPage) return;

        IsSearching = true;
        CurrentPage--;
        await ExecuteSearchAsync();
        IsSearching = false;
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchFileNumber = string.Empty;
        SearchTrackingId = string.Empty;
        SearchSubject = string.Empty;
        SearchSenderRecipient = string.Empty;
        SearchFromDate = null;
        SearchToDate = null;
        CurrentPage = 1;
        TotalCount = 0;
        HasResults = false;
        SearchResults.Clear();

        // Reload all documents by executing an unfiltered search
        _ = SearchAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────

    private async Task ExecuteSearchAsync()
    {
        var dto = new SearchDocumentDto
        {
            OriginalFileNumber = string.IsNullOrWhiteSpace(SearchFileNumber) ? null : SearchFileNumber,
            TrackingId = string.IsNullOrWhiteSpace(SearchTrackingId) ? null : SearchTrackingId,
            Subject = string.IsNullOrWhiteSpace(SearchSubject) ? null : SearchSubject,
            SenderOrRecipient = string.IsNullOrWhiteSpace(SearchSenderRecipient) ? null : SearchSenderRecipient,
            FromDate = SearchFromDate,
            ToDate = SearchToDate,
            Page = CurrentPage,
            PageSize = PageSize
        };

        var result = await _docService.SearchAsync(dto);

        SearchResults = new ObservableCollection<Document>(result.Results);
        TotalCount = result.TotalCount;
        HasResults = result.TotalCount > 0;

        // Notify computed properties
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(HasPreviousPage));
        OnPropertyChanged(nameof(HasNextPage));
        OnPropertyChanged(nameof(PageIndicator));
    }
}
