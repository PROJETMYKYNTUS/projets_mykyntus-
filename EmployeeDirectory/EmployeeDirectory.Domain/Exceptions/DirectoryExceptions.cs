namespace EmployeeDirectory.Domain.Exceptions;

public class EmailAlreadyUsedException(string email)
    : InvalidOperationException($"Email déjà utilisé : {email}");

public class OrgNodeNotFoundException(string nodeId)
    : KeyNotFoundException($"Nœud org introuvable : {nodeId}");

public class BusinessDepartmentConflictException(string message)
    : InvalidOperationException(message);
