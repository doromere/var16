using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindowFactory.Core.Models;
using WindowFactory.Core.Services;
using WindowFactory.Desktop.Services;

namespace WindowFactory.Desktop.ViewModels;

/// <summary>Master-Detail: реквизиты заголовка + спецификация; итоги пересчитываются на лету.</summary>
public partial class OrderEditViewModel : DialogViewModelBase
{
    private static readonly CultureInfo Ru = new("ru-RU");
    private readonly User _user;
    private readonly IOrderService _orders;
    private readonly IHomeownerService _homeowners;
    private readonly ICatalogService _catalog;
    private readonly IOrderCalculationService _calc;
    private readonly ICuttingService _cutting;
    private readonly IDialogService _dialogs;
    private readonly int? _orderId;
    private WindowOrder? _order;
    private List<ProfileSystem> _profiles = new();
    private List<GlazingType> _glazings = new();

    public OrderEditViewModel(User user, int? orderId, IOrderService orders, IHomeownerService homeowners,
        ICatalogService catalog, IOrderCalculationService calc, ICuttingService cutting, IDialogService dialogs)
    {
        _user = user; _orderId = orderId; _orders = orders; _homeowners = homeowners;
        _catalog = catalog; _calc = calc; _cutting = cutting; _dialogs = dialogs;
        Units.CollectionChanged += (_, _) => Recalculate();
    }

    public ObservableCollection<Homeowner> Homeowners { get; } = new();
    public ObservableCollection<UnitRowViewModel> Units { get; } = new();

    [ObservableProperty] private string _title = "Новый заказ";
    [ObservableProperty] private string _orderNumber = "будет присвоен при сохранении";
    [ObservableProperty] private string _orderDateText = DateTime.Now.ToString("dd.MM.yyyy HH:mm", Ru);
    [ObservableProperty] private string _status = OrderStatuses.Measured;
    [ObservableProperty] private string? _cancelReason;
    [ObservableProperty] private bool _canEdit;
    [ObservableProperty] private string? _errorText;
    [ObservableProperty] private string _readOnlyHint = string.Empty;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HomeownerInfo))] private Homeowner? _selectedHomeowner;
    [ObservableProperty] private bool _hasInstallation = true;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(EditUnitCommand), nameof(DeleteUnitCommand))] private UnitRowViewModel? _selectedUnit;

    [ObservableProperty] private string _constructionsText = "0,00 ₽";
    [ObservableProperty] private string _installationText = "0,00 ₽";
    [ObservableProperty] private string _floorSurchargeText = "0,00 ₽";
    [ObservableProperty] private string _totalText = "0,00 ₽";
    [ObservableProperty] private string _materialsText = string.Empty;

    public bool Saved { get; private set; }
    public bool IsReadOnly => !CanEdit;

    public string HomeownerInfo => SelectedHomeowner is null ? string.Empty
        : $"{SelectedHomeowner.DeliveryAddress}; этаж {SelectedHomeowner.FloorNumber}, лифт: {(SelectedHomeowner.HasElevator ? "есть" : "нет")}";

    partial void OnCanEditChanged(bool value) => OnPropertyChanged(nameof(IsReadOnly));
    partial void OnSelectedHomeownerChanged(Homeowner? value) => Recalculate();
    partial void OnHasInstallationChanged(bool value) => Recalculate();

    public async Task InitializeAsync()
    {
        _profiles = await _catalog.GetProfilesAsync();
        _glazings = await _catalog.GetGlazingTypesAsync();
        foreach (var h in await _homeowners.GetAllAsync()) Homeowners.Add(h);

        var roleCanEdit = RolePermissions.CanManageOrders(_user.Role?.Name ?? string.Empty);
        if (_orderId is int id)
        {
            _order = await _orders.GetOrderAsync(id) ?? throw new InvalidOperationException("Заказ не найден.");
            Title = $"Заказ {_order.OrderNum}";
            OrderNumber = _order.OrderNum;
            OrderDateText = _order.OrderDate.ToString("dd.MM.yyyy HH:mm", Ru);
            Status = _order.Status;
            CancelReason = _order.CancelReason;
            HasInstallation = _order.HasInstallation;
            SelectedHomeowner = Homeowners.FirstOrDefault(h => h.Id == _order.HomeownerId);
            var n = 1;
            foreach (var u in _order.OrderItems)
                Units.Add(new UnitRowViewModel(n++, u.ProfileSystem!, u.GlazingType!, u.WidthMm, u.HeightMm, u.SashesCount, u.UnitCalculatedCost));
            CanEdit = roleCanEdit && new OrderStateMachine().IsEditable(_order.Status);
            ReadOnlyHint = CanEdit ? string.Empty
                : !roleCanEdit ? "Редактирование доступно только менеджеру."
                : $"Заказ в статусе «{_order.Status}» закрыт для редактирования.";
        }
        else
        {
            CanEdit = roleCanEdit;
            SelectedHomeowner = Homeowners.FirstOrDefault();
        }
        Recalculate();
    }

    private void Recalculate()
    {
        var floor = SelectedHomeowner?.FloorNumber ?? 1;
        var lift = SelectedHomeowner?.HasElevator ?? true;
        var t = _calc.CalculateOrderTotals(Units.Select(u => u.Cost), HasInstallation, floor, lift);
        ConstructionsText = Money(t.ConstructionsCost);
        InstallationText = Money(t.InstallationCost);
        FloorSurchargeText = Money(t.FloorSurcharge);
        TotalText = Money(t.Total);
        decimal profile = 0, glass = 0;
        foreach (var u in Units)
        {
            var plan = _cutting.Plan(new WindowOrderUnit { WidthMm = u.WidthMm, HeightMm = u.HeightMm, SashesCount = u.SashesCount });
            profile += plan.ProfileMeters; glass += plan.GlassAreaM2;
        }
        MaterialsText = $"Расход профиля: {profile.ToString("N2", Ru)} м.п.; стеклопакетов: {glass.ToString("N2", Ru)} м²; позиций: {Units.Count}";
        for (var i = 0; i < Units.Count; i++) Units[i].No = i + 1;
    }

    private static string Money(decimal v) => v.ToString("N2", Ru) + " ₽";

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task AddUnitAsync()
    {
        var input = await _dialogs.EditUnitAsync(_profiles, _glazings, null);
        if (input is null) return;
        Units.Add(Build(input));
    }

    private bool CanChangeUnit() => CanEdit && SelectedUnit is not null;

    [RelayCommand(CanExecute = nameof(CanChangeUnit))]
    private async Task EditUnitAsync()
    {
        var cur = SelectedUnit;
        if (cur is null) return;
        var input = await _dialogs.EditUnitAsync(_profiles, _glazings, cur.ToInput());
        if (input is null) return;
        var idx = Units.IndexOf(cur);
        Units[idx] = Build(input);
        SelectedUnit = Units[idx];
    }

    [RelayCommand(CanExecute = nameof(CanChangeUnit))]
    private async Task DeleteUnitAsync()
    {
        var cur = SelectedUnit;
        if (cur is null) return;
        if (!await _dialogs.ConfirmAsync("Удаление позиции", $"Удалить позицию №{cur.No} ({cur.Size} мм)?")) return;
        Units.Remove(cur);
    }

    private UnitRowViewModel Build(UnitInput i)
    {
        var p = _profiles.First(x => x.Id == i.ProfileSystemId);
        var g = _glazings.First(x => x.Id == i.GlazingTypeId);
        var cost = _calc.CalculateUnitCost(i.WidthMm, i.HeightMm, i.SashesCount, p.PricePerMeter, g.PricePerM2);
        return new UnitRowViewModel(Units.Count + 1, p, g, i.WidthMm, i.HeightMm, i.SashesCount, cost);
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task SaveAsync()
    {
        ErrorText = null;
        if (SelectedHomeowner is null) { ErrorText = "Выберите заказчика."; return; }
        try
        {
            var req = new OrderSaveRequest(_orderId, _user.Id, SelectedHomeowner.Id, HasInstallation,
                Units.Select(u => u.ToInput()).ToList());
            var saved = await _orders.SaveAsync(req);
            Saved = true;
            Close(true);
        }
        catch (ValidationFailedException ex) { ErrorText = string.Join("\n", ex.Errors); }
        catch (InvalidOperationException ex) { ErrorText = ex.Message; }
        catch (Exception ex) { ErrorText = "Ошибка сохранения: " + ex.GetBaseException().Message; }
    }

    [RelayCommand]
    private void CloseWindow() => Close(Saved);
}
