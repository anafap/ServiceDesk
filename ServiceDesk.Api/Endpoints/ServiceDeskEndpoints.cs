using System.Security.Claims;
using ServiceDesk.Api.Security;
using ServiceDesk.Application.Services;
using ServiceDesk.Domain.Enums;

namespace ServiceDesk.Api.Endpoints;

public static class ServiceDeskEndpoints
{
    public static void MapServiceDeskEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();
        var tickets = api.MapGroup("/tickets");

        tickets.MapGet("", async (TicketService service, ClaimsPrincipal user) =>
            Results.Ok(await service.GetAllTicketsAsync(JwtIdentity.StoreId(user))));

        tickets.MapGet("/{ticketId:int}", async (int ticketId, TicketService service, ClaimsPrincipal user) =>
        {
            var ticket = await service.GetTicketByIdAsync(ticketId);
            if (ticket is null || !JwtIdentity.CanAccessStore(user, ticket.StoreId)) return Results.NotFound();
            return Results.Ok(new TicketDetailsResponse(ticket.Id, ticket.Title, ticket.Description, ticket.Status,
                ticket.Priority, ticket.StoreId, ticket.CategoryId, ticket.CreatedByUserId, ticket.AssetId, ticket.CreatedAt));
        });

        tickets.MapPut("/{ticketId:int}", async (int ticketId, EditTicketRequest request, TicketService service, ClaimsPrincipal user) =>
        {
            var ticket = await service.GetTicketByIdAsync(ticketId);
            if (ticket is null || !JwtIdentity.CanAccessStore(user, ticket.StoreId)) return Results.NotFound();
            return await RunTransition(() => service.UpdateTicketDetailsAsync(ticketId, request.Title, request.Description, request.Priority));
        });

        tickets.MapPost("", async (CreateTicketRequest request, TicketService service, ReferenceService references, ClaimsPrincipal user) =>
        {
            if (!JwtIdentity.CanAccessStore(user, request.StoreId)) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 200
                || string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length > 4000
                || !Enum.IsDefined(request.Priority))
                return Results.BadRequest(new { message = "A valid title, description, and priority are required." });
            if (!(await references.GetAllStoresAsync()).Any(store => store.Id == request.StoreId)
                || !(await references.GetAllCategoriesAsync()).Any(category => category.Id == request.CategoryId))
                return Results.BadRequest(new { message = "Select an available store and category." });
            var ticket = await service.CreateTicketAsync(request.Title.Trim(), request.Description.Trim(),
                JwtIdentity.UserId(user), request.CategoryId, request.StoreId, request.Priority);
            return Results.Created($"/api/tickets/{ticket.Id}", ticket);
        });

        tickets.MapPost("/{ticketId:int}/approve", (int ticketId, TransitionRequest request, TicketService service, ClaimsPrincipal user) =>
            RunTransition(() => service.ApproveTicketAsync(ticketId, JwtIdentity.UserId(user), request.Reason))).RequireAuthorization("Admin");
        tickets.MapPost("/{ticketId:int}/reject", (int ticketId, TransitionRequest request, TicketService service, ClaimsPrincipal user) =>
            RunTransition(() => service.RejectTicketAsync(ticketId, JwtIdentity.UserId(user), request.Reason))).RequireAuthorization("Admin");
        tickets.MapPost("/{ticketId:int}/assign", (int ticketId, AssignTicketRequest request, TicketService service, ClaimsPrincipal user) =>
            RunTransition(() => service.AssignTicketAsync(ticketId, request.ExternalTechnicianId, JwtIdentity.UserId(user),
                request.ScheduledAt, request.Notes, JwtIdentity.UserId(user), request.Reason))).RequireAuthorization("Admin");
        tickets.MapPost("/{ticketId:int}/progress", (int ticketId, TransitionRequest request, TicketService service, ClaimsPrincipal user) =>
            RunTransition(() => service.StartProgressTicketAsync(ticketId, JwtIdentity.UserId(user), request.Reason))).RequireAuthorization("Admin");
        tickets.MapPost("/{ticketId:int}/resolve", (int ticketId, TransitionRequest request, TicketService service, ClaimsPrincipal user) =>
            RunTransition(() => service.ResolveTicketAsync(ticketId, JwtIdentity.UserId(user), request.Reason))).RequireAuthorization("Admin");
        tickets.MapPost("/{ticketId:int}/close", async (int ticketId, TransitionRequest request, TicketService service, ClaimsPrincipal user) =>
        {
            var ticket = await service.GetTicketByIdAsync(ticketId);
            if (ticket is null || !JwtIdentity.CanAccessStore(user, ticket.StoreId)) return Results.NotFound();
            return await RunTransition(() => service.CloseTicketAsync(ticketId, JwtIdentity.UserId(user), request.Reason));
        });
        tickets.MapGet("/{ticketId:int}/history", async (int ticketId, TicketService service, ClaimsPrincipal user) =>
        {
            var ticket = await service.GetTicketByIdAsync(ticketId);
            if (ticket is null || !JwtIdentity.CanAccessStore(user, ticket.StoreId)) return Results.NotFound();
            return Results.Ok(await service.GetHistoryByTicketIdAsync(ticketId));
        });

        api.MapGet("/stores", async (ReferenceService references, ClaimsPrincipal user) =>
            Results.Ok((await references.GetAllStoresAsync()).Where(store => JwtIdentity.CanAccessStore(user, store.Id))));
        api.MapGet("/categories", async (ReferenceService references) => Results.Ok(await references.GetAllCategoriesAsync()));
        api.MapGet("/external-technicians", async (ReferenceService references) => Results.Ok(await references.GetAllExternalAsync()))
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> RunTransition(Func<Task> action)
    {
        try { await action(); return Results.NoContent(); }
        catch (KeyNotFoundException exception) { return Results.NotFound(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Results.Conflict(new { message = exception.Message }); }
        catch (ArgumentException exception) { return Results.BadRequest(new { message = exception.Message }); }
    }
}

public record CreateTicketRequest(string Title, string Description, int CategoryId, int StoreId, TicketPriority Priority);
public record EditTicketRequest(string Title, string Description, TicketPriority Priority);
public record TicketDetailsResponse(int Id, string Title, string Description, TicketStatus Status,
    TicketPriority Priority, int StoreId, int CategoryId, int CreatedByUserId, int? AssetId, DateTime CreatedAt);
public record TransitionRequest(string? Reason);
public record AssignTicketRequest(int ExternalTechnicianId, string Notes, DateTime ScheduledAt, string? Reason);
