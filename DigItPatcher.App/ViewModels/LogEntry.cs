using CommunityToolkit.Mvvm.ComponentModel;

namespace DigItPatcher.App.ViewModels;

/// <summary>Severity of a log line, which decides the tone it is drawn in.</summary>
internal enum LogLevel
{
    Plain,
    Info,
    Success,
    Warning,
    Error
}

/// <summary>One line of the log, whose text changes while a line is the one being worked on.</summary>
internal sealed partial class LogEntry(LogLevel level, string text) : ObservableObject
{
    [ObservableProperty]
    private string _text = text;

    public LogLevel Level { get; } = level;
}
