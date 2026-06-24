namespace Auth.Domain.Interfaces;

public interface ISubjectIdResolver
{
    Guid ResolveForEmail(string email);
}
