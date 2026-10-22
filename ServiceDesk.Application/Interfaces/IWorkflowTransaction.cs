namespace ServiceDesk.Application.Interfaces;

public interface IWorkflowTransaction
{
    Task ExecuteAsync(Func<Task> operation);
}
