using ChecksumVerify.Core;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;

namespace ChecksumVerify;

public sealed partial class MainWindow : Window
{
    private readonly string _filePath;
    private CancellationTokenSource? _cts;
    private string? _hashFilePath;

    private static readonly (string Label, HashAlgorithmId Id)[] Algorithms =
    [
        ("SHA-256",     HashAlgorithmId.Sha256),
        ("SHA-512",     HashAlgorithmId.Sha512),
        ("SHA3-256",    HashAlgorithmId.Sha3_256),
        ("SHA3-512",    HashAlgorithmId.Sha3_512),
        ("BLAKE3",      HashAlgorithmId.Blake3),
        ("BLAKE2b-256", HashAlgorithmId.Blake2b_256),
        ("BLAKE2b-512", HashAlgorithmId.Blake2b_512),
        ("MD5",         HashAlgorithmId.Md5),
        ("SHA-1",       HashAlgorithmId.Sha1),
    ];

    public MainWindow(string filePath)
    {
        _filePath = filePath;
        InitializeComponent();
        SystemBackdrop = MicaController.IsSupported()
            ? new MicaBackdrop()
            : new DesktopAcrylicBackdrop();

        HashModeRadio.IsChecked = true;
        FileNameText.Text = Path.GetFileName(filePath);

        foreach (var (label, id) in Algorithms)
        {
            var item = new ComboBoxItem { Tag = id };
            if (!HashEngine.IsSupported(id))
            {
                item.Content = $"{label} (requires Windows 11 22H2+)";
                item.IsEnabled = false;
            }
            else
            {
                item.Content = label;
            }
            AlgorithmCombo.Items.Add(item);
        }
        AlgorithmCombo.SelectedIndex = 0;

        TryFillFromClipboard();

        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter p)
            p.IsAlwaysOnTop = true;

        ResizeAndCenter(520, 400);
    }

    private void ResizeAndCenter(int width, int height)
    {
        AppWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
        var display = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
            AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
        var x = display.WorkArea.X + (display.WorkArea.Width  - AppWindow.Size.Width)  / 2;
        var y = display.WorkArea.Y + (display.WorkArea.Height - AppWindow.Size.Height) / 2;
        AppWindow.Move(new Windows.Graphics.PointInt32(x, y));
    }

    private void ResizeOnly(int width, int height) =>
        AppWindow.Resize(new Windows.Graphics.SizeInt32(width, height));

    // ── Clipboard + algorithm detection ──────────────────────────────────────

    private void TryFillFromClipboard()
    {
        try
        {
            var text = Clipboard.GetContent()?.GetTextAsync().AsTask().GetAwaiter().GetResult();
            var detected = ClipboardDetector.TryDetect(text);
            if (detected is null) return;
            HashModeRadio.IsChecked = true;
            HashInput.Text = detected.Hash;
            if (detected.SuggestedAlgorithm is { } suggested)
                SelectAlgorithm(suggested);
        }
        catch { }
    }

    private void SelectAlgorithm(HashAlgorithmId id)
    {
        for (int i = 0; i < AlgorithmCombo.Items.Count; i++)
        {
            if (AlgorithmCombo.Items[i] is ComboBoxItem { Tag: HashAlgorithmId aid } && aid == id)
            {
                AlgorithmCombo.SelectedIndex = i;
                return;
            }
        }
    }

    private HashAlgorithmId SelectedAlgorithm =>
        AlgorithmCombo.SelectedItem is ComboBoxItem { Tag: HashAlgorithmId id } ? id : HashAlgorithmId.Sha256;

    // ── Input handlers ────────────────────────────────────────────────────────

    private void OnModeChanged(object sender, RoutedEventArgs e)
    {
        var isHash = HashModeRadio.IsChecked == true;
        HashPanel.Visibility = isHash ? Visibility.Visible   : Visibility.Collapsed;
        FilePanel.Visibility = isHash ? Visibility.Collapsed : Visibility.Visible;
        InvalidateResult();
    }

    private void OnHashInputChanged(object sender, TextChangedEventArgs e)
    {
        var detected = ClipboardDetector.TryDetect(HashInput.Text);
        if (detected?.SuggestedAlgorithm is { } suggested)
            SelectAlgorithm(suggested);
        InvalidateResult();
    }

    private void OnAlgorithmChanged(object sender, SelectionChangedEventArgs e) => InvalidateResult();


    private void OnPaste(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = Clipboard.GetContent()?.GetTextAsync().AsTask().GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(text)) return;
            var detected = ClipboardDetector.TryDetect(text);
            if (detected is not null)
            {
                HashInput.Text = detected.Hash;
                if (detected.SuggestedAlgorithm is { } suggested)
                    SelectAlgorithm(suggested);
            }
            else
            {
                HashInput.Text = text.Trim();
            }
        }
        catch { }
    }

    private async void OnBrowse(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add(".sha256");
        picker.FileTypeFilter.Add(".sha512");
        picker.FileTypeFilter.Add(".sha3-256");
        picker.FileTypeFilter.Add(".sha3-512");
        picker.FileTypeFilter.Add(".b2");
        picker.FileTypeFilter.Add(".b3");
        picker.FileTypeFilter.Add(".blake2");
        picker.FileTypeFilter.Add(".blake3");
        picker.FileTypeFilter.Add(".md5");
        picker.FileTypeFilter.Add(".sha256sum");
        picker.FileTypeFilter.Add(".sha512sum");
        picker.FileTypeFilter.Add("*");

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        _hashFilePath = file.Path;
        HashFileInput.Text = file.Name;

        var ext = Path.GetExtension(file.Name);
        if (HashFileParser.AlgorithmFromExtension(ext) is { } detected)
            SelectAlgorithm(detected);
    }

    // ── Verify ────────────────────────────────────────────────────────────────

    private async void OnVerify(object sender, RoutedEventArgs e)
    {
        _cts = new CancellationTokenSource();
        VerifyButton.IsEnabled = false;
        CancelButton.Content = "Cancel";
        HideResultElements();
        ComputeProgress.Visibility = Visibility.Visible;

        var fileSize = new FileInfo(_filePath).Length;
        ComputeProgress.Maximum = fileSize;
        var progress = new Progress<long>(p => ComputeProgress.Value = p);

        try
        {
            var computed = await HashEngine.ComputeAsync(
                _filePath, SelectedAlgorithm, progress, _cts.Token);

            string? expected = null;

            if (HashModeRadio.IsChecked == true)
            {
                expected = ClipboardDetector.StripAlgoPrefix(HashInput.Text);
            }
            else if (_hashFilePath is not null)
            {
                var parsed = HashFileParser.TryParse(_hashFilePath, Path.GetFileName(_filePath));
                expected = parsed?.Hash;
                if (expected is null)
                {
                    ShowResult(false, $"No entry found for {Path.GetFileName(_filePath)} in the hash file.");
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(expected))
            {
                ShowResult(false, "No hash provided. Enter or paste an expected hash first.");
                return;
            }

            if (HashEngine.Verify(computed, expected))
            {
                ShowResult(true, "Checksum verified — the file matches the expected hash.");
            }
            else
            {
                CopyToClipboard(computed);
                ComputedHashDisplay.Text = computed;
                ShowResult(false, "Checksum mismatch — the file does not match the expected hash.", showHash: true);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ShowResult(false, $"Error: {ex.Message}");
        }
        finally
        {
            ComputeProgress.Visibility = Visibility.Collapsed;
            VerifyButton.IsEnabled = true;
            CancelButton.Content = "Close";
        }
    }

    // ── Result banner ─────────────────────────────────────────────────────────

    private void ShowResult(bool success, string message, bool showHash = false)
    {
        var root = (FrameworkElement)Content;
        ResultBorder.Background  = (Brush)root.Resources[success ? "ResultSuccessBackgroundBrush" : "ResultErrorBackgroundBrush"];
        ResultText.Foreground    = (Brush)root.Resources[success ? "ResultSuccessForegroundBrush" : "ResultErrorForegroundBrush"];
        ResultText.Text          = message;
        HashSection.Visibility   = showHash ? Visibility.Visible   : Visibility.Collapsed;
        CopyHashButton.Visibility = showHash ? Visibility.Visible  : Visibility.Collapsed;
        ResultBorder.Visibility  = Visibility.Visible;

        ResizeOnly(520, showHash ? 560 : 460);
    }

    // Called at the start of a new verify run — hides elements, no resize.
    private void HideResultElements()
    {
        ResultBorder.Visibility   = Visibility.Collapsed;
        HashSection.Visibility    = Visibility.Collapsed;
        CopyHashButton.Visibility = Visibility.Collapsed;
    }

    // Called when the user changes inputs — hides elements AND shrinks back to input height.
    private void InvalidateResult()
    {
        if (ResultBorder.Visibility != Visibility.Visible) return;
        HideResultElements();
        ResizeOnly(520, 400);
    }

    private void OnCopyHash(object sender, RoutedEventArgs e) =>
        CopyToClipboard(ComputedHashDisplay.Text);

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        Close();
    }

    private static void CopyToClipboard(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }
}
