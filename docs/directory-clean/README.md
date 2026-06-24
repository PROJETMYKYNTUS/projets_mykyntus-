# EmployeeDirectory

Refonte parall�le en Clean Architecture � **parit� fonctionnelle** avec `EmployeeDirectoryBackend`.

## Structure

| Projet | R�le |
|--------|------|
| `EmployeeDirectory.Domain` | Entit�s, enums, exceptions (z�ro d�pendance externe) |
| `EmployeeDirectory.Application` | DTOs, ports (`IDirectoryReadService`, `IDirectoryWriteService`�) |
| `EmployeeDirectory.Infrastructure` | EF Core, services, MassTransit, outbox |
| `EmployeeDirectory.API` | Controllers REST (m�mes routes que legacy) |
| `*Tests` | Architecture (NetArchTest), unitaires, int�gration |

## D�marrage local

```powershell
cd EmployeeDirectory
dotnet run --project EmployeeDirectory.API
# http://localhost:8566/api/directory/health
```

Legacy inchang� : port **8565** (`EmployeeDirectoryBackend`).

## Tests

```powershell
dotnet test EmployeeDirectory.slnx -c Release
# 6 tests : 4 architecture + 1 unit + 1 integration
```

## Parit� API

Toutes les routes de `DirectoryControllers.cs` sont disponibles :
- `/api/directory/employees`, `/api/directory/org/*`, `/api/directory/rebac/*`
- `/api/directory/business-departments/*`, `/api/directory/reconcile/*`
- `/api/iam/*`

## Prochaines �tapes (plan)

1. Handlers MediatR par use case (remplacer injection directe des services)
2. Domain riche (logique m�tier dans les entit�s)
3. Contract tests old (8565) vs new (8566)
4. Bascule docker-compose production
