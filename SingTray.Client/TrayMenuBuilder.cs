using SingTray.Shared.Enums;
using SingTray.Shared.Models;

namespace SingTray.Client;

public sealed class TrayMenuBuilder
{
    private const int MenuWidth = 260;
    private const int HeaderHeight = 30;
    private const int StandardItemHeight = 28;

    private readonly HeaderMenuItem _toggleItem;
    private readonly ToolStripMenuItem _importConfigItem;
    private readonly ToolStripMenuItem _importCoreItem;
    private readonly ToolStripMenuItem _openDataFolderItem;
    private readonly ToolStripMenuItem _exitItem;

    public TrayMenuBuilder(
        EventHandler toggleHandler,
        EventHandler importConfigHandler,
        EventHandler importCoreHandler,
        EventHandler openDataFolderHandler,
        EventHandler exitHandler)
    {
        _toggleItem = new HeaderMenuItem(toggleHandler)
        {
            AutoSize = false,
            Size = new Size(MenuWidth, HeaderHeight)
        };

        _importConfigItem = CreateStatusItem("Import Config", importConfigHandler);
        _importCoreItem = CreateStatusItem("Import Core", importCoreHandler);
        _openDataFolderItem = CreateStandardItem("Open Data Folder", openDataFolderHandler);
        _exitItem = CreateStandardItem("Exit", exitHandler);
    }

    public ContextMenuStrip Build()
    {
        return new ContextMenuStrip
        {
            ShowImageMargin = false,
            Items =
            {
                _toggleItem,
                new ToolStripSeparator(),
                _importConfigItem,
                _importCoreItem,
                _openDataFolderItem,
                _exitItem
            }
        };
    }

    public void ApplyStatus(StatusInfo? status, bool serviceAvailable)
    {
        if (!serviceAvailable || status is null)
        {
            _toggleItem.SetState("Unavailable");
            _toggleItem.Enabled = false;
            _toggleItem.Checked = false;

            SetStatusItem(_importConfigItem, "Unavailable", enabled: false);
            SetStatusItem(_importCoreItem, "Unavailable", enabled: false);
            _openDataFolderItem.Enabled = true;
            return;
        }

        var stateLabel = status.RunState switch
        {
            RunState.Running => "Running",
            RunState.Stopped => "Stopped",
            RunState.Starting => "Starting",
            RunState.Stopping => "Stopping",
            RunState.Error => "Error",
            _ => "Unknown"
        };

        _toggleItem.SetState(stateLabel);
        _toggleItem.Enabled = status.RunState is not RunState.Starting and not RunState.Stopping;
        _toggleItem.Checked = status.RunState == RunState.Running;

        var importEnabled = status.RunState is not RunState.Starting and not RunState.Stopping and not RunState.Running;
        SetStatusItem(_importConfigItem, BuildConfigStatusLabel(status), enabled: importEnabled);
        SetStatusItem(_importCoreItem, BuildCoreStatusLabel(status), enabled: importEnabled);
        _openDataFolderItem.Enabled = true;
    }

    private static ToolStripMenuItem CreateStatusItem(string text, EventHandler clickHandler)
    {
        return new ToolStripMenuItem(text, null, clickHandler)
        {
            AutoSize = false,
            Size = new Size(MenuWidth, StandardItemHeight),
            TextAlign = ContentAlignment.MiddleLeft,
            ShowShortcutKeys = true
        };
    }

    private static ToolStripMenuItem CreateStandardItem(string text, EventHandler clickHandler)
    {
        return new ToolStripMenuItem(text, null, clickHandler)
        {
            AutoSize = false,
            Size = new Size(MenuWidth, StandardItemHeight),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static void SetStatusItem(ToolStripMenuItem item, string status, bool enabled)
    {
        item.ShortcutKeyDisplayString = status;
        item.Enabled = enabled;
    }

    private static string BuildConfigStatusLabel(StatusInfo status)
    {
        if (!status.Config.Installed)
        {
            return "Unconfigured";
        }

        if (!status.Core.Installed || !status.Core.Valid)
        {
            return "Waiting";
        }

        if (!status.Config.Valid)
        {
            return "Error";
        }

        return string.IsNullOrWhiteSpace(status.Config.FileName) ? "Configured" : status.Config.FileName!;
    }

    private static string BuildCoreStatusLabel(StatusInfo status)
    {
        if (!status.Core.Installed)
        {
            return "Missing";
        }

        if (!status.Core.Valid)
        {
            return "Error";
        }

        if (string.IsNullOrWhiteSpace(status.Core.Version))
        {
            return "Ready";
        }

        var version = status.Core.Version!;
        return version.StartsWith("sing-box ", StringComparison.OrdinalIgnoreCase)
            ? version["sing-box ".Length..]
            : version;
    }
}

internal sealed class HeaderMenuItem : ToolStripMenuItem
{
    private string _state = "Loading";

    public HeaderMenuItem(EventHandler clickHandler) : base("SingTray", null, clickHandler)
    {
    }

    public void SetState(string state)
    {
        _state = state;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var bounds = new Rectangle(Point.Empty, Size);
        var backgroundColor = Selected && Enabled ? SystemColors.Highlight : SystemColors.ControlLightLight;
        var textColor = Selected && Enabled ? SystemColors.HighlightText : SystemColors.ControlText;
        var secondaryColor = Selected && Enabled ? SystemColors.HighlightText : Color.FromArgb(90, 90, 90);

        using var background = new SolidBrush(backgroundColor);
        e.Graphics.FillRectangle(background, bounds);

        var titleRect = new Rectangle(10, 0, bounds.Width - 120, bounds.Height);
        var stateRect = new Rectangle(bounds.Width - 96, 0, 66, bounds.Height);
        var headerFont = Font ?? SystemFonts.MenuFont ?? Control.DefaultFont;

        TextRenderer.DrawText(
            e.Graphics,
            "SingTray",
            new Font(headerFont, FontStyle.Bold),
            titleRect,
            textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        TextRenderer.DrawText(
            e.Graphics,
            _state,
            headerFont,
            stateRect,
            secondaryColor,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}
