namespace ServiceDesk.Domain.Entities;

public class Asset
{
    public int Id { get; private set; }
    public string AssetTag { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string SerialNumber { get; private set; } = string.Empty;
    public string Type { get; private set; } = string.Empty;
    public int StoreId { get; private set; }
    public bool IsActive { get; private set; } = true;

    public Asset(string assetTag, string name, string serialNumber, string type, int storeId)
    {
        if (string.IsNullOrWhiteSpace(assetTag))
            throw new ArgumentException("Asset Tag is required.", nameof(assetTag));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(serialNumber))
            throw new ArgumentException("Serial Number is required.", nameof(serialNumber));
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Type is required.", nameof(type));
        if (storeId <= 0)
            throw new ArgumentException("A valid store is required.", nameof(storeId));

        AssetTag = assetTag;
        Name = name;
        SerialNumber = serialNumber;
        Type = type;
        StoreId = storeId;
    }
}
