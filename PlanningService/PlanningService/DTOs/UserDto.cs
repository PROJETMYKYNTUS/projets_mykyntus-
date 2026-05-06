public class UserDto
{
    public int Id { get; set; }
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public int? SubServiceId { get; set; }
    public string? SubServiceName { get; set; }
    public List<SubServiceSimpleDto> ManagedSubServices { get; set; } = new();
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime HireDate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Level { get; set; }
  
}

public class SubServiceSimpleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
}

public class CreateUserDto
{
    public int RoleId { get; set; }
    public int? SubServiceId { get; set; }
    public List<int> ManagedSubServiceIds { get; set; } = new();
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime HireDate { get; set; }
    public string Email { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
}

public class UpdateUserDto
{
    public int RoleId { get; set; }
    public int? SubServiceId { get; set; }
    public List<int> ManagedSubServiceIds { get; set; } = new();
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime HireDate { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int Level { get; set; } = 1;
}