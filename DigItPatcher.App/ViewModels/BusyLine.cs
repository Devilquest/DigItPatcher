using System.Windows.Threading;

namespace DigItPatcher.App.ViewModels;

/// <summary>Turns one log line into the sign that work is under way, and puts it back when the work ends.</summary>
internal static class BusyLine
{
    private static readonly char[] Frames = ['|', '/', '-', '\\'];

    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(120);

    // Long enough that a scan finishing in microseconds still reads as something the tool did, and short
    // enough not to be a wait. It is a floor rather than a delay: real work that takes longer is not padded.
    private static readonly TimeSpan MinimumVisible = TimeSpan.FromMilliseconds(750);

    /// <summary>Runs work with no result off the interface thread while its log line spins.</summary>
    internal static Task While(LogEntry line, Action work) => While(line, () => { work(); return true; });

    /// <summary>Runs work off the interface thread while its log line spins, so a slow copy cannot freeze the window.</summary>
    internal static async Task<T> While<T>(LogEntry line, Func<T> work)
    {
        var text = line.Text;
        var frame = 0;

        var spinner = new DispatcherTimer(FrameInterval, DispatcherPriority.Normal,
            (_, _) => line.Text = $"{text}  {Frames[frame++ % Frames.Length]}", Dispatcher.CurrentDispatcher);

        spinner.Start();

        try
        {
            var running = Task.Run(work);
            await Task.WhenAll(running, Task.Delay(MinimumVisible));
            return await running;
        }
        finally
        {
            spinner.Stop();
            line.Text = text;
        }
    }
}
