using Microsoft.EntityFrameworkCore;
using WindowFactory.Core.Data;
using WindowFactory.Core.Models;

namespace WindowFactory.Core.Services;

// ---------- Авторизация ----------
public interface IAuthService
{
    Task<User?> LoginAsync(string login, string password);
}

public sealed class AuthService : IAuthService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IPasswordHasher _hasher;
    public AuthService(IDbContextFactory<AppDbContext> factory, IPasswordHasher hasher) { _factory = factory; _hasher = hasher; }

    public async Task<User?> LoginAsync(string login, string password)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var user = await db.Users.AsNoTracking().Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Login == login.Trim());
        return user is not null && _hasher.Verify(password, user.PasswordHash) ? user : null;
    }
}

// ---------- Справочники ----------
public interface ICatalogService
{
    Task<List<ProfileSystem>> GetProfilesAsync();
    Task<List<GlazingType>> GetGlazingTypesAsync();
    Task SaveProfileAsync(ProfileSystem p);
    Task SaveGlazingAsync(GlazingType g);
    Task DeleteProfileAsync(int id);
    Task DeleteGlazingAsync(int id);
}

public sealed class CatalogService : ICatalogService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IValidationService _v;
    public CatalogService(IDbContextFactory<AppDbContext> factory, IValidationService v) { _factory = factory; _v = v; }

    public async Task<List<ProfileSystem>> GetProfilesAsync()
    { await using var db = await _factory.CreateDbContextAsync(); return await db.ProfileSystems.AsNoTracking().OrderBy(x => x.BrandTitle).ToListAsync(); }

    public async Task<List<GlazingType>> GetGlazingTypesAsync()
    { await using var db = await _factory.CreateDbContextAsync(); return await db.GlazingTypes.AsNoTracking().OrderBy(x => x.GlazingName).ToListAsync(); }

    public async Task SaveProfileAsync(ProfileSystem p)
    {
        _v.ValidateProfile(p.BrandTitle, p.ChambersCount, p.FrameDepthMm, p.PricePerMeter).ThrowIfInvalid();
        await using var db = await _factory.CreateDbContextAsync();
        if (p.Id == 0) db.ProfileSystems.Add(p); else db.ProfileSystems.Update(p);
        await db.SaveChangesAsync();
    }

    public async Task SaveGlazingAsync(GlazingType g)
    {
        _v.ValidateGlazing(g.GlazingName, g.ThicknessMm, g.PricePerM2).ThrowIfInvalid();
        await using var db = await _factory.CreateDbContextAsync();
        if (g.Id == 0) db.GlazingTypes.Add(g); else db.GlazingTypes.Update(g);
        await db.SaveChangesAsync();
    }

    public async Task DeleteProfileAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        if (await db.WindowOrderUnits.AnyAsync(u => u.ProfileSystemId == id))
            throw new InvalidOperationException("Профильная система используется в заказах и не может быть удалена.");
        await db.ProfileSystems.Where(x => x.Id == id).ExecuteDeleteAsync();
    }

    public async Task DeleteGlazingAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        if (await db.WindowOrderUnits.AnyAsync(u => u.GlazingTypeId == id))
            throw new InvalidOperationException("Тип стеклопакета используется в заказах и не может быть удалён.");
        await db.GlazingTypes.Where(x => x.Id == id).ExecuteDeleteAsync();
    }
}

// ---------- Заказчики ----------
public interface IHomeownerService
{
    Task<List<Homeowner>> GetAllAsync();
    Task SaveAsync(Homeowner h);
    Task DeleteAsync(int id);
}

public sealed class HomeownerService : IHomeownerService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IValidationService _v;
    public HomeownerService(IDbContextFactory<AppDbContext> factory, IValidationService v) { _factory = factory; _v = v; }

    public async Task<List<Homeowner>> GetAllAsync()
    { await using var db = await _factory.CreateDbContextAsync(); return await db.Homeowners.AsNoTracking().OrderBy(x => x.FullName).ToListAsync(); }

    public async Task SaveAsync(Homeowner h)
    {
        _v.ValidateHomeowner(h.FullName, h.DeliveryAddress, h.FloorNumber, h.Phone).ThrowIfInvalid();
        await using var db = await _factory.CreateDbContextAsync();
        if (h.Id == 0) db.Homeowners.Add(h); else db.Homeowners.Update(h);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        if (await db.WindowOrders.AnyAsync(o => o.HomeownerId == id))
            throw new InvalidOperationException("У заказчика есть заказы, удаление невозможно.");
        await db.Homeowners.Where(x => x.Id == id).ExecuteDeleteAsync();
    }
}
