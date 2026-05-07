namespace Conge.Domain.Enums;

public enum TypeCongeExceptionnel
{
    Mariage = 1,         // 4 jours
    DecesConjoint = 2,   // 3 jours
    DecesParent = 3,     // 2 jours
    Naissance = 4,       // 3 jours (paternité)
    Maternite = 5        // 98 jours (14 semaines)
}