using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using WindowFactory.Core.Models;
using WindowFactory.Core.Services;
using WindowFactory.Desktop.ViewModels;
using WindowFactory.Desktop.Views;
using WindowFactory.Desktop.Views.Dialogs;

namespace WindowFactory.Desktop.Services;

public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message);
    Task ShowMessageAsync(string title, string message);
    Task<string?> AskReasonAsync(string title, string prompt);
    Task<UnitInput?> EditUnitAsync(IReadOnlyList<ProfileSystem> profiles, IReadOnlyList<GlazingType> glazings, UnitInput? existing);
    Task<Homeowner?> EditHomeownerAsync(Homeowner? existing);
    Task<ProfileSystem?> EditProfileAsync(ProfileSystem? existing);
    Task<GlazingType?> EditGlazingAsync(GlazingType? existing);
    /// <summary>Открывает Master-Detail окно заказа; true — если заказ сохранён.</summary>
    Task<bool> ShowOrderAsync(User user, int? orderId);
    Task<string?> PickSaveFileAsync(string suggestedName);
}

public sealed class DialogService : IDialogService
{
    private readonly IServiceProvider _sp;
    public DialogService(IServiceProvider sp) => _sp = sp;

    private T Get<T>() where T : notnull => (T)_sp.GetService(typeof(T))!;

    private static Window? Owner
    {
        get
        {
            var life = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            return life?.Windows.LastOrDefault(w => w.IsActive) ?? life?.Windows.LastOrDefault(w => w.IsVisible) ?? life?.MainWindow;
        }
    }

    private static async Task<object?> ShowAsync(Window window, DialogViewModelBase vm)
    {
        window.DataContext = vm;
        vm.CloseRequested += r => window.Close(r);
        var owner = Owner;
        if (owner is null) return null;
        return await window.ShowDialog<object?>(owner);
    }

    public async Task<bool> ConfirmAsync(string title, string message)
        => await ShowAsync(new MessageDialog(), new MessageViewModel(title, message, true, "Да")) is true;

    public Task ShowMessageAsync(string title, string message)
        => ShowAsync(new MessageDialog(), new MessageViewModel(title, message, false));

    public async Task<string?> AskReasonAsync(string title, string prompt)
        => await ShowAsync(new ReasonDialog(), new ReasonViewModel(title, prompt, ProductionLimits.MinCancelReasonLength)) as string;

    public async Task<UnitInput?> EditUnitAsync(IReadOnlyList<ProfileSystem> profiles, IReadOnlyList<GlazingType> glazings, UnitInput? existing)
        => await ShowAsync(new UnitEditDialog(),
            new UnitEditViewModel(profiles, glazings, existing, Get<IOrderCalculationService>(), Get<IValidationService>())) as UnitInput;

    public async Task<Homeowner?> EditHomeownerAsync(Homeowner? existing)
        => await ShowAsync(new HomeownerEditDialog(), new HomeownerEditViewModel(existing, Get<IValidationService>())) as Homeowner;

    public async Task<ProfileSystem?> EditProfileAsync(ProfileSystem? existing)
        => await ShowAsync(new ProfileEditDialog(), new ProfileEditViewModel(existing, Get<IValidationService>())) as ProfileSystem;

    public async Task<GlazingType?> EditGlazingAsync(GlazingType? existing)
        => await ShowAsync(new GlazingEditDialog(), new GlazingEditViewModel(existing, Get<IValidationService>())) as GlazingType;

    public async Task<bool> ShowOrderAsync(User user, int? orderId)
    {
        var vm = new OrderEditViewModel(user, orderId, Get<IOrderService>(), Get<IHomeownerService>(), Get<ICatalogService>(),
            Get<IOrderCalculationService>(), Get<ICuttingService>(), this);
        try { await vm.InitializeAsync(); }
        catch (Exception ex) { await ShowMessageAsync("Ошибка", ex.GetBaseException().Message); return false; }
        return await ShowAsync(new OrderEditWindow(), vm) is true;
    }

    public async Task<string?> PickSaveFileAsync(string suggestedName)
    {
        var owner = Owner;
        if (owner is null) return null;
        try
        {
            if (owner.StorageProvider.CanSave)
            {
                var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Сохранить документ",
                    SuggestedFileName = suggestedName,
                    DefaultExtension = Path.GetExtension(suggestedName).TrimStart('.'),
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("Текстовый файл (.txt)") { Patterns = new[] { "*.txt" } },
                        new FilePickerFileType("HTML (.html)") { Patterns = new[] { "*.html" } },
                    },
                });
                // null — пользователь отменил выбор
                return file?.TryGetLocalPath();
            }
        }
        catch (Exception)
        {
            // системный диалог недоступен — сохраняем в папку по умолчанию
        }
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WindowFactory");
        if (!Directory.Exists(dir)) dir = Path.Combine(AppContext.BaseDirectory, "exports");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, suggestedName);
    }
}
