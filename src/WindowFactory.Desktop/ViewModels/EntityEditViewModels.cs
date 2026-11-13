using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindowFactory.Core.Models;
using WindowFactory.Core.Services;

namespace WindowFactory.Desktop.ViewModels;

public partial class HomeownerEditViewModel : DialogViewModelBase
{
    private readonly IValidationService _v;
    private readonly int _id;
    public string Title { get; }

    [ObservableProperty] private string _fullName = string.Empty;
    [ObservableProperty] private string _deliveryAddress = string.Empty;
    [ObservableProperty] private decimal? _floorNumber = 1;
    [ObservableProperty] private bool _hasElevator;
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private string? _errorText;

    public HomeownerEditViewModel(Homeowner? h, IValidationService v)
    {
        _v = v;
        Title = h is null ? "Новый заказчик" : "Редактирование заказчика";
        if (h is null) return;
        _id = h.Id; FullName = h.FullName; DeliveryAddress = h.DeliveryAddress;
        FloorNumber = h.FloorNumber; HasElevator = h.HasElevator; Phone = h.Phone;
    }

    [RelayCommand]
    private void Ok()
    {
        var floor = (int)(FloorNumber ?? 0);
        var rep = _v.ValidateHomeowner(FullName, DeliveryAddress, floor, Phone);
        if (!rep.IsValid) { ErrorText = string.Join("\n", rep.Errors); return; }
        Close(new Homeowner
        {
            Id = _id, FullName = FullName.Trim(), DeliveryAddress = DeliveryAddress.Trim(),
            FloorNumber = floor, HasElevator = HasElevator, Phone = Phone.Trim(),
        });
    }

    [RelayCommand] private void Cancel() => Close(null);
}

public partial class ProfileEditViewModel : DialogViewModelBase
{
    private readonly IValidationService _v;
    private readonly int _id;
    public string Title { get; }

    [ObservableProperty] private string _brandTitle = string.Empty;
    [ObservableProperty] private decimal? _chambersCount = 3;
    [ObservableProperty] private decimal? _frameDepthMm = 60;
    [ObservableProperty] private decimal? _pricePerMeter = 0;
    [ObservableProperty] private string? _errorText;

    public ProfileEditViewModel(ProfileSystem? p, IValidationService v)
    {
        _v = v;
        Title = p is null ? "Новая профильная система" : "Редактирование профильной системы";
        if (p is null) return;
        _id = p.Id; BrandTitle = p.BrandTitle; ChambersCount = p.ChambersCount;
        FrameDepthMm = p.FrameDepthMm; PricePerMeter = p.PricePerMeter;
    }

    [RelayCommand]
    private void Ok()
    {
        var rep = _v.ValidateProfile(BrandTitle, (int)(ChambersCount ?? 0), (int)(FrameDepthMm ?? 0), PricePerMeter ?? -1);
        if (!rep.IsValid) { ErrorText = string.Join("\n", rep.Errors); return; }
        Close(new ProfileSystem
        {
            Id = _id, BrandTitle = BrandTitle.Trim(), ChambersCount = (int)ChambersCount!.Value,
            FrameDepthMm = (int)FrameDepthMm!.Value, PricePerMeter = PricePerMeter!.Value,
        });
    }

    [RelayCommand] private void Cancel() => Close(null);
}

public partial class GlazingEditViewModel : DialogViewModelBase
{
    private readonly IValidationService _v;
    private readonly int _id;
    public string Title { get; }

    [ObservableProperty] private string _glazingName = string.Empty;
    [ObservableProperty] private decimal? _thicknessMm = 32;
    [ObservableProperty] private bool _energySaving;
    [ObservableProperty] private decimal? _pricePerM2 = 0;
    [ObservableProperty] private string? _errorText;

    public GlazingEditViewModel(GlazingType? g, IValidationService v)
    {
        _v = v;
        Title = g is null ? "Новый стеклопакет" : "Редактирование стеклопакета";
        if (g is null) return;
        _id = g.Id; GlazingName = g.GlazingName; ThicknessMm = g.ThicknessMm;
        EnergySaving = g.EnergySaving; PricePerM2 = g.PricePerM2;
    }

    [RelayCommand]
    private void Ok()
    {
        var rep = _v.ValidateGlazing(GlazingName, (int)(ThicknessMm ?? 0), PricePerM2 ?? -1);
        if (!rep.IsValid) { ErrorText = string.Join("\n", rep.Errors); return; }
        Close(new GlazingType
        {
            Id = _id, GlazingName = GlazingName.Trim(), ThicknessMm = (int)ThicknessMm!.Value,
            EnergySaving = EnergySaving, PricePerM2 = PricePerM2!.Value,
        });
    }

    [RelayCommand] private void Cancel() => Close(null);
}

public partial class ReasonViewModel : DialogViewModelBase
{
    public string Title { get; }
    public string Prompt { get; }
    public int MinLength { get; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(Counter)), NotifyCanExecuteChangedFor(nameof(OkCommand))]
    private string _reason = string.Empty;

    public ReasonViewModel(string title, string prompt, int minLength) { Title = title; Prompt = prompt; MinLength = minLength; }

    public string Counter => $"{Reason.Trim().Length} / мин. {MinLength} знаков";
    private bool CanOk() => Reason.Trim().Length >= MinLength;

    [RelayCommand(CanExecute = nameof(CanOk))] private void Ok() => Close(Reason.Trim());
    [RelayCommand] private void Cancel() => Close(null);
}

public partial class MessageViewModel : DialogViewModelBase
{
    public string Title { get; }
    public string Message { get; }
    public bool ShowCancel { get; }
    public string OkText { get; }

    public MessageViewModel(string title, string message, bool showCancel, string okText = "OK")
    { Title = title; Message = message; ShowCancel = showCancel; OkText = okText; }

    [RelayCommand] private void Ok() => Close(true);
    [RelayCommand] private void Cancel() => Close(false);
}
