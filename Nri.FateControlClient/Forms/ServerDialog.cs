using System;
using System.Windows.Forms;

namespace Nri.FateControlClient.Forms;

public sealed class ServerDialog : Form
{
    private readonly TextBox _hostBox = new TextBox();
    private readonly NumericUpDown _portBox = new NumericUpDown();

    public string Host => _hostBox.Text.Trim();
    public int Port => (int)_portBox.Value;

    public ServerDialog(string host, int port)
    {
        Text = "Server";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        Width = 320;
        Height = 170;

        var hostLabel = new Label { Text = "Host", Left = 12, Top = 18, Width = 80 };
        _hostBox.Left = 100;
        _hostBox.Top = 14;
        _hostBox.Width = 180;
        _hostBox.Text = host;

        var portLabel = new Label { Text = "Port", Left = 12, Top = 52, Width = 80 };
        _portBox.Left = 100;
        _portBox.Top = 48;
        _portBox.Width = 180;
        _portBox.Minimum = 1;
        _portBox.Maximum = 65535;
        _portBox.Value = Math.Max(1, Math.Min(65535, port));

        var okButton = new Button { Text = "OK", Left = 124, Top = 86, Width = 75, DialogResult = DialogResult.OK };
        var cancelButton = new Button { Text = "Cancel", Left = 205, Top = 86, Width = 75, DialogResult = DialogResult.Cancel };

        Controls.Add(hostLabel);
        Controls.Add(_hostBox);
        Controls.Add(portLabel);
        Controls.Add(_portBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }
}
