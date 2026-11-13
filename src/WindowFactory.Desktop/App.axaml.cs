using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WindowFactory.Core.Data;
using WindowFactory.Core.Services;
using WindowFactory.Desktop.Services;
using WindowFactory.Desktop.ViewModels;
using WindowFactory.Desktop.Views;

namespace WindowFactory.Desktop;

public partial class App : Application
{
    private IServiceProvider _services = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        _services = BuildServices();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            ShowLogin(desktop);
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Регистрация зависимостей.</summary>
    private static IServiceProvider BuildServices()
    {
        var sc = new ServiceCollection();
        sc.AddDbContextFactory<AppDbContext>(o => o.UseMySql(AppSettings.ConnectionString,
            ServerVersion.Create(10, 4, 0, Pomelo.EntityFrameworkCore.MySql.Infrastructure.ServerType.MariaDb)));
        sc.AddSingleton<IPasswordHasher, PasswordHasher>();
        sc.AddSingleton<IValidationService, ValidationService>();
        sc.AddSingleton<IOrderCalculationService>(_ => new OrderCalculationService(new PricingOptions()));
        sc.AddSingleton<IWarrantyService, WarrantyService>();
        sc.AddSingleton<IOrderStateMachine, OrderStateMachine>();
        sc.AddSingleton<IDocumentNumberGenerator, DocumentNumberGenerator>();
        sc.AddSingleton<IIntegrityService, IntegrityService>();
        sc.AddSingleton<ICuttingService>(_ => new CuttingService(new CuttingNorms()));
        sc.AddSingleton<IExportService, ExportService>();
        sc.AddSingleton<IAuthService, AuthService>();
        sc.AddSingleton<ICatalogService, CatalogService>();
        sc.AddSingleton<IHomeownerService, HomeownerService>();
        sc.AddSingleton<IOrderService>(p => new OrderService(
            p.GetRequiredService<IDbContextFactory<AppDbContext>>(), p.GetRequiredService<IOrderCalculationService>(),
            p.GetRequiredService<IOrderStateMachine>(), p.GetRequiredService<IDocumentNumberGenerator>(),
            p.GetRequiredService<IValidationService>()));
        sc.AddSingleton<IDialogService, DialogService>();
        return sc.BuildServiceProvider();
    }

    private void ShowLogin(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var vm = new LoginViewModel(_services.GetRequiredService<IAuthService>());
        var window = new LoginWindow { DataContext = vm };
        vm.LoggedIn += user =>
        {
            var main = new MainWindow();
            var mainVm = new MainViewModel(user, _services);
            main.DataContext = mainVm;
            mainVm.LogoutRequested += () => { ShowLogin(desktop); main.Close(); };
            desktop.MainWindow = main;
            main.Show();
            window.Close();
            _ = mainVm.LoadAsync();
        };
        desktop.MainWindow = window;
        window.Show();
    }
}
