using ServiceDesk.Application.Interfaces;
using ServiceDesk.Application.Services;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Enums;

namespace ServiceDesk.Tests;

public class TicketServiceTests
{
    [Fact]
    public async Task
    ApproveTicketAsync_approves_and_saves_ticket()
    {
        var ticket = new SupportTicket(
            "POS issue",
            "POS is not working",
            1,
            1,
            1,
            TicketPriority.High);

        var repository = new FakeTicketRepository(ticket);
        var service = new TicketService(repository);

        await service.ApproveTicketAsync(ticketId: 1);

        Assert.Equal(TicketStatus.Approved, ticket.Status);
        Assert.True(repository.WasSaved);
    }

    [Fact]
    public async Task
    ApproveTicketAsync_throws_when_ticket_is_not_found()
    {
        var repository = new FakeTicketRepository(ticket: null);
        var service = new TicketService(repository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ApproveTicketAsync(ticketId: 99));
    }

    private class FakeTicketRepository : ISupportTicketRepository
    {
        private readonly SupportTicket? _ticket;

        public bool WasSaved { get; private set; }

        public FakeTicketRepository(SupportTicket? ticket)
        {
            _ticket = ticket;
        }

        public Task<SupportTicket?> GetByIdAsync(int ticketId)
        {
            return Task.FromResult(_ticket);
        }

        public Task SaveAsync(SupportTicket ticket)
        {
            WasSaved = true;
            return Task.CompletedTask;
        }
    }
}

