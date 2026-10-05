using ServiceDesk.Application.Interfaces;
using ServiceDesk.Application.Services;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Enums;

namespace ServiceDesk.Tests;

public class TicketServiceTests
{
    [Fact]
    public async Task ApproveTicketAsync_approves_and_saves_ticket()
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
        var historyRepository = new FakeTicketStatusHistoryRepository();
        var service = new TicketService(repository, assignmentRepository, historyRepository);

        await service.ApproveTicketAsync(ticketId: 1, changedByUserId: 1, reason: "");

        Assert.Equal(TicketStatus.Approved, ticket.Status);
        Assert.True(repository.WasSaved);
    }

    [Fact]
    public async Task ApproveTicketAsync_throws_when_ticket_is_not_found()
    {
        var repository = new FakeTicketRepository(ticket: null);
        var assignmentRepository = new FakeTechnicianAssignmentRepository();
        var historyRepository = new FakeTicketStatusHistoryRepository();

        var service = new TicketService(repository, assignmentRepository, historyRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ApproveTicketAsync(ticketId: 99, changedByUserId: 1, reason: ""));
    }

    [Fact]
    public async Task RejectTicketAsync_rejects_and_saves_ticket()
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
        var historyRepository = new FakeTicketStatusHistoryRepository();

        var service = new TicketService(repository, assignmentRepository, historyRepository);

        await service.RejectTicketAsync(ticketId: 1, changedByUserId: 1, reason: "");

        Assert.Equal(TicketStatus.Rejected, ticket.Status);
        Assert.True(repository.WasSaved);
    }

    [Fact]
    public async Task RejectTicketAsync_throws_when_ticket_is_not_found()
    {
        var repository = new FakeTicketRepository(ticket: null);
        var assignmentRepository = new FakeTechnicianAssignmentRepository();
        var historyRepository = new FakeTicketStatusHistoryRepository();

        var service = new TicketService(repository, assignmentRepository, historyRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.RejectTicketAsync(ticketId: 99, changedByUserId: 1, reason: ""));
    }
    [Fact]
    public async Task RejectTicketAsync_throws_when_ticket_already_approved()
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
        var historyRepository = new FakeTicketStatusHistoryRepository();

        var service = new TicketService(repository, assignmentRepository, historyRepository);


        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RejectTicketAsync(ticketId: 1, changedByUserId: 1, reason: ""));
    }

    [Fact]
    public async Task CreateTicketAsync_creates_and_saves_ticket_awaiting_approval()
    {
        var repository = new FakeTicketRepository(ticket: null);
        var assignmentRepository = new FakeTechnicianAssignmentRepository();
        var historyRepository = new FakeTicketStatusHistoryRepository();

        var service = new TicketService(repository, assignmentRepository, historyRepository);


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
        var historyRepository = new FakeTicketStatusHistoryRepository();

        var service = new TicketService(repository, assignmentRepository, historyRepository);

        await service.AssignTicketAsync(
            ticketId: 1,
            externaltechnicianId: 2,
            assignedByUserId: 3,
            scheduledAt: DateTime.UtcNow.AddDays(1),
            notes: "Bring replacement POS terminal",
            changedByUserId: 1,
            reason: ""
            );

        Assert.Equal(TicketStatus.Assigned, ticket.Status);
        Assert.True(repository.WasSaved);
        Assert.NotNull(assignmentRepository.AddedAssignment);
    }


    [Fact]
    public async Task AssignTicketAsync_throws_when_ticket_is_not_approved()
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
        var historyRepository = new FakeTicketStatusHistoryRepository();

        var service = new TicketService(repository, assignmentRepository, historyRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
        service.AssignTicketAsync(
            ticketId: 1,
            externaltechnicianId: 2,
            assignedByUserId: 3,
            scheduledAt: DateTime.UtcNow.AddDays(1),
            notes: "Bring replacement POS terminal",
            changedByUserId: 1,
            reason: ""
        ));
    }

    [Fact]
    public async Task AssignTicketAsync_throws_when_schedule_is_in_the_past()
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
        var historyRepository = new FakeTicketStatusHistoryRepository();

        var service = new TicketService(repository, assignmentRepository, historyRepository);

        await Assert.ThrowsAsync<ArgumentException>(() =>
        service.AssignTicketAsync(
            ticketId: 1,
            externaltechnicianId: 2,
            assignedByUserId: 3,
            scheduledAt: DateTime.UtcNow.AddDays(-1),
            notes: "Invalid appointments",
            changedByUserId: 1,
            reason: ""
        ));
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
        var historyRepository = new FakeTicketStatusHistoryRepository();

        var service = new TicketService(repository, assignmentRepository, historyRepository);

        await service.StartProgressTicketAsync(ticketId: 1, changedByUserId: 1, reason: "");

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
        var historyRepository = new FakeTicketStatusHistoryRepository();

        var service = new TicketService(repository, assignmentRepository, historyRepository);


        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartProgressTicketAsync(ticketId: 1, changedByUserId: 1, reason: ""));

    }
    [Fact]
    public async Task StartProgressTicketAsync_throws_when_ticket_not_found()
    {
        var repository = new FakeTicketRepository(ticket: null);
        var assignmentRepository = new FakeTechnicianAssignmentRepository();
        var historyRepository = new FakeTicketStatusHistoryRepository();

        var service = new TicketService(repository, assignmentRepository, historyRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.StartProgressTicketAsync(ticketId: 99, changedByUserId: 1, reason: ""));
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
        var historyRepository = new FakeTicketStatusHistoryRepository();
        var service = new TicketService(repository, assignmentRepository, historyRepository);

        // Act
        await service.ResolveTicketAsync(ticketId: 1, changedByUserId: 1, reason: "");

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
        var historyRepository = new FakeTicketStatusHistoryRepository();
        var service = new TicketService(repository, assignmentRepository, historyRepository);

        // Act and Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ResolveTicketAsync(ticketId: 1, changedByUserId: 1, reason: ""));
    }
    [Fact]
    public async Task CloseTicketAsync_throws_when_ticket_is_not_found()
    {
        // Arrange
        var repository = new FakeTicketRepository(ticket: null);
        var assignmentRepository = new FakeTechnicianAssignmentRepository();
        var historyRepository = new FakeTicketStatusHistoryRepository();
        var service = new TicketService(repository, assignmentRepository, historyRepository);
        // Act and Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CloseTicketAsync(ticketId: 99, changedByUserId: 1, reason: ""));
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
        var historyRepository = new FakeTicketStatusHistoryRepository();
        var service = new TicketService(repository, assignmentRepository, historyRepository);

        await service.CloseTicketAsync(ticketId: 1, changedByUserId: 1, reason: "");

        Assert.Equal(TicketStatus.Closed, ticket.Status);
        Assert.True(repository.WasSaved);
    }

    [Fact]
    public async Task CloseTicketAsync_throws_when_ticket_is_not_resolved()
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
        var historyRepository = new FakeTicketStatusHistoryRepository();
        var service = new TicketService(repository, assignmentRepository, historyRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
        service.CloseTicketAsync(ticketId: 1, changedByUserId: 1, reason: ""));


    }

    [Fact]
    public async Task TicketService_can_complete_full_workflow()
    {
        var repository = new FakeTicketRepository(ticket: null);
        var assignmentRepository = new FakeTechnicianAssignmentRepository();
        var historyRepository = new FakeTicketStatusHistoryRepository();
        var service = new TicketService(repository, assignmentRepository, historyRepository);

        var ticket = await service.CreateTicketAsync(
            "POS issue",
            "POS is not working",
            createdByUserId: 1,
            categoryId: 1,
            storeId: 1,
            TicketPriority.High);

        repository.SetTicket(ticket);

        await service.ApproveTicketAsync(ticketId: 1, changedByUserId: 1, reason: "");

        await service.AssignTicketAsync(
            ticketId: 1,
            externaltechnicianId: 2,
            assignedByUserId: 3,
            scheduledAt: DateTime.UtcNow.AddDays(1),
            notes: "Bring replacement terminal",
            changedByUserId: 1,
            reason: "");

        await service.StartProgressTicketAsync(ticketId: 1, changedByUserId: 1, reason: "");
        await service.ResolveTicketAsync(ticketId: 1, changedByUserId: 1, reason: "");
        await service.CloseTicketAsync(ticketId: 1, changedByUserId: 1, reason: "");

        Assert.Collection(historyRepository.Histories, history =>
        {
            Assert.Equal(TicketStatus.AwaitingApproval, history.PreviousStatus);
            Assert.Equal(TicketStatus.Approved, history.NewStatus);
        },
        history =>
        {
            Assert.Equal(TicketStatus.Approved, history.PreviousStatus);
            Assert.Equal(TicketStatus.Assigned, history.NewStatus);
        },
         history =>
        {
            Assert.Equal(TicketStatus.Assigned, history.PreviousStatus);
            Assert.Equal(TicketStatus.InProgress, history.NewStatus);
        },
        history =>
        {
            Assert.Equal(TicketStatus.InProgress, history.PreviousStatus);
            Assert.Equal(TicketStatus.Resolved, history.NewStatus);
        },
        history =>
        {
            Assert.Equal(TicketStatus.Resolved, history.PreviousStatus);
            Assert.Equal(TicketStatus.Closed, history.NewStatus);
        });

        Assert.Equal(5, historyRepository.Histories.Count);
        Assert.Equal(TicketStatus.Closed, ticket.Status);
    }

    private class FakeTicketStatusHistoryRepository : ITicketStatusHistoryRepository
    {
        public List<TicketStatusHistory> Histories { get; } = [];
        public Task AddAsync(TicketStatusHistory history)
        {
            Histories.Add(history);
            return Task.CompletedTask;
        }
        public Task<List<TicketStatusHistory>> GetByTicketId(int ticketId)
        {


            return Task.FromResult(Histories);

        }

    }

    private class FakeTicketRepository : ISupportTicketRepository
    {
        private SupportTicket? _ticket;

        public bool WasSaved { get; private set; }
        public SupportTicket? AddedTicket { get; private set; }

        public FakeTicketRepository(SupportTicket? ticket)
        {
            _ticket = ticket;
        }

        public void SetTicket(SupportTicket? ticket)
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
        public Task<List<SupportTicket>> GetAllAsync()
        {
            var tickets = _ticket is not null
                          ? new List<SupportTicket> { _ticket }
                          : new List<SupportTicket>();
            return Task.FromResult(tickets);
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
