using Microsoft.AspNetCore.Identity;
using ServiceDesk.Application.Interfaces;
using ServiceDesk.Domain.Entities;

namespace ServiceDesk.Application.Services;

public class LoginService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher<User> _passwordHasher;
    public LoginService(IUserRepository users, IPasswordHasher<User> passwordHasher)
    {
        _users = users;
        _passwordHasher = passwordHasher;
    }
    public async Task<bool> UserLoginAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return false;
        var user = await _users.GetByEmailAsync(email);

        if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash))
            return false;

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);

        return result != PasswordVerificationResult.Failed;
    }
}
