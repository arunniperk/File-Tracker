using System.Windows;
using FileTracker.Core.Models;
using FileTracker.App.ViewModels;

namespace FileTracker.App.Views;

public partial class RecordMovementWindow : Window
{
    public RecordMovementWindow()
    {
        InitializeComponent();
    }

    public async Task LoadDocumentAsync(Document document)
    {
        if (DataContext is RecordMovementViewModel vm)
        {
            vm.RequestClose += () => { DialogResult = true; Close(); };
            await vm.LoadCommand.ExecuteAsync(document);
        }
    }
}
