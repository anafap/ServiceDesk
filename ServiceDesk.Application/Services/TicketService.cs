using ServiceDesk.Application.Interfaces;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Enums;

namespace ServiceDesk.Application.Services;

public class TicketService
{
    private readonly ISupportTicketRepository _ticketRepository;
    private readonly ITicketStatusHistoryRepository _historyRepository;

    private readonly ITechnicianAssignmentRepository _assignmentRepository;

    public TicketService(ISupportTicketRepository ticketRepository,
        ITechnicianAssignmentRepository assignmentRepository,
        ITicketStatusHistoryRepository historyRepository)
    {
        _ticketRepository = ticketRepository;
        _assignmentRepository = assignmentRepository;
        _historyRepository = historyRepository;

    }

    public async Task ApproveTicketAsync(int ticketId, int changedByUserId, string? reason)
    {
        var ticket = await
        _ticketRepository.GetByIdAsync(ticketId);

        if (ticket is null)
            throw new KeyNotFoundException(
                $"Ticket with ID {ticketId} was not found.");

        var previousStatus = ticket.Status;

        ticket.Approve();
        var history = new TicketStatusHistory(ticketId, previousStatus, ticket.Status, changedByUserId, reason);

        await _ticketRepository.SaveAsync(ticket);
        await _historyRepository.AddAsync(history);

    }

    public async Task RejectTicketAsync(int ticketId, int changedByUserId, string? reason)
    {
        var ticket = await
        _ticketRepository.GetByIdAsync(ticketId);

        if (ticket is null)
            throw new KeyNotFoundException(
                $"Ticket with ID {ticketId} was not found.");

        var previousStatus = ticket.Status;
        ticket.Reject();
        var history = new TicketStatusHistory(ticketId, previousStatus, ticket.Status, changedByUserId, reason);


        await _ticketRepository.SaveAsync(ticket);
        await _historyRepository.AddAsync(history);

    }
    public async Task AssignTicketAsync(int ticketId, int externaltechnicianId, int assignedByUserId, DateTime scheduledAt, string notes, int changedByUserId, string? reason)
    {
        var ticket = await
        _ticketRepository.GetByIdAsync(ticketId);

        if (ticket is null)
            throw new KeyNotFoundException(
                $"Ticket with ID {ticketId} was not found.");
        var previousStatus = ticket.Status;
        ticket.Assign();
        var history = new TicketStatusHistory(ticketId, previousStatus, ticket.Status, changedByUserId, reason);
        var assignment = new TechnicianAssignment(ticketId, externaltechnicianId, assignedByUserId, scheduledAt, notes);

        await _ticketRepository.SaveAsync(ticket);
        await _assignmentRepository.AddAsync(assignment);
        await _historyRepository.AddAsync(history);
    }
    public async Task<SupportTicket> CreateTicketAsync(string title, string description, int createdByUserId, int categoryId, int storeId,
        TicketPriority priority)
    {
        var ticket = new SupportTicket(title, description, createdByUserId, categoryId, storeId, priority);

        await _ticketRepository.AddAsync(ticket);
        return ticket;
    }

    public async Task StartProgressTicketAsync(int ticketId, int changedByUserId, string? reason)
    {
        var ticket = await
        _ticketRepository.GetByIdAsync(ticketId);

        if (ticket is null)
            throw new KeyNotFoundException(
                $"Ticket with ID {ticketId} was not found.");

        var previousStatus = ticket.Status;
        ticket.StartProgress();
        var history = new TicketStatusHistory(ticketId, previousStatus, ticket.Status, changedByUserId, reason);

        await _ticketRepository.SaveAsync(ticket);
        await _historyRepository.AddAsync(history);
    }
    public async Task ResolveTicketAsync(int ticketId, int changedByUserId, string? reason)
    {
        var ticket = await
        _ticketRepository.GetByIdAsync(ticketId);

        if (ticket is null)
            throw new KeyNotFoundException(
                $"Ticket with ID {ticketId} was not found.");

        var previousStatus = ticket.Status;
        ticket.Resolve();
        var history = new TicketStatusHistory(ticketId, previousStatus, ticket.Status, changedByUserId, reason);
        await _ticketRepository.SaveAsync(ticket);
        await _historyRepository.AddAsync(history);
    }
    public async Task CloseTicketAsync(int ticketId, int changedByUserId, string? reason)
    {
        var ticket = await
        _ticketRepository.GetByIdAsync(ticketId);

        if (ticket is null)
            throw new KeyNotFoundException(
                $"Ticket with ID {ticketId} was not found.");

        var previousStatus = ticket.Status;
        ticket.Close();
        var history = new TicketStatusHistory(ticketId, previousStatus, ticket.Status, changedByUserId, reason);
        await _ticketRepository.SaveAsync(ticket);
        await _historyRepository.AddAsync(history);

    }

    public async Task<List<SupportTicket>> GetAllTicketsAsync()
    {
        return await _ticketRepository.GetAllAsync();
    }
    public async Task<List<TicketStatusHistory>> GetHistoryByTicketIdAsync(int ticketId)
    {
        return await _historyRepository.GetByTicketId(ticketId);
    }


}

