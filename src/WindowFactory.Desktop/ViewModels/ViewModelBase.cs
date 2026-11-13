using CommunityToolkit.Mvvm.ComponentModel;

namespace WindowFactory.Desktop.ViewModels;

public abstract class ViewModelBase : ObservableObject { }

/// <summary>Базовая ViewModel модального окна: закрытие с результатом.</summary>
public abstract class DialogViewModelBase : ViewModelBase
{
    public event Action<object?>? CloseRequested;
    protected void Close(object? result = null) => CloseRequested?.Invoke(result);
}
