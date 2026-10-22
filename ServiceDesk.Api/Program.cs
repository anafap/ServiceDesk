using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using ServiceDesk.Api.Errors;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Interfaces;
using ServiceDesk.Application.Services;
using ServiceDesk.Infrastructure;
using ServiceDesk.Infrastructure.Repositories;
using ServiceDesk.Infrastructure.Transactions;
using ServiceDesk.Domain.Enums;
using ServiceDesk.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ServiceDesk.Api.Security;
using ServiceDesk.Api.Endpoints;
using Microsoft.OpenApi;


var builder = WebApplication.CreateBuilder(args);
var browserTests = ServiceDesk.Api.Development.BrowserTestDatabase.Configure(builder, args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Clamp(builder.Configuration.GetValue("LoginRateLimit:PermitLimit", 20), 1, 10000),
            Window = TimeSpan.FromSeconds(Math.Clamp(builder.Configuration.GetValue("LoginRateLimit:WindowSeconds", 60), 1, 3600)),
            QueueLimit = 0, AutoReplenishment = true,
        }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry) ? Math.Max(1, (int)Math.Ceiling(retry.TotalSeconds)) : 60;
        context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await Results.Problem(statusCode: 429, title: "Too many sign-in attempts", detail: "Please wait before signing in again.",
            extensions: new Dictionary<string, object?> { ["traceId"] = context.HttpContext.TraceIdentifier }).ExecuteAsync(context.HttpContext);
    };
});
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Sign in through /api/auth/login, then enter the returned accessToken."
    });
    options.OperationFilter<BearerSecurityOperationFilter>();
});

builder.Services.AddScoped<ISupportTicketRepository, SupportTicketRepository>();
builder.Services.AddDbContext<ServiceDeskDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ITechnicianAssignmentRepository, TechnicianAssignmentRepository>();
builder.Services.AddScoped<ITicketStatusHistoryRepository, TicketStatusHistoryRepository>();
builder.Services.AddScoped<IReferenceDataRepository, ReferenceRepository>();

builder.Services.AddScoped<IWorkflowTransaction, WorkflowTransaction>();
builder.Services.AddScoped<IAdministrationRepository, AdministrationRepository>();
builder.Services.AddScoped<AdministrationService>();
builder.Services.AddScoped<IManagerRepository, ManagerRepository>();
builder.Services.AddScoped<ManagerService>();
builder.Services.AddScoped<IDisplayNameRepository, DisplayNameRepository>();
builder.Services.AddScoped<TicketService>();
builder.Services.AddScoped<ReferenceService>();

builder.Services.AddScoped<LoginService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services.AddOptions<JwtSettings>()
    .BindConfiguration("Jwt")
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.Key) && Encoding.UTF8.GetByteCount(settings.Key) >= 32,
        "Jwt:Key must contain at least 32 UTF-8 bytes.")
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.Issuer) && !string.IsNullOrWhiteSpace(settings.Audience),
        "Jwt:Issuer and Jwt:Audience are required.")
    .ValidateOnStart();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<Microsoft.Extensions.Options.IOptions<JwtSettings>>((options, configured) =>
    {
        var settings = configured.Value;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = settings.Issuer,
            ValidateAudience = true,
            ValidAudience = settings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var identity = context.Principal;
                if (identity is null || !JwtIdentity.HasValidClaims(identity)) { context.Fail("Invalid identity."); return; }
                var users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                var account = await users.GetByIdAsync(JwtIdentity.UserId(identity));
                if (account is null || !account.IsActive
                    || identity.FindFirst("session_version")?.Value != account.SessionVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    || !identity.IsInRole(account.Role.ToString())
                    || (account.Role == UserRole.StoreManager && JwtIdentity.StoreId(identity) != account.StoreId))
                    context.Fail("This session is no longer valid. Sign in again.");
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser().RequireRole(nameof(UserRole.Admin), nameof(UserRole.StoreManager)).Build();
    options.FallbackPolicy = options.DefaultPolicy;
    options.AddPolicy("Admin", policy => policy.RequireRole(nameof(UserRole.Admin)));
});

var app = builder.Build();
if (args.Contains("--cleanup-browser-tests"))
{
    await ServiceDesk.Api.Development.BrowserTestDatabase.DeleteAsync(app.Services);
    return;
}
if (browserTests)
{
    await ServiceDesk.Api.Development.BrowserTestDatabase.PrepareAsync(app.Services);
    await ServiceDesk.Api.Development.DemoDataSeeder.SeedAsync(app.Services, app.Configuration);
}


if (args.Contains("--seed-demo"))
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Demo provisioning is allowed only in Development.");
    await ServiceDesk.Api.Development.DemoDataSeeder.SeedAsync(app.Services, app.Configuration);
    Console.WriteLine("Demo provisioning completed. Existing credentials were not reset.");
    return;
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    var email = app.Configuration["DemoUser:Email"];
    var password = app.Configuration["DemoUser:Password"];

    if (!browserTests && !string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
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

app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;
    await Results.Problem(statusCode: response.StatusCode, title: "Request rejected",
        extensions: new Dictionary<string, object?> { ["traceId"] = context.HttpContext.TraceIdentifier }).ExecuteAsync(context.HttpContext);
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/api/auth/login", async (LoginRequest request, LoginService login, JwtTokenService tokens, HttpResponse response, IDisplayNameRepository display) =>
{
    response.Headers.CacheControl = "no-store";
    response.Headers.Pragma = "no-cache";
    var user = await login.AuthenticateAsync(request.Email, request.Password);
    if (user is null) return Results.Unauthorized();
    var names = await display.GetAsync(user.StoreId.HasValue ? [user.StoreId.Value] : [], [], [], []);
    var session = tokens.Issue(user);
    return Results.Ok(session with { User = session.User with { StoreName = user.StoreId.HasValue ? names.Stores.GetValueOrDefault(user.StoreId.Value) : null } });
}).AllowAnonymous().RequireRateLimiting("login");

app.MapServiceDeskEndpoints();
app.MapAdministrationEndpoints();
app.MapManagerEndpoints();
app.Run();

public record LoginRequest(string Email, string Password);
public partial class Program { }
