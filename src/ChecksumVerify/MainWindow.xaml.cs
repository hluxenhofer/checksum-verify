using ChecksumVerify.Core;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage.Pickers;

namespace ChecksumVerify;

public sealed partial class MainWindow : Window
{
    // Client-area width in device-independent pixels (scaled by the monitor DPI before use).
    // The height is not fixed: the window is sized to its content (see FitToContent).
    private const int WindowWidth = 520;
    // Only until the first layout pass can measure the real content
    private const int InitialHeight = 400;

    private readonly string _filePath;
    private CancellationTokenSource? _cts;
    private string? _hashFilePath;
    private bool _isComputing;
    private RectInt32 _initialWorkArea;

    // The algorithm the user picked by hand; auto-detection never overrides it while the hash length fits
    private HashAlgorithmId? _userChoice;
    private bool _suppressAlgorithmEvents;

    // Display order in the dropdown
    private static readonly HashAlgorithmId[] Algorithms =
    [
        HashAlgorithmId.Sha256,
        HashAlgorithmId.Sha512,
        HashAlgorithmId.Sha3_256,
        HashAlgorithmId.Sha3_512,
        HashAlgorithmId.Blake3,
        HashAlgorithmId.Blake2b_256,
        HashAlgorithmId.Blake2b_512,
        HashAlgorithmId.Md5,
        HashAlgorithmId.Sha1,
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

        foreach (var id in Algorithms)
        {
            var label = id.DisplayName();
            var item = new ComboBoxItem { Tag = id };
            if (!HashEngine.IsSupported(id))
            {
                item.Content = $"{label} (requires {HashAlgorithmNames.Sha3Requirement})";
                item.IsEnabled = false;
                ToolTipService.SetToolTip(item,
                    $"{label} needs {HashAlgorithmNames.Sha3Requirement}: older Windows versions lack SHA-3 in the system cryptography library.");
            }
            else
            {
                item.Content = label;
            }
            AlgorithmCombo.Items.Add(item);
        }
        SelectAlgorithm(HashAlgorithmId.Sha256);

        if (AppWindow.Presenter is OverlappedPresenter p)
            p.IsAlwaysOnTop = true;

        PositionOnActiveMonitor();
        Closed += (_, _) => _cts?.Cancel();

        // Templates (ComboBox, TextBox, ...) only exist once the tree is live, so the real
        // content height can be measured only now. Size to it and re-center once.
        ((FrameworkElement)Content).Loaded += (_, _) =>
        {
            FitToContent();
            CenterInInitialWorkArea();
        };

        _ = FillFromClipboardAsync();
    }

    // ── Window size and placement ────────────────────────────────────────────

    // AppWindow works in physical pixels, XAML in effective pixels: scale by the monitor DPI.
    private double DpiScale =>
        NativeMethods.GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;

    private void ResizeClient(int widthDip, int heightDip)
    {
        var scale = DpiScale;
        AppWindow.ResizeClient(new SizeInt32(
            (int)Math.Ceiling(widthDip * scale),
            (int)Math.Ceiling(heightDip * scale)));
        KeepInsideWorkArea();
    }

    private void PositionOnActiveMonitor()
    {
        // The monitor under the cursor is the one the user was just working on
        var display = NativeMethods.GetCursorPos(out var cursor)
            ? DisplayArea.GetFromPoint(new PointInt32(cursor.X, cursor.Y), DisplayAreaFallback.Nearest)
            : DisplayArea.Primary;
        _initialWorkArea = display.WorkArea;

        // Move first so the DPI of the target monitor applies to the resize below
        AppWindow.Move(new PointInt32(_initialWorkArea.X, _initialWorkArea.Y));
        ResizeClient(WindowWidth, InitialHeight);
        CenterInInitialWorkArea();
    }

    private void CenterInInitialWorkArea() =>
        AppWindow.Move(new PointInt32(
            _initialWorkArea.X + (_initialWorkArea.Width - AppWindow.Size.Width) / 2,
            _initialWorkArea.Y + (_initialWorkArea.Height - AppWindow.Size.Height) / 2));

    // Sizes the window to what the content needs at the fixed width, so nothing is clipped
    // at any DPI, text scale or error-message length, and there is no dead space.
    // Call after anything that changes the content's height.
    private void FitToContent()
    {
        if (Content is not FrameworkElement root || root.XamlRoot is null) return;
        root.Measure(new Windows.Foundation.Size(WindowWidth, double.PositiveInfinity));
        ResizeClient(WindowWidth, (int)Math.Ceiling(root.DesiredSize.Height));
    }

    // Growing for a result must not push the buttons off the bottom of the screen
    private void KeepInsideWorkArea()
    {
        var work = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        var position = AppWindow.Position;
        var size = AppWindow.Size;
        var y = Math.Max(work.Y, Math.Min(position.Y, work.Y + work.Height - size.Height));
        var x = Math.Max(work.X, Math.Min(position.X, work.X + work.Width - size.Width));
        if (x != position.X || y != position.Y)
            AppWindow.Move(new PointInt32(x, y));
    }

    // ── Clipboard + algorithm detection ──────────────────────────────────────

    private async Task FillFromClipboardAsync()
    {
        try
        {
            var content = Clipboard.GetContent();
            if (content is null || !content.Contains(StandardDataFormats.Text)) return;
            var detected = ClipboardDetector.TryDetect(await content.GetTextAsync());
            if (detected is null) return;
            HashModeRadio.IsChecked = true;
            ApplyDetected(detected);
        }
        catch { }
    }

    private void ApplyDetected(DetectedHash detected)
    {
        HashInput.Text = detected.Hash;
        AutoSelectAlgorithm(detected);
    }

    private void AutoSelectAlgorithm(DetectedHash detected)
    {
        if (AlgorithmSelector.Choose(detected, SelectedAlgorithm, _userChoice) is { } id)
            SelectAlgorithm(id);
    }

    private bool SelectAlgorithm(HashAlgorithmId id)
    {
        for (int i = 0; i < AlgorithmCombo.Items.Count; i++)
        {
            if (AlgorithmCombo.Items[i] is ComboBoxItem { Tag: HashAlgorithmId aid } item && aid == id)
            {
                if (!item.IsEnabled) return false;
                _suppressAlgorithmEvents = true;
                try { AlgorithmCombo.SelectedIndex = i; }
                finally { _suppressAlgorithmEvents = false; }
                return true;
            }
        }
        return false;
    }

    private HashAlgorithmId SelectedAlgorithm =>
        AlgorithmCombo.SelectedItem is ComboBoxItem { Tag: HashAlgorithmId id } ? id : HashAlgorithmId.Sha256;

    private void UpdateAmbiguityNote()
    {
        var note = HashModeRadio.IsChecked == true
            ? AlgorithmSelector.DescribeAmbiguity(ClipboardDetector.TryDetect(HashInput.Text), SelectedAlgorithm)
            : null;
        AmbiguityNote.Text = note ?? string.Empty;
        AmbiguityNote.Visibility = note is null ? Visibility.Collapsed : Visibility.Visible;
        FitToContent();
    }

    // ── Input handlers ────────────────────────────────────────────────────────

    private void OnModeChanged(object sender, RoutedEventArgs e)
    {
        var isHash = HashModeRadio.IsChecked == true;
        HashPanel.Visibility = isHash ? Visibility.Visible   : Visibility.Collapsed;
        FilePanel.Visibility = isHash ? Visibility.Collapsed : Visibility.Visible;
        UpdateAmbiguityNote(); // also refits the window
        InvalidateResult();
    }

    private void OnHashInputChanged(object sender, TextChangedEventArgs e)
    {
        if (ClipboardDetector.TryDetect(HashInput.Text) is { } detected)
            AutoSelectAlgorithm(detected);
        UpdateAmbiguityNote();
        InvalidateResult();
    }

    private void OnAlgorithmChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_suppressAlgorithmEvents)
            _userChoice = SelectedAlgorithm;
        UpdateAmbiguityNote();
        InvalidateResult();
    }

    private async void OnPaste(object sender, RoutedEventArgs e)
    {
        try
        {
            var content = Clipboard.GetContent();
            if (content is null || !content.Contains(StandardDataFormats.Text)) return;
            var text = await content.GetTextAsync();
            if (string.IsNullOrWhiteSpace(text)) return;

            if (ClipboardDetector.TryDetect(text) is { } detected)
                ApplyDetected(detected);
            else
                HashInput.Text = text.Trim();
        }
        catch { }
    }

    private async void OnBrowse(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        foreach (var extension in new[]
        {
            ".sha256", ".sha512", ".sha3-256", ".sha3-512", ".sha1", ".md5",
            ".b2", ".b3", ".blake2", ".blake2b", ".blake3",
            ".sha256sum", ".sha512sum",
        })
            picker.FileTypeFilter.Add(extension);
        picker.FileTypeFilter.Add("*");

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        _hashFilePath = file.Path;
        HashFileInput.Text = file.Name;

        if (HashFileParser.AlgorithmForHashFile(file.Path) is { } fromExtension)
            SelectAlgorithm(fromExtension);
        InvalidateResult();
    }

    // ── Verify ────────────────────────────────────────────────────────────────

    // Everything that can be wrong with the inputs is checked before the (possibly multi-GB)
    // file is read, so the user hears about it immediately.
    private VerifyInput ResolveInput()
    {
        var input = HashModeRadio.IsChecked == true
            ? VerifyInputResolver.FromHash(HashInput.Text, SelectedAlgorithm)
            : VerifyInputResolver.FromHashFile(_hashFilePath, Path.GetFileName(_filePath), SelectedAlgorithm);

        // Show the algorithm that will actually be used (a hash file may dictate it)
        SelectAlgorithm(input.Algorithm);
        return input;
    }

    private async void OnVerify(object sender, RoutedEventArgs e)
    {
        HideResultElements();

        VerifyInput input;
        try { input = ResolveInput(); }
        catch (Exception ex)
        {
            ShowResult(false, $"Error: {ex.Message}");
            return;
        }
        if (input.Error is not null)
        {
            ShowResult(false, input.Error);
            return;
        }

        _cts = new CancellationTokenSource();
        _isComputing = true;
        VerifyButton.IsEnabled = false;
        CancelButton.Content = "Cancel";
        ComputeProgress.Value = 0;
        ComputeProgress.Visibility = Visibility.Visible;
        FitToContent();

        try
        {
            ComputeProgress.Maximum = Math.Max(1, new FileInfo(_filePath).Length);
            var progress = new Progress<long>(p => ComputeProgress.Value = p);

            var computed = await HashEngine.ComputeAsync(_filePath, input.Algorithm, progress, _cts.Token);

            if (HashEngine.Verify(computed, input.Expected!))
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
            _isComputing = false;
            ComputeProgress.Visibility = Visibility.Collapsed;
            FitToContent();
            VerifyButton.IsEnabled = true;
            CancelButton.Content = "Close";
            _cts?.Dispose();
            _cts = null;
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
        CancelButton.Content     = "Close";

        FitToContent();
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
        FitToContent();
    }

    private void OnCopyHash(object sender, RoutedEventArgs e) =>
        CopyToClipboard(ComputedHashDisplay.Text);

    // While hashing, Cancel only stops the computation (the window stays for another try);
    // otherwise it closes the window.
    private void OnCancel(object sender, RoutedEventArgs e)
    {
        if (_isComputing)
            _cts?.Cancel();
        else
            Close();
    }

    private static void CopyToClipboard(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }
}
