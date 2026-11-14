using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Exceptions;

namespace ServiceDesk.Tests;

public class VisitTests
{
    private static TechnicianAssignment Visit() => new(1, 2, 3, DateTime.UtcNow.AddDays(1), "Visit");

    [Fact]
    public void Confirmation_requires_contact_and_rescheduling_clears_current_confirmation_but_keeps_audit()
    {
        var visit = Visit();
        Assert.Throws<StateConflictException>(() => visit.StartRepair(3));
        foreach (var (name, phone) in new[] { (" ", "123"), ("Contact", " "), (new string('x', 101), "123"), ("Contact", new string('x', 51)) })
            Assert.Throws<ArgumentException>(() => visit.ConfirmVisit(name, phone, 4));
        Assert.Empty(visit.VisitEvents);
        visit.ConfirmVisit(" Store contact ", " +973 12345678 ", 4);
        Assert.Equal("Store contact", visit.ContactName);
        Assert.Equal("+973 12345678", visit.ContactPhone);
        Assert.Equal(4, visit.ConfirmedByUserId);
        Assert.NotNull(visit.ConfirmedAt);
        visit.Reschedule(DateTime.UtcNow.AddDays(2), 3);
        Assert.Equal("AwaitingConfirmation", visit.VisitState);
        Assert.Null(visit.ContactName);
        Assert.Null(visit.ConfirmedAt);
        Assert.Throws<StateConflictException>(() => visit.StartRepair(3));
        Assert.Equal("Store contact", visit.VisitEvents.First().ContactName);
        visit.ConfirmVisit(new string('x', 100), new string('1', 50), 4);
        visit.StartRepair(3);
        Assert.NotNull(visit.RepairStartedAt);
        Assert.Throws<StateConflictException>(() => visit.Reschedule(DateTime.UtcNow.AddDays(3), 3));
        Assert.Throws<StateConflictException>(() => visit.StartRepair(3));
    }

    [Fact]
    public void Decline_requires_reason_and_new_time_before_confirmation()
    {
        var visit = Visit();
        Assert.Throws<ArgumentException>(() => visit.DeclineVisit(" ", 4));
        Assert.Throws<ArgumentException>(() => visit.DeclineVisit(new string('x', 2001), 4));
        visit.DeclineVisit(" Store is closed then ", 4);
        Assert.Equal("Store is closed then", visit.DeclineReason);
        Assert.Throws<StateConflictException>(() => visit.ConfirmVisit("Contact", "123", 4));
        Assert.Throws<StateConflictException>(() => visit.StartRepair(3));
        Assert.Throws<ArgumentException>(() => visit.Reschedule(DateTime.UtcNow.AddDays(-1), 3));
        visit.Reschedule(DateTime.UtcNow.AddDays(2), 3);
        Assert.Null(visit.DeclineReason);
        visit.ConfirmVisit("Contact", "123", 4);
        Assert.Equal(new[] { "Declined", "Rescheduled", "Confirmed" }, visit.VisitEvents.Select(e => e.Kind));
        Assert.Equal("Store is closed then", visit.VisitEvents.First().Reason);
    }

    [Fact]
    public void Withdraw_restore_and_repair_correction_require_fresh_confirmation()
    {
        var visit = Visit();
        visit.ConfirmVisit("Contact", "123", 4);
        visit.Withdraw(); visit.Restore();
        Assert.Throws<StateConflictException>(() => visit.StartRepair(3));
        visit.ConfirmVisit("Contact", "123", 4);
        visit.StartRepair(3);
        visit.ResetVisit(3);
        Assert.Null(visit.RepairStartedAt);
        Assert.Throws<StateConflictException>(() => visit.StartRepair(3));
    }
}
