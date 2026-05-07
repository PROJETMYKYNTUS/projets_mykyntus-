using Conge.Domain.Enums;
using MediatR;
using System;

namespace Conge.Application.Commands.DemanderConge;

public record DemanderCongeCommand(
    Guid EmployeId,
    TypeConge TypeConge,
    DateTime DateDebut,
    DateTime DateFin,
    string? Motif,
    TypeCongeExceptionnel? TypeExceptionnel = null
) : IRequest<Guid>;
