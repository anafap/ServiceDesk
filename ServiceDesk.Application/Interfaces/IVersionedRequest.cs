namespace ServiceDesk.Application.Interfaces;

public interface IVersionedRequest
{
    string? ExpectedVersion { get; }
}
