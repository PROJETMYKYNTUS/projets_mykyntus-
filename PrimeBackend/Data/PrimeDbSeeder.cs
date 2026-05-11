using Microsoft.EntityFrameworkCore;

namespace PrimeBackend.Data;

public static class PrimeDbSeeder
{
    /// <summary>Same demo tree as <see cref="Services.PrimeInMemoryStore"/> constructor.</summary>
    public static async Task SeedAsync(PrimeDbContext db, CancellationToken cancellationToken = default)
    {
        var d1 = new DepartmentEntity
        {
            Id = "d1",
            Name = "Operations",
            Poles =
            [
                new PoleEntity
                {
                    Id = "p1",
                    Name = "Pôle Client",
                    DepartmentId = "d1",
                    Cells =
                    [
                        new CelluleEntity
                        {
                            Id = "c1",
                            Name = "Support Client",
                            PoleId = "p1",
                            Teams =
                            [
                                new TeamEntity { Id = "t1", Name = "Team Alpha", CelluleId = "c1" },
                                new TeamEntity { Id = "t2", Name = "Team Beta", CelluleId = "c1" },
                            ]
                        },
                        new CelluleEntity
                        {
                            Id = "c2",
                            Name = "Satisfaction Client",
                            PoleId = "p1",
                            Teams = [new TeamEntity { Id = "t3", Name = "Team Gamma", CelluleId = "c2" }]
                        },
                    ]
                },
                new PoleEntity
                {
                    Id = "p2",
                    Name = "Pôle Escalade",
                    DepartmentId = "d1",
                    Cells =
                    [
                        new CelluleEntity
                        {
                            Id = "c3",
                            Name = "Gestion de retards",
                            PoleId = "p2",
                            Teams = [new TeamEntity { Id = "t4", Name = "Team Delta", CelluleId = "c3" }]
                        }
                    ]
                }
            ]
        };

        var d2 = new DepartmentEntity
        {
            Id = "d2",
            Name = "IT / Technical",
            Poles =
            [
                new PoleEntity
                {
                    Id = "p3",
                    Name = "Infrastructure",
                    DepartmentId = "d2",
                    Cells =
                    [
                        new CelluleEntity
                        {
                            Id = "c4",
                            Name = "Network",
                            PoleId = "p3",
                            Teams = [new TeamEntity { Id = "t5", Name = "NetOps", CelluleId = "c4" }]
                        }
                    ]
                }
            ]
        };

        db.Departments.AddRange(d1, d2);

        static EmployeeEntity E(
            string id, string fn, string ln, string role, string? parentId, string teamId,
            string deptId, string poleId, string celluleId, string email) =>
            new()
            {
                Id = id,
                FirstName = fn,
                LastName = ln,
                Role = role,
                ParentId = parentId,
                TeamId = teamId,
                DepartementId = deptId,
                PoleId = poleId,
                CelluleId = celluleId,
                Email = email
            };

        db.Employees.AddRange(
            E("e1", "Alice", "Dupont", "Pilote", "e8", "t1", "d1", "p1", "c1", "alice.dupont@mykyntus.com"),
            E("e2", "Bob", "Martin", "Pilote", "e8", "t1", "d1", "p1", "c1", "bob.martin@mykyntus.com"),
            E("e3", "Charlie", "Durand", "Manager", "e6", "t1", "d1", "p1", "c1", "charlie.durand@mykyntus.com"),
            E("e4", "Diana", "Bernard", "Pilote", "e8", "t2", "d1", "p1", "c1", "diana.bernard@mykyntus.com"),
            E("e5", "Eve", "Thomas", "RH", null, "t5", "d2", "p3", "c4", "eve.thomas@mykyntus.com"),
            E("e6", "Rachid", "El Amrani", "RP", null, "t1", "d1", "p1", "c1", "rachid.elamrani@mykyntus.com"),
            E("e7", "Salma", "Bennani", "Audit", null, "t1", "d1", "p1", "c1", "salma.bennani@mykyntus.com"),
            E("e8", "Marc", "Lefèvre", "Coach", "e9", "t1", "d1", "p1", "c1", "marc.lefevre@mykyntus.com"),
            E("e9", "Nadia", "Karimi", "Superviseur", "e3", "t1", "d1", "p1", "c1", "nadia.karimi@mykyntus.com"),
            E("e-admin", "Système", "Admin", "Admin", null, "t1", "d1", "p1", "c1", "admin@mykyntus.com")
        );

        await db.SaveChangesAsync(cancellationToken);
    }
}
