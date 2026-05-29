using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileTracker.Core.Dtos;
using FileTracker.Core.Services;
using Microsoft.Win32;

namespace FileTracker.App.ViewModels;

public partial class ReportViewModel : ObservableObject
{
    private readonly IReportService _reportService;

    [ObservableProperty]
    private int _selectedMonth;

    [ObservableProperty]
    private int _selectedYear;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private ReportDataDto? _reportData;

    public List<MonthItem> Months { get; }
    public List<int> Years { get; }

    public ReportViewModel(IReportService reportService)
    {
        _reportService = reportService;

        var now = DateTime.Now;
        SelectedMonth = now.Month;
        SelectedYear = now.Year;

        Months = Enumerable.Range(1, 12)
            .Select(m => new MonthItem { Number = m, Name = new DateTime(2000, m, 1).ToString("MMMM") })
            .ToList();

        Years = Enumerable.Range(now.Year - 4, 5).ToList();
    }

    [RelayCommand]
    private async Task GeneratePdfAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "PDF Files (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            FileName = $"MonthlyReport_{SelectedYear}_{SelectedMonth:D2}.pdf",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() != true) return;

        IsGenerating = true;
        StatusMessage = "Generating PDF report...";

        try
        {
            var request = new ReportRequestDto { Month = SelectedMonth, Year = SelectedYear };
            await Task.Run(() => _reportService.GeneratePdfReportAsync(request, dialog.FileName));
            StatusMessage = "PDF report saved successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task GenerateExcelAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            DefaultExt = ".xlsx",
            FileName = $"DocumentExport_{SelectedYear}_{SelectedMonth:D2}.xlsx",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() != true) return;

        IsGenerating = true;
        StatusMessage = "Generating Excel export...";

        try
        {
            var request = new ReportRequestDto { Month = SelectedMonth, Year = SelectedYear };
            await Task.Run(() => _reportService.GenerateExcelExportAsync(request, dialog.FileName));
            StatusMessage = "Excel export saved successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task PreviewAsync()
    {
        IsGenerating = true;
        StatusMessage = "Loading report data...";

        try
        {
            var request = new ReportRequestDto { Month = SelectedMonth, Year = SelectedYear };
            ReportData = await _reportService.GenerateReportDataAsync(request);
            StatusMessage = "Preview loaded.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }
}

public class MonthItem
{
    public int Number { get; set; }
    public string Name { get; set; } = string.Empty;
}
