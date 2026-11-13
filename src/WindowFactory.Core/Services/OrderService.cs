using Microsoft.EntityFrameworkCore;
using WindowFactory.Core.Data;
using WindowFactory.Core.Models;

namespace WindowFactory.Core.Services;

public sealed record UnitInput(int ProfileSystemId, int GlazingTypeId, int WidthMm, int HeightMm, int SashesCount);

public sealed record OrderSaveRequest(int? OrderId, int UserId, int HomeownerId, bool HasInstallation, IReadOnlyList<UnitInput> Units);

public interface IOrderService
{
    Task<List<WindowOrder>> GetOrdersAsync();
    Task<WindowOrder?> GetOrderAsync(int id);
    Task<WindowOrder> SaveAsync(OrderSaveRequest request);
    Task DeleteAsync(int orderId);
    Task<WindowOrder> ChangeStatusAsync(int orderId, string newStatus, string? reason, User actor);
}

/// <summary>
/// Оформление заказов через связку «Заголовок — Спецификация»: .Include(x => x.OrderItems)
/// и транзакционное сохранение. Стоимость пересчитывается на сервере.
/// </summary>
public sealed class OrderService : IOrderService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IOrderCalculationService _calc;
    private readonly IOrderStateMachine _fsm;
    private readonly IDocumentNumberGenerator _numbers;
    private readonly IValidationService _validation;
    private readonly Func<DateTime> _now;

    public OrderService(IDbContextFactory<AppDbContext> factory, IOrderCalculationService calc, IOrderStateMachine fsm,
        IDocumentNumberGenerator numbers, IValidationService validation, Func<DateTime>? now = null)
    { _factory = factory; _calc = calc; _fsm = fsm; _numbers = numbers; _validation = validation; _now = now ?? (() => DateTime.Now); }

    private static IQueryable<WindowOrder> Full(AppDbContext db) => db.WindowOrders
        .Include(x => x.OrderItems).ThenInclude(u => u.ProfileSystem)
        .Include(x => x.OrderItems).ThenInclude(u => u.GlazingType)
        .Include(x => x.Homeowner)
        .Include(x => x.User);

    public async Task<List<WindowOrder>> GetOrdersAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await Full(db).AsNoTracking().OrderByDescending(x => x.OrderDate).ThenByDescending(x => x.Id).ToListAsync();
    }

    public async Task<WindowOrder?> GetOrderAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await Full(db).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<WindowOrder> SaveAsync(OrderSaveRequest r)
    {
        var errors = new List<string>();
        foreach (var u in r.Units) errors.AddRange(_validation.ValidateUnit(u.WidthMm, u.HeightMm, u.SashesCount).Errors);
        if (errors.Count > 0) throw new ValidationFailedException(errors.Distinct().ToList());

        await using var db = await _factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var homeowner = await db.Homeowners.FindAsync(r.HomeownerId)
            ?? throw new InvalidOperationException("Заказчик не найден.");
        var profiles = await db.ProfileSystems.ToDictionaryAsync(p => p.Id);
        var glazings = await db.GlazingTypes.ToDictionaryAsync(g => g.Id);

        WindowOrder order;
        if (r.OrderId is int id)
        {
            order = await db.WindowOrders.Include(x => x.OrderItems).FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new InvalidOperationException("Заказ не найден.");
            _fsm.EnsureEditable(order);
            db.WindowOrderUnits.RemoveRange(order.OrderItems);
            order.OrderItems.Clear();
        }
        else
        {
            var now = _now();
            var prefix = _numbers.Prefix(now.Year);
            var existing = await db.WindowOrders.Where(x => x.OrderNum.StartsWith(prefix)).Select(x => x.OrderNum).ToListAsync();
            order = new WindowOrder
            {
                OrderNum = _numbers.Format(now.Year, _numbers.NextIndex(now.Year, existing)),
                OrderDate = now,
                UserId = r.UserId,
                Status = OrderStatuses.Measured,
            };
            db.WindowOrders.Add(order);
        }

        order.HomeownerId = r.HomeownerId;
        order.HasInstallation = r.HasInstallation;
        foreach (var u in r.Units)
        {
            var unit = new WindowOrderUnit
            {
                ProfileSystemId = u.ProfileSystemId, GlazingTypeId = u.GlazingTypeId,
                WidthMm = u.WidthMm, HeightMm = u.HeightMm, SashesCount = u.SashesCount,
            };
            unit.UnitCalculatedCost = _calc.CalculateUnitCost(unit, profiles[u.ProfileSystemId], glazings[u.GlazingTypeId]);
            order.OrderItems.Add(unit);
        }
        order.TotalPrice = _calc.CalculateOrderTotals(order.OrderItems.Select(x => x.UnitCalculatedCost),
            order.HasInstallation, homeowner.FloorNumber, homeowner.HasElevator).Total;

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return (await GetOrderAsync(order.Id))!;
    }

    public async Task DeleteAsync(int orderId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var order = await db.WindowOrders.Include(x => x.OrderItems).FirstOrDefaultAsync(x => x.Id == orderId)
            ?? throw new InvalidOperationException("Заказ не найден.");
        _fsm.EnsureEditable(order);
        db.WindowOrders.Remove(order);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<WindowOrder> ChangeStatusAsync(int orderId, string newStatus, string? reason, User actor)
    {
        await using var db = await _factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var order = await db.WindowOrders.Include(x => x.OrderItems).FirstOrDefaultAsync(x => x.Id == orderId)
            ?? throw new InvalidOperationException("Заказ не найден.");
        var role = actor.Role?.Name ?? string.Empty;
        if (!RolePermissions.CanPerformTransition(role, order.Status, newStatus))
            throw new UnauthorizedAccessException("Для вашей роли этот переход статуса недоступен.");
        var now = _now();
        _fsm.EnsureTransition(order, newStatus, reason, now);
        order.Status = newStatus;
        if (newStatus == OrderStatuses.Claim) order.CancelReason = reason!.Trim();
        if (newStatus == OrderStatuses.Installed) order.CompletedAt = now;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return (await GetOrderAsync(orderId))!;
    }
}
