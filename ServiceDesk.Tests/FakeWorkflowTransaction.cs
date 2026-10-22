using ServiceDesk.Application.Interfaces;

namespace ServiceDesk.Tests;

// Application/security tests isolate orchestration. SQL integration tests verify rollback.
internal sealed class FakeWorkflowTransaction : IWorkflowTransaction
{
    public Task ExecuteAsync(Func<Task> operation) => operation();
}
