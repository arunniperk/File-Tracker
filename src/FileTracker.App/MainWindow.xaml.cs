namespace FileTracker.App;

public partial class MainWindow : System.Windows.Window
{
    public MainWindow(
        ViewModels.MainViewModel mainVm,
        ViewModels.RegisterDocumentViewModel registerVm)
    {
        InitializeComponent();
        DataContext = mainVm;
        RegisterDocumentViewControl.DataContext = registerVm;
    }
}
