using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Planning.Infrastructure.Services;

namespace Planning.Infrastructure.Services.EmployeeImport;

public sealed class EmployeeImportExecuteHostedService(
    IEmployeeImportExecuteQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<EmployeeImportExecuteHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            EmployeeImportExecuteWorkItem work;
            try
            {
                work = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            DirectoryHttpAuthContext.AuthorizationHeader.Value = work.AuthorizationHeader;
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var executor = scope.ServiceProvider.GetRequiredService<IEmployeeImportExecutor>();
                await executor.ExecuteJobAsync(work.JobId, work.Request, work.StartedByEmail, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Import employé job {JobId} en échec.", work.JobId);
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<Persistence.AppDbContext>();
                    var job = await db.EmployeeImportJobs.FindAsync([work.JobId], stoppingToken);
                    if (job is not null && job.CompletedAt is null)
                    {
                        job.Status = "Failed";
                        job.ErrorMessage = ex.InnerException?.Message ?? ex.Message;
                        job.CompletedAt = DateTime.UtcNow;
                        await db.SaveChangesAsync(stoppingToken);
                    }
                }
                catch (Exception markEx)
                {
                    logger.LogError(markEx, "Impossible de marquer le job {JobId} en Failed.", work.JobId);
                }
            }
            finally
            {
                DirectoryHttpAuthContext.AuthorizationHeader.Value = null;
            }
        }
    }
}
