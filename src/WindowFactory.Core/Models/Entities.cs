namespace WindowFactory.Core.Models;

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<User> Users { get; set; } = new List<User>();
}

public class User
{
    public int Id { get; set; }
    public int RoleId { get; set; }
    public string Login { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public Role? Role { get; set; }
}

public class ProfileSystem
{
    public int Id { get; set; }
    public string BrandTitle { get; set; } = string.Empty;
    public int ChambersCount { get; set; }
    public int FrameDepthMm { get; set; }
    public decimal PricePerMeter { get; set; }
    public override string ToString() => BrandTitle;
}

public class GlazingType
{
    public int Id { get; set; }
    public string GlazingName { get; set; } = string.Empty;
    public int ThicknessMm { get; set; }
    public bool EnergySaving { get; set; }
    public decimal PricePerM2 { get; set; }
    public override string ToString() => GlazingName;
}

public class Homeowner
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public int FloorNumber { get; set; } = 1;
    public bool HasElevator { get; set; }
    public string Phone { get; set; } = string.Empty;
    public override string ToString() => FullName;
}

public class WindowOrder
{
    public int Id { get; set; }
    public string OrderNum { get; set; } = string.Empty;
    public int UserId { get; set; }
    public int HomeownerId { get; set; }
    public DateTime OrderDate { get; set; }
    public bool HasInstallation { get; set; }
    public decimal TotalPrice { get; set; }
    public string Status { get; set; } = OrderStatuses.Measured;
    public string? CancelReason { get; set; }
    /// <summary>Дата сдачи (перевод в «Смонтировано»), от неё считается гарантия 36 месяцев.</summary>
    public DateTime? CompletedAt { get; set; }

    public User? User { get; set; }
    public Homeowner? Homeowner { get; set; }
    public List<WindowOrderUnit> OrderItems { get; set; } = new();
}

public class WindowOrderUnit
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProfileSystemId { get; set; }
    public int GlazingTypeId { get; set; }
    public int WidthMm { get; set; }
    public int HeightMm { get; set; }
    public int SashesCount { get; set; }
    public decimal UnitCalculatedCost { get; set; }

    public WindowOrder? WindowOrder { get; set; }
    public ProfileSystem? ProfileSystem { get; set; }
    public GlazingType? GlazingType { get; set; }
}
