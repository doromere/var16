namespace WindowFactory.Core.Models;

/// <summary>Статусы жизненного цикла заказа (FSM).</summary>
public static class OrderStatuses
{
    public const string Measured = "Замер выполнен";
    public const string SentToWorkshop = "Передано в цех";
    public const string Manufactured = "Изготовлено";
    public const string Installed = "Смонтировано";
    public const string Claim = "Рекламация";

    public static readonly IReadOnlyList<string> All =
        new[] { Measured, SentToWorkshop, Manufactured, Installed, Claim };
}

public static class RoleNames
{
    public const string Technologist = "Главный технолог";
    public const string Manager = "Менеджер по замерам и продажам";
}
