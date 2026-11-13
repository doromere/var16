using CommunityToolkit.Mvvm.ComponentModel;
using WindowFactory.Core.Models;
using WindowFactory.Core.Services;

namespace WindowFactory.Desktop.ViewModels;

/// <summary>Строка спецификации заказа (Detail).</summary>
public partial class UnitRowViewModel : ObservableObject
{
    public UnitRowViewModel(int no, ProfileSystem profile, GlazingType glazing, int w, int h, int sashes, decimal cost)
    {
        No = no; Profile = profile; Glazing = glazing; WidthMm = w; HeightMm = h; SashesCount = sashes; Cost = cost;
    }

    [ObservableProperty] private int _no;
    public ProfileSystem Profile { get; }
    public GlazingType Glazing { get; }
    public int WidthMm { get; }
    public int HeightMm { get; }
    public int SashesCount { get; }
    public decimal Cost { get; }

    public string ProfileTitle => Profile.BrandTitle;
    public string GlazingTitle => Glazing.GlazingName;
    public decimal AreaM2 => (decimal)WidthMm * HeightMm / 1_000_000m;
    public string Size => $"{WidthMm} × {HeightMm}";

    public UnitInput ToInput() => new(Profile.Id, Glazing.Id, WidthMm, HeightMm, SashesCount);
}
