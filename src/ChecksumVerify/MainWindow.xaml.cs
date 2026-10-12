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
    // Client-area size in device-independent pixels (scaled by the monitor DPI before use)
    private const int WindowWidth = 520;
    private const int InputHeight = 400;
    private const int ResultHeight = 460;
    private const int ResultWithHashHeight = 560;

    private const string Sha3Requirement = "Windows 11 24H2+";

    private readonly string _filePath;
    private CancellationTokenSource? _cts;
    private string? _hashFilePath;
    private bool _isComputing;

    // The algorithm the user picked by hand; auto-detection never overrides it while the hash length fits
    private HashAlgorithmId? _userChoice;
    private bool _suppressAlgorithmEvents;

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

    private readonly record struct VerifyInput(string? Expected, HashAlgorithmId Algorithm, string? Error);

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
                item.Content = $"{label} (requires {Sha3Requirement})";
                item.IsEnabled = false;
                ToolTipService.SetToolTip(item,
                    $"{label} needs {Sha3Requirement}: older Windows versions lack SHA-3 in the system cryptography library.");
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
        var work = display.WorkArea;

        // Move first so the DPI of the target monitor applies to the resize below
        AppWindow.Move(new PointInt32(work.X, work.Y));
        ResizeClient(WindowWidth, InputHeight);
        AppWindow.Move(new PointInt32(
            work.X + (work.Width - AppWindow.Size.Width) / 2,
            work.Y + (work.Height - AppWindow.Size.Height) / 2));
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

    // Auto-detection by length only decides when it has to: an algorithm the user picked
    // (or that already fits the hash length) is kept, because 64 hex chars are ambiguous.
    private void AutoSelectAlgorithm(DetectedHash detected)
    {
        if (detected.SuggestedAlgorithm is not { } suggested) return;

        if (detected.IsExplicit)
        {
            SelectAlgorithm(suggested);
            return;
        }

        var length = detected.Hash.Length;
        if (_userChoice is { } choice && HashEngine.HexLength(choice) == length)
            SelectAlgorithm(choice);
        else if (HashEngine.HexLength(SelectedAlgorithm) != length)
            SelectAlgorithm(suggested);
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

    private static string LabelOf(HashAlgorithmId id) => Algorithms.First(a => a.Id == id).Label;

    private void UpdateAmbiguityNote()
    {
        AmbiguityNote.Visibility = Visibility.Collapsed;
        if (HashModeRadio.IsChecked != true) return;

        var detected = ClipboardDetector.TryDetect(HashInput.Text);
        if (detected is null or { IsExplicit: true }) return;

        var candidates = HashEngine.CandidatesForHexLength(detected.Hash.Length);
        if (candidates.Count < 2) return;

        AmbiguityNote.Text =
            $"A {detected.Hash.Length}-character hash could be {string.Join(", ", candidates.Select(LabelOf))}. " +
            $"Selected: {LabelOf(SelectedAlgorithm)} — change the algorithm if the hash comes from another tool.";
        AmbiguityNote.Visibility = Visibility.Visible;
    }

    // ── Input handlers ────────────────────────────────────────────────────────

    private void OnModeChanged(object sender, RoutedEventArgs e)
    {
        var isHash = HashModeRadio.IsChecked == true;
        HashPanel.Visibility = isHash ? Visibility.Visible   : Visibility.Collapsed;
        FilePanel.Visibility = isHash ? Visibility.Collapsed : Visibility.Visible;
        UpdateAmbiguityNote();
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

    // Everything that can be wrong with the inputs is checked here, *before* the (possibly
    // multi-GB) file is read, so the user hears about it immediately.
    private VerifyInput ResolveInput()
    {
        var algorithm = SelectedAlgorithm;
        string expected;

        if (HashModeRadio.IsChecked == true)
        {
            expected = ClipboardDetector.StripAlgoPrefix(HashInput.Text);
            if (expected.Length == 0)
                return new(null, algorithm, "No hash provided. Enter or paste an expected hash first.");
            if (!ClipboardDetector.IsHexString(expected))
                return new(null, algorithm, "The expected hash must contain only hexadecimal characters (0-9, a-f).");
        }
        else
        {
            if (_hashFilePath is null)
                return new(null, algorithm, "No hash file selected. Browse for a hash file first.");

            var fileName = Path.GetFileName(_filePath);
            var parsed = HashFileParser.TryParse(_hashFilePath, fileName);
            if (parsed is null)
                return new(null, algorithm, $"No entry found for {fileName} in the hash file.");

            expected = parsed.Hash;
            if (parsed.Algorithm is { } fromFile)
            {
                if (!HashEngine.IsSupported(fromFile))
                    return new(null, fromFile, $"{LabelOf(fromFile)} needs {Sha3Requirement}.");

                // A tag/extension is authoritative; a guess from the length only wins if the current choice cannot fit
                if (parsed.IsAlgorithmCertain || HashEngine.HexLength(algorithm) != expected.Length)
                {
                    algorithm = fromFile;
                    SelectAlgorithm(fromFile);
                }
            }
        }

        var needed = HashEngine.HexLength(algorithm);
        if (expected.Length != needed)
            return new(null, algorithm,
                $"The expected hash has {expected.Length} characters, but {LabelOf(algorithm)} produces {needed}. Check the selected algorithm.");

        return new(expected, algorithm, null);
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

        ResizeClient(WindowWidth, showHash ? ResultWithHashHeight : ResultHeight);
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
        ResizeClient(WindowWidth, InputHeight);
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
