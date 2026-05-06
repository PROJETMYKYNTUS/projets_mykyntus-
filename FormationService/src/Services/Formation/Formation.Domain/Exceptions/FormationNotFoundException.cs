using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Formation.Domain.Exceptions;

public class FormationNotFoundException : Exception
{
    public FormationNotFoundException(Guid id)
        : base($"Formation avec l'ID {id} introuvable.") { }
}

public class InscriptionNotFoundException : Exception
{
    public InscriptionNotFoundException(Guid id)
        : base($"Inscription avec l'ID {id} introuvable.") { }
}