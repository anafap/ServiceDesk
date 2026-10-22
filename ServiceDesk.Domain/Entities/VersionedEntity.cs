using ServiceDesk.Domain.Exceptions;
namespace ServiceDesk.Domain.Entities;

public abstract class VersionedEntity
{
    public byte[] RowVersion { get; private set; } = new byte[8];
    public string Version => Convert.ToBase64String(RowVersion);

    public void EnsureVersion(string expectedVersion)
    {
        if (string.IsNullOrWhiteSpace(expectedVersion)) throw new ArgumentException("The current record version is required.");
        if (expectedVersion != Version) throw new StateConflictException("This record changed. Refresh before trying again.");
    }
}
