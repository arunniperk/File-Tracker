using System.ComponentModel;
using System.Windows;

namespace FileTracker.App;

public partial class MainWindow : Window
{
    public MainWindow(
        ViewModels.MainViewModel mainVm,
        ViewModels.RegisterDocumentViewModel registerVm)
    {
        InitializeComponent();
        DataContext = mainVm;
        RegisterDocumentViewControl.DataContext = registerVm;
    }

    /// <summary>
    /// D-11: Unsaved changes warning.
    /// The ONE allowed code-behind pattern per RESEARCH.md.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm && vm.HasUnsavedChanges)
        {
            var result = MessageBox.Show(
                "You have unsaved changes. Discard them?",
                "Unsaved Changes",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
                e.Cancel = true;
        }

        base.OnClosing(e);
    }
}
