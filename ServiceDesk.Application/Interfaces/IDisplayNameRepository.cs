namespace ServiceDesk.Application.Interfaces;

public sealed record DisplayNames(Dictionary<int, string> Stores, Dictionary<int, string> Categories,
    Dictionary<int, string> Users, Dictionary<int, string> Technicians);

public interface IDisplayNameRepository
{
    Task<DisplayNames> GetAsync(int[] storeIds, int[] categoryIds, int[] userIds, int[] technicianIds);
}
