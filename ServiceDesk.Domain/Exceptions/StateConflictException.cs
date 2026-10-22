namespace ServiceDesk.Domain.Exceptions;

public sealed class StateConflictException(string message) : InvalidOperationException(message);
