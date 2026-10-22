using System.Security.Claims;
using ServiceDesk.Api.Security;
using ServiceDesk.Application.Services;
using ServiceDesk.Application.Interfaces;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Enums;

namespace ServiceDesk.Api.Endpoints;

public static class ServiceDeskEndpoints
{
    public static void MapServiceDeskEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();
        var tickets = api.MapGroup("/tickets");

        tickets.MapGet("", async (TicketService service, ClaimsPrincipal user, IDisplayNameRepository display) =>
        {
            var accessible = await service.GetAllTicketsAsync(JwtIdentity.StoreId(user));
            var names = await display.GetAsync(accessible.Select(item => item.StoreId).Distinct().ToArray(), accessible.Select(item => item.CategoryId).Distinct().ToArray(), accessible.Select(item => item.CreatedByUserId).Distinct().ToArray(), []);
            return Results.Ok(accessible.Select(item => TicketResponse.From(item, names)));
        });

        tickets.MapGet("/{ticketId:int}", async (int ticketId, TicketService service, ClaimsPrincipal user, IDisplayNameRepository display) =>
        {
            var ticket = await service.GetTicketByIdAsync(ticketId);
            if (ticket is null || !JwtIdentity.CanAccessStore(user, ticket.StoreId)) return Results.NotFound();
            var names = await display.GetAsync([ticket.StoreId], [ticket.CategoryId], [ticket.CreatedByUserId], []);
            return Results.Ok(TicketResponse.From(ticket, names));
        });

        tickets.MapPut("/{ticketId:int}", async (int ticketId, EditTicketRequest request, TicketService service, ClaimsPrincipal user) =>
        {
            var ticket = await service.GetTicketByIdAsync(ticketId);
            if (ticket is null || !JwtIdentity.CanAccessStore(user, ticket.StoreId)) return Results.NotFound();
            return await RunTransition(() => service.UpdateTicketDetailsAsync(ticketId, request.Title, request.Description, request.Priority));
        });

        tickets.MapGet("/{ticketId:int}/assignment", async (int ticketId, TicketService service, ClaimsPrincipal user, IDisplayNameRepository display) =>
        {
            var ticket = await service.GetTicketByIdAsync(ticketId);
            if (ticket is null || !JwtIdentity.CanAccessStore(user, ticket.StoreId)) return Results.NotFound();
            var assignment = await service.GetLatestAssignmentAsync(ticketId);
            if (assignment is null) return Results.NoContent();
            var names = await display.GetAsync([], [], [assignment.AssignedByUserId], [assignment.ExternalTechnicianId]);
            return Results.Ok(new { assignment.Id, assignment.SupportTicketId, assignment.ExternalTechnicianId, assignment.AssignedByUserId,
                assignment.AssignedAt, assignment.ScheduledAt, assignment.Notes,
                TechnicianName = names.Technicians.GetValueOrDefault(assignment.ExternalTechnicianId), AssignedByName = names.Users.GetValueOrDefault(assignment.AssignedByUserId) });
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
        tickets.MapPost("/{ticketId:int}/assign", async (int ticketId, AssignTicketRequest request, TicketService service, ReferenceService references, ClaimsPrincipal user) =>
        {
            if (request.ScheduledAt <= DateTimeOffset.UtcNow)
                return Results.BadRequest(new { message = "Choose a future visit time." });
            if (!(await references.GetAllExternalAsync()).Any(technician => technician.Id == request.ExternalTechnicianId))
                return Results.BadRequest(new { message = "Select an active external technician." });
            return await RunTransition(() => service.AssignTicketAsync(ticketId, request.ExternalTechnicianId, JwtIdentity.UserId(user),
                request.ScheduledAt.UtcDateTime, request.Notes, JwtIdentity.UserId(user), request.Reason));
        }).RequireAuthorization("Admin");
        tickets.MapPost("/{ticketId:int}/progress", async (int ticketId, TransitionRequest request, TicketService service, ClaimsPrincipal user) =>
        {
            var ticket = await service.GetTicketByIdAsync(ticketId);
            if (ticket is null || !JwtIdentity.CanAccessStore(user, ticket.StoreId)) return Results.NotFound();
            return await RunTransition(() => service.StartProgressTicketAsync(ticketId, JwtIdentity.UserId(user), request.Reason));
        });
        tickets.MapPost("/{ticketId:int}/resolve", async (int ticketId, TransitionRequest request, TicketService service, ClaimsPrincipal user) =>
        {
            var ticket = await service.GetTicketByIdAsync(ticketId);
            if (ticket is null || !JwtIdentity.CanAccessStore(user, ticket.StoreId)) return Results.NotFound();
            return await RunTransition(() => service.ResolveTicketAsync(ticketId, JwtIdentity.UserId(user), request.Reason));
        });
        tickets.MapPost("/{ticketId:int}/close", async (int ticketId, TransitionRequest request, TicketService service, ClaimsPrincipal user) =>
        {
            var ticket = await service.GetTicketByIdAsync(ticketId);
            if (ticket is null || !JwtIdentity.CanAccessStore(user, ticket.StoreId)) return Results.NotFound();
            return await RunTransition(() => service.CloseTicketAsync(ticketId, JwtIdentity.UserId(user), request.Reason));
        });
        tickets.MapGet("/{ticketId:int}/history", async (int ticketId, TicketService service, ClaimsPrincipal user, IDisplayNameRepository display) =>
        {
            var ticket = await service.GetTicketByIdAsync(ticketId);
            if (ticket is null || !JwtIdentity.CanAccessStore(user, ticket.StoreId)) return Results.NotFound();
            var history = await service.GetHistoryByTicketIdAsync(ticketId);
            var names = await display.GetAsync([], [], history.Select(item => item.ChangedByUserId).Distinct().ToArray(), []);
            return Results.Ok(history.Select(item => new { item.Id, item.SupportTicketId, item.PreviousStatus, item.NewStatus,
                item.ChangedByUserId, item.ChangedAt, item.Reason, ChangedByName = names.Users.GetValueOrDefault(item.ChangedByUserId) }));
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
public record TransitionRequest(string? Reason);
public record AssignTicketRequest(int ExternalTechnicianId, string Notes, DateTimeOffset ScheduledAt, string? Reason);

public record TicketResponse(int Id, string Title, string Description, TicketStatus Status,
    TicketPriority Priority, int StoreId, int CategoryId, int CreatedByUserId, int? AssetId, DateTime CreatedAt,
    string? StoreName, string? CategoryName, string? CreatedByName)
{
    public static TicketResponse From(SupportTicket ticket, DisplayNames names) => new(ticket.Id, ticket.Title, ticket.Description,
        ticket.Status, ticket.Priority, ticket.StoreId, ticket.CategoryId, ticket.CreatedByUserId, ticket.AssetId, ticket.CreatedAt,
        names.Stores.GetValueOrDefault(ticket.StoreId), names.Categories.GetValueOrDefault(ticket.CategoryId), names.Users.GetValueOrDefault(ticket.CreatedByUserId));
}
