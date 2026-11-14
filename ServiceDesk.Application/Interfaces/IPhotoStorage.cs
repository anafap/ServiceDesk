namespace ServiceDesk.Application.Interfaces;
public interface IPhotoStorage
{
    Task PutOriginalAsync(Guid id, Stream source, CancellationToken cancellationToken);
    Stream OpenOriginal(Guid id);
    Task PutReadyAsync(string key, Stream source, CancellationToken cancellationToken);
    Stream OpenReady(string key);
    void DeleteOriginal(Guid id);
    void DeleteReady(string key);
    void Cleanup(DateTime before, IReadOnlySet<string> referencedKeys);
}
public interface IPhotoScanner { Task ScanAsync(Stream source, CancellationToken cancellationToken); }
