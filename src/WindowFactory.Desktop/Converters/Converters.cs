using Avalonia.Data.Converters;
using Avalonia.Media;
using WindowFactory.Core.Models;
using WindowFactory.Desktop.ViewModels;

namespace WindowFactory.Desktop.Converters;

public static class AppConverters
{
    /// <summary>Цветовая индикация строк списка заказов.</summary>
    public static readonly FuncValueConverter<RowSeverity, IBrush> SeverityToBrush = new(s => s switch
    {
        RowSeverity.Critical => SolidColorBrush.Parse("#FECACA"),
        RowSeverity.Overdue => SolidColorBrush.Parse("#FED7AA"),
        RowSeverity.Warning => SolidColorBrush.Parse("#FEF08A"),
        RowSeverity.Success => SolidColorBrush.Parse("#DCFCE7"),
        _ => Brushes.Transparent,
    });

    public static readonly FuncValueConverter<string?, IBrush> StatusToBrush = new(s => s switch
    {
        OrderStatuses.Measured => SolidColorBrush.Parse("#64748B"),
        OrderStatuses.SentToWorkshop => SolidColorBrush.Parse("#2563EB"),
        OrderStatuses.Manufactured => SolidColorBrush.Parse("#7C3AED"),
        OrderStatuses.Installed => SolidColorBrush.Parse("#16A34A"),
        OrderStatuses.Claim => SolidColorBrush.Parse("#DC2626"),
        _ => SolidColorBrush.Parse("#64748B"),
    });

    public static readonly FuncValueConverter<bool, string> YesNo = new(b => b ? "Да" : "Нет");
    public static readonly FuncValueConverter<bool, IBrush> BoolToErrorBrush = new(b => b ? SolidColorBrush.Parse("#B91C1C") : Brushes.Gray);
}
