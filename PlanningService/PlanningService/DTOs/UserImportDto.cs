public class UserImportDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public int? SubServiceId { get; set; }
    public DateTime? HireDate { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ImportResultDto
{
    public int TotalLignes { get; set; }
    public int Importes { get; set; }
    public int Erreurs { get; set; }
    public List<string> Details { get; set; } = new();
}