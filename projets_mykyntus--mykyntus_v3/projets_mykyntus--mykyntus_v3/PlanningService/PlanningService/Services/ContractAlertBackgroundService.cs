// Services/ContractAlertBackgroundService.cs
// Ce service tourne en arrière-plan et vérifie les alertes chaque jour à minuit

using PlanningService.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace KyntusRH.Services
{
    public class ContractAlertBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ContractAlertBackgroundService> _logger;

        public ContractAlertBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<ContractAlertBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("✅ ContractAlertBackgroundService démarré.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var contractService = scope.ServiceProvider
                                                     .GetRequiredService<IContractService>();

                    _logger.LogInformation("🔍 Vérification des alertes contrats...");
                    await contractService.CheckAndGenerateAlertsAsync();
                    _logger.LogInformation("✅ Alertes contrats vérifiées.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Erreur lors de la vérification des alertes contrats.");
                }

                // Attendre jusqu'à minuit prochain
                var now = DateTime.Now;
                var nextMidnight = now.Date.AddDays(1);
                var delay = nextMidnight - now;

                _logger.LogInformation($"⏰ Prochaine vérification dans {delay.Hours}h {delay.Minutes}min.");
                await Task.Delay(delay, stoppingToken);
            }
        }
    }
}