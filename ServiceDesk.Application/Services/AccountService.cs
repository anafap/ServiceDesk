using Microsoft.AspNetCore.Identity;
using ServiceDesk.Application.Interfaces;
using ServiceDesk.Domain.Entities;
namespace ServiceDesk.Application.Services;
public sealed class AccountService(IAccountRepository repository, IPasswordHasher<User> hasher)
{
    public async Task<User> GetAsync(int id)
    {
        var user = await repository.GetTrackedAsync(id) ?? throw new KeyNotFoundException("Account not found.");
        if (!user.IsActive) throw new KeyNotFoundException("Account not found.");
        return user;
    }
    public async Task<User> UpdateProfileAsync(int id, string firstName, string lastName, string phoneNumber, string expectedVersion)
    {
        var user = await GetAsync(id);
        user.EnsureVersion(expectedVersion);
        user.UpdateProfile(firstName, lastName, phoneNumber);
        await repository.SaveAsync();
        return user;
    }
    public async Task ChangePasswordAsync(int id, string currentPassword, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length is < 8 or > 128)
            throw new ArgumentException("New password must contain 8 to 128 characters.");
        var user = await repository.GetTrackedAsync(id) ?? throw new KeyNotFoundException("Account not found.");
        if (!user.IsActive || string.IsNullOrEmpty(currentPassword) || currentPassword.Length > 128 ||
            string.IsNullOrEmpty(user.PasswordHash) || hasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
            throw new ArgumentException("The current password could not be verified.");
        if (currentPassword == newPassword) throw new ArgumentException("Choose a different new password.");
        user.SetPasswordHash(hasher.HashPassword(user, newPassword));
        await repository.SaveAsync();
    }
}
