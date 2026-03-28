using TrustRent.Modules.Identity.Models;

namespace TrustRent.Modules.Identity.Contracts.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task AddAsync(User user);
    Task<bool> IsNifUniqueAsync(string nif, Guid excludeUserId);
    Task<bool> IsCcUniqueAsync(string cc, Guid excludeUserId);
}

