using WindowFactory.Core.Models;

namespace WindowFactory.Core.Services;

/// <summary>Разграничение прав между ролями.</summary>
public static class RolePermissions
{
    public static bool CanEditCatalog(string role) => role == RoleNames.Technologist;
    public static bool CanManageOrders(string role) => role == RoleNames.Manager;
    public static bool CanManageHomeowners(string role) => role == RoleNames.Manager;

    public static bool CanPerformTransition(string role, string from, string to) => role switch
    {
        RoleNames.Technologist => from == OrderStatuses.SentToWorkshop && to == OrderStatuses.Manufactured,
        RoleNames.Manager =>
            (from == OrderStatuses.Measured && to == OrderStatuses.SentToWorkshop) ||
            (from == OrderStatuses.Manufactured && to == OrderStatuses.Installed) ||
            (from is OrderStatuses.Manufactured or OrderStatuses.Installed && to == OrderStatuses.Claim),
        _ => false,
    };
}
