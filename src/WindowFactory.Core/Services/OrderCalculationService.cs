using WindowFactory.Core.Models;

namespace WindowFactory.Core.Services;

public sealed record OrderTotals(decimal ConstructionsCost, decimal InstallationCost, decimal FloorSurcharge)
{
    public decimal Total => ConstructionsCost + InstallationCost + FloorSurcharge;
}

public interface IOrderCalculationService
{
    decimal CalculateArea(int widthMm, int heightMm);
    decimal CalculatePerimeter(int widthMm, int heightMm);
    decimal CalculateUnitCost(int widthMm, int heightMm, int sashesCount, decimal pricePerMeter, decimal pricePerM2);
    decimal CalculateUnitCost(WindowOrderUnit unit, ProfileSystem profile, GlazingType glazing);
    OrderTotals CalculateOrderTotals(IEnumerable<decimal> unitCosts, bool hasInstallation, int floorNumber, bool hasElevator);
}

/// <summary>
/// Расчёт стоимости (ПК 1.1, 1.2). Не зависит от UI-библиотек.
/// S = w*h/1e6; P = 2*(w+h)/1000;
/// cost = P*price_per_meter + S*price_per_m2 + sashes*2500.
/// Монтаж +20% от стоимости конструкций; подъём выше 5 этажа без лифта +150 руб./этаж.
/// </summary>
public sealed class OrderCalculationService : IOrderCalculationService
{
    private readonly PricingOptions _o;
    public OrderCalculationService() : this(new PricingOptions()) { }
    public OrderCalculationService(PricingOptions options) => _o = options;

    public decimal CalculateArea(int widthMm, int heightMm) => (decimal)widthMm * heightMm / 1_000_000m;

    public decimal CalculatePerimeter(int widthMm, int heightMm) => 2m * ((decimal)widthMm + heightMm) / 1000m;

    public decimal CalculateUnitCost(int widthMm, int heightMm, int sashesCount, decimal pricePerMeter, decimal pricePerM2)
    {
        if (widthMm <= 0 || heightMm <= 0) throw new ArgumentOutOfRangeException(nameof(widthMm), "Размеры должны быть положительными.");
        if (sashesCount < 0) throw new ArgumentOutOfRangeException(nameof(sashesCount));
        if (pricePerMeter < 0 || pricePerM2 < 0) throw new ArgumentOutOfRangeException(nameof(pricePerMeter), "Цена не может быть отрицательной.");
        var cost = CalculatePerimeter(widthMm, heightMm) * pricePerMeter
                 + CalculateArea(widthMm, heightMm) * pricePerM2
                 + sashesCount * _o.SashFittingPrice;
        return Round(cost);
    }

    public decimal CalculateUnitCost(WindowOrderUnit unit, ProfileSystem profile, GlazingType glazing)
        => CalculateUnitCost(unit.WidthMm, unit.HeightMm, unit.SashesCount, profile.PricePerMeter, glazing.PricePerM2);

    public OrderTotals CalculateOrderTotals(IEnumerable<decimal> unitCosts, bool hasInstallation, int floorNumber, bool hasElevator)
    {
        var constructions = unitCosts.Sum();
        var installation = hasInstallation ? Round(constructions * _o.InstallationRate) : 0m;
        var surcharge = 0m;
        if (hasInstallation && !hasElevator && floorNumber > _o.FreeFloorsLimit)
            surcharge = (floorNumber - _o.FreeFloorsLimit) * _o.FloorSurchargePerFloor;
        return new OrderTotals(Round(constructions), installation, surcharge);
    }

    private static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
