using WindowFactory.Core.Models;

namespace WindowFactory.Core.Services;

public sealed record CutPiece(string Name, int LengthMm, int Quantity);

public sealed record UnitCutPlan(
    WindowOrderUnit Unit,
    IReadOnlyList<CutPiece> ProfilePieces,
    int GlassPaneCount,
    int GlassWidthMm,
    int GlassHeightMm,
    decimal ProfileMeters,
    decimal GlassAreaM2);

public interface ICuttingService
{
    UnitCutPlan Plan(WindowOrderUnit unit);
}

/// <summary>Раскрой профиля и размеры стеклопакетов по нормам раскроя.</summary>
public sealed class CuttingService : ICuttingService
{
    private readonly CuttingNorms _n;
    public CuttingService() : this(new CuttingNorms()) { }
    public CuttingService(CuttingNorms norms) => _n = norms;

    public UnitCutPlan Plan(WindowOrderUnit unit)
    {
        var pieces = new List<CutPiece>
        {
            new("Рама, горизонталь", unit.WidthMm + _n.WeldAllowanceMm, 2),
            new("Рама, вертикаль", unit.HeightMm + _n.WeldAllowanceMm, 2),
        };
        var panes = Math.Max(unit.SashesCount, 1);
        var gw = (unit.WidthMm - _n.GlassDeductionMm) / panes;
        var gh = unit.HeightMm - _n.GlassDeductionMm;
        var profileM = 2m * ((decimal)unit.WidthMm + unit.HeightMm) / 1000m;
        var glassArea = panes * (decimal)gw * gh / 1_000_000m;
        return new UnitCutPlan(unit, pieces, panes, gw, gh, profileM, Math.Round(glassArea, 3));
    }
}
