using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediatR;

namespace Formation.Application.Commands.CreateFormation;

public record CreateFormationCommand(
    string Titre,
    string Description,
    string Formateur,
    DateTime DateDebut,
    DateTime DateFin,
    int CapaciteMax,
    decimal Prix
) : IRequest<Guid>;