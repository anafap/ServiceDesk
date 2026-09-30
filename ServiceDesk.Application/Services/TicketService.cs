using ServiceDesk.Application.Interfaces;

namespace ServiceDesk.Application.Services;

public class TicketService
{
    private readonly ISupportTicketRepository _ticketRepository;

    public TicketService(ISupportTicketRepository ticketRepository)
    {
        _ticketRepository = ticketRepository;
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
}
