using ServiceDesk.Domain.Exceptions;
namespace ServiceDesk.Domain.Entities;

using ServiceDesk.Domain.Enums;


public class User : VersionedEntity
{
    public bool IsActive { get; private set; } = true;
    public int SessionVersion { get; private set; } = 1;
    public string NormalizedEmail { get; private set; } = string.Empty;
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
        Email = email.Trim().ToLowerInvariant();
        NormalizedEmail = Email.ToUpperInvariant();
        PhoneNumber = phoneNumber;
        Role = role;
        StoreId = storeId;

    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("password is required.", nameof(passwordHash));
        if (PasswordHash is not null) SessionVersion++;
        PasswordHash = passwordHash;
    }
    public void UpdateManager(string firstName, string lastName, string email, string phoneNumber, int storeId)
    {
        if (Role != UserRole.StoreManager) throw new StateConflictException("Only StoreManager accounts can be managed here.");
        if (string.IsNullOrWhiteSpace(firstName) || firstName.Trim().Length > 100
            || string.IsNullOrWhiteSpace(lastName) || lastName.Trim().Length > 100
            || string.IsNullOrWhiteSpace(phoneNumber) || phoneNumber.Trim().Length > 50
            || string.IsNullOrWhiteSpace(email) || email.Trim().Length > 320
            || !System.Net.Mail.MailAddress.TryCreate(email.Trim(), out var parsed) || parsed.Address != email.Trim()
            || storeId <= 0) throw new ArgumentException("Valid name, email, phone number, and store are required.");
        var normalized = email.Trim().ToLowerInvariant();
        if (Email != normalized || StoreId != storeId) SessionVersion++;
        FirstName = firstName.Trim(); LastName = lastName.Trim(); PhoneNumber = phoneNumber.Trim();
        Email = normalized; NormalizedEmail = normalized.ToUpperInvariant(); StoreId = storeId;
    }

    public void SetActive(bool active)
    {
        if (IsActive == active) return;
        IsActive = active;
        SessionVersion++;
    }
}