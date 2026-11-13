using WindowFactory.Core.Models;
using WindowFactory.Core.Services;

namespace WindowFactory.Tests;

public class CalculationTests
{
    private readonly OrderCalculationService _calc = new();

    // Тест 1 (ПК 1.4): штатный расчёт позиции и итога заказа.
    [Fact]
    public void Test1_StandardUnitAndOrderTotal()
    {
        // 1000x1200: S=1.2, P=4.4; 4.4*500 + 1.2*2000 + 2*2500 = 2200+2400+5000 = 9600
        var unit = _calc.CalculateUnitCost(1000, 1200, 2, 500m, 2000m);
        Assert.Equal(9600m, unit);

        var totals = _calc.CalculateOrderTotals(new[] { unit, 400m }, hasInstallation: false, floorNumber: 3, hasElevator: false);
        Assert.Equal(10000m, totals.Total);
    }

    // Тест 2 (ПК 1.4): надбавки — монтаж +20% и подъём выше 5 этажа без лифта +150 руб./этаж.
    [Fact]
    public void Test2_InstallationAndFloorSurcharges()
    {
        var costs = new[] { 10000m };
        var withInstall = _calc.CalculateOrderTotals(costs, true, 3, false);
        Assert.Equal(2000m, withInstall.InstallationCost);
        Assert.Equal(0m, withInstall.FloorSurcharge);
        Assert.Equal(12000m, withInstall.Total);

        var floor8 = _calc.CalculateOrderTotals(costs, true, 8, false); // 3 этажа * 150
        Assert.Equal(450m, floor8.FloorSurcharge);
        Assert.Equal(12450m, floor8.Total);

        var withLift = _calc.CalculateOrderTotals(costs, true, 8, true);
        Assert.Equal(0m, withLift.FloorSurcharge);

        var noInstall = _calc.CalculateOrderTotals(costs, false, 8, false);
        Assert.Equal(10000m, noInstall.Total);
    }

    [Fact]
    public void AreaAndPerimeter()
    {
        Assert.Equal(1.2m, _calc.CalculateArea(1000, 1200));
        Assert.Equal(4.4m, _calc.CalculatePerimeter(1000, 1200));
    }

    [Fact]
    public void NegativePriceRejected()
        => Assert.Throws<ArgumentOutOfRangeException>(() => _calc.CalculateUnitCost(1000, 1000, 1, -1m, 100m));
}

public class StateMachineTests
{
    private readonly OrderStateMachine _fsm = new();
    private static readonly DateTime Now = new(2026, 6, 1);

    private static WindowOrder Order(string status, int units = 1, DateTime? completed = null)
    {
        var o = new WindowOrder { OrderNum = "ПР23-ОКН-2026-00001", Status = status, CompletedAt = completed };
        for (var i = 0; i < units; i++) o.OrderItems.Add(new WindowOrderUnit { WidthMm = 1000, HeightMm = 1200, SashesCount = 1 });
        return o;
    }

    // Тест 3 (ПК 1.4): валидный переход по всей цепочке FSM.
    [Fact]
    public void Test3_AllowedChainPasses()
    {
        var o = Order(OrderStatuses.Measured);
        string[] chain = { OrderStatuses.SentToWorkshop, OrderStatuses.Manufactured, OrderStatuses.Installed };
        foreach (var next in chain)
        {
            _fsm.EnsureTransition(o, next, null, Now);
            o.Status = next;
        }
        Assert.Equal(OrderStatuses.Installed, o.Status);
        Assert.True(_fsm.CanTransition(OrderStatuses.Manufactured, OrderStatuses.Claim));
    }

    // Тест 4 (ПК 1.4): недопустимый переход и правка закрытого документа → InvalidOperationException.
    [Fact]
    public void Test4_InvalidTransitionThrows()
    {
        var o = Order(OrderStatuses.Measured);
        Assert.Throws<InvalidOperationException>(() => _fsm.EnsureTransition(o, OrderStatuses.Installed, null, Now));
        Assert.Throws<InvalidOperationException>(() => _fsm.EnsureTransition(Order(OrderStatuses.Claim), OrderStatuses.Installed, null, Now));
    }

    [Fact]
    public void Test4_EditingClosedDocumentThrows()
    {
        foreach (var s in new[] { OrderStatuses.SentToWorkshop, OrderStatuses.Manufactured, OrderStatuses.Installed, OrderStatuses.Claim })
            Assert.Throws<InvalidOperationException>(() => _fsm.EnsureEditable(Order(s)));
        _fsm.EnsureEditable(Order(OrderStatuses.Measured));
    }

    [Fact]
    public void EmptyOrderCannotGoToWorkshop()
        => Assert.Throws<InvalidOperationException>(() =>
            _fsm.EnsureTransition(Order(OrderStatuses.Measured, units: 0), OrderStatuses.SentToWorkshop, null, Now));

    [Fact]
    public void OversizedWidthBlocksTransfer()
    {
        var o = Order(OrderStatuses.Measured);
        o.OrderItems[0].WidthMm = 1600;
        Assert.Throws<InvalidOperationException>(() => _fsm.EnsureTransition(o, OrderStatuses.SentToWorkshop, null, Now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("коротко")]
    [InlineData("         ")]
    public void ClaimRequiresReasonOfTenChars(string? reason)
        => Assert.Throws<InvalidOperationException>(() =>
            _fsm.EnsureTransition(Order(OrderStatuses.Manufactured), OrderStatuses.Claim, reason, Now));

    [Fact]
    public void ClaimWithValidReasonPasses()
        => _fsm.EnsureTransition(Order(OrderStatuses.Manufactured), OrderStatuses.Claim, "Скол на стеклопакете", Now);

    [Fact]
    public void ClaimAfterWarrantyExpiredThrows()
    {
        var o = Order(OrderStatuses.Installed, completed: new DateTime(2022, 1, 1));
        Assert.Throws<InvalidOperationException>(() =>
            _fsm.EnsureTransition(o, OrderStatuses.Claim, "Продувает по периметру", Now));
    }

    [Fact]
    public void RolePermissions_SplitBetweenRoles()
    {
        Assert.True(RolePermissions.CanPerformTransition(RoleNames.Technologist, OrderStatuses.SentToWorkshop, OrderStatuses.Manufactured));
        Assert.False(RolePermissions.CanPerformTransition(RoleNames.Technologist, OrderStatuses.Measured, OrderStatuses.SentToWorkshop));
        Assert.True(RolePermissions.CanPerformTransition(RoleNames.Manager, OrderStatuses.Measured, OrderStatuses.SentToWorkshop));
        Assert.False(RolePermissions.CanPerformTransition(RoleNames.Manager, OrderStatuses.SentToWorkshop, OrderStatuses.Manufactured));
    }
}

public class SupportServicesTests
{
    [Fact]
    public void DocumentNumberMask()
    {
        var g = new DocumentNumberGenerator();
        Assert.Equal("ПР23-ОКН-2026-00007", g.Format(2026, 7));
        Assert.Equal(8, g.NextIndex(2026, new[] { "ПР23-ОКН-2026-00007", "ПР23-ОКН-2025-00099", "мусор" }));
        Assert.Equal(1, g.NextIndex(2027, new[] { "ПР23-ОКН-2026-00007" }));
        Assert.True(new ValidationService().IsValidDocumentNumber("ПР23-ОКН-2026-00007"));
        Assert.False(new ValidationService().IsValidDocumentNumber("ПР23-ОКН-2026-7"));
    }

    [Fact]
    public void Sha256SignatureIsStableAndSensitive()
    {
        var s = new IntegrityService();
        var a = s.BuildCanonicalString("ПР23-ОКН-2026-00001", new DateTime(2026, 3, 1), "Иванов И.И.", 12000m, 3);
        Assert.Equal("ПР23-ОКН-2026-00001|2026-03-01|Иванов И.И.|12000.00|3", a);
        var h1 = s.ComputeSha256(a);
        Assert.Equal(64, h1.Length);
        Assert.Equal(h1, s.ComputeSha256(a));
        Assert.NotEqual(h1, s.ComputeSha256(s.BuildCanonicalString("ПР23-ОКН-2026-00001", new DateTime(2026, 3, 1), "Иванов И.И.", 12000.01m, 3)));
    }

    [Fact]
    public void PasswordHashIsSha256Hex()
    {
        var h = new PasswordHasher();
        Assert.Equal("5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8", h.Hash("password"));
        Assert.True(h.Verify("password", h.Hash("password")));
        Assert.False(h.Verify("Password", h.Hash("password")));
    }

    [Theory]
    [InlineData("+7 (900) 123-45-67", true)]
    [InlineData("89001234567", true)]
    [InlineData("8 900 123 45 67", true)]
    [InlineData("12345", false)]
    [InlineData("+7 (900) 123-45-6a", false)]
    public void PhoneRegex(string phone, bool ok) => Assert.Equal(ok, new ValidationService().IsValidPhone(phone));

    [Theory]
    [InlineData(400, 400, 1, true)]
    [InlineData(1500, 2200, 6, true)]
    [InlineData(399, 1000, 1, false)]
    [InlineData(1501, 1000, 1, false)]
    [InlineData(-5, 1000, 1, false)]
    public void UnitSizeLimits(int w, int h, int s, bool ok) => Assert.Equal(ok, new ValidationService().ValidateUnit(w, h, s).IsValid);

    [Fact]
    public void WarrantyThirtySixMonths()
    {
        var w = new WarrantyService();
        var o = new WindowOrder { CompletedAt = new DateTime(2023, 12, 1) };
        Assert.Equal(WarrantyState.Active, w.Evaluate(o, new DateTime(2025, 1, 1)).State);
        Assert.Equal(WarrantyState.ExpiringSoon, w.Evaluate(o, new DateTime(2026, 11, 13)).State);
        Assert.Equal(WarrantyState.Expired, w.Evaluate(o, new DateTime(2026, 12, 1)).State);
        Assert.Equal(WarrantyState.NotStarted, w.Evaluate(new WindowOrder(), DateTime.Now).State);
    }

    [Fact]
    public void ExportContainsHashFooter()
    {
        var calc = new OrderCalculationService();
        var integrity = new IntegrityService();
        var exp = new ExportService(integrity, new CuttingService(), calc);
        var o = new WindowOrder
        {
            OrderNum = "ПР23-ОКН-2026-00001", OrderDate = new DateTime(2026, 3, 1), TotalPrice = 9600m,
            Homeowner = new Homeowner { FullName = "Иванов И.И.", DeliveryAddress = "ул. Ленина, 1", FloorNumber = 2 },
        };
        o.OrderItems.Add(new WindowOrderUnit { WidthMm = 1000, HeightMm = 1200, SashesCount = 2, UnitCalculatedCost = 9600m });
        var hash = integrity.ComputeForOrder(o);
        foreach (var kind in Enum.GetValues<ExportDocumentKind>())
            foreach (var fmt in Enum.GetValues<ExportFormat>())
                Assert.Contains("SHA256: " + hash, exp.Render(o, kind, fmt));
    }
}
