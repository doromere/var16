using WindowFactory.Core.Models;

namespace WindowFactory.Core.Services;

public enum WarrantyState { NotStarted, Active, ExpiringSoon, Expired }

public sealed record WarrantyInfo(WarrantyState State, DateTime? EndsAt, int DaysLeft);

public interface IWarrantyService
{
    WarrantyInfo Evaluate(WindowOrder order, DateTime now);
}

/// <summary>Гарантия на монтаж и герметичность стеклопакетов — 36 месяцев со дня сдачи.</summary>
public sealed class WarrantyService : IWarrantyService
{
    public WarrantyInfo Evaluate(WindowOrder order, DateTime now)
    {
        if (order.CompletedAt is null) return new WarrantyInfo(WarrantyState.NotStarted, null, 0);
        var end = order.CompletedAt.Value.AddMonths(ProductionLimits.WarrantyMonths);
        var days = (int)Math.Ceiling((end - now).TotalDays);
        var state = now >= end ? WarrantyState.Expired
            : days <= ProductionLimits.WarrantyWarningDays ? WarrantyState.ExpiringSoon
            : WarrantyState.Active;
        return new WarrantyInfo(state, end, Math.Max(days, 0));
    }
}
