using System;
using ServiceDesk.Domain.Entities;

namespace ServiceDesk.Application.Interfaces;

public interface ITicketStatusHistoryRepository
{
    Task AddAsync(TicketStatusHistory history);
    Task<List<TicketStatusHistory>> GetByTicketId(int ticketId);
}


