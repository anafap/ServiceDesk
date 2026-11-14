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
        var api = app.MapGroup("/api").RequireAuthorization().AddEndpointFilter<VersionFilter>();
        var tickets = api.MapGroup("/tickets");

        tickets.MapGet("", async (int? page, int? pageSize, string? title, TicketStatus? status, TicketPriority? priority, bool? openOnly,
            ITicketQueryRepository queries, ClaimsPrincipal user, IDisplayNameRepository display) =>
        {
            var result = await queries.SearchAsync(JwtIdentity.StoreId(user), new(page ?? 1, pageSize ?? 25, title, status, priority, openOnly ?? false));
            var accessible = result.Items;
            var visits = await queries.GetVisitStatesAsync(accessible.Select(t => t.Id).ToArray());
            var names = await display.GetAsync(accessible.Select(item => item.StoreId).Distinct().ToArray(), accessible.Select(item => item.CategoryId).Distinct().ToArray(), accessible.Select(item => item.CreatedByUserId).Distinct().ToArray(), []);
            return Results.Ok(new { Items = accessible.Select(item => TicketResponse.From(item, names, visits.GetValueOrDefault(item.Id))), result.TotalCount, result.Page, result.PageSize });
        });
        api.MapGet("/dashboard", async (ITicketQueryRepository queries, ClaimsPrincipal user, IDisplayNameRepository display) =>
        {
            var result = await queries.DashboardAsync(JwtIdentity.StoreId(user));
            var visible = result.Recent.Concat(result.Attention).ToList();
            var visits = await queries.GetVisitStatesAsync(visible.Select(t => t.Id).Distinct().ToArray());
            var names = await display.GetAsync(visible.Select(t => t.StoreId).Distinct().ToArray(), visible.Select(t => t.CategoryId).Distinct().ToArray(), visible.Select(t => t.CreatedByUserId).Distinct().ToArray(), []);
            return Results.Ok(new { result.Summary, Recent = result.Recent.Select(t => TicketResponse.From(t, names, visits.GetValueOrDefault(t.Id))),
                Attention = result.Attention.Select(t => new { Ticket = TicketResponse.From(t, names, visits.GetValueOrDefault(t.Id)), Action = t.Status switch {
                    TicketStatus.AwaitingApproval => "Review request", TicketStatus.Approved => "Assign technician",
                    TicketStatus.Resolved => "Confirm closure", TicketStatus.Assigned => visits.GetValueOrDefault(t.Id) switch {
                        "Declined" => JwtIdentity.StoreId(user).HasValue ? "Waiting for Admin" : "Change visit time",
                        "Confirmed" => "Start repair", _ => "Confirm visit" }, _ => "Update progress" } }) });
        });

        tickets.MapGet("/{ticketId:int}", async (int ticketId, TicketService service, ClaimsPrincipal user, IDisplayNameRepository display, ITicketQueryRepository queries) =>
        {
            var ticket = await service.GetTicketByIdAsync(ticketId);
            if (ticket is null || !JwtIdentity.CanAccessStore(user, ticket.StoreId)) return Results.NotFound();
            var names = await display.GetAsync([ticket.StoreId], [ticket.CategoryId], [ticket.CreatedByUserId], []);
            var visits = await queries.GetVisitStatesAsync([ticket.Id]);
            return Results.Ok(TicketResponse.From(ticket, names, visits.GetValueOrDefault(ticket.Id)));
        });

        tickets.MapPut("/{ticketId:int}", async (int ticketId, EditTicketRequest request, TicketService service, ClaimsPrincipal user) =>
        {
            var ticket = await service.GetTicketByIdAsync(ticketId);
            if (ticket is null || !JwtIdentity.CanAccessStore(user, ticket.StoreId)) return Results.NotFound();
            return await RunTransition(() => service.UpdateTicketDetailsAsync(ticketId, request.Title, request.Description, request.Priority, request.ExpectedVersion));
        });

        tickets.MapGet("/{ticketId:int}/assignment", async (int ticketId, TicketService service, ClaimsPrincipal user, IDisplayNameRepository display) =>
        {
            var ticket = await service.GetTicketByIdAsync(ticketId);
            if (ticket is null || !JwtIdentity.CanAccessStore(user, ticket.StoreId)) return Results.NotFound();
            var assignment = await service.GetLatestAssignmentAsync(ticketId);
            if (assignment is null) return Results.NoContent();
            var names = await display.GetAsync([], [], new[] { assignment.AssignedByUserId, assignment.ConfirmedByUserId ?? assignment.AssignedByUserId }.Distinct().ToArray(), [assignment.ExternalTechnicianId]);
            return Results.Ok(new
            {
                assignment.Id,
                assignment.SupportTicketId,
                assignment.ExternalTechnicianId,
                assignment.AssignedByUserId,
                assignment.AssignedAt,
                assignment.ScheduledAt,
                assignment.Notes,
                assignment.WithdrawnAt,
                assignment.Version,
                assignment.VisitState,
                assignment.ContactName,
                assignment.ContactPhone,
                assignment.ConfirmedByUserId,
                assignment.ConfirmedAt,
                assignment.DeclineReason,
                assignment.RepairStartedAt,
                TechnicianName = names.Technicians.GetValueOrDefault(assignment.ExternalTechnicianId),
                ConfirmedByName = assignment.ConfirmedByUserId is int confirmer ? names.Users.GetValueOrDefault(confirmer) : null,
                AssignedByName = names.Users.GetValueOrDefault(assignment.AssignedByUserId)
            });
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
            RunTransition(() => service.ApproveTicketAsync(ticketId, JwtIdentity.UserId(user), request.Reason, request.ExpectedVersion))).RequireAuthorization("Admin");
        tickets.MapPost("/{ticketId:int}/reject", (int ticketId, TransitionRequest request, TicketService service, ClaimsPrincipal user) =>
            RunTransition(() => service.RejectTicketAsync(ticketId, JwtIdentity.UserId(user), request.Reason, request.ExpectedVersion))).RequireAuthorization("Admin");
        tickets.MapPost("/{ticketId:int}/assign", async (int ticketId, AssignTicketRequest request, TicketService service, ReferenceService references, ClaimsPrincipal user) =>
        {
            if (request.ScheduledAt <= DateTimeOffset.UtcNow)
                return Results.BadRequest(new { message = "Choose a future visit time." });
            if (!(await references.GetAllExternalAsync()).Any(technician => technician.Id == request.ExternalTechnicianId))
                return Results.BadRequest(new { message = "Select an active external technician." });
            return await RunTransition(() => service.AssignTicketAsync(ticketId, request.ExternalTechnicianId, JwtIdentity.UserId(user),
                request.ScheduledAt.UtcDateTime, request.Notes, JwtIdentity.UserId(user), request.Reason, request.ExpectedVersion));
        }).RequireAuthorization("Admin");
        foreach (var visitAction in new[] { "confirm", "decline", "reschedule" })
        {
            var action = visitAction;
            var endpoint = tickets.MapPost("/{ticketId:int}/visit/" + action, async (
                int ticketId, VisitRequest request, TicketService service, ClaimsPrincipal user) =>
            {
                var ticket = await service.GetTicketByIdAsync(ticketId);
                if (ticket is null || !JwtIdentity.CanAccessStore(user, ticket.StoreId)) return Results.NotFound();
                return await RunTransition(() => service.ChangeVisitAsync(ticketId, JwtIdentity.UserId(user), action,
                    request.ExpectedVersion!, request.ExpectedAssignmentVersion!, request.ContactName, request.ContactPhone,
                    request.Reason, request.ScheduledAt?.UtcDateTime));
            });
            if (action == "reschedule") endpoint.RequireAuthorization("Admin");
            else endpoint.RequireAuthorization(policy => policy.RequireRole("StoreManager"));
        }
        tickets.MapPost("/{ticketId:int}/progress", async (int ticketId, StartRepairRequest request, TicketService service, ClaimsPrincipal user) =>
        {
            var ticket = await service.GetTicketByIdAsync(ticketId);
            if (ticket is null || !JwtIdentity.CanAccessStore(user, ticket.StoreId)) return Results.NotFound();
            return await RunTransition(() => service.StartProgressTicketAsync(ticketId, JwtIdentity.UserId(user), request.Reason, request.ExpectedVersion, request.ExpectedAssignmentVersion));
        });
        tickets.MapPost("/{ticketId:int}/resolve", async (int ticketId, TransitionRequest request, TicketService service, ClaimsPrincipal user) =>
        {
            var ticket = await service.GetTicketByIdAsync(ticketId);
            if (ticket is null || !JwtIdentity.CanAccessStore(user, ticket.StoreId)) return Results.NotFound();
            return await RunTransition(() => service.ResolveTicketAsync(ticketId, JwtIdentity.UserId(user), request.Reason, request.ExpectedVersion));
        });
        tickets.MapPost("/{ticketId:int}/close", async (int ticketId, TransitionRequest request, TicketService service, ClaimsPrincipal user) =>
        {
            var ticket = await service.GetTicketByIdAsync(ticketId);
            if (ticket is null || !JwtIdentity.CanAccessStore(user, ticket.StoreId)) return Results.NotFound();
            return await RunTransition(() => service.CloseTicketAsync(ticketId, JwtIdentity.UserId(user), request.Reason, request.ExpectedVersion));
        }).RequireAuthorization("Admin");
        tickets.MapPost("/{ticketId:int}/correct-status", (int ticketId, CorrectionRequest request, TicketService service, ClaimsPrincipal user) =>
            RunTransition(() => service.CorrectStatusAsync(ticketId, request.EventId, JwtIdentity.UserId(user), request.Reason, request.ExpectedVersion!))).RequireAuthorization("Admin");
        tickets.MapGet("/{ticketId:int}/history", async (int ticketId, TicketService service, ClaimsPrincipal user, IDisplayNameRepository display) =>
        {
            var ticket = await service.GetTicketByIdAsync(ticketId);
            if (ticket is null || !JwtIdentity.CanAccessStore(user, ticket.StoreId)) return Results.NotFound();
            var history = await service.GetHistoryByTicketIdAsync(ticketId);
            var names = await display.GetAsync([], [], history.Select(item => item.ChangedByUserId).Distinct().ToArray(), []);
            return Results.Ok(history.Select(item => new
            {
                item.Id,
                item.SupportTicketId,
                item.PreviousStatus,
                item.NewStatus,
                item.ChangedByUserId,
                item.ChangedAt,
                item.Reason,
                item.ReversesHistoryId,
                item.TechnicianAssignmentId,
                ChangedByName = names.Users.GetValueOrDefault(item.ChangedByUserId)
            }));
        });

        api.MapGet("/stores", async (ReferenceService references, ClaimsPrincipal user) =>
            Results.Ok((await references.GetAllStoresAsync()).Where(store => JwtIdentity.CanAccessStore(user, store.Id))));
        api.MapGet("/categories", async (ReferenceService references) => Results.Ok(await references.GetAllCategoriesAsync()));
        api.MapGet("/external-technicians", async (ReferenceService references) => Results.Ok(await references.GetAllExternalAsync()))
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> RunTransition(Func<Task> action)
    {
        await action();
        return Results.NoContent();
    }
}

public record CreateTicketRequest(string Title, string Description, int CategoryId, int StoreId, TicketPriority Priority);
public record EditTicketRequest(string Title, string Description, TicketPriority Priority, string? ExpectedVersion = null) : IVersionedRequest;
public record VisitRequest(string? ExpectedAssignmentVersion, string? ContactName = null, string? ContactPhone = null,
    string? Reason = null, DateTimeOffset? ScheduledAt = null, string? ExpectedVersion = null) : IVersionedRequest;
public record StartRepairRequest(string? Reason, string? ExpectedVersion = null, string? ExpectedAssignmentVersion = null) : IVersionedRequest;
public record TransitionRequest(string? Reason, string? ExpectedVersion = null) : IVersionedRequest;
public record AssignTicketRequest(int ExternalTechnicianId, string Notes, DateTimeOffset ScheduledAt, string? Reason, string? ExpectedVersion = null) : IVersionedRequest;
public record CorrectionRequest(int EventId, string Reason, string? ExpectedVersion = null) : IVersionedRequest;

public record TicketResponse(int Id, string Title, string Description, TicketStatus Status,
    TicketPriority Priority, int StoreId, int CategoryId, int CreatedByUserId, int? AssetId, DateTime CreatedAt,
    string? StoreName, string? CategoryName, string? CreatedByName, string Version, string? VisitState)
{
    public static TicketResponse From(SupportTicket ticket, DisplayNames names, string? visitState = null) => new(ticket.Id, ticket.Title, ticket.Description,
        ticket.Status, ticket.Priority, ticket.StoreId, ticket.CategoryId, ticket.CreatedByUserId, ticket.AssetId, ticket.CreatedAt,
        names.Stores.GetValueOrDefault(ticket.StoreId), names.Categories.GetValueOrDefault(ticket.CategoryId), names.Users.GetValueOrDefault(ticket.CreatedByUserId), ticket.Version, ticket.Status == TicketStatus.Assigned ? visitState : null);
}
