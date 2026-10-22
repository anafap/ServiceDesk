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

    public Task<User?> GetByIdAsync(int id) => _dbContext.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == id);

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _dbContext.Users.FirstOrDefaultAsync(user => user.NormalizedEmail == email.Trim().ToUpper());


    }
}
