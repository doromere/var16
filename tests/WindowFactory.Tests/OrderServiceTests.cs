using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WindowFactory.Core.Data;
using WindowFactory.Core.Models;
using WindowFactory.Core.Services;

namespace WindowFactory.Tests;

/// <summary>Интеграционные тесты сервиса заказов на SQLite (in-memory) с транзакциями.</summary>
public class OrderServiceTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly DbContextOptions<AppDbContext> _options;
    private DateTime _now = new(2026, 5, 10, 12, 0, 0);
    private readonly OrderService _svc;
    private readonly User _manager, _tech;

    private sealed class Factory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _o;
        public Factory(DbContextOptions<AppDbContext> o) => _o = o;
        public AppDbContext CreateDbContext() => new(_o);
    }

    public OrderServiceTests()
    {
        _conn.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options;
        using (var db = new AppDbContext(_options))
        {
            db.Database.EnsureCreated();
            var rm = new Role { Name = RoleNames.Manager };
            var rt = new Role { Name = RoleNames.Technologist };
            db.Roles.AddRange(rm, rt);
            db.ProfileSystems.Add(new ProfileSystem { BrandTitle = "Rehau", ChambersCount = 3, FrameDepthMm = 60, PricePerMeter = 500m });
            db.GlazingTypes.Add(new GlazingType { GlazingName = "2К", ThicknessMm = 32, PricePerM2 = 2000m });
            db.Homeowners.Add(new Homeowner { FullName = "Иванов", DeliveryAddress = "ул. 1", FloorNumber = 8, HasElevator = false, Phone = "+7 900 123 45 67" });
            db.SaveChanges();
            db.Users.Add(new User { Login = "m", FullName = "М", PasswordHash = "x", RoleId = rm.Id });
            db.Users.Add(new User { Login = "t", FullName = "Т", PasswordHash = "x", RoleId = rt.Id });
            db.SaveChanges();
            _manager = db.Users.Include(u => u.Role).First(u => u.Login == "m");
            _tech = db.Users.Include(u => u.Role).First(u => u.Login == "t");
        }
        _svc = new OrderService(new Factory(_options), new OrderCalculationService(), new OrderStateMachine(),
            new DocumentNumberGenerator(), new ValidationService(), () => _now);
    }

    public void Dispose() => _conn.Dispose();

    private OrderSaveRequest Req(int? id = null, params UnitInput[] units)
        => new(id, _manager.Id, 1, true, units);

    private static UnitInput U(int w = 1000, int h = 1200, int s = 2) => new(1, 1, w, h, s);

    [Fact]
    public async Task Save_AssignsNumberAndComputesTotals()
    {
        var o = await _svc.SaveAsync(Req(null, U()));
        Assert.Equal("ПР23-ОКН-2026-00001", o.OrderNum);
        Assert.Single(o.OrderItems);
        Assert.Equal(9600m, o.OrderItems[0].UnitCalculatedCost);
        // 9600 + 20% (1920) + 3 этажа*150 (этаж 8, без лифта)
        Assert.Equal(9600m + 1920m + 450m, o.TotalPrice);
        var o2 = await _svc.SaveAsync(Req(null, U()));
        Assert.Equal("ПР23-ОКН-2026-00002", o2.OrderNum);
    }

    [Fact]
    public async Task Edit_RecalculatesTotals_AndReplacesLines()
    {
        var o = await _svc.SaveAsync(Req(null, U(), U()));
        var edited = await _svc.SaveAsync(Req(o.Id, U()));
        Assert.Single(edited.OrderItems);
        Assert.Equal(9600m + 1920m + 450m, edited.TotalPrice);
    }

    [Fact]
    public async Task Edit_AfterSentToWorkshop_Throws()
    {
        var o = await _svc.SaveAsync(Req(null, U()));
        await _svc.ChangeStatusAsync(o.Id, OrderStatuses.SentToWorkshop, null, _manager);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _svc.SaveAsync(Req(o.Id, U())));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _svc.DeleteAsync(o.Id));
    }

    [Fact]
    public async Task FullLifecycle_SetsCompletedAt_AndClaimReason()
    {
        var o = await _svc.SaveAsync(Req(null, U()));
        await _svc.ChangeStatusAsync(o.Id, OrderStatuses.SentToWorkshop, null, _manager);
        await _svc.ChangeStatusAsync(o.Id, OrderStatuses.Manufactured, null, _tech);
        var done = await _svc.ChangeStatusAsync(o.Id, OrderStatuses.Installed, null, _manager);
        Assert.Equal(_now, done.CompletedAt);
        var claim = await _svc.ChangeStatusAsync(o.Id, OrderStatuses.Claim, "Царапина на стеклопакете", _manager);
        Assert.Equal("Царапина на стеклопакете", claim.CancelReason);
    }

    [Fact]
    public async Task EmptyOrderCannotBeSentToWorkshop()
    {
        var o = await _svc.SaveAsync(Req(null));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _svc.ChangeStatusAsync(o.Id, OrderStatuses.SentToWorkshop, null, _manager));
    }

    [Fact]
    public async Task WrongRoleCannotChangeStatus()
    {
        var o = await _svc.SaveAsync(Req(null, U()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _svc.ChangeStatusAsync(o.Id, OrderStatuses.SentToWorkshop, null, _tech));
    }

    [Fact]
    public async Task InvalidSize_RollsBackNothingSaved()
    {
        await Assert.ThrowsAsync<ValidationFailedException>(() => _svc.SaveAsync(Req(null, U(w: 2000))));
        Assert.Empty(await _svc.GetOrdersAsync());
    }
}
