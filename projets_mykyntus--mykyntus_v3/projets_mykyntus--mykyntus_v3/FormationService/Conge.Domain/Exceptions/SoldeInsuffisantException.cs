namespace Conge.Domain.Exceptions;

public class SoldeInsuffisantException : Exception
{
    public SoldeInsuffisantException(Guid employeId, double demande, double disponible)
        : base($"Solde insuffisant pour l'employé {employeId}. Demandé: {demande} jours, Disponible: {disponible} jours.")
    { }
}