using System.Security.Claims;
using ServiceDesk.Api.Security;
using ServiceDesk.Application.Interfaces;
using ServiceDesk.Application.Services;
namespace ServiceDesk.Api.Endpoints;
public static class DiscussionEndpoints
{
    public static void MapDiscussionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tickets/{ticketId:int}/comments").RequireAuthorization();
        group.MapGet("", async (int ticketId, int? beforeId, TicketService tickets, DiscussionService comments,
            ClaimsPrincipal user, IDisplayNameRepository display) =>
        {
            var ticket = await tickets.GetTicketByIdAsync(ticketId);
            if (ticket is null || !JwtIdentity.CanAccessStore(user, ticket.StoreId)) return Results.NotFound();
            var page = await comments.GetAsync(ticketId, beforeId);
            var entries = page.Take(25).ToArray();
            var names = await display.GetAsync([], [], entries.Select(c => c.AuthorId).Distinct().ToArray(), []);
            return Results.Ok(new { Items = entries.Select(c => new { c.Id, c.SupportTicketId, c.AuthorId,
                AuthorName = names.Users.GetValueOrDefault(c.AuthorId), c.Text, c.CreatedAt }),
                NextBeforeId = page.Count > 25 ? (int?)entries.Last().Id : null });
        });
        group.MapPost("", async (int ticketId, CommentInput input, TicketService tickets, DiscussionService comments, ClaimsPrincipal user) =>
        {
            var ticket = await tickets.GetTicketByIdAsync(ticketId);
            if (ticket is null || !JwtIdentity.CanAccessStore(user, ticket.StoreId)) return Results.NotFound();
            var comment = await comments.AddAsync(ticketId, JwtIdentity.UserId(user), input.Text);
            return Results.Created($"/api/tickets/{ticketId}/comments", new { comment.Id });
        });
    }
}
public record CommentInput(string Text);
