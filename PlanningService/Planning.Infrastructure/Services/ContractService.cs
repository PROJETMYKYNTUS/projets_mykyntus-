using Planning.Infrastructure.Persistence;
using Planning.Application.DTOs;
using Planning.Application.Abstractions;
using Planning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Planning.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Planning.Infrastructure.Services
{
    public class ContractService : IContractService
    {
        private readonly AppDbContext _context;

        private static readonly Dictionary<ContractType, int> DefaultProbationDays = new()
        {
            { ContractType.CDI,     90 },
            { ContractType.CDD,     30 },
            { ContractType.Stage,   15 },
            { ContractType.ANAPEC,  0 },
        };

        public ContractService(AppDbContext context)
        {
            _context = context;
        }

        // ------------------------------------------------
        // CRUD
        // ------------------------------------------------

        public async Task<IEnumerable<ContractResponseDto>> GetAllContractsAsync()
        {
            var contracts = await _context.Contracts
                .Include(c => c.User)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
            return contracts.Select(MapToResponseDto);
        }

        public async Task<ContractResponseDto?> GetContractByIdAsync(int id)
        {
            var contract = await _context.Contracts
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == id);
            return contract == null ? null : MapToResponseDto(contract);
        }

        public async Task<IEnumerable<ContractResponseDto>> GetContractsByUserIdAsync(int userId)
        {
            var contracts = await _context.Contracts
                .Include(c => c.User)
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();
            return contracts.Select(MapToResponseDto);
        }

        public async Task<EmploymentSummaryDto?> GetEmploymentSummaryByEmployeeGuidAsync(Guid employeeGuid)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Guid == employeeGuid);
            if (user is null) return null;

            var contract = await _context.Contracts
                .AsNoTracking()
                .Where(c => c.UserId == user.Id)
                .OrderByDescending(c => c.StartDate)
                .FirstOrDefaultAsync();

            var today = DateTime.UtcNow.Date;
            var summary = new EmploymentSummaryDto
            {
                UserId = user.Id,
                EmployeeGuid = employeeGuid,
                IsActive = user.IsActive,
                HasContract = contract is not null,
            };

            if (contract is null)
            {
                summary.IsEligibleForPaymentConfirmation = false;
                summary.BlockReason = "Aucun contrat enregistré pour cet employé.";
                return summary;
            }

            summary.ContractStatus = contract.Status switch
            {
                ContractStatus.EnPeriodeEssai => "En période d'essai",
                ContractStatus.Actif => "Actif",
                ContractStatus.Expire => "Expiré",
                ContractStatus.Resilie => "Résilié",
                _ => contract.Status.ToString(),
            };
            summary.ProbationEndDate = contract.ProbationEndDate;

            if (!user.IsActive)
            {
                summary.IsEligibleForPaymentConfirmation = false;
                summary.BlockReason = "Employé inactif.";
                return summary;
            }

            if (contract.Status == ContractStatus.Resilie || contract.Status == ContractStatus.Expire)
            {
                summary.IsEligibleForPaymentConfirmation = false;
                summary.BlockReason = $"Contrat {summary.ContractStatus?.ToLowerInvariant()}.";
                return summary;
            }

            if (contract.Status == ContractStatus.EnPeriodeEssai &&
                contract.ProbationEndDate.HasValue &&
                contract.ProbationEndDate.Value.Date >= today)
            {
                summary.IsEligibleForPaymentConfirmation = false;
                summary.BlockReason = "Contrat encore en période d'essai.";
                return summary;
            }

            summary.IsEligibleForPaymentConfirmation = true;
            return summary;
        }

        public async Task<ContractResponseDto> CreateContractAsync(CreateContractDto dto)
        {
            if (dto.Type != ContractType.CDI && dto.EndDate == null)
                throw new ArgumentException($"EndDate est obligatoire pour le type {dto.Type}.");

            int probationDays = dto.ProbationDays ?? DefaultProbationDays[dto.Type];
            var probationStart = await ResolveProbationStartDateAsync(dto.UserId, dto.StartDate);
            DateTime? probationEndDate = probationDays > 0
                ? probationStart.AddDays(probationDays)
                : null;

            var defaultStatus = probationDays > 0
                ? ContractStatus.EnPeriodeEssai
                : ContractStatus.Actif;
            var contract = new Contract
            {
                UserId = dto.UserId,
                Type = dto.Type,
                Status = dto.Status ?? defaultStatus,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                ProbationDays = probationDays,
                ProbationEndDate = probationEndDate,
                AlertThresholdDays = dto.AlertThresholdDays,
                Notes = dto.Notes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Contracts.Add(contract);
            await _context.SaveChangesAsync();
            return MapToResponseDto(contract);
        }

        public async Task<ContractResponseDto?> UpdateContractAsync(int id, UpdateContractDto dto)
        {
            var contract = await _context.Contracts
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (contract == null) return null;

            if (dto.Type.HasValue)
            {
                contract.Type = dto.Type.Value;
                int probDays = dto.ProbationDays ?? DefaultProbationDays[dto.Type.Value];
                contract.ProbationDays = probDays;
                contract.ProbationEndDate = probDays > 0
                    ? (await ResolveProbationStartDateAsync(contract.UserId, contract.StartDate)).AddDays(probDays)
                    : null;
            }

            if (dto.Status.HasValue) contract.Status = dto.Status.Value;
            if (dto.EndDate.HasValue) contract.EndDate = dto.EndDate.Value;
            if (dto.ProbationDays.HasValue) contract.ProbationDays = dto.ProbationDays.Value;
            if (dto.AlertThresholdDays.HasValue) contract.AlertThresholdDays = dto.AlertThresholdDays.Value;
            if (dto.Notes != null) contract.Notes = dto.Notes;

            contract.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return MapToResponseDto(contract);
        }

        public async Task<bool> DeleteContractAsync(int id)
        {
            var contract = await _context.Contracts.FindAsync(id);
            if (contract == null) return false;
            _context.Contracts.Remove(contract);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<ContractResponseDto?> ConfirmProbationPeriodAsync(int contractId)
        {
            var contract = await _context.Contracts
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == contractId);
            if (contract is null) return null;
            if (contract.Status != ContractStatus.EnPeriodeEssai)
                throw new InvalidOperationException("Le contrat n'est pas en p�riode d'essai.");

            contract.Status = ContractStatus.Actif;
            contract.UpdatedAt = DateTime.UtcNow;
            if (contract.User is not null)
                contract.User.IsActive = true;

            await _context.SaveChangesAsync();
            return MapToResponseDto(contract);
        }

        // ------------------------------------------------
        // NOTIFICATIONS � LOGIQUE LIVE
        // ------------------------------------------------
        // Au lieu de stocker des notifications en base et attendre minuit,
        // on calcule EN TEMPS R�EL quels contrats sont en alerte
        // et on les retourne comme notifications.

        public async Task<IEnumerable<NotificationResponseDto>> GetUnreadNotificationsAsync()
        {
            var today = DateTime.UtcNow.Date;

            var contracts = await _context.Contracts
                .Include(c => c.User)
                .Where(c => c.Status == ContractStatus.Actif ||
                            c.Status == ContractStatus.EnPeriodeEssai)
                .ToListAsync();

            var userIds = contracts.Select(c => c.UserId).Distinct().ToList();
            var hrByUser = await _context.UserHrProfiles.AsNoTracking()
                .Where(p => userIds.Contains(p.UserId))
                .ToDictionaryAsync(p => p.UserId);

            var notifications = new List<NotificationResponseDto>();

            foreach (var c in contracts)
            {
                var userName = $"{c.User?.FirstName} {c.User?.LastName}";
                var threshold = c.AlertThresholdDays;
                hrByUser.TryGetValue(c.UserId, out var hr);

                // -- Alerte : X jours avant fin contrat (CDD / Stage / Int�rim) --
                if (c.EndDate.HasValue && c.Type != ContractType.CDI)
                {
                    var joursRestants = (c.EndDate.Value.Date - today).Days;

                    if (joursRestants >= 0 && joursRestants <= threshold)
                    {
                        notifications.Add(BuildNotif(c.Id, c.UserId, userName,
                            "AvantFinCDD",
                            $"?? {joursRestants}j avant fin {c.Type} � {userName} (fin le {c.EndDate.Value:dd/MM/yyyy})",
                            c.UpdatedAt));
                    }
                    // Mi-parcours : on est entre 45% et 55% du contrat
                    else
                    {
                        var totalDays = (c.EndDate.Value.Date - c.StartDate.Date).Days;
                        var elapsed = (today - c.StartDate.Date).Days;
                        var pct = totalDays > 0 ? (double)elapsed / totalDays * 100 : 0;

                        if (pct >= 45 && pct <= 55)
                        {
                            notifications.Add(BuildNotif(c.Id, c.UserId, userName,
                                "MiParcoursCDD",
                                $"? Mi-parcours {c.Type} � {userName} : il reste {joursRestants} jours",
                                c.UpdatedAt));
                        }
                    }
                }

                // -- Alerte : X jours avant fin p�riode d'essai --
                if (c.ProbationEndDate.HasValue && c.Status == ContractStatus.EnPeriodeEssai)
                {
                    var joursEssai = (c.ProbationEndDate.Value.Date - today).Days;

                    if (joursEssai >= 0 && joursEssai <= threshold)
                    {
                        notifications.Add(BuildNotif(c.Id, c.UserId, userName,
                            "AvantFinPeriodeEssai",
                            $"?? {joursEssai}j avant fin p�riode d'essai � {userName} (fin le {c.ProbationEndDate.Value:dd/MM/yyyy})",
                            c.UpdatedAt));
                    }
                    // Mi-parcours p�riode d'essai
                    else
                    {
                        var probStart = ResolveProbationStartDate(hr, c.StartDate);
                        var totalProb = (c.ProbationEndDate.Value.Date - probStart.Date).Days;
                        var elapsedP = (today - probStart.Date).Days;
                        var pctP = totalProb > 0 ? (double)elapsedP / totalProb * 100 : 0;

                        if (pctP >= 45 && pctP <= 55)
                        {
                            notifications.Add(BuildNotif(c.Id, c.UserId, userName,
                                "MiParcoursPeriodeEssai",
                                $"?? Mi-parcours p�riode d'essai � {userName} : il reste {joursEssai} jours",
                                c.UpdatedAt));
                        }
                    }
                }
            }

            // Trier par date d�croissante
            return notifications.OrderByDescending(n => n.CreatedAt);
        }

        public async Task<int> GetUnreadNotificationsCountAsync()
        {
            var notifs = await GetUnreadNotificationsAsync();
            return notifs.Count();
        }

        // Ces m�thodes ne font plus rien (logique live = pas de stockage)
        // Gard�es pour respecter l'interface
        public Task MarkNotificationAsReadAsync(int notificationId) => Task.CompletedTask;
        public Task MarkAllNotificationsAsReadAsync() => Task.CompletedTask;
        public Task CheckAndGenerateAlertsAsync() => Task.CompletedTask;

        // ------------------------------------------------
        // HELPERS PRIV�S
        // ------------------------------------------------

        private async Task<DateTime> ResolveProbationStartDateAsync(int userId, DateTime contractStart)
        {
            var hr = await _context.UserHrProfiles.AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId);
            return ResolveProbationStartDate(hr, contractStart);
        }

        private static DateTime ResolveProbationStartDate(UserHrProfile? hr, DateTime contractStart)
        {
            if (hr?.EnFormation == true && hr.DateFinFormationPrevue.HasValue)
            {
                var formationEnd = hr.DateFinFormationPrevue.Value.ToDateTime(TimeOnly.MinValue);
                return formationEnd > contractStart ? formationEnd : contractStart;
            }
            return contractStart;
        }

        private static NotificationResponseDto BuildNotif(
            int contractId, int userId, string userName,
            string type, string message, DateTime createdAt)
        {
            return new NotificationResponseDto
            {
                Id = contractId, // utilise l'id contrat comme id notif
                ContractId = contractId,
                UserId = userId,
                UserFullName = userName,
                Type = type,
                Message = message,
                IsRead = false,      // toujours "non lu" tant que l'alerte est active
                CreatedAt = createdAt
            };
        }

        private static ContractResponseDto MapToResponseDto(Contract c)
        {
            var today = DateTime.UtcNow.Date;

            int? joursRestants = c.EndDate.HasValue
                ? (int?)(c.EndDate.Value.Date - today).Days
                : null;

            int? joursRestantsEssai = c.ProbationEndDate.HasValue
                ? (int?)(c.ProbationEndDate.Value.Date - today).Days
                : null;

            bool isAlert =
                (joursRestants.HasValue && joursRestants >= 0 && joursRestants <= c.AlertThresholdDays) ||
                (joursRestantsEssai.HasValue && joursRestantsEssai >= 0 && joursRestantsEssai <= c.AlertThresholdDays);

            return new ContractResponseDto
            {
                Id = c.Id,
                UserId = c.UserId,
                UserFullName = $"{c.User?.FirstName} {c.User?.LastName}",
                Type = c.Type.ToString(),
                Status = c.Status switch
                {
                    ContractStatus.EnPeriodeEssai => "En p�riode d'essai",
                    ContractStatus.Actif => "Actif",
                    ContractStatus.Expire => "Expir�",
                    ContractStatus.Resilie => "R�sili�",
                    _ => c.Status.ToString()
                },
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                ProbationEndDate = c.ProbationEndDate,
                JoursRestants = joursRestants,
                JoursRestantsPeriodeEssai = joursRestantsEssai,
                IsAlertActive = isAlert,
                AlertThresholdDays = c.AlertThresholdDays,
                Notes = c.Notes,
                CreatedAt = c.CreatedAt
            };
        }
    }
}