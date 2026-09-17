namespace PropNest.Application.Abstractions;

public interface ICurrentUser
{
    long? UserId { get; }

    bool IsInRole(string role);
}
