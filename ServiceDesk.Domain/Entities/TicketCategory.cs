namespace ServiceDesk.Domain.Entities;

public class TicketCategory
{
    public int Id { get; private set; }

    public string Name { get; private set; } = string.Empty;
}