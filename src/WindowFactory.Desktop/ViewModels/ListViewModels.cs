using System.Collections.ObjectModel;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindowFactory.Core.Models;
using WindowFactory.Core.Services;
using WindowFactory.Desktop.Services;

namespace WindowFactory.Desktop.ViewModels;

public partial class HomeownersViewModel : ViewModelBase
{
    private readonly IHomeownerService _svc;
    private readonly IDialogService _dialogs;

    public ObservableCollection<Homeowner> Items { get; } = new();
    public DataGridCollectionView View { get; }
    public bool CanManage { get; }

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(EditCommand), nameof(DeleteCommand))] private Homeowner? _selected;

    public HomeownersViewModel(User user, IHomeownerService svc, IDialogService dialogs)
    {
        _svc = svc; _dialogs = dialogs;
        CanManage = RolePermissions.CanManageHomeowners(user.Role?.Name ?? string.Empty);
        View = new DataGridCollectionView(Items) { Filter = Filter };
    }

    partial void OnSearchTextChanged(string value) => View.Refresh();

    private bool Filter(object o)
    {
        if (o is not Homeowner h) return false;
        var q = SearchText.Trim();
        return q.Length == 0 || h.FullName.Contains(q, StringComparison.CurrentCultureIgnoreCase)
            || h.DeliveryAddress.Contains(q, StringComparison.CurrentCultureIgnoreCase)
            || h.Phone.Contains(q, StringComparison.CurrentCultureIgnoreCase);
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        try
        {
            Items.Clear();
            foreach (var h in await _svc.GetAllAsync()) Items.Add(h);
        }
        catch (Exception ex) { await _dialogs.ShowMessageAsync("Ошибка", ex.GetBaseException().Message); }
    }

    [RelayCommand(CanExecute = nameof(CanManage))]
    private async Task AddAsync()
    {
        var h = await _dialogs.EditHomeownerAsync(null);
        if (h is null) return;
        await Run(() => _svc.SaveAsync(h));
    }

    private bool CanChange() => CanManage && Selected is not null;

    [RelayCommand(CanExecute = nameof(CanChange))]
    private async Task EditAsync()
    {
        if (Selected is null) return;
        var h = await _dialogs.EditHomeownerAsync(Selected);
        if (h is null) return;
        await Run(() => _svc.SaveAsync(h));
    }

    [RelayCommand(CanExecute = nameof(CanChange))]
    private async Task DeleteAsync()
    {
        if (Selected is null) return;
        if (!await _dialogs.ConfirmAsync("Удаление заказчика", $"Удалить заказчика «{Selected.FullName}»?")) return;
        var id = Selected.Id;
        await Run(() => _svc.DeleteAsync(id));
    }

    private async Task Run(Func<Task> action)
    {
        try { await action(); await RefreshAsync(); }
        catch (ValidationFailedException ex) { await _dialogs.ShowMessageAsync("Проверьте данные", string.Join("\n", ex.Errors)); }
        catch (Exception ex) { await _dialogs.ShowMessageAsync("Ошибка", ex.GetBaseException().Message); }
    }
}

public partial class CatalogViewModel : ViewModelBase
{
    private readonly ICatalogService _svc;
    private readonly IDialogService _dialogs;

    public ObservableCollection<ProfileSystem> Profiles { get; } = new();
    public ObservableCollection<GlazingType> Glazings { get; } = new();
    public bool CanEdit { get; }
    public string Hint => CanEdit ? "Цены и параметры редактирует главный технолог." : "Справочники доступны только для просмотра (редактирует главный технолог).";

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(EditProfileCommand), nameof(DeleteProfileCommand))] private ProfileSystem? _selectedProfile;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(EditGlazingCommand), nameof(DeleteGlazingCommand))] private GlazingType? _selectedGlazing;

    public CatalogViewModel(User user, ICatalogService svc, IDialogService dialogs)
    {
        _svc = svc; _dialogs = dialogs;
        CanEdit = RolePermissions.CanEditCatalog(user.Role?.Name ?? string.Empty);
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        try
        {
            Profiles.Clear(); Glazings.Clear();
            foreach (var p in await _svc.GetProfilesAsync()) Profiles.Add(p);
            foreach (var g in await _svc.GetGlazingTypesAsync()) Glazings.Add(g);
        }
        catch (Exception ex) { await _dialogs.ShowMessageAsync("Ошибка", ex.GetBaseException().Message); }
    }

    private async Task Run(Func<Task> action)
    {
        try { await action(); await RefreshAsync(); }
        catch (ValidationFailedException ex) { await _dialogs.ShowMessageAsync("Проверьте данные", string.Join("\n", ex.Errors)); }
        catch (Exception ex) { await _dialogs.ShowMessageAsync("Ошибка", ex.GetBaseException().Message); }
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task AddProfileAsync()
    { var p = await _dialogs.EditProfileAsync(null); if (p is not null) await Run(() => _svc.SaveProfileAsync(p)); }

    private bool CanChangeProfile() => CanEdit && SelectedProfile is not null;

    [RelayCommand(CanExecute = nameof(CanChangeProfile))]
    private async Task EditProfileAsync()
    { var p = await _dialogs.EditProfileAsync(SelectedProfile); if (p is not null) await Run(() => _svc.SaveProfileAsync(p)); }

    [RelayCommand(CanExecute = nameof(CanChangeProfile))]
    private async Task DeleteProfileAsync()
    {
        var p = SelectedProfile; if (p is null) return;
        if (await _dialogs.ConfirmAsync("Удаление", $"Удалить профильную систему «{p.BrandTitle}»?")) await Run(() => _svc.DeleteProfileAsync(p.Id));
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task AddGlazingAsync()
    { var g = await _dialogs.EditGlazingAsync(null); if (g is not null) await Run(() => _svc.SaveGlazingAsync(g)); }

    private bool CanChangeGlazing() => CanEdit && SelectedGlazing is not null;

    [RelayCommand(CanExecute = nameof(CanChangeGlazing))]
    private async Task EditGlazingAsync()
    { var g = await _dialogs.EditGlazingAsync(SelectedGlazing); if (g is not null) await Run(() => _svc.SaveGlazingAsync(g)); }

    [RelayCommand(CanExecute = nameof(CanChangeGlazing))]
    private async Task DeleteGlazingAsync()
    {
        var g = SelectedGlazing; if (g is null) return;
        if (await _dialogs.ConfirmAsync("Удаление", $"Удалить стеклопакет «{g.GlazingName}»?")) await Run(() => _svc.DeleteGlazingAsync(g.Id));
    }
}
