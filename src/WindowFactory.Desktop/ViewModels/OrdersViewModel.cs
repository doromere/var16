using System.Collections.ObjectModel;
using Avalonia.Collections;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindowFactory.Core.Models;
using WindowFactory.Core.Services;
using WindowFactory.Desktop.Services;

namespace WindowFactory.Desktop.ViewModels;

public partial class OrdersViewModel : ViewModelBase
{
    public const string AllStatuses = "Все статусы";
    public const string AllProfiles = "Все профили";

    private readonly User _user;
    private readonly IServiceProvider _sp;
    private readonly IOrderService _orders;
    private readonly ICatalogService _catalog;
    private readonly IWarrantyService _warranty;
    private readonly IExportService _export;
    private readonly IDialogService _dialogs;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };

    public ObservableCollection<OrderRowViewModel> Rows { get; } = new();
    public DataGridCollectionView View { get; }
    public ObservableCollection<string> StatusFilters { get; } = new(new[] { AllStatuses }.Concat(OrderStatuses.All));
    public ObservableCollection<string> ProfileFilters { get; } = new(new[] { AllProfiles });

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedStatus = AllStatuses;
    [ObservableProperty] private string _selectedProfile = AllProfiles;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _warrantySummary = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenOrderCommand), nameof(DeleteOrderCommand), nameof(MoveCommand),
        nameof(ExportCuttingMapCommand), nameof(ExportActCommand))]
    private OrderRowViewModel? _selectedOrder;

    public string RoleName => _user.Role?.Name ?? string.Empty;
    public bool CanManage => RolePermissions.CanManageOrders(RoleName);

    public OrdersViewModel(User user, IServiceProvider sp)
    {
        _user = user; _sp = sp;
        _orders = (IOrderService)sp.GetService(typeof(IOrderService))!;
        _catalog = (ICatalogService)sp.GetService(typeof(ICatalogService))!;
        _warranty = (IWarrantyService)sp.GetService(typeof(IWarrantyService))!;
        _export = (IExportService)sp.GetService(typeof(IExportService))!;
        _dialogs = (IDialogService)sp.GetService(typeof(IDialogService))!;
        View = new DataGridCollectionView(Rows) { Filter = FilterRow };
        // Автоматический контроль времени: гарантия пересчитывается без участия оператора.
        _timer.Tick += (_, _) => RefreshWarranty();
        _timer.Start();
    }

    partial void OnSearchTextChanged(string value) => View.Refresh();
    partial void OnSelectedStatusChanged(string value) => View.Refresh();
    partial void OnSelectedProfileChanged(string value) => View.Refresh();

    private bool FilterRow(object o)
    {
        if (o is not OrderRowViewModel r) return false;
        if (SelectedStatus != AllStatuses && r.Status != SelectedStatus) return false;
        if (SelectedProfile != AllProfiles && !r.Model.OrderItems.Any(u => u.ProfileSystem?.BrandTitle == SelectedProfile)) return false;
        var q = SearchText.Trim();
        if (q.Length == 0) return true;
        return r.Number.Contains(q, StringComparison.CurrentCultureIgnoreCase)
            || r.Homeowner.Contains(q, StringComparison.CurrentCultureIgnoreCase)
            || r.Address.Contains(q, StringComparison.CurrentCultureIgnoreCase)
            || r.Manager.Contains(q, StringComparison.CurrentCultureIgnoreCase);
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        try
        {
            var keepId = SelectedOrder?.Id;
            var orders = await _orders.GetOrdersAsync();
            var profiles = await _catalog.GetProfilesAsync();
            ProfileFilters.Clear();
            ProfileFilters.Add(AllProfiles);
            foreach (var p in profiles) ProfileFilters.Add(p.BrandTitle);
            SelectedProfile = AllProfiles;
            Rows.Clear();
            foreach (var o in orders) Rows.Add(new OrderRowViewModel(o));
            RefreshWarranty();
            SelectedOrder = Rows.FirstOrDefault(r => r.Id == keepId);
            StatusMessage = $"Заказов: {Rows.Count}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Ошибка загрузки: " + ex.GetBaseException().Message;
        }
    }

    private void RefreshWarranty()
    {
        var now = DateTime.Now;
        foreach (var r in Rows) r.UpdateWarranty(_warranty, now);
        var expired = Rows.Count(r => r.Severity == RowSeverity.Overdue);
        var soon = Rows.Count(r => r.Severity == RowSeverity.Warning);
        WarrantySummary = expired + soon == 0 ? string.Empty
            : $"Гарантия: истекла у {expired}, истекает в ближайшие {ProductionLimits.WarrantyWarningDays} дн. у {soon}";
    }

    [RelayCommand(CanExecute = nameof(CanManage))]
    private async Task NewOrderAsync()
    {
        if (await _dialogs.ShowOrderAsync(_user, null)) await RefreshAsync();
    }

    private bool HasSelection() => SelectedOrder is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task OpenOrderAsync()
    {
        if (SelectedOrder is null) return;
        if (await _dialogs.ShowOrderAsync(_user, SelectedOrder.Id)) await RefreshAsync();
    }

    private bool CanDelete() => CanManage && SelectedOrder is { Status: OrderStatuses.Measured };

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteOrderAsync()
    {
        var o = SelectedOrder;
        if (o is null) return;
        if (!await _dialogs.ConfirmAsync("Удаление заказа", $"Удалить заказ {o.Number} со всеми позициями?")) return;
        try { await _orders.DeleteAsync(o.Id); await RefreshAsync(); }
        catch (Exception ex) { await _dialogs.ShowMessageAsync("Ошибка", ex.GetBaseException().Message); }
    }

    private bool CanMove(string? target)
        => SelectedOrder is { } o && target is not null
           && RolePermissions.CanPerformTransition(RoleName, o.Status, target);

    [RelayCommand(CanExecute = nameof(CanMove))]
    private async Task MoveAsync(string? target)
    {
        var o = SelectedOrder;
        if (o is null || target is null) return;
        string? reason = null;
        if (target == OrderStatuses.Claim)
        {
            reason = await _dialogs.AskReasonAsync("Рекламация",
                $"Укажите причину рекламации по заказу {o.Number} (не менее {ProductionLimits.MinCancelReasonLength} знаков):");
            if (reason is null) return;
        }
        else if (!await _dialogs.ConfirmAsync("Смена статуса", $"Перевести заказ {o.Number} в статус «{target}»?"))
            return;
        try
        {
            await _orders.ChangeStatusAsync(o.Id, target, reason, _user);
            await RefreshAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            await _dialogs.ShowMessageAsync("Переход невозможен", ex.Message);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessageAsync("Ошибка", ex.GetBaseException().Message);
        }
    }

    private static readonly string[] AfterWorkshop =
        { OrderStatuses.SentToWorkshop, OrderStatuses.Manufactured, OrderStatuses.Installed, OrderStatuses.Claim };

    private bool CanExportMap() => SelectedOrder is { } o && AfterWorkshop.Contains(o.Status) && o.UnitsCount > 0;
    private bool CanExportAct() => SelectedOrder is { Status: OrderStatuses.Installed } o && o.UnitsCount > 0;

    [RelayCommand(CanExecute = nameof(CanExportMap))]
    private Task ExportCuttingMapAsync() => ExportAsync(ExportDocumentKind.CuttingMap);

    [RelayCommand(CanExecute = nameof(CanExportAct))]
    private Task ExportActAsync() => ExportAsync(ExportDocumentKind.AcceptanceAct);

    private async Task ExportAsync(ExportDocumentKind kind)
    {
        if (SelectedOrder is null) return;
        try
        {
            var order = await _orders.GetOrderAsync(SelectedOrder.Id);
            if (order is null) return;
            var path = await _dialogs.PickSaveFileAsync(_export.SuggestFileName(order, kind, ExportFormat.Txt));
            if (path is null) return;
            var format = path.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ? ExportFormat.Html : ExportFormat.Txt;
            await _export.ExportAsync(order, kind, format, path);
            StatusMessage = $"Документ сохранён: {path}";
            await _dialogs.ShowMessageAsync("Экспорт", $"Документ сохранён:\n{path}\n\nВ нижней строке указан SHA256-отпечаток реквизитов.");
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessageAsync("Ошибка экспорта", ex.GetBaseException().Message);
        }
    }
}
