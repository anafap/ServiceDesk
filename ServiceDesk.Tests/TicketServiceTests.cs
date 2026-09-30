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
        var assignmentRepository = new FakeTechnicianAssignmentRepository();
        var service = new TicketService(repository, assignmentRepository);

        await service.ApproveTicketAsync(ticketId: 1);

        Assert.Equal(TicketStatus.Approved, ticket.Status);
        Assert.True(repository.WasSaved);
    }

    [Fact]
    public async Task
    ApproveTicketAsync_throws_when_ticket_is_not_found()
    {
        var repository = new FakeTicketRepository(ticket: null);
        var assignmentRepository = new FakeTechnicianAssignmentRepository();
        var service = new TicketService(repository, assignmentRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ApproveTicketAsync(ticketId: 99));
    }



    [Fact]
    public async Task
    RejectTicketAsync_rejects_and_saves_ticket()
    {
        var ticket = new SupportTicket(
            "POS issue",
            "POS is not working",
            1,
            1,
            1,
            TicketPriority.High);

        var repository = new FakeTicketRepository(ticket);
        var assignmentRepository = new FakeTechnicianAssignmentRepository();
        var service = new TicketService(repository, assignmentRepository);

        await service.RejectTicketAsync(ticketId: 1);

        Assert.Equal(TicketStatus.Rejected, ticket.Status);
        Assert.True(repository.WasSaved);
    }

    [Fact]
    public async Task
    RejectTicketAsync_throws_when_ticket_is_not_found()
    {
        var repository = new FakeTicketRepository(ticket: null);
        var assignmentRepository = new FakeTechnicianAssignmentRepository();
        var service = new TicketService(repository, assignmentRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.RejectTicketAsync(ticketId: 99));
    }
    [Fact]
    public async Task
    RejectTicketAsync_throws_when_ticket_already_approved()
    {
        var ticket = new SupportTicket(
            "POS issue",
            "POS is not working",
            1,
            1,
            1,
            TicketPriority.High);

        ticket.Approve(); // Simulate an approved ticket

        var repository = new FakeTicketRepository(ticket);
        var assignmentRepository = new FakeTechnicianAssignmentRepository();
        var service = new TicketService(repository, assignmentRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RejectTicketAsync(ticketId: 1));
    }

    [Fact]
    public async Task CreateTicketAsync_creates_and_saves_ticket_awaiting_approval()
    {
        var repository = new FakeTicketRepository(ticket: null);
        var assignmentRepository = new FakeTechnicianAssignmentRepository();
        var service = new TicketService(repository, assignmentRepository);

        var ticket = await service.CreateTicketAsync(
            "POS issue",
            "POS is not working",
            createdByUserId: 1,
            categoryId: 1,
            storeId: 1,
            TicketPriority.High);

        Assert.Equal(TicketStatus.AwaitingApproval, ticket.Status);
        Assert.Same(ticket, repository.AddedTicket);
    }

    [Fact]
    public async Task AssignTicketAsync_assigns_and_persists_assignment()
    {
        var ticket = new SupportTicket(
            "POS issue",
            "POS is not working",
            1,
            1,
            1,
            TicketPriority.High);
        ticket.Approve();

        var repository = new FakeTicketRepository(ticket);
        var assignmentRepository = new FakeTechnicianAssignmentRepository();
        var service = new TicketService(repository, assignmentRepository);

        await service.AssignTicketAsync(
            ticketId: 1,
            externaltechnicianId: 2,
            assignedByUserId: 3,
            scheduledAt: DateTime.UtcNow.AddDays(1),
            notes: "Bring replacement POS terminal");

        Assert.Equal(TicketStatus.Assigned, ticket.Status);
        Assert.True(repository.WasSaved);
        Assert.NotNull(assignmentRepository.AddedAssignment);
    }
    [Fact]
    public async Task StartProgressTicketAsync_moves_assigned_ticket_to_in_progress()
    {
        var ticket = new SupportTicket(
                 "POS issue",
                 "POS is not working",
                 createdByUserId: 1,
                 categoryId: 1,
                 storeId: 1,
                 TicketPriority.High);
        ticket.Approve(); // Simulate an approved ticket
        ticket.Assign(); // Simulate an assigned ticket 

        var repository = new FakeTicketRepository(ticket);
        var assignmentRepository = new FakeTechnicianAssignmentRepository();
        var service = new TicketService(repository, assignmentRepository);

        await service.StartProgressTicketAsync(ticketId: 1);

        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.True(repository.WasSaved);

    }

    [Fact]
    public async Task StartProgressTicketAsync_throws_when_ticket_not_assigned()
    {
        var ticket = new SupportTicket(
                 "POS issue",
                 "POS is not working",
                 createdByUserId: 1,
                 categoryId: 1,
                 storeId: 1,
                 TicketPriority.High);


        var repository = new FakeTicketRepository(ticket);
        var assignmentRepository = new FakeTechnicianAssignmentRepository();
        var service = new TicketService(repository, assignmentRepository);


        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartProgressTicketAsync(ticketId: 1));

    }
    [Fact]
    public async Task StartProgressTicketAsync_throws_when_ticket_not_found()
    {
        var repository = new FakeTicketRepository(ticket: null);
        var assignmentRepository = new FakeTechnicianAssignmentRepository();
        var service = new TicketService(repository, assignmentRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.StartProgressTicketAsync(ticketId: 99));
    }
    [Fact]
    public async Task ResolveTicketAsync_moves_in_progress_ticket_to_resolved()
    {
        // Arrange
        var ticket = new SupportTicket(
            "POS issue",
            "POS is not working",
            1,
            1,
            1,
            TicketPriority.High);

        ticket.Approve();
        ticket.Assign();
        ticket.StartProgress();

        var repository = new FakeTicketRepository(ticket);
        var assignmentRepository = new FakeTechnicianAssignmentRepository();
        var service = new TicketService(repository, assignmentRepository);

        // Act
        await service.ResolveTicketAsync(ticketId: 1);

        // Assert
        Assert.Equal(TicketStatus.Resolved, ticket.Status);
        Assert.True(repository.WasSaved);
    }


    [Fact]
    public async Task ResolveTicketAsync_throws_when_ticket_is_not_in_progress()
    {
        // Arrange
        var ticket = new SupportTicket(
            "POS issue",
            "POS is not working",
            1,
            1,
            1,
            TicketPriority.High);

        ticket.Approve();
        ticket.Assign();

        var repository = new FakeTicketRepository(ticket);
        var assignmentRepository = new FakeTechnicianAssignmentRepository();
        var service = new TicketService(repository, assignmentRepository);

        // Act and Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ResolveTicketAsync(ticketId: 1));
    }


    [Fact]
    public async Task CloseTicketAsync_throws_when_ticket_is_not_found()
    {
        // Arrange
        var repository = new FakeTicketRepository(ticket: null);
        var assignmentRepository = new FakeTechnicianAssignmentRepository();
        var service = new TicketService(repository, assignmentRepository);

        // Act and Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CloseTicketAsync(ticketId: 99));
    }

    [Fact]
    public async Task CloseTicketAsync_closes_resolved_ticket()
    {
        var ticket = new SupportTicket(
            "POS issue",
            "POS is not working",
            1,
            1,
            1,
            TicketPriority.High);
        ticket.Approve();
        ticket.Assign();
        ticket.StartProgress();
        ticket.Resolve();

        var repository = new FakeTicketRepository(ticket);
        var assignmentRepository = new FakeTechnicianAssignmentRepository();
        var service = new TicketService(repository, assignmentRepository);

        await service.CloseTicketAsync(ticketId: 1);

        Assert.Equal(TicketStatus.Closed, ticket.Status);
        Assert.True(repository.WasSaved);
    }

    private class FakeTicketRepository : ISupportTicketRepository
    {
        private readonly SupportTicket? _ticket;

        public bool WasSaved { get; private set; }
        public SupportTicket? AddedTicket { get; private set; }

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
        public Task AddAsync(SupportTicket ticket)
        {
            AddedTicket = ticket;
            return Task.CompletedTask;
        }

    }

    private class FakeTechnicianAssignmentRepository : ITechnicianAssignmentRepository
    {
        public TechnicianAssignment? AddedAssignment { get; private set; }

        public Task AddAsync(TechnicianAssignment assignment)
        {
            AddedAssignment = assignment;
            return Task.CompletedTask;
        }
    }

}
