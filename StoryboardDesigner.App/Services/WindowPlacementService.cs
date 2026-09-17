using System.IO;
using System.Text.Json;
using System.Windows;
using Storyboard.Shared.Serialization;

namespace StoryboardDesigner.App.Services;

public sealed class WindowPlacementService : IWindowPlacementService
{
    private readonly JsonSerializerOptions _jsonOptions = StoryboardJsonSerializerOptions.Create();

    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StoryboardDesigner",
        "window-placement.json");

    public void Save(Window window, double? leftPaneWidth = null, double? rightPaneWidth = null)
    {
        var bounds = window.WindowState == WindowState.Normal
            ? new Rect(window.Left, window.Top, window.Width, window.Height)
            : window.RestoreBounds;

        var state = window.WindowState == WindowState.Minimized
            ? WindowState.Normal
            : window.WindowState;

        var placement = new WindowPlacement
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            WindowState = state.ToString(),
            LeftPaneWidth = leftPaneWidth,
            RightPaneWidth = rightPaneWidth
        };

        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(placement, _jsonOptions);
        File.WriteAllText(_settingsPath, json);
    }

    public bool TryApply(Window window, out double? leftPaneWidth, out double? rightPaneWidth)
    {
        leftPaneWidth = null;
        rightPaneWidth = null;

        if (!File.Exists(_settingsPath))
        {
            return false;
        }

        var json = File.ReadAllText(_settingsPath);
        var placement = JsonSerializer.Deserialize<WindowPlacement>(json, _jsonOptions);
        if (placement is null)
        {
            return false;
        }

        leftPaneWidth = placement.LeftPaneWidth;
        rightPaneWidth = placement.RightPaneWidth;

        if (!IsPlacementVisible(placement))
        {
            return false;
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = placement.Left;
        window.Top = placement.Top;
        window.Width = placement.Width;
        window.Height = placement.Height;

        if (Enum.TryParse<WindowState>(placement.WindowState, out var state) && state != WindowState.Minimized)
        {
            window.WindowState = state;
        }

        return true;
    }

    private static bool IsPlacementVisible(WindowPlacement placement)
    {
        if (placement.Width < 200 || placement.Height < 200)
        {
            return false;
        }

        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
        var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;

        var right = placement.Left + placement.Width;
        var bottom = placement.Top + placement.Height;

        return right > virtualLeft &&
               placement.Left < virtualRight &&
               bottom > virtualTop &&
               placement.Top < virtualBottom;
    }
}
