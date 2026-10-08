namespace ServiceDesk.Domain.Entities;

using ServiceDesk.Domain.Enums;


public class User
{
    public int Id { get; private set; }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string PhoneNumber { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public int? StoreId { get; private set; }
    public string FullName => $"{FirstName} {LastName}";
    public string? PasswordHash { get; private set; }

    public User(string firstName, string lastName, string email, string phoneNumber, UserRole role, int? storeId)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First Name is required.", nameof(firstName));
        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last Name is required.", nameof(lastName));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("Phone Number is required.", nameof(phoneNumber));
        if (!Enum.IsDefined(typeof(UserRole), role))
            throw new ArgumentException("Invalid user role.",
            nameof(role));
        if (role == UserRole.StoreManager && (!storeId.HasValue || storeId <= 0))
            throw new ArgumentException(
                "A store manager must be assigned to a valid store.",
                nameof(storeId));
        if (role == UserRole.Admin && storeId.HasValue)
            throw new ArgumentException("An admin should not be assigned to a specific store.",
              nameof(storeId));

        FirstName = firstName;
        LastName = lastName;
        Email = email;
        PhoneNumber = phoneNumber;
        Role = role;
        StoreId = storeId;

    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("password is required.", nameof(passwordHash));
        PasswordHash = passwordHash;
    }
}