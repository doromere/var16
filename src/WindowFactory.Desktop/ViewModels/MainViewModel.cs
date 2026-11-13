using CommunityToolkit.Mvvm.Input;
using WindowFactory.Core.Models;
using WindowFactory.Core.Services;
using WindowFactory.Desktop.Services;

namespace WindowFactory.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public User CurrentUser { get; }
    public OrdersViewModel Orders { get; }
    public HomeownersViewModel Homeowners { get; }
    public CatalogViewModel Catalog { get; }

    public string FullName => CurrentUser.FullName;
    public string RoleName => CurrentUser.Role?.Name ?? string.Empty;

    public event Action? LogoutRequested;

    public MainViewModel(User user, IServiceProvider sp)
    {
        CurrentUser = user;
        var dialogs = (IDialogService)sp.GetService(typeof(IDialogService))!;
        Orders = new OrdersViewModel(user, sp);
        Homeowners = new HomeownersViewModel(user, (IHomeownerService)sp.GetService(typeof(IHomeownerService))!, dialogs);
        Catalog = new CatalogViewModel(user, (ICatalogService)sp.GetService(typeof(ICatalogService))!, dialogs);
    }

    public async Task LoadAsync()
    {
        await Orders.RefreshAsync();
        await Homeowners.RefreshAsync();
        await Catalog.RefreshAsync();
    }

    [RelayCommand]
    private void Logout() => LogoutRequested?.Invoke();
}
