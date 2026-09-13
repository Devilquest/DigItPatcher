using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using DigItPatcher.App.Interop;
using DigItPatcher.App.ViewModels;

namespace DigItPatcher.App.Views;

internal partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // A user who has scrolled up to read an earlier line still gets pulled back down: the log is a
        // running receipt, not a document to leave a scroll position in.
        viewModel.Log.CollectionChanged += (_, _) => LogScrollViewer.ScrollToEnd();

        CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, (_, _) => SystemCommands.MinimizeWindow(this)));
        CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (_, _) => SystemCommands.CloseWindow(this)));

        SourceInitialized += (_, _) =>
        {
            DwmInterop.ApplyDarkTitleBar(this);
            DwmInterop.SquareOffCorners(this);
            WindowStyleInterop.DisableMaximize(this);
        };
    }

    // The stock placement modes flip a tooltip to the opposite side of its owner whenever the platform
    // judges the requested side a poor fit, so the corner is computed here rather than asked for.
    private static readonly CustomPopupPlacementCallback PlaceBelowOwner =
        (_, targetSize, offset) => [new CustomPopupPlacement(new Point(offset.X, targetSize.Height + offset.Y), PopupPrimaryAxis.None)];

    private static ToolTip? PrepareExplanation(object sender)
    {
        if (sender is not FrameworkElement { ToolTip: ToolTip explanation } owner) return null;

        explanation.PlacementTarget = owner;
        explanation.CustomPopupPlacementCallback = PlaceBelowOwner;
        return explanation;
    }

    // WPF opens a tooltip on hover only, so reaching one from the keyboard means opening it by hand.
    private static void ShowExplanation(object sender, bool open)
    {
        if (PrepareExplanation(sender) is { } explanation) explanation.IsOpen = open;
    }

    private void InfoButton_ToolTipOpening(object sender, ToolTipEventArgs e) => PrepareExplanation(sender);

    private void InfoButton_Click(object sender, RoutedEventArgs e) => ShowExplanation(sender, true);

    private void InfoButton_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => ShowExplanation(sender, true);

    private void InfoButton_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => ShowExplanation(sender, false);

    private void Identity_Click(object sender, RoutedEventArgs e) => AboutWindow.Show(this);

    private void Tools_Click(object sender, RoutedEventArgs e) => ToolsWindow.Show(this);
}
