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
    [InlineData("GET", "/api/tickets/1")]
    [InlineData("POST", "/api/tickets")]
    [InlineData("PUT", "/api/tickets/1")]
    [InlineData("GET", "/api/tickets/1/history")]
    [InlineData("GET", "/api/tickets/1/assignment")]
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
    [InlineData("GET", "/api/admin/stores")]
    [InlineData("POST", "/api/admin/stores")]
    [InlineData("PUT", "/api/admin/stores/1")]
    [InlineData("PATCH", "/api/admin/stores/1/status")]
    [InlineData("GET", "/api/admin/external-technicians")]
    [InlineData("POST", "/api/admin/external-technicians")]
    [InlineData("PUT", "/api/admin/external-technicians/1")]
    [InlineData("PATCH", "/api/admin/external-technicians/1/status")]
    public async Task Administration_requires_admin(string method, string path)
    {
        using var anonymous = new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { }) };
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(anonymous)).StatusCode);
        await SignIn("manager@example.test");
        using var manager = new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { }) };
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(manager)).StatusCode);
    }

    [Fact]
    public async Task Manager_can_progress_and_resolve_own_store_only_with_trusted_actor()
    {
        foreach (var ticket in _api.Data.Tickets) { ticket.Approve(); ticket.Assign(); }
        await SignIn("manager@example.test");
        foreach (var action in new[] { "progress", "resolve" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsJsonAsync($"/api/tickets/2/{action}", new { })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsJsonAsync($"/api/tickets/999/{action}", new { })).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsJsonAsync($"/api/tickets/1/{action}", new { changedByUserId = 999 })).StatusCode);
        }
        Assert.All(_api.Data.History, entry => Assert.Equal(21, entry.ChangedByUserId));
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsJsonAsync("/api/tickets/1/progress", new { })).StatusCode);
    }

    [Fact]
    public async Task Admin_can_read_ticket_details_from_any_store()
    {
        await SignIn();
        var ticket = await _client.GetFromJsonAsync<JsonElement>("/api/tickets/2");
        Assert.Equal(2, ticket.GetProperty("id").GetInt32());
        Assert.Equal(2, ticket.GetProperty("storeId").GetInt32());
        Assert.Equal("Broken printer", ticket.GetProperty("description").GetString());
        Assert.Equal(1, ticket.GetProperty("categoryId").GetInt32());
        Assert.Equal(11, ticket.GetProperty("createdByUserId").GetInt32());
        Assert.Equal(1, ticket.GetProperty("status").GetInt32());
        Assert.Equal(2, ticket.GetProperty("priority").GetInt32());
    }

    [Fact]
    public async Task Manager_can_read_only_own_store_ticket_details()
    {
        await SignIn("manager@example.test");
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/tickets/1")).StatusCode);
        var response = await _client.GetAsync("/api/tickets/2");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("Broken printer", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Missing_ticket_details_return_404()
    {
        await SignIn();
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/tickets/999")).StatusCode);
    }

    [Fact]
    public async Task Manager_edits_own_ticket_without_changing_identity_or_status()
    {
        await SignIn("manager@example.test");
        var response = await _client.PutAsJsonAsync("/api/tickets/1", new { title = " Updated ", description = " Updated description ", priority = 4, storeId = 2, createdByUserId = 999, status = 6 });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var ticket = _api.Data.Tickets.Single(ticket => ticket.Id == 1);
        Assert.Equal("Updated", ticket.Title);
        Assert.Equal(TicketPriority.Critical, ticket.Priority);
        Assert.Equal(1, ticket.StoreId);
        Assert.Equal(11, ticket.CreatedByUserId);
        Assert.Equal(TicketStatus.AwaitingApproval, ticket.Status);
        Assert.Empty(_api.Data.History);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsJsonAsync("/api/tickets/2", new { title = "Other", description = "Other", priority = 2 })).StatusCode);
    }

    [Fact]
    public async Task Admin_edits_any_store_and_invalid_or_missing_edits_fail()
    {
        await SignIn();
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PutAsJsonAsync("/api/tickets/2", new { title = "Fixed title", description = "Description", priority = 3 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PutAsJsonAsync("/api/tickets/2", new { title = " ", description = "Description", priority = 3 })).StatusCode);
        Assert.Equal("Fixed title", _api.Data.Tickets[1].Title);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsJsonAsync("/api/tickets/999", new { title = "Title", description = "Description", priority = 3 })).StatusCode);
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    [InlineData("assign")]
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
    public async Task Assignment_validates_options_and_normalizes_visit_time_to_utc()
    {
        await SignIn();
        await _client.PostAsJsonAsync("/api/tickets/1/approve", new { });
        var future = DateTimeOffset.UtcNow.AddDays(1).ToOffset(TimeSpan.FromHours(3));
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/tickets/1/assign", new { externalTechnicianId = 99, scheduledAt = future, notes = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/tickets/1/assign", new { externalTechnicianId = 1, scheduledAt = DateTimeOffset.UtcNow.AddDays(-1), notes = "" })).StatusCode);
        Assert.Equal(TicketStatus.Approved, _api.Data.Tickets[0].Status);
        Assert.Empty(_api.Data.Assignments);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsJsonAsync("/api/tickets/1/assign", new { externalTechnicianId = 1, scheduledAt = future, notes = "Bring parts" })).StatusCode);
        Assert.Equal(future.UtcDateTime, Assert.Single(_api.Data.Assignments).ScheduledAt);
    }

    [Fact]
    public async Task Assignment_read_is_store_scoped_and_returns_saved_schedule()
    {
        await SignIn();
        await _client.PostAsJsonAsync("/api/tickets/1/approve", new { });
        var visit = DateTimeOffset.UtcNow.AddDays(1);
        await _client.PostAsJsonAsync("/api/tickets/1/assign", new { externalTechnicianId = 1, scheduledAt = visit, notes = "Bring parts" });
        await SignIn("manager@example.test");
        var assignment = await _client.GetFromJsonAsync<JsonElement>("/api/tickets/1/assignment");
        Assert.Equal(1, assignment.GetProperty("externalTechnicianId").GetInt32());
        Assert.Equal("Bring parts", assignment.GetProperty("notes").GetString());
        Assert.Equal(11, assignment.GetProperty("assignedByUserId").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/tickets/2/assignment")).StatusCode);
        await SignIn();
        Assert.Equal(HttpStatusCode.NoContent, (await _client.GetAsync("/api/tickets/2/assignment")).StatusCode);
    }

    [Fact]
    public async Task Api_can_complete_full_workflow_with_admin_and_manager_audit_actors()
    {
        await SignIn();
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsJsonAsync("/api/tickets/1/approve", new { reason = "Valid request" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsJsonAsync("/api/tickets/1/assign", new { externalTechnicianId = 1, scheduledAt = DateTimeOffset.UtcNow.AddDays(1), notes = "Visit" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsJsonAsync("/api/tickets/1/progress", new { reason = "Work started" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsJsonAsync("/api/tickets/1/resolve", new { reason = "Repaired" })).StatusCode);
        await SignIn("manager@example.test");
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsJsonAsync("/api/tickets/1/close", new { reason = "Confirmed" })).StatusCode);
        Assert.Equal(TicketStatus.Closed, _api.Data.Tickets[0].Status);
        Assert.Equal(5, _api.Data.History.Count);
        Assert.All(_api.Data.History.Take(4), entry => Assert.Equal(11, entry.ChangedByUserId));
        Assert.Equal(21, _api.Data.History.Last().ChangedByUserId);
    }

    [Fact]
    public async Task Rejection_ends_workflow_and_records_reason()
    {
        await SignIn();
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsJsonAsync("/api/tickets/1/reject", new { reason = "Duplicate request" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsJsonAsync("/api/tickets/1/approve", new { })).StatusCode);
        Assert.Equal("Duplicate request", Assert.Single(_api.Data.History).Reason);
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
                services.RemoveAll<IWorkflowTransaction>();
                services.AddSingleton<IWorkflowTransaction, FakeWorkflowTransaction>();
                services.RemoveAll<IDisplayNameRepository>();
                services.AddSingleton<IDisplayNameRepository, TestDisplayNames>();
                services.AddSingleton<ISupportTicketRepository>(Data);
                services.AddSingleton<IReferenceDataRepository>(Data);
                services.AddSingleton<ITicketStatusHistoryRepository>(Data);
                services.AddSingleton<ITechnicianAssignmentRepository>(Data);
            });
        }
    }

    private sealed class TestDisplayNames : IDisplayNameRepository
    {
        public Task<DisplayNames> GetAsync(int[] stores, int[] categories, int[] users, int[] technicians) => Task.FromResult(new DisplayNames(
            stores.ToDictionary(id => id, id => $"Store {id}"), categories.ToDictionary(id => id, id => "Maintenance"),
            users.ToDictionary(id => id, id => $"User {id}"), technicians.ToDictionary(id => id, id => $"Technician {id}")));
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
        public Task AddAsync(TechnicianAssignment assignment) { SetId(assignment, Assignments.Count + 1); Assignments.Add(assignment); return Task.CompletedTask; }
        public Task<TechnicianAssignment?> GetLatestByTicketIdAsync(int ticketId) => Task.FromResult(Assignments.LastOrDefault(item => item.SupportTicketId == ticketId));
        public Task<List<TicketStatusHistory>> GetByTicketId(int id) => Task.FromResult(History.Where(item => item.SupportTicketId == id).ToList());
        public Task<List<Store>> GetStoreAsync()
        {
            var stores = new List<Store>();
            for (var id = 1; id <= 2; id++) { var store = new Store($"S{id}", $"Store {id}", "Address", "City"); SetId(store, id); stores.Add(store); }
            return Task.FromResult(stores);
        }
        public Task<List<TicketCategory>> GetCategoriesAsync() { var category = new TicketCategory(); SetId(category, 1); return Task.FromResult(new List<TicketCategory> { category }); }
        public Task<List<ExternalTechnician>> GetTechniciansAsync()
        {
            var technician = new ExternalTechnician("Test technician", "Test company", "000", "General maintenance");
            SetId(technician, 1);
            return Task.FromResult(new List<ExternalTechnician> { technician });
        }
    }
}
