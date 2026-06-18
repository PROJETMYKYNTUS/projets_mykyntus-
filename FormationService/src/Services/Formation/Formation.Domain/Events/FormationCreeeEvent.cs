using System;

namespace Formation.Domain.Events;

public record FormationCreeeEvent(Guid FormationId, string Titre);
public record FormationValideeEvent(Guid FormationId, string Titre);