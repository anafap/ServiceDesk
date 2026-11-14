using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using ServiceDesk.Api.Security;
using ServiceDesk.Application.Interfaces;
using ServiceDesk.Application.Services;
namespace ServiceDesk.Api.Endpoints;
public static class AttachmentEndpoints
{
    public static void MapAttachmentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tickets/{ticketId:int}/attachments").RequireAuthorization();
        group.MapGet("", async (int ticketId, ClaimsPrincipal user, TicketService tickets, AttachmentService photos, IConfiguration configuration) =>
        {
            var ticket = await tickets.GetTicketByIdAsync(ticketId);
            if (ticket is null || !JwtIdentity.CanAccessStore(user, ticket.StoreId)) return Results.NotFound();
            var items = await photos.ListAsync(ticketId);
            return Results.Ok(new { Items = items.Select(a => new { a.Id, a.SupportTicketId, a.AuthorId, a.State, a.CreatedAt }), UploadsEnabled = configuration.GetValue("Photos:Enabled", false) });
        });
        group.MapPost("", async (int ticketId, HttpRequest request, ClaimsPrincipal user, TicketService tickets, AttachmentService photos, IConfiguration configuration) =>
        {
            var ticket = await tickets.GetTicketByIdAsync(ticketId);
            if (ticket is null || !JwtIdentity.CanAccessStore(user, ticket.StoreId)) return Results.NotFound();
            if (ticket.Status is Domain.Enums.TicketStatus.Closed or Domain.Enums.TicketStatus.Rejected)
                return Results.Conflict(new { message = "Photos are read-only for closed or rejected tickets." });
            if (!configuration.GetValue("Photos:Enabled", false)) return Results.Problem(statusCode: 503, detail: "Photo uploads are not configured for this environment.");
            if (request.ContentLength > 6 * 1024 * 1024) return Results.StatusCode(413);
            if (!request.HasFormContentType) return Results.BadRequest(new { message = "Send one JPEG or PNG photo." });
            var form = await request.ReadFormAsync(request.HttpContext.RequestAborted);
            if (form.Files.Count != 1 || form.Files[0].Length > 5 * 1024 * 1024)
                return Results.BadRequest(new { message = "Send one photo no larger than 5 MiB." });
            await using var stream = form.Files[0].OpenReadStream();
            var item = await photos.UploadAsync(ticketId, JwtIdentity.UserId(user), stream, request.HttpContext.RequestAborted);
            return Results.Accepted($"/api/tickets/{ticketId}/attachments", new { item.Id, item.State });
        }).RequireRateLimiting("photos");
        group.MapGet("/{id:guid}/content", async (int ticketId, Guid id, ClaimsPrincipal user, TicketService tickets,
            IAttachmentRepository repository, IPhotoStorage storage, HttpResponse response) =>
        {
            var ticket = await tickets.GetTicketByIdAsync(ticketId);
            if (ticket is null || !JwtIdentity.CanAccessStore(user, ticket.StoreId)) return Results.NotFound();
            var item = await repository.GetAsync(id);
            if (item is null || item.SupportTicketId != ticketId || item.State != "ready" || item.ContentKey is null) return Results.NotFound();
            response.Headers.CacheControl = "private, no-store"; response.Headers.XContentTypeOptions = "nosniff";
            try { return Results.File(storage.OpenReady(item.ContentKey), "image/png"); }
            catch (FileNotFoundException) { return Results.NotFound(); }
        });
    }
}
