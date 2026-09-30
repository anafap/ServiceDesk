using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Interfaces;
using ServiceDesk.Application.Services;
using ServiceDesk.Infrastructure;
using ServiceDesk.Infrastructure.Repositories;
using ServiceDesk.Domain.Enums;
using System.IO.Pipelines;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<ISupportTicketRepository, SupportTicketRepository>();
builder.Services.AddDbContext<ServiceDeskDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ITechnicianAssignmentRepository, TechnicianAssignmentRepository>();
builder.Services.AddScoped<TicketService>();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

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


app.MapGet("/api/tickets", async (
    TicketService ticketService) =>
{
    var tickets = await ticketService.GetAllTicketsAsync();

    return Results.Ok(tickets);

});


app.MapPost("/api/tickets/{ticketId:int}/approve", async (
    int ticketId, TicketService ticketService) =>
{
    await ticketService.ApproveTicketAsync(ticketId);
    return Results.NoContent();

});

app.MapPost("/api/tickets/{ticketId:int}/reject", async Task<IResult> (
    int ticketId, TicketService ticketService) =>
{
    try
    {
        await ticketService.RejectTicketAsync(ticketId);
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

app.Run();

public record CreateTicketRequest(
    string Title,
    string Description,
    int CreatedByUserId,
    int CategoryId,
    int StoreId,
    TicketPriority Priority

);

