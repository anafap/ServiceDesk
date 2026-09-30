namespace ServiceDesk.Domain.Entities;

public class ExternalTechnician
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

        FullName = fullName;
        CompanyName = companyName;
        PhoneNumber = phoneNumber;
        Specialisation = specialisation;
    }


}