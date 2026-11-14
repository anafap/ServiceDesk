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


if (args.Length == 3 && args[0] == "--sanitize-photo")
{
    try { ServiceDesk.Infrastructure.Photos.PhotoSanitizer.Process(args[1], args[2]); }
    catch { Environment.ExitCode = 1; }
    return;
}
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
    options.AddPolicy("photos", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
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
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options => options.MultipartBodyLengthLimit = 6 * 1024 * 1024);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 6 * 1024 * 1024);
builder.Services.AddScoped<IAttachmentRepository, AttachmentRepository>();
builder.Services.AddScoped<AttachmentService>();
builder.Services.AddSingleton<IPhotoStorage>(_ => new ServiceDesk.Infrastructure.Photos.LocalPhotoStorage(
    builder.Configuration["Photos:Root"] ?? Path.Combine(builder.Environment.ContentRootPath, ".servicedesk-photos")));
builder.Services.AddSingleton<IPhotoScanner>(_ => new ServiceDesk.Infrastructure.Photos.ClamAvScanner(
    builder.Configuration["Photos:ScannerHost"] ?? "127.0.0.1", builder.Configuration.GetValue("Photos:ScannerPort", 3310)));
builder.Services.AddSingleton<ServiceDesk.Api.Photos.PhotoWorker>();
builder.Services.AddScoped<TicketService>();
builder.Services.AddScoped<ITicketQueryRepository, TicketQueryRepository>();
builder.Services.AddScoped<IDiscussionRepository, DiscussionRepository>();
builder.Services.AddScoped<DiscussionService>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<INotificationPublisher, NotificationRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
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

builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownProxies.Clear(); options.KnownIPNetworks.Clear();
    foreach (var proxy in builder.Configuration.GetSection("Hosting:TrustedProxies").Get<string[]>() ?? [])
        options.KnownProxies.Add(System.Net.IPAddress.Parse(proxy));
});
var app = builder.Build();
if (args.Contains("--provision-admin"))
{
    await ServiceDesk.Api.Development.AdminProvisioner.RunAsync(app.Services, app.Configuration);
    Console.WriteLine("Admin provisioning completed. Existing credentials were not reset.");
    return;
}
if (args.Contains("--provision-demo"))
{
    if (!app.Configuration.GetValue("Hosting:DemoInstance", false)) throw new InvalidOperationException("Explicit demo provisioning requires Hosting:DemoInstance=true on an isolated demo database.");
    await ServiceDesk.Api.Development.DemoDataSeeder.SeedAsync(app.Services, app.Configuration);
    return;
}
if (args.Contains("--photo-worker"))
{
    using var stopping = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stopping.Cancel(); };
    System.Runtime.InteropServices.PosixSignalRegistration? signal = null;
    if (!OperatingSystem.IsWindows()) signal = System.Runtime.InteropServices.PosixSignalRegistration.Create(
        System.Runtime.InteropServices.PosixSignal.SIGTERM, context => { context.Cancel = true; stopping.Cancel(); });
    try { await app.Services.GetRequiredService<ServiceDesk.Api.Photos.PhotoWorker>().RunAsync(stopping.Token); }
    catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
    finally { signal?.Dispose(); }
    return;
}
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
        if (password.Length is < 8 or > 128) throw new InvalidOperationException("DemoUser password must contain 8 to 128 characters.");
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
if ((app.Configuration.GetSection("Hosting:TrustedProxies").Get<string[]>() ?? []).Length > 0) app.UseForwardedHeaders();
if (app.Environment.IsProduction())
{
    app.UseHsts();
    if (app.Configuration.GetValue("Hosting:RequireHttps", true)) app.UseHttpsRedirection();
}
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseRateLimiter();
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
app.MapDiscussionEndpoints();
app.MapNotificationEndpoints();
app.MapAccountEndpoints();
app.MapAttachmentEndpoints();
app.MapAdministrationEndpoints();
app.MapManagerEndpoints();
app.MapGet("/health/live", () => Results.Ok(new { status = "alive" })).AllowAnonymous();
app.MapGet("/health/ready", async (ServiceDeskDbContext db) =>
{
    try
    {
        if (await db.Database.CanConnectAsync() && !(await db.Database.GetPendingMigrationsAsync()).Any())
            return Results.Ok(new { status = "ready" });
    }
    catch { }
    return Results.StatusCode(503);
}).AllowAnonymous();
app.MapFallback("/{**path}", async context =>
{
    if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/health") ||
        Path.HasExtension(context.Request.Path.Value)) { context.Response.StatusCode = 404; return; }
    var index = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "index.html");
    if (!File.Exists(index)) { context.Response.StatusCode = 404; return; }
    context.Response.ContentType = "text/html";
    context.Response.Headers.CacheControl = "no-cache";
    await context.Response.SendFileAsync(index);
}).AllowAnonymous();
app.Run();

public record LoginRequest(string Email, string Password);
public partial class Program { }
