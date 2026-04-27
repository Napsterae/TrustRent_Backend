using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Identity.Contracts.Database;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Identity.Models;
using TrustRent.Shared.Security;

namespace TrustRent.Modules.Identity.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IdentityDbContext _context;
    public UserRepository(IdentityDbContext context) => _context = context;

    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _context.Users.SingleOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        if (!EmailHelper.TryNormalizeEmail(email, out var normalized))
            return null;
        return await _context.Users.SingleOrDefaultAsync(u => u.Email == normalized);
    }

    public async Task<bool> IsEmailUniqueAsync(string email, Guid excludeUserId)
    {
        if (!EmailHelper.TryNormalizeEmail(email, out var normalized))
            return false;
        return !await _context.Users.AnyAsync(u => u.Email == normalized && u.Id != excludeUserId);
    }

    public async Task AddAsync(User user)
    {
        await _context.Users.AddAsync(user);
    }

    public async Task<bool> IsNifUniqueAsync(string nif, Guid excludeUserId)
    {
        return !await _context.Users.AnyAsync(u => u.Nif == nif && u.Id != excludeUserId);
    }

    public async Task<bool> IsCcUniqueAsync(string cc, Guid excludeUserId)
    {
        return !await _context.Users.AnyAsync(u => u.CitizenCardNumber == cc && u.Id != excludeUserId);
    }
}

