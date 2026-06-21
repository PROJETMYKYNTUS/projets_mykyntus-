namespace PlanningService.Services.EmployeeImport;

public static class EmployeeImportFieldRegistry
{
    /// <summary>Colonnes du modèle Excel standard (11 colonnes).</summary>
    public static readonly IReadOnlyList<EmployeeImportFieldDefinition> TemplateFields =
    [
        new("email", "Email", true, true, 1,
            ["email", "e-mail", "mail", "adresse email", "courriel"]),
        new("firstName", "Prénom", true, true, 2,
            ["firstname", "first name", "prenom", "prénom"]),
        new("lastName", "Nom", true, true, 3,
            ["lastname", "last name", "nom", "nom de famille", "family name"]),
        new("password", "Mot de passe", true, false, 4,
            ["password", "mot de passe", "mdp", "pwd"]),
        new("role", "Rôle", true, true, 5,
            ["role", "rôle", "roleid", "role id", "fonction", "profil", "profil metier", "type", "grade"]),
        new("pole", "Pôle", true, false, 6,
            ["pole", "pôle", "etage", "étage", "department", "departement"]),
        new("cellule", "Cellule", true, false, 7,
            ["cellule", "cell", "cellule id", "unite", "unité", "equipe mere"]),
        new("service", "Service", true, false, 8,
            ["service", "equipe", "équipe", "sous-service", "sous service", "subservice", "sub service"]),
        new("hireDate", "Date d'embauche", true, false, 9,
            ["hiredate", "hire date", "date embauche", "date d'embauche", "embauche"]),
        new("isActive", "Actif", true, false, 10,
            ["isactive", "is active", "actif", "active", "statut"]),
        new("level", "Niveau", true, false, 11,
            ["level", "niveau", "debutant", "débutant", "intermediaire", "intermédiaire", "expert"]),
        new("operationalDepartment", "Département opérationnel", true, false, 12,
            ["departementmetier", "departement operationnel", "departementoperationnel", "deptmetier",
             "deptoperationnel", "operationaldepartment", "businessdepartment"]),
    ];

    /// <summary>Champs retirés du modèle (désactivés à l'import, rétrocompatibilité fichiers anciens).</summary>
    private static readonly IReadOnlyList<EmployeeImportFieldDefinition> RetiredFields =
    [
        new("isNewEmployee", "Nouvel employé", false, false, 90,
            ["isnewemployee", "nouvel employe", "nouvel employé", "new employee"]),
        new("managerEmail", "Email manager", false, false, 91,
            ["manageremail", "email manager", "manager", "responsable", "n+1"]),
        new("structurePole", "Pôle affectation structure", false, false, 92,
            ["structurepole", "pole affectation structure", "pole structure", "pôle affectation"]),
        new("structureCellule", "Cellule affectation structure", false, false, 93,
            ["structurecellule", "cellule affectation structure", "cellule structure"]),
        new("structureService", "Service affectation structure", false, false, 94,
            ["structureservice", "service affectation structure", "service structure"]),
        new("subService", "Sous-service (legacy)", false, false, 99,
            ["subserviceid", "subservice id"]),
    ];

    public static readonly IReadOnlyList<EmployeeImportFieldDefinition> DefaultFields =
        TemplateFields.Concat(RetiredFields).ToList();

    public static bool IsAdminRoleName(string? roleName) =>
        string.Equals(roleName?.Trim(), "Admin", StringComparison.OrdinalIgnoreCase);

    public static bool IsImportForbiddenRoleName(string? roleName) =>
        EmployeeImportRoleSynonymRegistry.IsImportForbiddenRoleName(roleName);

    public static bool IsSystemFieldKey(string fieldKey) =>
        DefaultFields.Any(f => string.Equals(f.FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase));
}

public sealed record EmployeeImportFieldDefinition(
    string FieldKey,
    string Label,
    bool IsEnabledByDefault,
    bool IsRequiredOnCreate,
    int SortOrder,
    IReadOnlyList<string> Aliases);
