using Microsoft.AspNetCore.Identity;

namespace SProtectPlatform.Api.Services;

public interface IPasswordService
{
    string HashPassword(string key, string password);
    bool VerifyPassword(string key, string hash, string password);
}

public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<string> _hasher = new();

    public string HashPassword(string key, string password) => _hasher.HashPassword(key, password);

    public bool VerifyPassword(string key, string hash, string password)
    {
        return _hasher.VerifyHashedPassword(key, hash, password) is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
