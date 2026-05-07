using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Conge.Domain.Exceptions;
public class EligibiliteException : Exception
{
    public EligibiliteException(Guid employeId, string raison)
        : base($"Employé {employeId} non éligible: {raison}")
    { }
}