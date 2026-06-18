using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Conge.Domain.Exceptions;

public class CongeNotFoundException : Exception
{
    public CongeNotFoundException(Guid demandeId)
        : base($"Demande de congé introuvable: {demandeId}")
    { }
}

public class EmployeNotFoundException : Exception
{
    public EmployeNotFoundException(Guid employeId)
        : base($"Employé introuvable dans le snapshot: {employeId}")
    { }
}

public class SoldeNotFoundException : Exception
{
    public SoldeNotFoundException(Guid employeId, int annee)
        : base($"Solde introuvable pour l'employé {employeId} pour l'année {annee}.")
    { }
}
