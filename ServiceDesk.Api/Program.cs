using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Interfaces;
using ServiceDesk.Application.Services;
using ServiceDesk.Infrastructure;
using ServiceDesk.Infrastructure.Repositories;
using ServiceDesk.Domain.Enums;
using ServiceDesk.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<ISupportTicketRepository, SupportTicketRepository>();
builder.Services.AddDbContext<ServiceDeskDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ITechnicianAssignmentRepository, TechnicianAssignmentRepository>();
builder.Services.AddScoped<ITicketStatusHistoryRepository, TicketStatusHistoryRepository>();
builder.Services.AddScoped<IReferenceDataRepository, ReferenceRepository>();

builder.Services.AddScoped<TicketService>();
builder.Services.AddScoped<ReferenceService>();

builder.Services.AddScoped<LoginService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();


var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    var email = app.Configuration["DemoUser:Email"];
    var password = app.Configuration["DemoUser:Password"];

    if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null)
        {
            user = new User("Demo", "Admin", email, "00000000", UserRole.Admin, storeId: null);
            db.Users.Add(user);
        }
        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            user.SetPasswordHash(hasher.HashPassword(user, password));
            await db.SaveChangesAsync();
        }
    }
}

//create
app.MapPost("/api/tickets", async (
    CreateTicketRequest request,
    TicketService ticketService) =>
{
    var ticket = await ticketService.CreateTicketAsync(
        request.Title,
        request.Description,
        request.CreatedByUserId,
        request.CategoryId,
        request.StoreId,
        request.Priority
    );
    return Results.Created($"/api/tickets/{ticket.Id}", ticket);

});

//read all
app.MapGet("/api/tickets", async (
    TicketService ticketService) =>
{
    var tickets = await ticketService.GetAllTicketsAsync();

    return Results.Ok(tickets);

});

//approve
app.MapPost("/api/tickets/{ticketId:int}/approve", async Task<IResult> (
    int ticketId, TicketService ticketService,
    ApproveTicketRequest request) =>
{
    try
    {
        await ticketService.ApproveTicketAsync(ticketId, request.ChangedByUserId, request.Reason);
        return Results.NoContent();
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new
        {
            message = exception.Message
        });
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new
        {
            message = exception.Message
        });
    }


});

//reject
app.MapPost("/api/tickets/{ticketId:int}/reject", async Task<IResult> (
    int ticketId, TicketService ticketService, ApproveTicketRequest request) =>
{
    try
    {
        await ticketService.RejectTicketAsync(ticketId, request.ChangedByUserId, request.Reason);
        return Results.NoContent();
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new
        {
            message = exception.Message
        });
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new
        {
            message = exception.Message
        });
    }
});

//assign
app.MapPost("/api/tickets/{ticketId:int}/assign", async Task<IResult> (
    int ticketId,
    AssignTicketRequest request,
    TicketService ticketService) =>
{
    try
    {
        await ticketService.AssignTicketAsync(
            ticketId,
            request.ExternalTechnicianId,
            request.AssignedByUserId,
            request.ScheduledAt,
            request.Notes,
            request.ChangedByUserId,
            request.Reason
        );
        return Results.NoContent();
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new
        {
            message = exception.Message
        });
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new
        {
            message = exception.Message
        });
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new
        {
            message = exception.Message
        });
    }

});

//progress
app.MapPost("/api/tickets/{ticketId:int}/progress", async (
    int ticketId,
    TicketService ticketService,
    ApproveTicketRequest request) =>
{
    try
    {
        await ticketService.StartProgressTicketAsync(ticketId, request.ChangedByUserId, request.Reason);
        return Results.NoContent();
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new
        {
            message = exception.Message
        });
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new
        {
            message = exception.Message
        });
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new
        {
            message = exception.Message
        });
    }

});

//resolve
app.MapPost("/api/tickets/{ticketId:int}/resolve", async (
    int ticketId,
    TicketService ticketService,
    ApproveTicketRequest request) =>
{
    try
    {
        await ticketService.ResolveTicketAsync(ticketId, request.ChangedByUserId, request.Reason);
        return Results.NoContent();
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new
        {
            message = exception.Message
        });
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new
        {
            message = exception.Message
        });
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new
        {
            message = exception.Message
        });
    }

});

//close
app.MapPost("/api/tickets/{ticketId:int}/close", async (
    int ticketId,
    TicketService ticketService, ApproveTicketRequest request) =>
{
    try
    {
        await ticketService.CloseTicketAsync(ticketId, request.ChangedByUserId, request.Reason);
        return Results.NoContent();
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new
        {
            message = exception.Message
        });
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new
        {
            message = exception.Message
        });
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new
        {
            message = exception.Message
        });
    }

});

//get history
app.MapGet("/api/tickets/{ticketId:int}/history", async (
    int ticketId, TicketService ticketService) =>
{
    var histories = await ticketService.GetHistoryByTicketIdAsync(ticketId);
    return Results.Ok(histories);
}
);

app.MapGet("/api/stores", async (
    ReferenceService reference) =>
    {
        var stores = await reference.GetAllStoresAsync();
        return Results.Ok(stores);
    }

);
app.MapGet("/api/categories", async (
    ReferenceService reference) =>
    {
        var categories = await reference.GetAllCategoriesAsync();
        return Results.Ok(categories);
    }

);
app.MapGet("/api/external-technicians", async (
    ReferenceService reference) =>
    {
        var externaltechnicians = await reference.GetAllExternalAsync();
        return Results.Ok(externaltechnicians);
    }

);

app.MapPost("/api/auth/login", async (
    LoginRequest request,
    LoginService login) =>
    {
        var valid = await login.UserLoginAsync(request.Email, request.Password);
        return valid ? Results.Ok() : Results.Unauthorized();
    }

);


app.Run();

public record CreateTicketRequest(
    string Title,
    string Description,
    int CreatedByUserId,
    int CategoryId,
    int StoreId,
    TicketPriority Priority

);

public record AssignTicketRequest(
    int ExternalTechnicianId,
    int AssignedByUserId,
    string Notes,
    DateTime ScheduledAt,
    int ChangedByUserId,
    string? Reason

);

public record ApproveTicketRequest(
    int ChangedByUserId,
    string? Reason
);

public record LoginRequest(string Email, string Password);

