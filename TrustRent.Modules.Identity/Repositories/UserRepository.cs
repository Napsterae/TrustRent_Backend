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
        var blindIndex = EncryptionHelperV2.ComputeBlindIndex(EncryptionHelperV2.NormalizeEmail(normalized));
        return await _context.Users.SingleOrDefaultAsync(u => u.EmailBlindIndex == blindIndex);
    }

    public async Task<User?> GetByPhoneNumberAsync(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return null;

        var blindIndex = EncryptionHelperV2.ComputeBlindIndex(EncryptionHelperV2.NormalizePhone(phoneNumber));
        return await _context.Users.SingleOrDefaultAsync(u => u.PhoneNumberBlindIndex == blindIndex);
    }

    public async Task<bool> IsEmailUniqueAsync(string email, Guid excludeUserId)
    {
        if (!EmailHelper.TryNormalizeEmail(email, out var normalized))
            return false;
        var blindIndex = EncryptionHelperV2.ComputeBlindIndex(EncryptionHelperV2.NormalizeEmail(normalized));
        return !await _context.Users.AnyAsync(u => u.EmailBlindIndex == blindIndex && u.Id != excludeUserId);
    }

    public async Task<bool> IsPhoneNumberUniqueAsync(string phoneNumber, Guid excludeUserId)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return false;

        var blindIndex = EncryptionHelperV2.ComputeBlindIndex(EncryptionHelperV2.NormalizePhone(phoneNumber));
        return !await _context.Users.AnyAsync(
            u => u.PhoneNumberBlindIndex == blindIndex && u.Id != excludeUserId);
    }

    public async Task AddAsync(User user)
    {
        await _context.Users.AddAsync(user);
    }

    public async Task<bool> IsNifUniqueAsync(string nif, Guid excludeUserId)
    {
        var blindIndex = EncryptionHelperV2.ComputeBlindIndex(EncryptionHelperV2.NormalizeNif(nif));
        return !await _context.Users.AnyAsync(u => u.NifBlindIndex == blindIndex && u.Id != excludeUserId);
    }

    public async Task<bool> IsCcUniqueAsync(string cc, Guid excludeUserId)
    {
        var normalized = new string(cc.Where(char.IsDigit).ToArray());
        var blindIndex = EncryptionHelperV2.ComputeBlindIndex(normalized);
        return !await _context.Users.AnyAsync(u => u.CitizenCardNumberBlindIndex == blindIndex && u.Id != excludeUserId);
    }
}

