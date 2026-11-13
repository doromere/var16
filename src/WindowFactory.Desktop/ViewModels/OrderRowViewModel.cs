using CommunityToolkit.Mvvm.ComponentModel;
using WindowFactory.Core.Models;
using WindowFactory.Core.Services;

namespace WindowFactory.Desktop.ViewModels;

public enum RowSeverity { Normal, Success, Warning, Overdue, Critical }

/// <summary>Строка списка заказов (DataGrid) с динамически обновляемой гарантией.</summary>
public partial class OrderRowViewModel : ObservableObject
{
    public WindowOrder Model { get; }
    public OrderRowViewModel(WindowOrder model) => Model = model;

    public int Id => Model.Id;
    public string Number => Model.OrderNum;
    public DateTime OrderDate => Model.OrderDate;
    public string Homeowner => Model.Homeowner?.FullName ?? "—";
    public string Address => Model.Homeowner?.DeliveryAddress ?? "";
    public string Manager => Model.User?.FullName ?? "";
    public string Status => Model.Status;
    public decimal TotalPrice => Model.TotalPrice;
    public int UnitsCount => Model.OrderItems.Count;
    public bool HasInstallation => Model.HasInstallation;
    public string Profiles => string.Join(", ", Model.OrderItems.Select(u => u.ProfileSystem?.BrandTitle).Distinct());

    [ObservableProperty] private string _warrantyText = "—";
    [ObservableProperty] private RowSeverity _severity;

    public void UpdateWarranty(IWarrantyService svc, DateTime now)
    {
        var info = svc.Evaluate(Model, now);
        if (Model.Status == OrderStatuses.Claim)
        {
            Severity = RowSeverity.Critical;
            WarrantyText = "Рекламация";
            return;
        }
        switch (info.State)
        {
            case WarrantyState.Expired:
                Severity = RowSeverity.Overdue;
                WarrantyText = $"Истекла {info.EndsAt:dd.MM.yyyy}";
                break;
            case WarrantyState.ExpiringSoon:
                Severity = RowSeverity.Warning;
                WarrantyText = $"Истекает через {info.DaysLeft} дн.";
                break;
            case WarrantyState.Active:
                Severity = RowSeverity.Success;
                WarrantyText = $"До {info.EndsAt:dd.MM.yyyy}";
                break;
            default:
                Severity = RowSeverity.Normal;
                WarrantyText = "—";
                break;
        }
    }
}
