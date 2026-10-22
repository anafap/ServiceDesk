using System;
using ServiceDesk.Domain.Entities;

namespace ServiceDesk.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id);
    Task<User?> GetByEmailAsync(string email);
}
