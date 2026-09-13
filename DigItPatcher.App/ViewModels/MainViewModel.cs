using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigItPatcher.App.Views;
using DigItPatcher.Core;
using DigItPatcher.Core.Install;
using DigItPatcher.Core.Patching;
using Microsoft.Win32;

namespace DigItPatcher.App.ViewModels;

/// <summary>State of the main window: the folder, the two sections, the buttons, and the log.</summary>
internal sealed partial class MainViewModel : ObservableObject
{
    private readonly AppIdentity _identity;
    private readonly Scanner _scanner = new();
    private readonly PatchRunner _runner = new();

    private GameInstall? _install;
    private ScanResult? _lastScan;
    private CancellationTokenSource? _cancellation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFolder))]
    private string? _folderPath;

    [ObservableProperty]
    private string _identificationLine = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApply))]
    private bool _repairOffered;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApply))]
    private bool _repairChecked;

    [ObservableProperty]
    private string _repairSubtitle = string.Empty;

    [ObservableProperty]
    private bool _fixesOffered;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApply))]
    private bool _fixesChecked;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApply))]
    private bool _fixesPending;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApply))]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _backupExists;

    // Narrower than IsBusy: a scan has nothing to stop, so Cancel belongs to a run and to nothing else.
    [ObservableProperty]
    private bool _isRunning;

    public MainViewModel(AppIdentity identity)
    {
        _identity = identity;
        Log.CollectionChanged += (_, _) => OnPropertyChanged(nameof(LogIsEmpty));

        // An install the tool is sitting inside is the only folder opened without being asked for: nothing
        // is remembered between runs, so every other case starts empty and goes through Browse.
        if (GameInstall.TryOpen(AppContext.BaseDirectory, out var besideExecutable)) _ = OpenAsync(besideExecutable);
    }

    public ObservableCollection<FixRowViewModel> Rows { get; } = [];

    public ObservableCollection<LogEntry> Log { get; } = [];

    public bool HasFolder => !string.IsNullOrEmpty(FolderPath);

    public string IdentityLine => $"{_identity.Product} {_identity.Version}";

    /// <summary>The line the log area carries while it has nothing to record.</summary>
    public string WelcomeLine => $"Welcome to {IdentityLine}";

    /// <summary>Whether the log has anything to show, which is what the welcome line stands in for.</summary>
    public bool LogIsEmpty => Log.Count == 0;

    /// <summary>Whether a run would do anything, which is what the copy allows and the user has left ticked.</summary>
    public bool CanApply => IsIdle && ((RepairOffered && RepairChecked) || (FixesPending && FixesChecked));

    /// <summary>Whether the window is between jobs, which is when its controls answer.</summary>
    public bool IsIdle => !IsBusy;

    /// <summary>Asks for a folder and scans it, or reports in the log that it holds no game.</summary>
    [RelayCommand]
    private async Task BrowseAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select the Dig It! Game Folder",
            InitialDirectory = FolderPath ?? string.Empty,
        };

        if (dialog.ShowDialog() != true) return;

        if (!GameInstall.TryOpen(dialog.FolderName, out var install))
        {
            // A rejected folder leaves the window on whatever it was already showing, which stays true.
            Log.Add(new LogEntry(LogLevel.Warning, $"No Dig It! game found in {dialog.FolderName}."));
            return;
        }

        await OpenAsync(install);
    }

    /// <summary>Runs the plan the checkboxes describe, then re-scans so the window shows what the files now hold.</summary>
    [RelayCommand]
    private async Task ApplyAsync()
    {
        if (_install is null || _lastScan is null) return;

        var install = _install;
        var scan = _lastScan;
        var plan = PatchPlan.From(scan, RepairChecked, FixesChecked);

        var line = new LogEntry(LogLevel.Info, Wording.ApplyingLine(plan));
        Log.Add(line);

        _cancellation = new CancellationTokenSource();
        IsBusy = true;
        IsRunning = true;

        try
        {
            var token = _cancellation.Token;
            var result = await BusyLine.While(line, () => _runner.Run(install, plan, scan.Build, _identity, token));
            foreach (var entry in Wording.ApplyResult(result)) Log.Add(entry);

            var after = await BusyLine.While(Log[^1], () => _scanner.Scan(install));
            Render(install, after);
            foreach (var entry in Wording.Discrepancies(RunVerification.AfterRun(plan, result, scan, after))) Log.Add(entry);
        }
        catch (OperationCanceledException)
        {
            var canceled = new LogEntry(LogLevel.Warning, "The operation was canceled and nothing was written.");
            Log.Add(canceled);

            var after = await BusyLine.While(canceled, () => _scanner.Scan(install));
            Render(install, after);

            var nothingWritten = new PatchRunResult(true, [], [], [], null);
            foreach (var entry in Wording.Discrepancies(RunVerification.AfterRun(plan, nothingWritten, scan, after))) Log.Add(entry);
        }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
            IsRunning = false;
            IsBusy = false;
        }
    }

    /// <summary>Stops a run that is still searching, which is every moment before the first byte is written.</summary>
    [RelayCommand]
    private void Cancel() => _cancellation?.Cancel();

    /// <summary>Copies the backup folder back over the install, after the user confirms which folder that is.</summary>
    [RelayCommand]
    private async Task RestoreBackupAsync()
    {
        if (_install is null) return;

        var install = _install;
        var confirmed = MessageDialog.Confirm(Application.Current.MainWindow, "Restore Backup",
            $"This restores the files in {install.Folder} to the state they were in before this tool last modified them.");
        if (!confirmed) return;

        var line = new LogEntry(LogLevel.Info, "Restoring backup...");
        Log.Add(line);

        IsBusy = true;

        try
        {
            var (restored, failureReason) = await BusyLine.While(line, () =>
            {
                var success = BackupSet.TryRestore(install, out var reason);
                return (success, reason);
            });
            Log.Add(Wording.RestoreResult(restored, failureReason));

            var after = await BusyLine.While(Log[^1], () => _scanner.Scan(install));
            Render(install, after);

            if (restored) foreach (var entry in Wording.Discrepancies(RunVerification.AfterRestore(install, after))) Log.Add(entry);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task OpenAsync(GameInstall install)
    {
        FolderPath = install.Folder;

        // The log is the receipt of one copy, so opening another starts it again rather than stacking onto it.
        Log.Clear();

        var line = new LogEntry(LogLevel.Info, $"Scanning {install.Folder}...");
        Log.Add(line);

        IsBusy = true;

        try
        {
            var scan = await BusyLine.While(line, () => _scanner.Scan(install));
            Render(install, scan);
            foreach (var entry in Wording.ScanLog(scan)) Log.Add(entry);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Updates the folder's identification, sections, rows, and backup button from a fresh scan.</summary>
    private void Render(GameInstall install, ScanResult scan)
    {
        _install = install;
        _lastScan = scan;

        IdentificationLine = Wording.IdentificationLine(scan);

        RepairOffered = scan.RepairsOffered;
        RepairChecked = RepairOffered;
        RepairSubtitle = Wording.RepairSubtitle(scan);

        FixesOffered = scan.FixesOffered;
        FixesPending = scan.FixesOffered && !scan.AlreadyFixed;
        FixesChecked = FixesPending;

        Rows.Clear();
        foreach (var fix in scan.Fixes) Rows.Add(new FixRowViewModel(fix));

        BackupExists = BackupSet.Exists(install);
    }
}
