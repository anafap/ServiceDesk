using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Enums;

namespace ServiceDesk.Tests;

public class SupportTicketTests
{
    [Fact]
    public void Editing_details_preserves_lifecycle_and_ownership()
    {
        var ticket = new SupportTicket("Old title", "Old description", 1, 2, 3, TicketPriority.Low);
        ticket.Approve();
        var createdAt = ticket.CreatedAt;
        ticket.UpdateDetails(" Updated title ", " Updated description ", TicketPriority.Critical);
        Assert.Equal("Updated title", ticket.Title);
        Assert.Equal("Updated description", ticket.Description);
        Assert.Equal(TicketPriority.Critical, ticket.Priority);
        Assert.Equal(TicketStatus.Approved, ticket.Status);
        Assert.Equal(1, ticket.CreatedByUserId);
        Assert.Equal(2, ticket.CategoryId);
        Assert.Equal(3, ticket.StoreId);
        Assert.Equal(createdAt, ticket.CreatedAt);
    }

    [Theory]
    [InlineData("", "Description", TicketPriority.Low)]
    [InlineData("Title", " ", TicketPriority.Low)]
    [InlineData("Title", "Description", (TicketPriority)99)]
    public void Invalid_edit_does_not_change_ticket(string title, string description, TicketPriority priority)
    {
        var ticket = new SupportTicket("Original", "Original description", 1, 1, 1, TicketPriority.Low);
        Assert.Throws<ArgumentException>(() => ticket.UpdateDetails(title, description, priority));
        Assert.Equal("Original", ticket.Title);
        Assert.Equal(TicketPriority.Low, ticket.Priority);
    }

    [Fact]
    public void Editing_enforces_database_text_limits()
    {
        var ticket = new SupportTicket("Title", "Description", 1, 1, 1, TicketPriority.Low);
        Assert.Throws<ArgumentException>(() => ticket.UpdateDetails(new string('x', 201), "Description", TicketPriority.Low));
        Assert.Throws<ArgumentException>(() => ticket.UpdateDetails("Title", new string('x', 4001), TicketPriority.Low));
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

            TicketPriority.High));


    }
    [Fact]
    public void Ticket_with_invalid_createdbyuserid_throws_exception()
    {
        Assert.Throws<ArgumentException>(() =>
            new SupportTicket(
                "POS not working",
                "The POS terminal does not turn on.",
                0,
                1,
                1,

                TicketPriority.High));
    }
    [Fact]

    public void Ticket_with_invalid_categoryid_throws_exception()
    {
        Assert.Throws<ArgumentException>(() =>
            new SupportTicket(
                "POS not working",
                "The POS terminal does not turn on.",
                1,
                1,
                0,

                TicketPriority.High));
    }
    [Fact]
    public void Ticket_with_invalid_store_id_throws_exception()
    {
        Assert.Throws<ArgumentException>(() =>
            new SupportTicket(
                "POS not working",
                "The POS terminal does not turn on.",
                1,
                1,
                0,
                TicketPriority.High));
    }

    [Fact]
    public void Ticket_with_invalid_priority_throws_exception()
    {
        Assert.Throws<ArgumentException>(() =>
            new SupportTicket(
                "POS not working",
                "The POS terminal does not turn on.",
                1,
                1,
                1,
                (TicketPriority)99));
    }

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






}
