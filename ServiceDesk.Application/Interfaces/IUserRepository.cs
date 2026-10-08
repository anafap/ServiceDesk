using System;
using ServiceDesk.Domain.Entities;

namespace ServiceDesk.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);
}
