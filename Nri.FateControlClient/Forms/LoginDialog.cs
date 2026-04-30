using System.Windows.Forms;

namespace Nri.FateControlClient.Forms;

public sealed class LoginDialog : Form
{
    private readonly TextBox _loginBox = new TextBox();
    private readonly TextBox _passwordBox = new TextBox();

    public string Login => _loginBox.Text.Trim();
    public string Password => _passwordBox.Text;

    public LoginDialog()
    {
        Text = "Login";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        Width = 340;
        Height = 180;

        var loginLabel = new Label { Text = "Login", Left = 12, Top = 18, Width = 90 };
        _loginBox.Left = 110;
        _loginBox.Top = 14;
        _loginBox.Width = 200;

        var passLabel = new Label { Text = "Password", Left = 12, Top = 52, Width = 90 };
        _passwordBox.Left = 110;
        _passwordBox.Top = 48;
        _passwordBox.Width = 200;
        _passwordBox.PasswordChar = '*';

        var okButton = new Button { Text = "Login", Left = 154, Top = 88, Width = 75, DialogResult = DialogResult.OK };
        var cancelButton = new Button { Text = "Cancel", Left = 235, Top = 88, Width = 75, DialogResult = DialogResult.Cancel };

        Controls.Add(loginLabel);
        Controls.Add(_loginBox);
        Controls.Add(passLabel);
        Controls.Add(_passwordBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }
}
