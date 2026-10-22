namespace ServiceDesk.Domain.Entities;

public class ExternalTechnician : VersionedEntity
{
    public int Id { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string CompanyName { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public string Specialisation { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    public ExternalTechnician(string fullName, string companyName, string phoneNumber, string specialisation)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full Name is required.", nameof(fullName));
        if (string.IsNullOrWhiteSpace(companyName))
            throw new ArgumentException("Company Name is required.", nameof(companyName));
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("PhoneNumber is required.", nameof(phoneNumber));
        if (string.IsNullOrWhiteSpace(specialisation))
            throw new ArgumentException("Specialisation is required.", nameof(specialisation));

        UpdateDetails(fullName, companyName, phoneNumber, specialisation);
    }


    public void UpdateDetails(string fullName, string companyName, string phoneNumber, string specialisation)
    {
        FullName = Required(fullName, 200, "Full name");
        CompanyName = Required(companyName, 200, "Company name");
        PhoneNumber = Required(phoneNumber, 20, "Phone number");
        Specialisation = Required(specialisation, 200, "Specialisation");
    }
    public void SetActive(bool active) => IsActive = active;
    private static string Required(string value, int maximum, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximum)
            throw new ArgumentException($"{field} is required and must be at most {maximum} characters.");
        return value.Trim();
    }
}