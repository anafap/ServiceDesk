using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using ServiceDesk.Api.Security;
using ServiceDesk.Application.Interfaces;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Enums;

namespace ServiceDesk.Tests;

public class SecurityApiTests : IDisposable
{
    private readonly TestApi _api = new();
    private readonly HttpClient _client;

    public SecurityApiTests() => _client = _api.CreateClient();
    public void Dispose() { _client.Dispose(); _api.Dispose(); }

    private async Task<LoginResponse> SignIn(string email = "admin@example.test")
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = TestApi.Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return session;
    }

    [Fact]
    public async Task Swagger_is_accessible_in_development_and_documents_bearer_auth()
    {
        using var api = new TestApi(development: true);
        using var client = api.CreateClient();
        var document = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        Assert.Equal("bearer", document.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
        var paths = document.GetProperty("paths");
        var login = paths.GetProperty("/api/auth/login").GetProperty("post");
        Assert.True(!login.TryGetProperty("security", out var security) || security.GetArrayLength() == 0);
        Assert.NotEqual(0, paths.GetProperty("/api/tickets").GetProperty("get").GetProperty("security").GetArrayLength());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tickets")).StatusCode);
    }

    [Fact]
    public async Task Login_issues_expiring_token_and_only_public_user_metadata()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.test", password = TestApi.Password });
        var json = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        var session = JsonSerializer.Deserialize<LoginResponse>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal("Bearer", session.TokenType);
        Assert.Equal(11, session.User.Id);
        Assert.Equal("Admin", session.User.Role);
        Assert.InRange(session.ExpiresAt, DateTime.UtcNow.AddMinutes(29), DateTime.UtcNow.AddMinutes(31));
        var token = new JwtSecurityTokenHandler().ReadJwtToken(session.AccessToken);
        Assert.Equal("11", token.Subject);
        Assert.Contains(token.Claims, claim => claim.Type == "role" && claim.Value == "Admin");
        _client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/tickets")).StatusCode);
    }

    [Theory]
    [InlineData("admin@example.test", "wrong")]
    [InlineData("missing@example.test", "wrong")]
    [InlineData("", "")]
    public async Task Invalid_credentials_return_401(string email, string password)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/api/auth/login", new { email, password })).StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/tickets")]
    [InlineData("POST", "/api/tickets")]
    [InlineData("GET", "/api/tickets/1/history")]
    [InlineData("GET", "/api/stores")]
    [InlineData("GET", "/api/categories")]
    [InlineData("GET", "/api/external-technicians")]
    [InlineData("POST", "/api/tickets/1/approve")]
    [InlineData("POST", "/api/tickets/1/reject")]
    [InlineData("POST", "/api/tickets/1/assign")]
    [InlineData("POST", "/api/tickets/1/progress")]
    [InlineData("POST", "/api/tickets/1/resolve")]
    [InlineData("POST", "/api/tickets/1/close")]
    public async Task Every_data_endpoint_requires_authentication(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { }) };
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(request)).StatusCode);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("signature")]
    [InlineData("missing-store")]
    [InlineData("invalid-sub")]
    [InlineData("unknown-role")]
    public async Task Invalid_tokens_return_401(string failure)
    {
        var now = DateTime.UtcNow;
        var claims = new[] { new Claim("sub", failure == "invalid-sub" ? "0" : "21"),
            new Claim("role", failure == "unknown-role" ? "Technician" : "StoreManager") };
        var token = new JwtSecurityToken(
            failure == "issuer" ? "wrong" : TestApi.Issuer,
            failure == "audience" ? "wrong" : TestApi.Audience,
            failure == "missing-store" ? claims : claims.Append(new Claim("store_id", "1")),
            now.AddMinutes(-5), failure == "expired" ? now.AddMinutes(-1) : now.AddMinutes(10),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(failure == "signature" ? new string('x', 64) : _api.Key)), SecurityAlgorithms.HmacSha256));
        _client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/tickets")).StatusCode);
    }

    [Fact]
    public async Task Manager_sees_only_own_store_tickets_and_stores()
    {
        var session = await SignIn("manager@example.test");
        Assert.Equal(1, session.User.StoreId);
        var tickets = await _client.GetFromJsonAsync<JsonElement[]>("/api/tickets");
        Assert.NotEmpty(tickets!);
        Assert.All(tickets!, ticket => Assert.Equal(1, ticket.GetProperty("storeId").GetInt32()));
        var stores = await _client.GetFromJsonAsync<JsonElement[]>("/api/stores");
        Assert.Single(stores!);
        Assert.Equal(1, stores![0].GetProperty("id").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/categories")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/tickets/2/history")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/tickets/1/history")).StatusCode);
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    [InlineData("assign")]
    [InlineData("progress")]
    [InlineData("resolve")]
    public async Task Manager_cannot_perform_admin_actions(string action)
    {
        await SignIn("manager@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsJsonAsync($"/api/tickets/1/{action}", new { reason = "test" })).StatusCode);
        Assert.Empty(_api.Data.History);
    }

    [Fact]
    public async Task Technician_contacts_are_admin_only()
    {
        await SignIn("manager@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/external-technicians")).StatusCode);
        await SignIn();
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/external-technicians")).StatusCode);
    }

    [Fact]
    public async Task Creation_uses_authenticated_actor_and_rejects_cross_store_access()
    {
        await SignIn("manager@example.test");
        var denied = await _client.PostAsJsonAsync("/api/tickets", new { title = "Issue", description = "Broken", storeId = 2, categoryId = 1, priority = 2, createdByUserId = 999 });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var response = await _client.PostAsJsonAsync("/api/tickets", new { title = "Issue", description = "Broken", storeId = 1, categoryId = 1, priority = 2, createdByUserId = 999 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(21, _api.Data.Tickets.Last().CreatedByUserId);
    }

    [Fact]
    public async Task Admin_sees_all_stores_and_records_actual_approval_actor()
    {
        await SignIn();
        Assert.Equal(2, (await _client.GetFromJsonAsync<JsonElement[]>("/api/stores"))!.Length);
        Assert.Equal(2, (await _client.GetFromJsonAsync<JsonElement[]>("/api/tickets"))!.Length);
        var response = await _client.PostAsJsonAsync("/api/tickets/1/approve", new { reason = "Approved", changedByUserId = 999 });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(11, Assert.Single(_api.Data.History).ChangedByUserId);
    }

    [Fact]
    public async Task Assignment_records_authenticated_admin_for_both_actor_fields()
    {
        await SignIn();
        await _client.PostAsJsonAsync("/api/tickets/1/approve", new { reason = "Approved" });
        var response = await _client.PostAsJsonAsync("/api/tickets/1/assign", new { externalTechnicianId = 1, notes = "Visit", scheduledAt = DateTime.UtcNow.AddDays(1), assignedByUserId = 999, changedByUserId = 999 });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(11, Assert.Single(_api.Data.Assignments).AssignedByUserId);
        Assert.All(_api.Data.History, item => Assert.Equal(11, item.ChangedByUserId));
    }

    [Fact]
    public async Task Manager_can_close_only_own_resolved_ticket()
    {
        foreach (var ticket in _api.Data.Tickets) { ticket.Approve(); ticket.Assign(); ticket.StartProgress(); ticket.Resolve(); }
        await SignIn("manager@example.test");
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsJsonAsync("/api/tickets/2/close", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsJsonAsync("/api/tickets/1/close", new { changedByUserId = 999 })).StatusCode);
        Assert.Equal(21, Assert.Single(_api.Data.History).ChangedByUserId);
    }

    [Fact]
    public async Task Missing_history_is_404_and_invalid_transition_is_409()
    {
        await SignIn();
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/tickets/999/history")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsJsonAsync("/api/tickets/1/resolve", new { })).StatusCode);
    }

    private sealed class TestApi(bool development = false) : WebApplicationFactory<Program>
    {
        public const string Password = "Test-only-Passw0rd!";
        public const string Issuer = "servicedesk-tests";
        public const string Audience = "servicedesk-test-client";
        public string Key { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        public TestData Data { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(development ? "Development" : "Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = Key, ["Jwt:Issuer"] = Issuer, ["Jwt:Audience"] = Audience,
                ["DemoUser:Email"] = "", ["DemoUser:Password"] = ""
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserRepository>();
                services.RemoveAll<ISupportTicketRepository>();
                services.RemoveAll<IReferenceDataRepository>();
                services.RemoveAll<ITicketStatusHistoryRepository>();
                services.RemoveAll<ITechnicianAssignmentRepository>();
                services.AddSingleton<IUserRepository>(Data);
                services.AddSingleton<ISupportTicketRepository>(Data);
                services.AddSingleton<IReferenceDataRepository>(Data);
                services.AddSingleton<ITicketStatusHistoryRepository>(Data);
                services.AddSingleton<ITechnicianAssignmentRepository>(Data);
            });
        }
    }

    private sealed class TestData : IUserRepository, ISupportTicketRepository, IReferenceDataRepository,
        ITicketStatusHistoryRepository, ITechnicianAssignmentRepository
    {
        public List<SupportTicket> Tickets { get; } = [MakeTicket(1, 1), MakeTicket(2, 2)];
        public List<TicketStatusHistory> History { get; } = [];
        public List<TechnicianAssignment> Assignments { get; } = [];
        private readonly User[] _users = [MakeUser(11, "admin@example.test", UserRole.Admin, null), MakeUser(21, "manager@example.test", UserRole.StoreManager, 1)];

        private static void SetId(object entity, int id) => entity.GetType().GetProperty("Id")!.SetValue(entity, id);
        private static SupportTicket MakeTicket(int id, int storeId)
        {
            var ticket = new SupportTicket("Issue", "Broken printer", 11, 1, storeId, TicketPriority.Medium);
            SetId(ticket, id); return ticket;
        }
        private static User MakeUser(int id, string email, UserRole role, int? storeId)
        {
            var user = new User("Test", role.ToString(), email, "000", role, storeId);
            SetId(user, id);
            user.SetPasswordHash(new PasswordHasher<User>().HashPassword(user, TestApi.Password));
            return user;
        }
        public Task<User?> GetByEmailAsync(string email) => Task.FromResult(_users.SingleOrDefault(user => user.Email == email));
        public Task<SupportTicket?> GetByIdAsync(int id) => Task.FromResult(Tickets.SingleOrDefault(ticket => ticket.Id == id));
        public Task<List<SupportTicket>> GetAllAsync(int? storeId = null) => Task.FromResult(Tickets.Where(ticket => !storeId.HasValue || ticket.StoreId == storeId).ToList());
        public Task SaveAsync(SupportTicket ticket) => Task.CompletedTask;
        public Task AddAsync(SupportTicket ticket) { SetId(ticket, Tickets.Count + 1); Tickets.Add(ticket); return Task.CompletedTask; }
        public Task AddAsync(TicketStatusHistory history) { History.Add(history); return Task.CompletedTask; }
        public Task AddAsync(TechnicianAssignment assignment) { Assignments.Add(assignment); return Task.CompletedTask; }
        public Task<List<TicketStatusHistory>> GetByTicketId(int id) => Task.FromResult(History.Where(item => item.SupportTicketId == id).ToList());
        public Task<List<Store>> GetStoreAsync()
        {
            var stores = new List<Store>();
            for (var id = 1; id <= 2; id++) { var store = new Store($"S{id}", $"Store {id}", "Address", "City"); SetId(store, id); stores.Add(store); }
            return Task.FromResult(stores);
        }
        public Task<List<TicketCategory>> GetCategoriesAsync() { var category = new TicketCategory(); SetId(category, 1); return Task.FromResult(new List<TicketCategory> { category }); }
        public Task<List<ExternalTechnician>> GetTechniciansAsync() => Task.FromResult(new List<ExternalTechnician>());
    }
}
