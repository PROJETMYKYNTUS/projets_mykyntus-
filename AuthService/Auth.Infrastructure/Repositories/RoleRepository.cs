using Auth.Domain.Entities;
using Auth.Domain.Interfaces;
using Auth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Auth.Infrastructure.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly AuthDbContext _context;

    public RoleRepository(AuthDbContext context) => _context = context;

    public async Task<Role?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await _context.Roles.FindAsync([id], ct);

    public Task<Role?> GetByNameAsync(string name, CancellationToken ct = default) =>
        _context.Roles.FirstOrDefaultAsync(r => r.Name.ToLower() == name.ToLower(), ct);
}
