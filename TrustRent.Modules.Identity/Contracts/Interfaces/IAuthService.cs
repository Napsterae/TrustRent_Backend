namespace TrustRent.Modules.Identity.Contracts.Interfaces;

public interface IAuthService
{
    Task<string> SignInWithEmailAsync(string email);
}

