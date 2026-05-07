using Conge.Application.Commands.DemanderConge;
using Conge.Application.Commands.RefuserConge;
using Conge.Application.Commands.ValiderConge;
using Conge.Domain.Enums;
using FluentValidation;

namespace Conge.Application.Behaviors;

public class DemanderCongeValidator : AbstractValidator<DemanderCongeCommand>
{
    public DemanderCongeValidator()
    {
        RuleFor(x => x.EmployeId)
            .NotEmpty().WithMessage("L'identifiant de l'employé est obligatoire.");

        RuleFor(x => x.DateDebut)
            .GreaterThanOrEqualTo(DateTime.Today)
            .WithMessage("La date de début ne peut pas être dans le passé.");

        RuleFor(x => x.DateFin)
            .GreaterThan(x => x.DateDebut)
            .WithMessage("La date de fin doit être postérieure à la date de début.");

        RuleFor(x => x.TypeExceptionnel)
            .NotNull()
            .When(x => x.TypeConge == TypeConge.Exceptionnel)
            .WithMessage("Le type exceptionnel est obligatoire pour un congé exceptionnel.");
    }
}

public class ValiderCongeValidator : AbstractValidator<ValiderCongeCommand>
{
    public ValiderCongeValidator()
    {
        RuleFor(x => x.DemandeId).NotEmpty();
        RuleFor(x => x.ManagerId).NotEmpty();
    }
}

public class RefuserCongeValidator : AbstractValidator<RefuserCongeCommand>
{
    public RefuserCongeValidator()
    {
        RuleFor(x => x.DemandeId).NotEmpty();
        RuleFor(x => x.ManagerId).NotEmpty();
        RuleFor(x => x.Commentaire)
            .NotEmpty().WithMessage("Le motif de refus est obligatoire.")
            .MinimumLength(10).WithMessage("Le motif doit contenir au moins 10 caractères.");
    }
}