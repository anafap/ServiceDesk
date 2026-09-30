using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Interfaces;
using ServiceDesk.Application.Services;
using ServiceDesk.Infrastructure;
using ServiceDesk.Infrastructure.Repositories;
using ServiceDesk.Domain.Enums;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<ISupportTicketRepository, SupportTicketRepository>();
builder.Services.AddDbContext<ServiceDeskDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ITechnicianAssignmentRepository, TechnicianAssignmentRepository>();
builder.Services.AddScoped<TicketService>();

var app = builder.Build();

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


app.Run();

public record CreateTicketRequest(
    string Title,
    string Description,
    int CreatedByUserId,
    int CategoryId,
    int StoreId,
    TicketPriority Priority

);

