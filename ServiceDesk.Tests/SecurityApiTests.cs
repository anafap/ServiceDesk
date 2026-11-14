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
        var response = await PostAsync("/api/auth/login", new { email, password = TestApi.Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return session;
    }

    private Task<HttpResponseMessage> PostAsync(string path, object body) => MutateAsync(HttpMethod.Post, path, body);
    private Task<HttpResponseMessage> PutAsync(string path, object body) => MutateAsync(HttpMethod.Put, path, body);
    private Task<HttpResponseMessage> MutateAsync(HttpMethod method, string path, object body)
    {
        var json = JsonSerializer.SerializeToNode(body)!.AsObject();
        if (path.StartsWith("/api/tickets/") && !json.ContainsKey("expectedVersion")) json["expectedVersion"] = Convert.ToBase64String(new byte[8]);
        if ((path.EndsWith("/progress") || path.Contains("/visit/")) && !json.ContainsKey("expectedAssignmentVersion")) json["expectedAssignmentVersion"] = Convert.ToBase64String(new byte[8]);
        return _client.SendAsync(new HttpRequestMessage(method, path) { Content = JsonContent.Create(json) });
    }

    [Fact]
    public async Task Unexpected_failures_return_safe_problem_details_with_trace()
    {
        await SignIn();
        _api.Data.ThrowOnList = true;
        var response = await _client.GetAsync("/api/tickets");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("PRIVATE_TEST_SENTINEL", json);
        Assert.Contains("traceId", json);
    }

    [Fact]
    public async Task Version_is_required_and_stale_versions_have_problem_details()
    {
        await SignIn();
        var missing = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/tickets/1/approve") { Content = JsonContent.Create(new { reason = "No version" }) });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var stale = await PostAsync("/api/tickets/1/approve", new { expectedVersion = "AQEBAQEBAQE=" });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var problem = await stale.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.TryGetProperty("traceId", out _));
        Assert.Empty(_api.Data.History);
    }

    [Fact]
    public async Task Existing_tokens_fail_after_account_access_changes()
    {
        await SignIn("manager@example.test");
        var account = (await ((IUserRepository)_api.Data).GetByIdAsync(21))!;
        account.SetActive(false);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/tickets")).StatusCode);
        account.SetActive(true);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/tickets")).StatusCode);
        await SignIn("manager@example.test");
        account.UpdateManager("Test", "Manager", account.Email, "000", 2);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/tickets")).StatusCode);
    }

    [Fact]
    public async Task Manager_cannot_correct_status_and_admin_requires_a_reason()
    {
        await SignIn("manager@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync("/api/tickets/1/correct-status", new { eventId = 1, reason = "Correction" })).StatusCode);
        await SignIn();
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync("/api/tickets/1/correct-status", new { eventId = 1, reason = " " })).StatusCode);
        Assert.Empty(_api.Data.History);
    }

    [Fact]
    public async Task Login_rate_limit_returns_429_with_retry_after_and_trace()
    {
        using var api = new TestApi(loginLimit: 2);
        using var client = api.CreateClient();
        for (var index = 0; index < 2; index++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = "missing@example.test", password = "wrong" })).StatusCode);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "missing@example.test", password = "wrong" });
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("traceId", out _));
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
        var response = await PostAsync("/api/auth/login", new { email = "admin@example.test", password = TestApi.Password });
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
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostAsync("/api/auth/login", new { email, password })).StatusCode);
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
    [InlineData("POST", "/api/tickets/1/visit/confirm")]
    [InlineData("POST", "/api/tickets/1/visit/decline")]
    [InlineData("POST", "/api/tickets/1/visit/reschedule")]
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
    [InlineData("missing-session-version")]
    [InlineData("unknown-user")]
    public async Task Invalid_tokens_return_401(string failure)
    {
        var now = DateTime.UtcNow;
        var claims = new[] { new Claim("sub", failure == "invalid-sub" ? "0" : failure == "unknown-user" ? "9999" : "21"),
            new Claim("role", failure == "unknown-role" ? "Technician" : "StoreManager") };
        if (failure != "missing-session-version") claims = claims.Append(new Claim("session_version", "1")).ToArray();
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
        var tickets = (await _client.GetFromJsonAsync<JsonElement>("/api/tickets")).GetProperty("items").EnumerateArray().ToArray();
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
        var assignment = new TechnicianAssignment(1, 1, 11, DateTime.UtcNow.AddDays(1), "Visit");
        await _api.Data.AddAsync(assignment);
        await SignIn("manager@example.test");
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync("/api/tickets/1/visit/confirm", new { contactName = "Store contact", contactPhone = "12345678" })).StatusCode);
        foreach (var action in new[] { "progress", "resolve" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await PostAsync($"/api/tickets/2/{action}", new { })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await PostAsync($"/api/tickets/999/{action}", new { })).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await PostAsync($"/api/tickets/1/{action}", new { changedByUserId = 999 })).StatusCode);
        }
        Assert.All(_api.Data.History, entry => Assert.Equal(21, entry.ChangedByUserId));
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync("/api/tickets/1/progress", new { })).StatusCode);
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
        var response = await PutAsync("/api/tickets/1", new { title = " Updated ", description = " Updated description ", priority = 4, storeId = 2, createdByUserId = 999, status = 6 });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var ticket = _api.Data.Tickets.Single(ticket => ticket.Id == 1);
        Assert.Equal("Updated", ticket.Title);
        Assert.Equal(TicketPriority.Critical, ticket.Priority);
        Assert.Equal(1, ticket.StoreId);
        Assert.Equal(11, ticket.CreatedByUserId);
        Assert.Equal(TicketStatus.AwaitingApproval, ticket.Status);
        Assert.Empty(_api.Data.History);
        Assert.Equal(HttpStatusCode.NotFound, (await PutAsync("/api/tickets/2", new { title = "Other", description = "Other", priority = 2 })).StatusCode);
    }

    [Fact]
    public async Task Admin_edits_any_store_and_invalid_or_missing_edits_fail()
    {
        await SignIn();
        Assert.Equal(HttpStatusCode.NoContent, (await PutAsync("/api/tickets/2", new { title = "Fixed title", description = "Description", priority = 3 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PutAsync("/api/tickets/2", new { title = " ", description = "Description", priority = 3 })).StatusCode);
        Assert.Equal("Fixed title", _api.Data.Tickets[1].Title);
        Assert.Equal(HttpStatusCode.NotFound, (await PutAsync("/api/tickets/999", new { title = "Title", description = "Description", priority = 3 })).StatusCode);
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    [InlineData("assign")]
    public async Task Manager_cannot_perform_admin_actions(string action)
    {
        await SignIn("manager@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync($"/api/tickets/1/{action}", new { reason = "test" })).StatusCode);
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
        var denied = await PostAsync("/api/tickets", new { title = "Issue", description = "Broken", storeId = 2, categoryId = 1, priority = 2, createdByUserId = 999 });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var response = await PostAsync("/api/tickets", new { title = "Issue", description = "Broken", storeId = 1, categoryId = 1, priority = 2, createdByUserId = 999 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(21, _api.Data.Tickets.Last().CreatedByUserId);
    }

    [Fact]
    public async Task Admin_sees_all_stores_and_records_actual_approval_actor()
    {
        await SignIn();
        Assert.Equal(2, (await _client.GetFromJsonAsync<JsonElement[]>("/api/stores"))!.Length);
        Assert.Equal(2, (await _client.GetFromJsonAsync<JsonElement>("/api/tickets")).GetProperty("items").GetArrayLength());
        var response = await PostAsync("/api/tickets/1/approve", new { reason = "Approved", changedByUserId = 999 });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(11, Assert.Single(_api.Data.History).ChangedByUserId);
    }

    [Fact]
    public async Task Assignment_records_authenticated_admin_for_both_actor_fields()
    {
        await SignIn();
        await PostAsync("/api/tickets/1/approve", new { reason = "Approved" });
        var response = await PostAsync("/api/tickets/1/assign", new { externalTechnicianId = 1, notes = "Visit", scheduledAt = DateTime.UtcNow.AddDays(1), assignedByUserId = 999, changedByUserId = 999 });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(11, Assert.Single(_api.Data.Assignments).AssignedByUserId);
        Assert.All(_api.Data.History, item => Assert.Equal(11, item.ChangedByUserId));
    }

    [Fact]
    public async Task Assignment_validates_options_and_normalizes_visit_time_to_utc()
    {
        await SignIn();
        await PostAsync("/api/tickets/1/approve", new { });
        var future = DateTimeOffset.UtcNow.AddDays(1).ToOffset(TimeSpan.FromHours(3));
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync("/api/tickets/1/assign", new { externalTechnicianId = 99, scheduledAt = future, notes = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync("/api/tickets/1/assign", new { externalTechnicianId = 1, scheduledAt = DateTimeOffset.UtcNow.AddDays(-1), notes = "" })).StatusCode);
        Assert.Equal(TicketStatus.Approved, _api.Data.Tickets[0].Status);
        Assert.Empty(_api.Data.Assignments);
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync("/api/tickets/1/assign", new { externalTechnicianId = 1, scheduledAt = future, notes = "Bring parts" })).StatusCode);
        Assert.Equal(future.UtcDateTime, Assert.Single(_api.Data.Assignments).ScheduledAt);
    }

    [Fact]
    public async Task Assignment_read_is_store_scoped_and_returns_saved_schedule()
    {
        await SignIn();
        await PostAsync("/api/tickets/1/approve", new { });
        var visit = DateTimeOffset.UtcNow.AddDays(1);
        await PostAsync("/api/tickets/1/assign", new { externalTechnicianId = 1, scheduledAt = visit, notes = "Bring parts" });
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
    public async Task Visit_permissions_validation_and_confirmation_gate_are_enforced_by_server()
    {
        await SignIn();
        await PostAsync("/api/tickets/1/approve", new { });
        await PostAsync("/api/tickets/1/assign", new { externalTechnicianId = 1, scheduledAt = DateTimeOffset.UtcNow.AddDays(1), notes = "Visit" });
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync("/api/tickets/1/visit/confirm", new { contactName = "Contact", contactPhone = "123" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync("/api/tickets/1/visit/decline", new { reason = "No" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync("/api/tickets/1/progress", new { })).StatusCode);
        await SignIn("manager@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync("/api/tickets/1/visit/reschedule", new { scheduledAt = DateTimeOffset.UtcNow.AddDays(2) })).StatusCode);
        foreach (var action in new[] { "confirm", "decline" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await PostAsync($"/api/tickets/2/visit/{action}", new { })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await PostAsync($"/api/tickets/999/visit/{action}", new { })).StatusCode);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync("/api/tickets/1/visit/confirm", new { contactName = " ", contactPhone = "123" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync("/api/tickets/1/visit/decline", new { reason = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync("/api/tickets/1/visit/confirm", new { expectedAssignmentVersion = "", contactName = "Contact", contactPhone = "123" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync("/api/tickets/1/visit/confirm", new { expectedAssignmentVersion = "AQEBAQEBAQE=", contactName = "Contact", contactPhone = "123" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync("/api/tickets/1/visit/decline", new { reason = "Store closed then" })).StatusCode);
        var declinedDetail = await _client.GetFromJsonAsync<JsonElement>("/api/tickets/1");
        Assert.Equal("Declined", declinedDetail.GetProperty("visitState").GetString());
        var declinedList = await _client.GetFromJsonAsync<JsonElement>("/api/tickets");
        Assert.Equal("Declined", declinedList.GetProperty("items")[0].GetProperty("visitState").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync("/api/tickets/1/progress", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync("/api/tickets/1/visit/confirm", new { contactName = "Contact", contactPhone = "123" })).StatusCode);
        await SignIn();
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync("/api/tickets/1/visit/reschedule", new { scheduledAt = DateTimeOffset.UtcNow.AddDays(-1) })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync("/api/tickets/1/visit/reschedule", new { scheduledAt = DateTimeOffset.UtcNow.AddDays(2) })).StatusCode);
        await SignIn("manager@example.test");
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync("/api/tickets/1/visit/confirm", new { contactName = " Contact ", contactPhone = " 123 ", confirmedByUserId = 999 })).StatusCode);
        var assignment = Assert.Single(_api.Data.Assignments);
        Assert.Equal(21, assignment.ConfirmedByUserId);
        Assert.Equal("Contact", assignment.ContactName);
        await SignIn();
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync("/api/tickets/1/visit/reschedule", new { scheduledAt = DateTimeOffset.UtcNow.AddDays(3) })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync("/api/tickets/1/progress", new { })).StatusCode);
        Assert.Equal("AwaitingConfirmation", assignment.VisitState);
        Assert.Null(assignment.ContactName);
        Assert.Equal(2, _api.Data.History.Count);
        Assert.Equal(4, assignment.VisitEvents.Count);
    }

    [Fact]
    public async Task Api_can_complete_full_workflow_with_admin_and_manager_audit_actors()
    {
        await SignIn();
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync("/api/tickets/1/approve", new { reason = "Valid request" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync("/api/tickets/1/assign", new { externalTechnicianId = 1, scheduledAt = DateTimeOffset.UtcNow.AddDays(1), notes = "Visit" })).StatusCode);
        await SignIn("manager@example.test");
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync("/api/tickets/1/visit/confirm", new { contactName = "Store contact", contactPhone = "12345678" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync("/api/tickets/1/progress", new { reason = "Work started" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync("/api/tickets/1/resolve", new { reason = "Repaired" })).StatusCode);
        await SignIn();
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync("/api/tickets/1/close", new { reason = "Confirmed", changedByUserId = 999 })).StatusCode);
        Assert.Equal(TicketStatus.Closed, _api.Data.Tickets[0].Status);
        Assert.Equal(5, _api.Data.History.Count);
        Assert.All(_api.Data.History.Take(2), entry => Assert.Equal(11, entry.ChangedByUserId));
        Assert.All(_api.Data.History.Skip(2).Take(2), entry => Assert.Equal(21, entry.ChangedByUserId));
        Assert.Equal(11, _api.Data.History.Last().ChangedByUserId);
    }

    [Fact]
    public async Task Rejection_ends_workflow_and_records_reason()
    {
        await SignIn();
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync("/api/tickets/1/reject", new { reason = "Duplicate request" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync("/api/tickets/1/approve", new { })).StatusCode);
        Assert.Equal("Duplicate request", Assert.Single(_api.Data.History).Reason);
    }

    [Fact]
    public async Task Manager_cannot_close_any_ticket_or_record_history()
    {
        foreach (var ticket in _api.Data.Tickets) { ticket.Approve(); ticket.Assign(); ticket.StartProgress(); ticket.Resolve(); }
        await SignIn("manager@example.test");
        foreach (var id in new[] { 1, 2, 999 })
            Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync($"/api/tickets/{id}/close", new { changedByUserId = 999 })).StatusCode);
        Assert.All(_api.Data.Tickets, ticket => Assert.Equal(TicketStatus.Resolved, ticket.Status));
        Assert.Empty(_api.Data.History);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Admin_can_close_resolved_tickets_from_any_store_with_trusted_actor(int id)
    {
        foreach (var ticket in _api.Data.Tickets) { ticket.Approve(); ticket.Assign(); ticket.StartProgress(); ticket.Resolve(); }
        await SignIn();
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync($"/api/tickets/{id}/close", new { changedByUserId = 999 })).StatusCode);
        Assert.Equal(TicketStatus.Closed, _api.Data.Tickets.Single(ticket => ticket.Id == id).Status);
        Assert.Equal(11, Assert.Single(_api.Data.History).ChangedByUserId);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync($"/api/tickets/{id}/close", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync("/api/tickets/999/close", new { })).StatusCode);
    }

    [Fact]
    public async Task Missing_history_is_404_and_invalid_transition_is_409()
    {
        await SignIn();
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/tickets/999/history")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync("/api/tickets/1/resolve", new { })).StatusCode);
    }

    private static async Task SignInClient(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = TestApi.Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
    }

    private static async Task AssertDemoRejection(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Account changes are disabled in this demo.", problem.GetProperty("detail").GetString());
            Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        }
    }

    [Theory]
    [InlineData(true, "admin@example.test")]
    [InlineData(true, "manager@example.test")]
    [InlineData(false, "admin@example.test")]
    [InlineData(false, "manager@example.test")]
    [InlineData(null, "admin@example.test")]
    [InlineData(null, "manager@example.test")]
    public async Task Demo_mode_protects_profiles_and_passwords_without_changing_normal_mode(bool? demoInstance, string email)
    {
        using var api = new TestApi(demoInstance: demoInstance);
        using var client = api.CreateClient();
        await SignInClient(client, email);
        var before = api.Data.AccountSnapshot();
        var profile = await client.GetFromJsonAsync<JsonElement>("/api/account");
        var edit = await client.PutAsJsonAsync("/api/account/profile", new {
            firstName = "Updated", lastName = "Name", phoneNumber = "123", expectedVersion = profile.GetProperty("version").GetString()
        });
        if (demoInstance == true) await AssertDemoRejection(edit);
        else
        {
            Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
            Assert.Equal("Updated", (await edit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("firstName").GetString());
            edit.Dispose();
        }
        var password = await client.PostAsJsonAsync("/api/account/change-password", new { currentPassword = TestApi.Password, newPassword = "Changed-Test-Pass8!" });
        if (demoInstance == true)
        {
            await AssertDemoRejection(password);
            Assert.Equal(before, api.Data.AccountSnapshot());
            await SignInClient(client, email);
        }
        else
        {
            Assert.Equal(HttpStatusCode.NoContent, password.StatusCode);
            password.Dispose();
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Changed-Test-Pass8!" })).StatusCode);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public async Task Demo_mode_protects_all_manager_mutations_without_changing_normal_mode(bool? demoInstance)
    {
        using var api = new TestApi(demoInstance: demoInstance);
        using var client = api.CreateClient();
        await SignInClient(client, "admin@example.test");
        var before = api.Data.AccountSnapshot();
        var version = Convert.ToBase64String(new byte[8]);
        var requests = new (HttpMethod Method, string Path, object Body, HttpStatusCode Success)[] {
            (HttpMethod.Post, "/api/admin/store-managers", new { firstName = "New", lastName = "Manager", email = "new@example.test", phoneNumber = "123", storeId = 1, password = TestApi.Password }, HttpStatusCode.Created),
            (HttpMethod.Put, "/api/admin/store-managers/21", new { firstName = "Edited", lastName = "Manager", email = "edited@example.test", phoneNumber = "123", storeId = 2, expectedVersion = version }, HttpStatusCode.OK),
            (HttpMethod.Patch, "/api/admin/store-managers/21/status", new { isActive = false, expectedVersion = version }, HttpStatusCode.NoContent),
            (HttpMethod.Patch, "/api/admin/store-managers/21/status", new { isActive = true, expectedVersion = version }, HttpStatusCode.NoContent),
            (HttpMethod.Post, "/api/admin/store-managers/21/reset-password", new { password = "Changed-Test-Pass8!", expectedVersion = version }, HttpStatusCode.NoContent)
        };
        foreach (var request in requests)
        {
            using var message = new HttpRequestMessage(request.Method, request.Path) { Content = JsonContent.Create(request.Body) };
            var response = await client.SendAsync(message);
            if (demoInstance == true)
            {
                await AssertDemoRejection(response);
                Assert.Equal(before, api.Data.AccountSnapshot());
            }
            else
            {
                Assert.Equal(request.Success, response.StatusCode);
                response.Dispose();
            }
        }
        var managers = await client.GetFromJsonAsync<JsonElement>("/api/admin/store-managers");
        Assert.Equal(demoInstance == true ? 1 : 2, managers.GetArrayLength());
        if (demoInstance != true)
        {
            var edited = (await api.Data.GetManagerAsync(21))!;
            Assert.Equal("edited@example.test", edited.Email);
            Assert.Equal(2, edited.StoreId);
            Assert.True(edited.IsActive);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { email = edited.Email, password = "Changed-Test-Pass8!" })).StatusCode);
        }
    }

    [Fact]
    public async Task Demo_guard_preserves_authorization_ticket_workflows_and_precedes_version_validation()
    {
        using var api = new TestApi(demoInstance: true);
        using var client = api.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/account/change-password", new { currentPassword = TestApi.Password, newPassword = "Changed-Test-Pass8!" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/store-managers")).StatusCode);
        await SignInClient(client, "manager@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/store-managers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin/store-managers/21/reset-password", new { password = "Changed-Test-Pass8!" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/tickets")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/tickets/1/comments", new { text = "Demo discussion still works" })).StatusCode);
        await SignInClient(client, "admin@example.test");
        await AssertDemoRejection(await client.PostAsJsonAsync("/api/admin/store-managers/21/reset-password", new { password = "Changed-Test-Pass8!" }));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/tickets/1/approve", new { expectedVersion = Convert.ToBase64String(new byte[8]) })).StatusCode);
        Assert.Equal(TicketStatus.Approved, api.Data.Tickets[0].Status);
    }

    private sealed class TestApi(bool development = false, int loginLimit = 1000, bool? demoInstance = null) : WebApplicationFactory<Program>
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
                ["DemoUser:Email"] = "", ["DemoUser:Password"] = "", ["LoginRateLimit:PermitLimit"] = loginLimit.ToString()
            }));
            if (demoInstance.HasValue)
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                    new Dictionary<string, string?> { ["Hosting:DemoInstance"] = demoInstance.Value.ToString() }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserRepository>();
                services.RemoveAll<ISupportTicketRepository>();
                services.RemoveAll<IReferenceDataRepository>();
                services.RemoveAll<ITicketStatusHistoryRepository>();
                services.RemoveAll<ITechnicianAssignmentRepository>();
                services.RemoveAll<INotificationPublisher>();
                services.AddSingleton<INotificationPublisher, NoNotifications>();
                services.RemoveAll<ITicketQueryRepository>();
                services.AddSingleton<ITicketQueryRepository>(Data);
                services.RemoveAll<IDiscussionRepository>();
                services.AddSingleton<IDiscussionRepository>(Data);
                services.RemoveAll<IManagerRepository>();
                services.AddSingleton<IManagerRepository>(Data);
                services.RemoveAll<IAccountRepository>();
                services.AddSingleton<IAccountRepository>(Data);
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

    [Fact]
    public async Task Discussion_enforces_scope_authorship_validation_and_terminal_read_only()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/tickets/1/comments")).StatusCode);
        await SignIn("manager@example.test");
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync("/api/tickets/2/comments", new { text = "Other store" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync("/api/tickets/1/comments", new { text = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync("/api/tickets/1/comments", new { text = new string('x', 2001) })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await PostAsync("/api/tickets/1/comments", new { text = " <script>alert(1)</script> ", authorId = 11 })).StatusCode);
        var comment = Assert.Single(_api.Data.Comments);
        Assert.Equal(21, comment.AuthorId); Assert.Equal("<script>alert(1)</script>", comment.Text);
        Assert.Empty(_api.Data.History);
        var page = await _client.GetFromJsonAsync<JsonElement>("/api/tickets/1/comments");
        Assert.Equal(1, page.GetProperty("items").GetArrayLength());
        _api.Data.Tickets[0].Reject();
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync("/api/tickets/1/comments", new { text = "After rejection" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/tickets/1/comments")).StatusCode);
        Assert.Single(_api.Data.Comments);
    }

    [Fact]
    public async Task Ticket_queries_validate_boundaries_and_dashboard_uses_authorized_scope()
    {
        await SignIn("manager@example.test");
        foreach (var query in new[] { "page=0", "pageSize=101", "status=99", "priority=0", "page=2147483647&pageSize=100" })
            Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/api/tickets?" + query)).StatusCode);
        var page = await _client.GetFromJsonAsync<JsonElement>("/api/tickets?title=ISSUE&status=1&priority=2&pageSize=1");
        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, page.GetProperty("items")[0].GetProperty("storeId").GetInt32());
        var dashboard = await _client.GetFromJsonAsync<JsonElement>("/api/dashboard");
        Assert.Equal(1, dashboard.GetProperty("summary").GetProperty("total").GetInt32());
        Assert.Equal(1, dashboard.GetProperty("recent").GetArrayLength());
    }

    [Fact]
    public async Task Profile_is_authenticated_scoped_and_cannot_change_access_or_credentials()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/account")).StatusCode);
        await SignIn("manager@example.test");
        var profile = await _client.GetFromJsonAsync<JsonElement>("/api/account");
        Assert.Equal(21, profile.GetProperty("id").GetInt32());
        Assert.False(profile.TryGetProperty("passwordHash", out _));
        var version = profile.GetProperty("version").GetString();
        Assert.Equal(HttpStatusCode.Conflict, (await PutAsync("/api/account/profile", new { firstName = "New", lastName = "Name", phoneNumber = "123", expectedVersion = "AQEBAQEBAQE=" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PutAsync("/api/account/profile", new { firstName = " ", lastName = "Name", phoneNumber = "123", expectedVersion = version })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PutAsync("/api/account/profile", new { firstName = new string('x', 101), lastName = "Name", phoneNumber = "123", expectedVersion = version })).StatusCode);
        var saved = await PutAsync("/api/account/profile", new { id = 11, firstName = " New ", lastName = " Name ", phoneNumber = " 123 ", expectedVersion = version, role = "Admin", storeId = 2, email = "stolen@example.test" });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var result = await saved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("New", result.GetProperty("firstName").GetString());
        Assert.Equal("123", result.GetProperty("phoneNumber").GetString());
        Assert.Equal("manager@example.test", result.GetProperty("email").GetString());
        Assert.Equal("StoreManager", result.GetProperty("role").GetString());
        Assert.Equal(1, result.GetProperty("storeId").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/tickets")).StatusCode);
        var read = await _client.GetFromJsonAsync<JsonElement>("/api/account");
        Assert.Equal("New", read.GetProperty("firstName").GetString());
    }

    [Fact]
    public async Task Dashboard_attention_is_role_scoped_and_excludes_terminal_work()
    {
        await SignIn();
        var admin = await _client.GetFromJsonAsync<JsonElement>("/api/dashboard");
        Assert.Equal(2, admin.GetProperty("attention").GetArrayLength());
        Assert.All(admin.GetProperty("attention").EnumerateArray(), item => Assert.Equal("Review request", item.GetProperty("action").GetString()));
        await SignIn("manager@example.test");
        var manager = await _client.GetFromJsonAsync<JsonElement>("/api/dashboard");
        Assert.Empty(manager.GetProperty("attention").EnumerateArray());
        _api.Data.Tickets[0].Reject();
        await SignIn();
        var updated = await _client.GetFromJsonAsync<JsonElement>("/api/dashboard");
        Assert.Single(updated.GetProperty("attention").EnumerateArray());
    }

    [Fact]
    public async Task Password_change_checks_current_password_and_invalidates_old_tokens()
    {
        await SignIn("manager@example.test");
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync("/api/account/change-password", new { currentPassword = "wrong", newPassword = "New-Test-Pass8!" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync("/api/account/change-password", new { currentPassword = TestApi.Password, newPassword = "New-Test-Pass8!", userId = 11 })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/tickets")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostAsync("/api/auth/login", new { email = "manager@example.test", password = TestApi.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync("/api/auth/login", new { email = "manager@example.test", password = "New-Test-Pass8!" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync("/api/auth/login", new { email = "admin@example.test", password = TestApi.Password })).StatusCode);
    }

    [Fact]
    public async Task Health_is_public_and_unknown_api_routes_are_not_spa_pages()
    {
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/does-not-exist")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/assets/missing.js")).StatusCode);
    }

    private sealed class NoNotifications : INotificationPublisher
    {
        public Task PublishAsync(SupportTicket ticket, int actor, string eventKey, string kind) => Task.CompletedTask;
    }

    private sealed class TestDisplayNames : IDisplayNameRepository
    {
        public Task<DisplayNames> GetAsync(int[] stores, int[] categories, int[] users, int[] technicians) => Task.FromResult(new DisplayNames(
            stores.ToDictionary(id => id, id => $"Store {id}"), categories.ToDictionary(id => id, id => "Maintenance"),
            users.ToDictionary(id => id, id => $"User {id}"), technicians.ToDictionary(id => id, id => $"Technician {id}")));
    }

    private sealed class TestData : IManagerRepository, IUserRepository, ISupportTicketRepository, IReferenceDataRepository, ITicketQueryRepository, IDiscussionRepository, IAccountRepository,
        ITicketStatusHistoryRepository, ITechnicianAssignmentRepository
    {
        public List<TicketComment> Comments { get; } = [];
        public Task<List<TicketComment>> GetAsync(int ticketId, int? beforeId) => Task.FromResult(Comments.Where(c => c.SupportTicketId == ticketId && (!beforeId.HasValue || c.Id < beforeId)).OrderByDescending(c => c.Id).Take(26).ToList());
        public Task AddAsync(TicketComment comment) { SetId(comment, Comments.Count + 1); Comments.Add(comment); return Task.CompletedTask; }
        public Task<User?> GetTrackedAsync(int id) => ((IUserRepository)this).GetByIdAsync(id);
        public Task SaveAsync() => Task.CompletedTask;
        public async Task<TicketPage> SearchAsync(int? storeId, TicketQuery query)
        {
            query.Validate();
            var rows = (await GetAllAsync(storeId)).Where(t => string.IsNullOrWhiteSpace(query.Title) || t.Title.Contains(query.Title.Trim(), StringComparison.OrdinalIgnoreCase))
                .Where(t => !query.OpenOnly || t.Status is TicketStatus.AwaitingApproval or TicketStatus.Approved or TicketStatus.Assigned)
                .Where(t => !query.Status.HasValue || t.Status == query.Status).Where(t => !query.Priority.HasValue || t.Priority == query.Priority)
                .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id).ToList();
            return new(rows.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList(), rows.Count, query.Page, query.PageSize);
        }
        public Task<Dictionary<int, string?>> GetVisitStatesAsync(int[] ticketIds) => Task.FromResult(ticketIds.Distinct()
            .ToDictionary(id => id, id => Assignments.Where(a => a.SupportTicketId == id && a.WithdrawnAt == null)
                .OrderByDescending(a => a.AssignedAt).ThenByDescending(a => a.Id).Select(a => (string?)a.VisitState).FirstOrDefault()));
        public async Task<TicketDashboard> DashboardAsync(int? storeId)
        {
            var rows = await GetAllAsync(storeId);
            return new(new(rows.Count, rows.Count(t => (int)t.Status <= 3), rows.Count(t => (int)t.Status == 4), rows.Count(t => (int)t.Status == 5)), rows.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id).Take(5).ToList(),
                rows.Where(t => storeId.HasValue ? t.Status is TicketStatus.Assigned or TicketStatus.InProgress : t.Status is TicketStatus.AwaitingApproval or TicketStatus.Approved or TicketStatus.Resolved).OrderByDescending(t => t.Priority).ThenBy(t => t.CreatedAt).Take(8).ToList());
        }
        public bool ThrowOnList { get; set; }
        public List<SupportTicket> Tickets { get; } = [MakeTicket(1, 1), MakeTicket(2, 2)];
        public List<TicketStatusHistory> History { get; } = [];
        public List<TechnicianAssignment> Assignments { get; } = [];
        private readonly List<User> _users = [MakeUser(11, "admin@example.test", UserRole.Admin, null), MakeUser(21, "manager@example.test", UserRole.StoreManager, 1)];

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
        public string AccountSnapshot() => JsonSerializer.Serialize(_users);
        public Task<List<User>> GetManagersAsync() => Task.FromResult(_users.Where(user => user.Role == UserRole.StoreManager).ToList());
        public Task<User?> GetManagerAsync(int id) => Task.FromResult(_users.SingleOrDefault(user => user.Id == id && user.Role == UserRole.StoreManager));
        public Task<bool> EmailExistsAsync(string normalizedEmail, int? exceptId) => Task.FromResult(_users.Any(user => user.NormalizedEmail == normalizedEmail && user.Id != exceptId));
        public Task<bool> IsActiveStoreAsync(int storeId) => Task.FromResult(storeId is 1 or 2);
        public Task AddAsync(User user) { SetId(user, _users.Max(item => item.Id) + 1); _users.Add(user); return Task.CompletedTask; }
        Task<User?> IUserRepository.GetByIdAsync(int id) => Task.FromResult(_users.SingleOrDefault(user => user.Id == id));
        Task<TechnicianAssignment?> ITechnicianAssignmentRepository.GetByIdAsync(int id) => Task.FromResult(Assignments.SingleOrDefault(item => item.Id == id));
        public Task SaveAsync(TechnicianAssignment assignment) => Task.CompletedTask;
        public Task<User?> GetByEmailAsync(string email) => Task.FromResult(_users.SingleOrDefault(user => user.Email == email));
        public Task<SupportTicket?> GetByIdAsync(int id) => Task.FromResult(Tickets.SingleOrDefault(ticket => ticket.Id == id));
        public Task<List<SupportTicket>> GetAllAsync(int? storeId = null) => ThrowOnList ? throw new InvalidOperationException("PRIVATE_TEST_SENTINEL") : Task.FromResult(Tickets.Where(ticket => !storeId.HasValue || ticket.StoreId == storeId).ToList());
        public Task SaveAsync(SupportTicket ticket) => Task.CompletedTask;
        public Task AddAsync(SupportTicket ticket) { SetId(ticket, Tickets.Count + 1); Tickets.Add(ticket); return Task.CompletedTask; }
        public Task AddAsync(TicketStatusHistory history) { History.Add(history); return Task.CompletedTask; }
        public Task AddAsync(TechnicianAssignment assignment) { SetId(assignment, Assignments.Count + 1); Assignments.Add(assignment); return Task.CompletedTask; }
        public Task<TechnicianAssignment?> GetLatestByTicketIdAsync(int ticketId) => Task.FromResult(Assignments.LastOrDefault(item => item.SupportTicketId == ticketId && item.WithdrawnAt == null));
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
