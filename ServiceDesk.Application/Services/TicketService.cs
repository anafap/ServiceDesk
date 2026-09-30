using ServiceDesk.Application.Interfaces;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Enums;

namespace ServiceDesk.Application.Services;

public class TicketService
{
    private readonly ISupportTicketRepository _ticketRepository;
    private readonly ITechnicianAssignmentRepository _assignmentRepository;

    public TicketService(ISupportTicketRepository ticketRepository,
        ITechnicianAssignmentRepository assignmentRepository)
    {
        _ticketRepository = ticketRepository;
        _assignmentRepository = assignmentRepository;

    }

    public async Task ApproveTicketAsync(int ticketId)
    {
        var ticket = await
        _ticketRepository.GetByIdAsync(ticketId);

        if (ticket is null)
            throw new KeyNotFoundException(
                $"Ticket with ID {ticketId} was not found.");

        ticket.Approve();

        await _ticketRepository.SaveAsync(ticket);
    }

    public async Task RejectTicketAsync(int ticketId)
    {
        var ticket = await
        _ticketRepository.GetByIdAsync(ticketId);

        if (ticket is null)
            throw new KeyNotFoundException(
                $"Ticket with ID {ticketId} was not found.");

        ticket.Reject();

        await _ticketRepository.SaveAsync(ticket);
    }
    public async Task AssignTicketAsync(int ticketId, int externaltechnicianId, int assignedByUserId, DateTime scheduledAt, string notes)
    {
        var ticket = await
        _ticketRepository.GetByIdAsync(ticketId);

        if (ticket is null)
            throw new KeyNotFoundException(
                $"Ticket with ID {ticketId} was not found.");

        ticket.Assign();
        var assignment = new TechnicianAssignment(ticketId, externaltechnicianId, assignedByUserId, scheduledAt, notes);

        await _ticketRepository.SaveAsync(ticket);
        await _assignmentRepository.AddAsync(assignment);
    }
    public async Task<SupportTicket> CreateTicketAsync(string title, string description, int createdByUserId, int categoryId, int storeId,
        TicketPriority priority)
    {
        var ticket = new SupportTicket(title, description, createdByUserId, categoryId, storeId, priority);

        await _ticketRepository.AddAsync(ticket);
        return ticket;
    }

    public async Task StartProgressTicketAsync(int ticketId)
    {
        var ticket = await
        _ticketRepository.GetByIdAsync(ticketId);

        if (ticket is null)
            throw new KeyNotFoundException(
                $"Ticket with ID {ticketId} was not found.");

        ticket.StartProgress();

        await _ticketRepository.SaveAsync(ticket);
    }
    public async Task ResolveTicketAsync(int ticketId)
    {
        var ticket = await
        _ticketRepository.GetByIdAsync(ticketId);

        if (ticket is null)
            throw new KeyNotFoundException(
                $"Ticket with ID {ticketId} was not found.");

        ticket.Resolve();

        await _ticketRepository.SaveAsync(ticket);
    }
    public async Task CloseTicketAsync(int ticketId)
    {
        var ticket = await
        _ticketRepository.GetByIdAsync(ticketId);

        if (ticket is null)
            throw new KeyNotFoundException(
                $"Ticket with ID {ticketId} was not found.");

        ticket.Close();

        await _ticketRepository.SaveAsync(ticket);
    }

    public async Task<List<SupportTicket>> GetAllTicketsAsync()
    {
        return await _ticketRepository.GetAllAsync();
    }


}

