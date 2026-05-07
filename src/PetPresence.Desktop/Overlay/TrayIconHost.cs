using System.Drawing;
using System.Windows.Forms;

namespace PetPresence.Desktop.Overlay;

public sealed class TrayIconHost : IDisposable
{
    private readonly OverlayWindow _overlayWindow;
    private readonly NotifyIcon _notifyIcon;

    public TrayIconHost(OverlayWindow overlayWindow)
    {
        _overlayWindow = overlayWindow;
        _notifyIcon = new NotifyIcon
        {
            Text = "PetPresence",
            Icon = SystemIcons.Application,
            Visible = false,
            ContextMenuStrip = BuildMenu()
        };
    }

    public void Show() => _notifyIcon.Visible = true;

    public void Dispose() => _notifyIcon.Dispose();

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        var editMode = new ToolStripMenuItem("Edit pet positions") { CheckOnClick = true };
        editMode.CheckedChanged += (_, _) =>
        {
            if (_overlayWindow.DataContext is OverlayViewModel vm)
            {
                vm.LayoutEditMode = editMode.Checked;
            }
        };

        var show = new ToolStripMenuItem("Show overlay");
        show.Click += (_, _) => _overlayWindow.Show();

        var exit = new ToolStripMenuItem("Exit");
        exit.Click += (_, _) => System.Windows.Application.Current.Shutdown();

        menu.Items.Add(editMode);
        menu.Items.Add(show);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exit);
        return menu;
    }
}
