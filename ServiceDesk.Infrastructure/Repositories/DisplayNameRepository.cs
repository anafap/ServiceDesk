using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Interfaces;
namespace ServiceDesk.Infrastructure.Repositories;

public sealed class DisplayNameRepository(ServiceDeskDbContext db) : IDisplayNameRepository
{
    public async Task<DisplayNames> GetAsync(int[] storeIds, int[] categoryIds, int[] userIds, int[] technicianIds)
    {
        // Only resolve IDs from already-authorized resources, including inactive records.
        var stores = storeIds.Length == 0 ? [] : await db.Stores.AsNoTracking().Where(item => storeIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.Name);
        var categories = categoryIds.Length == 0 ? [] : await db.TicketCategories.AsNoTracking().Where(item => categoryIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.Name);
        var users = userIds.Length == 0 ? [] : await db.Users.AsNoTracking().Where(item => userIds.Contains(item.Id)).Select(item => new { item.Id, Name = item.FirstName + " " + item.LastName }).ToDictionaryAsync(item => item.Id, item => item.Name);
        var technicians = technicianIds.Length == 0 ? [] : await db.ExternalTechnicians.AsNoTracking().Where(item => technicianIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.FullName);
        return new(stores, categories, users, technicians);
    }
}
