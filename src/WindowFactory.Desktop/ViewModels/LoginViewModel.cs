using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindowFactory.Core.Models;
using WindowFactory.Core.Services;

namespace WindowFactory.Desktop.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    public const int MaxAttempts = 3;
    public const int LockSeconds = 10;

    private readonly IAuthService _auth;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _failedAttempts;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(LoginCommand))] private string _login = string.Empty;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(LoginCommand))] private string _password = string.Empty;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(LoginCommand))] private bool _isLocked;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(LoginCommand))] private bool _isBusy;
    [ObservableProperty] private bool _isPasswordRevealed;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(LockMessage))] private int _lockSecondsLeft;

    public event Action<User>? LoggedIn;

    public LoginViewModel(IAuthService auth)
    {
        _auth = auth;
        _timer.Tick += OnTick;
    }

    public string LockMessage => $"Слишком много неверных попыток. Повторите через {LockSecondsLeft} с.";

    private bool CanLogin() => !IsLocked && !IsBusy && !string.IsNullOrWhiteSpace(Login) && !string.IsNullOrEmpty(Password);

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            var user = await _auth.LoginAsync(Login, Password);
            if (user is not null)
            {
                _failedAttempts = 0;
                LoggedIn?.Invoke(user);
                return;
            }
            _failedAttempts++;
            ErrorMessage = $"Неверный логин или пароль (попытка {_failedAttempts} из {MaxAttempts}).";
            Password = string.Empty;
            if (_failedAttempts >= MaxAttempts) StartLock();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Не удалось подключиться к базе данных: " + ex.GetBaseException().Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void ToggleReveal() => IsPasswordRevealed = !IsPasswordRevealed;

    private void StartLock()
    {
        _failedAttempts = 0;
        LockSecondsLeft = LockSeconds;
        IsLocked = true;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        LockSecondsLeft--;
        if (LockSecondsLeft > 0) return;
        _timer.Stop();
        IsLocked = false;
        ErrorMessage = null;
    }
}
