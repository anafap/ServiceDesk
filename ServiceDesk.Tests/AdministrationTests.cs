using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Services;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Infrastructure.Repositories;
namespace ServiceDesk.Tests;

public class AdministrationTests
{
    [Fact]
    public void Store_normalizes_code_trims_text_and_preserves_identity_on_edit()
    {
        var store = new Store(" demo ", " Store ", " Address ", " City ");
        Assert.Equal("DEMO", store.StoreCode);
        Assert.Equal("Store", store.Name);
        store.SetActive(false);
        store.UpdateDetails("edited", "Updated", "Address", "City");
        Assert.False(store.IsActive);
        Assert.Equal("EDITED", store.StoreCode);
        store.SetActive(true);
        Assert.True(store.IsActive);
        Assert.Throws<ArgumentException>(() => store.UpdateDetails("Code", new string('x', 51), "Address", "City"));
        Assert.Throws<ArgumentException>(() => new Store("Code", "Name", new string('x', 51), "City"));
    }
    [Fact]
    public void Technician_validates_limits_and_supports_reactivation()
    {
        var technician = new ExternalTechnician(" Demo ", " Company ", " 000 ", " General ");
        Assert.Equal("Demo", technician.FullName);
        technician.SetActive(false);
        technician.UpdateDetails("Updated", "Company", "123", "HVAC");
        Assert.False(technician.IsActive);
        technician.SetActive(true);
        Assert.True(technician.IsActive);
        Assert.Throws<ArgumentException>(() => technician.UpdateDetails("Name", "Company", new string('x', 21), "General"));
        Assert.Throws<ArgumentException>(() => technician.UpdateDetails(" ", "Company", "123", "General"));
    }
    [SqlFact]
    public async Task Administration_persists_edits_and_filters_inactive_records_without_losing_tickets()
    {
        await using var database = await WorkflowTransactionSqlTests.TestDatabase.CreateAsync();
        await using var context = database.OpenContext();
        var service = new AdministrationService(new AdministrationRepository(context));
        var store = await service.SaveStoreAsync(null, new(" new-code ", "New store", "Address", "City"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveStoreAsync(null, new("NEW-code", "Duplicate", "Address", "City")));
        await service.SaveStoreAsync(store.Id, new("new-code", "Edited store", "Address", "City"));
        var ticket = await database.AddTicketAsync(context, Domain.Enums.TicketStatus.Approved);
        await service.SetStoreActiveAsync(database.StoreId, false);
        var technician = await service.SaveTechnicianAsync(null, new("Demo contact", "Company", "000", "General"));
        await service.SaveTechnicianAsync(technician.Id, new("Edited contact", "Company", "123", "HVAC"));
        await service.SetTechnicianActiveAsync(technician.Id, false);
        var references = new ReferenceRepository(context);
        Assert.DoesNotContain(await references.GetStoreAsync(), item => item.Id == database.StoreId);
        Assert.DoesNotContain(await references.GetTechniciansAsync(), item => item.Id == technician.Id);
        Assert.Contains(await service.GetStoresAsync(), item => item.Id == database.StoreId && !item.IsActive);
        Assert.Contains(await service.GetTechniciansAsync(), item => item.Id == technician.Id && !item.IsActive);
        // An inactive store can still finish an existing ticket.
        var tickets = new TicketService(new SupportTicketRepository(context), new TechnicianAssignmentRepository(context), new TicketStatusHistoryRepository(context), new Infrastructure.Transactions.WorkflowTransaction(context));
        await tickets.AssignTicketAsync(ticket.Id, 1, 1, DateTime.UtcNow.AddDays(1), "Visit", 1, null);
        await tickets.StartProgressTicketAsync(ticket.Id, 1, null);
        await tickets.ResolveTicketAsync(ticket.Id, 1, null);
        await tickets.CloseTicketAsync(ticket.Id, 1, null);
        await service.SetStoreActiveAsync(database.StoreId, true);
        await service.SetTechnicianActiveAsync(technician.Id, true);
        await using var verification = database.OpenContext();
        Assert.Equal("Edited store", (await verification.Stores.SingleAsync(item => item.Id == store.Id)).Name);
        Assert.Equal("Edited contact", (await verification.ExternalTechnicians.SingleAsync(item => item.Id == technician.Id)).FullName);
        Assert.Equal(Domain.Enums.TicketStatus.Closed, (await verification.SupportTickets.SingleAsync()).Status);
        Assert.Equal(4, await verification.TicketStatusHistories.CountAsync());
    }
}
