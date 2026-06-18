using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formation.Domain.Enums;

public enum StatutFormation
{
    Brouillon = 0,
    EnAttente = 1,
    Validee = 2,
    EnCours = 3,
    Terminee = 4,
    Annulee = 5
}

public enum StatutInscription
{
    EnAttente = 0,
    Confirmee = 1,
    Annulee = 2,
    Terminee = 3
}