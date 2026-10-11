using Microsoft.UI.Xaml;

namespace ChecksumVerify;

public partial class App : Application
{
    private readonly string[] _args;
    private Window? _window;

    public App(string[] args)
    {
        _args = args;
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var filePath = _args.Length > 0 ? _args[0].Trim('"') : null;
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            Exit();
            return;
        }

        _window = new MainWindow(filePath);
        _window.Activate();
    }
}
