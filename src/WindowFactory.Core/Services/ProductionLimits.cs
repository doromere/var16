namespace WindowFactory.Core.Services;

/// <summary>Технологические ограничения профиля.</summary>
public static class ProductionLimits
{
    public const int MinWidthMm = 400;
    public const int MaxWidthMm = 1500;
    public const int MinHeightMm = 400;
    public const int MaxHeightMm = 2200;
    public const int MinSashes = 1;
    public const int MaxSashes = 6;
    public const int MinCancelReasonLength = 10;
    public const int WarrantyMonths = 36;
    public const int WarrantyWarningDays = 30;
}

/// <summary>Параметры тарификации (константы формулы из ТЗ).</summary>
public sealed record PricingOptions
{
    public decimal SashFittingPrice { get; init; } = 2500m;
    public decimal InstallationRate { get; init; } = 0.20m;
    public int FreeFloorsLimit { get; init; } = 5;
    public decimal FloorSurchargePerFloor { get; init; } = 150m;
}

/// <summary>Нормы раскроя (задаются главным технологом).</summary>
public sealed record CuttingNorms
{
    /// <summary>Припуск на сварку на каждый отрез профиля, мм.</summary>
    public int WeldAllowanceMm { get; init; } = 6;
    /// <summary>Вычет на раму и створки при расчёте размера стеклопакета, мм.</summary>
    public int GlassDeductionMm { get; init; } = 100;
}

public sealed class ValidationFailedException : Exception
{
    public IReadOnlyList<string> Errors { get; }
    public ValidationFailedException(IReadOnlyList<string> errors) : base(string.Join(Environment.NewLine, errors))
        => Errors = errors;
}
