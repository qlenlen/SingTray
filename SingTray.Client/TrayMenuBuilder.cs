using SingTray.Shared.Enums;
using SingTray.Shared.Models;

namespace SingTray.Client;

public sealed class TrayMenuBuilder
{
    private readonly ToolStripMenuItem _toggleItem;
    private readonly ToolStripMenuItem _statusItem;
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
        _toggleItem = new ToolStripMenuItem("SingTray", null, toggleHandler)
        {
            Font = new Font(SystemFonts.MenuFont ?? Control.DefaultFont, FontStyle.Bold)
        };
        _statusItem = new ToolStripMenuItem("Status: Loading") { Enabled = false };
        _importConfigItem = new ToolStripMenuItem("Import Config", null, importConfigHandler);
        _importCoreItem = new ToolStripMenuItem("Import Core", null, importCoreHandler);
        _openDataFolderItem = new ToolStripMenuItem("Open Data Folder", null, openDataFolderHandler);
        _exitItem = new ToolStripMenuItem("Exit", null, exitHandler);
    }

    public ContextMenuStrip Build()
    {
        return new ContextMenuStrip
        {
            ShowImageMargin = false,
            Items =
            {
                _toggleItem,
                _statusItem,
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
            _toggleItem.Text = "SingTray";
            _toggleItem.Enabled = false;
            _toggleItem.Checked = false;
            _statusItem.Text = "Status: Service unavailable";
            _importConfigItem.Enabled = false;
            _importCoreItem.Enabled = false;
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

        _toggleItem.Text = "SingTray";
        _statusItem.Text = $"Status: {stateLabel}";
        _toggleItem.Enabled = status.RunState is not RunState.Starting and not RunState.Stopping;
        _toggleItem.Checked = status.RunState == RunState.Running;
        _importConfigItem.Enabled = !status.SingBoxRunning;
        _importCoreItem.Enabled = !status.SingBoxRunning;
        _openDataFolderItem.Enabled = true;
    }
}
