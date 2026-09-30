using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Enums;

namespace ServiceDesk.Tests;

public class SupportTicketTests
{
    [Fact]
    public void New_ticket_starts_awaiting_approval()
    {
        //Arrange
        var ticket = new SupportTicket(
            "POS not working",
            "The POS terminal does not turn on",
            1,
            1,
            1,
            1,
            TicketPriority.High
        );
        //Act
        var status = ticket.Status;
        //Assert
        Assert.Equal(TicketStatus.AwaitingApproval, status);

    }

    [Fact]
    public void Awaiting_approval_ticket_can_be_approved()
    {
        //Arrange
        var ticket = new SupportTicket(
            "POS not working",
            "The POS terminal does not turn on",
            1,
            1,
            1,
            1,
            TicketPriority.High
        );
        //Act
        ticket.Approve();
        //Assert
        Assert.Equal(TicketStatus.Approved, ticket.Status);

    }
    [Fact]
    public void Approved_ticket_cannot_be_approved_again()
    {
        // Arrange
        var ticket = new SupportTicket(
            "POS not working",
            "The POS terminal does not turn on.",
            1,
            1,
            1,
            1,
            TicketPriority.High);

        ticket.Approve();

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() =>
            ticket.Approve());
    }

    [Fact]
    public void Ticket_can_complete_full_lifecycle()
    {
        // Arrange
        var ticket = new SupportTicket(
            "POS not working",
            "The POS terminal does not turn on.",
            1,
            1,
            1,
            1,
            TicketPriority.High);

        // Act
        ticket.Approve();
        ticket.Assign();
        ticket.StartProgress();
        ticket.Resolve();
        ticket.Close();

        // Assert
        Assert.Equal(TicketStatus.Closed, ticket.Status);
    }

    [Fact]
    public void Ticket_cannot_be_assigned_before_approval()
    {
        // Arrange
        var ticket = new SupportTicket(
            "POS not working",
            "The POS terminal does not turn on.",
            1,
            1,
            1,
            1,
            TicketPriority.High);

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() =>
            ticket.Assign());
    }
    [Fact]
    public void Rejected_ticket_cannot_be_assigned()
    {
        // Arrange
        var ticket = new SupportTicket(
            "POS not working",
            "The POS terminal does not turn on.",
            1,
            1,
            1,
            1,
            TicketPriority.High);

        ticket.Reject();

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() =>
            ticket.Assign());
    }
    [Fact]
    public void Approved_tickets_cannot_be_resolved_directly()
    {
        // Arrange
        var ticket = new SupportTicket(
            "POS not working",
            "The POS terminal does not turn on.",
            1,
            1,
            1,
            1,
            TicketPriority.High);
        ticket.Approve();

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() =>
            ticket.Resolve());
    }
    [Fact]
    public void Resolved_tickets_cannot_be_closed_twice()
    {
        // Arrange
        var ticket = new SupportTicket(
            "POS not working",
            "The POS terminal does not turn on.",
            1,
            1,
            1,
            1,
            TicketPriority.High);

        ticket.Approve();
        ticket.Assign();
        ticket.StartProgress();
        ticket.Resolve();
        ticket.Close();
        // Act and Assert
        Assert.Throws<InvalidOperationException>(() =>
            ticket.Close());
    }
    [Fact]
    public void Empty_title_throws_ArgumentException()
    {
        // Arrange Act and Assert
        Assert.Throws<ArgumentException>(() => new SupportTicket(
           "",
           "The POS terminal does not turn on.",
           1,
           1,
           1,
           1,
           TicketPriority.High));


    }

    [Fact]
    public void Empty_description_throws_ArgumentException()
    {
        // Assert
        Assert.Throws<ArgumentException>(() => new SupportTicket(
            "POS not working",
            "",
            1,
            1,
            1,
            1,
            TicketPriority.High));


    }





}
