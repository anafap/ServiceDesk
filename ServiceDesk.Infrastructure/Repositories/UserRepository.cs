using System;
using ServiceDesk.Application.Interfaces;
using ServiceDesk.Domain.Entities;
using Microsoft.EntityFrameworkCore;


namespace ServiceDesk.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ServiceDeskDbContext _dbContext;
    public UserRepository(ServiceDeskDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _dbContext.Users.FirstOrDefaultAsync(user => user.Email == email);


    }
}
