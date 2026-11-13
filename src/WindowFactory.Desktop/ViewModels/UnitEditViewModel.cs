using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindowFactory.Core.Models;
using WindowFactory.Core.Services;

namespace WindowFactory.Desktop.ViewModels;

public partial class UnitEditViewModel : DialogViewModelBase
{
    private readonly IOrderCalculationService _calc;
    private readonly IValidationService _validation;

    public UnitEditViewModel(IReadOnlyList<ProfileSystem> profiles, IReadOnlyList<GlazingType> glazings,
        UnitInput? existing, IOrderCalculationService calc, IValidationService validation)
    {
        _calc = calc; _validation = validation;
        Profiles = profiles; Glazings = glazings;
        SelectedProfile = profiles.FirstOrDefault(p => p.Id == existing?.ProfileSystemId) ?? profiles.FirstOrDefault();
        SelectedGlazing = glazings.FirstOrDefault(g => g.Id == existing?.GlazingTypeId) ?? glazings.FirstOrDefault();
        Width = existing?.WidthMm ?? 1000;
        Height = existing?.HeightMm ?? 1200;
        Sashes = existing?.SashesCount ?? 1;
        Title = existing is null ? "Новая позиция" : "Редактирование позиции";
        UpdatePreview();
    }

    public string Title { get; }
    public IReadOnlyList<ProfileSystem> Profiles { get; }
    public IReadOnlyList<GlazingType> Glazings { get; }
    public int MinWidth => ProductionLimits.MinWidthMm;
    public int MaxWidth => ProductionLimits.MaxWidthMm;

    [ObservableProperty] private ProfileSystem? _selectedProfile;
    [ObservableProperty] private GlazingType? _selectedGlazing;
    [ObservableProperty] private decimal? _width;
    [ObservableProperty] private decimal? _height;
    [ObservableProperty] private decimal? _sashes;
    [ObservableProperty] private string _preview = string.Empty;
    [ObservableProperty] private string? _errorText;

    partial void OnSelectedProfileChanged(ProfileSystem? value) => UpdatePreview();
    partial void OnSelectedGlazingChanged(GlazingType? value) => UpdatePreview();
    partial void OnWidthChanged(decimal? value) => UpdatePreview();
    partial void OnHeightChanged(decimal? value) => UpdatePreview();
    partial void OnSashesChanged(decimal? value) => UpdatePreview();

    private (int w, int h, int s) Values() => ((int)(Width ?? 0), (int)(Height ?? 0), (int)(Sashes ?? 0));

    private void UpdatePreview()
    {
        var (w, h, s) = Values();
        if (SelectedProfile is null || SelectedGlazing is null || !_validation.ValidateUnit(w, h, s).IsValid)
        {
            Preview = "Стоимость позиции: —";
            return;
        }
        var cost = _calc.CalculateUnitCost(w, h, s, SelectedProfile.PricePerMeter, SelectedGlazing.PricePerM2);
        var area = _calc.CalculateArea(w, h);
        var per = _calc.CalculatePerimeter(w, h);
        Preview = string.Create(new CultureInfo("ru-RU"),
            $"S = {area:0.###} м², P = {per:0.###} м → стоимость позиции: {cost:N2} ₽");
    }

    [RelayCommand]
    private void Ok()
    {
        var (w, h, s) = Values();
        var report = _validation.ValidateUnit(w, h, s);
        var errors = report.Errors.ToList();
        if (SelectedProfile is null) errors.Add("Выберите профильную систему.");
        if (SelectedGlazing is null) errors.Add("Выберите тип стеклопакета.");
        if (errors.Count > 0) { ErrorText = string.Join("\n", errors); return; }
        Close(new UnitInput(SelectedProfile!.Id, SelectedGlazing!.Id, w, h, s));
    }

    [RelayCommand]
    private void Cancel() => Close(null);
}
