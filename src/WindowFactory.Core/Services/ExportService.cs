using System.Globalization;
using System.Net;
using System.Text;
using WindowFactory.Core.Models;

namespace WindowFactory.Core.Services;

public enum ExportDocumentKind { CuttingMap, AcceptanceAct }
public enum ExportFormat { Txt, Html }

public interface IExportService
{
    string Render(WindowOrder order, ExportDocumentKind kind, ExportFormat format);
    Task ExportAsync(WindowOrder order, ExportDocumentKind kind, ExportFormat format, string path);
    string SuggestFileName(WindowOrder order, ExportDocumentKind kind, ExportFormat format);
}

/// <summary>
/// Экспорт технологической карты раскроя и акта приёма-сдачи в .txt/.html.
/// В нижней строке документа печатается SHA256 канонических реквизитов.
/// Заказ должен быть загружен с Homeowner, OrderItems (+ProfileSystem, GlazingType).
/// </summary>
public sealed class ExportService : IExportService
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly CultureInfo Ru = new("ru-RU");
    private readonly IIntegrityService _integrity;
    private readonly ICuttingService _cutting;
    private readonly IOrderCalculationService _calc;

    public ExportService(IIntegrityService integrity, ICuttingService cutting, IOrderCalculationService calc)
    { _integrity = integrity; _cutting = cutting; _calc = calc; }

    public string SuggestFileName(WindowOrder order, ExportDocumentKind kind, ExportFormat format)
        => $"{(kind == ExportDocumentKind.CuttingMap ? "raskroy" : "akt")}_{order.OrderNum}.{(format == ExportFormat.Html ? "html" : "txt")}";

    public async Task ExportAsync(WindowOrder order, ExportDocumentKind kind, ExportFormat format, string path)
        => await File.WriteAllTextAsync(path, Render(order, kind, format), new UTF8Encoding(false));

    public string Render(WindowOrder order, ExportDocumentKind kind, ExportFormat format)
    {
        if (order.OrderItems.Count == 0)
            throw new InvalidOperationException("Нельзя выгрузить документ без позиций.");

        var title = kind == ExportDocumentKind.CuttingMap
            ? "ТЕХНОЛОГИЧЕСКАЯ КАРТА РАСКРОЯ"
            : "АКТ ПРИЁМА-СДАЧИ ОКОННЫХ КОНСТРУКЦИЙ";
        var customer = order.Homeowner?.FullName ?? "—";
        var lines = new List<string>
        {
            $"Номер документа: {order.OrderNum}",
            $"Дата: {order.OrderDate.ToString("dd.MM.yyyy", Ru)}",
            $"Контрагент: {customer}",
            $"Адрес: {order.Homeowner?.DeliveryAddress ?? "—"}",
            $"Статус: {order.Status}",
            $"Монтаж: {(order.HasInstallation ? "да" : "нет")}",
            "",
        };

        var body = kind == ExportDocumentKind.CuttingMap ? CuttingBody(order) : ActBody(order);
        var canonical = _integrity.BuildCanonicalString(order.OrderNum, order.OrderDate, customer, order.TotalPrice, order.OrderItems.Count);
        var hash = _integrity.ComputeSha256(canonical);
        var footer = new[] { $"Реквизиты: {canonical}", $"SHA256: {hash}" };

        var all = new List<string>();
        all.AddRange(lines); all.AddRange(body);
        if (format == ExportFormat.Txt)
        {
            var sb = new StringBuilder();
            sb.AppendLine(title).AppendLine(new string('=', title.Length));
            foreach (var l in all) sb.AppendLine(l);
            sb.AppendLine(new string('-', 60));
            foreach (var l in footer) sb.AppendLine(l);
            return sb.ToString();
        }
        var h = new StringBuilder();
        h.AppendLine("<!DOCTYPE html><html lang=\"ru\"><head><meta charset=\"utf-8\">");
        h.AppendLine($"<title>{WebUtility.HtmlEncode(title)} {WebUtility.HtmlEncode(order.OrderNum)}</title>");
        h.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;margin:32px}pre{font-size:14px;line-height:1.45}footer{margin-top:24px;border-top:1px solid #888;padding-top:8px;font-family:monospace;font-size:12px;word-break:break-all}</style></head><body>");
        h.AppendLine($"<h1>{WebUtility.HtmlEncode(title)}</h1><pre>");
        foreach (var l in all) h.AppendLine(WebUtility.HtmlEncode(l));
        h.AppendLine("</pre><footer>");
        foreach (var l in footer) h.AppendLine(WebUtility.HtmlEncode(l) + "<br>");
        h.AppendLine("</footer></body></html>");
        return h.ToString();
    }

    private List<string> CuttingBody(WindowOrder o)
    {
        var l = new List<string>();
        decimal profile = 0, glass = 0; var n = 0;
        foreach (var u in o.OrderItems)
        {
            var p = _cutting.Plan(u);
            n++;
            l.Add($"Позиция {n}: {u.ProfileSystem?.BrandTitle} / {u.GlazingType?.GlazingName}");
            l.Add($"  Габариты: {u.WidthMm} x {u.HeightMm} мм, створок: {u.SashesCount}");
            foreach (var c in p.ProfilePieces) l.Add($"  Профиль: {c.Name} — {c.LengthMm} мм x {c.Quantity} шт.");
            l.Add($"  Стеклопакет: {p.GlassWidthMm} x {p.GlassHeightMm} мм x {p.GlassPaneCount} шт.");
            l.Add("");
            profile += p.ProfileMeters; glass += p.GlassAreaM2;
        }
        l.Add($"ИТОГО профиля: {profile.ToString("F2", Inv)} м.п.");
        l.Add($"ИТОГО стеклопакетов: {glass.ToString("F3", Inv)} м²");
        l.Add($"Количество позиций: {o.OrderItems.Count}");
        return l;
    }

    private List<string> ActBody(WindowOrder o)
    {
        var l = new List<string>();
        var n = 0;
        foreach (var u in o.OrderItems)
        {
            n++;
            l.Add($"{n}. {u.ProfileSystem?.BrandTitle}, {u.GlazingType?.GlazingName}: " +
                  $"{u.WidthMm} x {u.HeightMm} мм, створок {u.SashesCount} — {u.UnitCalculatedCost.ToString("N2", Ru)} руб.");
        }
        var constructions = o.OrderItems.Sum(x => x.UnitCalculatedCost);
        var totals = _calc.CalculateOrderTotals(o.OrderItems.Select(x => x.UnitCalculatedCost),
            o.HasInstallation, o.Homeowner?.FloorNumber ?? 1, o.Homeowner?.HasElevator ?? true);
        l.Add("");
        l.Add($"Стоимость конструкций: {totals.ConstructionsCost.ToString("N2", Ru)} руб.");
        l.Add($"Монтаж (+20%): {totals.InstallationCost.ToString("N2", Ru)} руб.");
        l.Add($"Подъём на этаж: {totals.FloorSurcharge.ToString("N2", Ru)} руб.");
        l.Add($"ИТОГО: {o.TotalPrice.ToString("N2", Ru)} руб.");
        l.Add($"Количество позиций: {o.OrderItems.Count}");
        l.Add("");
        l.Add($"Гарантия на монтаж и герметичность стеклопакетов: {ProductionLimits.WarrantyMonths} мес. со дня сдачи" +
              (o.CompletedAt is { } d ? $" ({d.ToString("dd.MM.yyyy", Ru)})." : "."));
        l.Add("");
        l.Add("Исполнитель: ____________    Заказчик: ____________");
        return l;
    }
}
