using ServiceDesk.Application.Interfaces;

namespace ServiceDesk.Infrastructure.Transactions;

public sealed class WorkflowTransaction(ServiceDeskDbContext dbContext) : IWorkflowTransaction
{
    public async Task ExecuteAsync(Func<Task> operation)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            await operation();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            // Rollback restores SQL, but tracked entities still contain the attempted changes.
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }
}
