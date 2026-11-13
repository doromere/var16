using WindowFactory.Core.Models;

namespace WindowFactory.Core.Services;

public interface IOrderStateMachine
{
    bool CanTransition(string from, string to);
    IReadOnlyCollection<string> GetAllowedTransitions(string from);
    bool IsEditable(string status);
    void EnsureEditable(WindowOrder order);
    void EnsureTransition(WindowOrder order, string to, string? reason, DateTime now);
}

/// <summary>
/// Строгий конечный автомат: Замер выполнен → Передано в цех → Изготовлено → Смонтировано / Рекламация.
/// Все нарушения правил — InvalidOperationException. Заказ должен быть загружен вместе с OrderItems.
/// </summary>
public sealed class OrderStateMachine : IOrderStateMachine
{
    private static readonly Dictionary<string, string[]> Map = new()
    {
        [OrderStatuses.Measured] = new[] { OrderStatuses.SentToWorkshop },
        [OrderStatuses.SentToWorkshop] = new[] { OrderStatuses.Manufactured },
        [OrderStatuses.Manufactured] = new[] { OrderStatuses.Installed, OrderStatuses.Claim },
        [OrderStatuses.Installed] = new[] { OrderStatuses.Claim },
        [OrderStatuses.Claim] = Array.Empty<string>(),
    };

    private readonly IWarrantyService _warranty;
    public OrderStateMachine() : this(new WarrantyService()) { }
    public OrderStateMachine(IWarrantyService warranty) => _warranty = warranty;

    public bool CanTransition(string from, string to)
        => Map.TryGetValue(from, out var next) && next.Contains(to);

    public IReadOnlyCollection<string> GetAllowedTransitions(string from)
        => Map.TryGetValue(from, out var next) ? next : Array.Empty<string>();

    /// <summary>Строки заказа можно менять только пока заказ в статусе «Замер выполнен».</summary>
    public bool IsEditable(string status) => status == OrderStatuses.Measured;

    public void EnsureEditable(WindowOrder order)
    {
        if (!IsEditable(order.Status))
            throw new InvalidOperationException(
                $"Заказ {order.OrderNum} в статусе «{order.Status}» закрыт для редактирования.");
    }

    public void EnsureTransition(WindowOrder order, string to, string? reason, DateTime now)
    {
        if (!Map.ContainsKey(order.Status) || !OrderStatuses.All.Contains(to))
            throw new InvalidOperationException($"Неизвестный статус: «{order.Status}» → «{to}».");
        if (!CanTransition(order.Status, to))
            throw new InvalidOperationException($"Недопустимый переход: «{order.Status}» → «{to}».");

        if (to == OrderStatuses.SentToWorkshop)
        {
            if (order.OrderItems.Count == 0)
                throw new InvalidOperationException("Нельзя передать в цех пустой заказ (0 строк).");
            foreach (var u in order.OrderItems)
            {
                if (u.WidthMm < ProductionLimits.MinWidthMm || u.WidthMm > ProductionLimits.MaxWidthMm)
                    throw new InvalidOperationException(
                        $"Ширина {u.WidthMm} мм вне технологических ограничений профиля ({ProductionLimits.MinWidthMm}–{ProductionLimits.MaxWidthMm} мм).");
                if (u.HeightMm < ProductionLimits.MinHeightMm || u.HeightMm > ProductionLimits.MaxHeightMm)
                    throw new InvalidOperationException(
                        $"Высота {u.HeightMm} мм вне технологических ограничений ({ProductionLimits.MinHeightMm}–{ProductionLimits.MaxHeightMm} мм).");
            }
        }

        if (to == OrderStatuses.Claim)
        {
            if ((reason ?? string.Empty).Trim().Length < ProductionLimits.MinCancelReasonLength)
                throw new InvalidOperationException(
                    $"Для рекламации обязательна причина (не менее {ProductionLimits.MinCancelReasonLength} знаков).");
            if (order.Status == OrderStatuses.Installed &&
                _warranty.Evaluate(order, now).State == WarrantyState.Expired)
                throw new InvalidOperationException("Гарантийный срок (36 месяцев) истёк, рекламация невозможна.");
        }
    }
}
