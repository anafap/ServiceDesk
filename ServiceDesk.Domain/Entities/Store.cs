namespace ServiceDesk.Domain.Entities;
public class Store : VersionedEntity
{
    public int Id { get; private set; }
    public string StoreCode { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public Store(string storeCode, string name, string address, string city)
    {
        if (string.IsNullOrWhiteSpace(storeCode))
            throw new ArgumentException("Store code is required", nameof(storeCode));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Store name is required.", nameof(name));

        if (string.IsNullOrWhiteSpace(city))
            throw new ArgumentException("City is required.", nameof(city));

        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("Address is required.", nameof(address));

        UpdateDetails(storeCode, name, address, city);
    }

    public void UpdateDetails(string storeCode, string name, string address, string city)
    {
        StoreCode = Required(storeCode, 50, "Store code").ToUpperInvariant();
        Name = Required(name, 50, "Name");
        Address = Required(address, 50, "Address");
        City = Required(city, 500, "City");
    }
    public void SetActive(bool active) => IsActive = active;
    private static string Required(string value, int maximum, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximum)
            throw new ArgumentException($"{field} is required and must be at most {maximum} characters.");
        return value.Trim();
    }
}
