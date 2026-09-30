namespace ServiceDesk.Domain.Entities;
public class Store
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

        StoreCode = storeCode;
        Name = name;
        City = city;
        Address = address;
    }

}
