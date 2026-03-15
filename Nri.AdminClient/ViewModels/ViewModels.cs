using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Nri.AdminClient.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Notify([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class LoginViewModel : ViewModelBase
{
    private string _login = string.Empty;
    public string Login
    {
        get => _login;
        set
        {
            _login = value;
            Notify();
        }
    }
}

public class AdminDashboardViewModel : ViewModelBase { }
public class CharacterViewModel : ViewModelBase { }
public class RequestsViewModel : ViewModelBase { }
public class CombatViewModel : ViewModelBase { }
public class ChatPanelViewModel : ViewModelBase { }
public class AudioPanelViewModel : ViewModelBase { }
