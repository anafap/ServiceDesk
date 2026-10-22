using Microsoft.AspNetCore.Identity;
using ServiceDesk.Application.Interfaces;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Enums;
using ServiceDesk.Domain.Exceptions;
namespace ServiceDesk.Application.Services;

public record ManagerInput(string FirstName, string LastName, string Email, string PhoneNumber, int StoreId,
    string? Password = null, string? ExpectedVersion = null) : IVersionedRequest;

public sealed class ManagerService(IManagerRepository repository, IPasswordHasher<User> hasher)
{
    public Task<List<User>> GetAllAsync() => repository.GetManagersAsync();
    public async Task<User> SaveAsync(int? id, ManagerInput input)
    {
        var user = id.HasValue ? await GetAsync(id.Value) : new User(input.FirstName, input.LastName, input.Email, input.PhoneNumber, UserRole.StoreManager, input.StoreId);
        if (id.HasValue) user.EnsureVersion(input.ExpectedVersion!);
        if ((!id.HasValue || user.StoreId != input.StoreId) && !await repository.IsActiveStoreAsync(input.StoreId))
            throw new ArgumentException("Select an active store.");
        user.UpdateManager(input.FirstName, input.LastName, input.Email, input.PhoneNumber, input.StoreId);
        if (await repository.EmailExistsAsync(user.NormalizedEmail, id)) throw new StateConflictException("This email address is already in use.");
        if (!id.HasValue)
        {
            ValidatePassword(input.Password);
            user.SetPasswordHash(hasher.HashPassword(user, input.Password!));
            await repository.AddAsync(user);
        }
        await repository.SaveAsync();
        return user;
    }
    public async Task SetActiveAsync(int id, bool active, string version)
    {
        var user = await GetAsync(id); user.EnsureVersion(version); user.SetActive(active); await repository.SaveAsync();
    }
    public async Task ResetPasswordAsync(int id, string password, string version)
    {
        ValidatePassword(password);
        var user = await GetAsync(id); user.EnsureVersion(version);
        user.SetPasswordHash(hasher.HashPassword(user, password)); await repository.SaveAsync();
    }
    private async Task<User> GetAsync(int id) => await repository.GetManagerAsync(id) ?? throw new KeyNotFoundException("StoreManager not found.");
    private static void ValidatePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length is < 8 or > 128)
            throw new ArgumentException("Password must contain 8 to 128 characters.");
    }
}
