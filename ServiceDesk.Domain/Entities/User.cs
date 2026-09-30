namespace ServiceDesk.Domain.Entities;

public class User
{
    public int Id { get; private set; }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public int DepartmentId { get; private set; }

    public string FullName => $"{FirstName} {LastName}";
}