using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TambayanCafeAPI.Services
{
    public class ReorderBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ReorderBackgroundService> _logger;

        public ReorderBackgroundService(IServiceProvider serviceProvider, ILogger<ReorderBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🔁 Reorder background service is starting. Interval: 30 seconds.");

            // Optional: Run once immediately on startup
            await DoWorkAsync();

            // 🔥 Changed: Run every 30 seconds instead of 5 minutes
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(2));

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    await DoWorkAsync();
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("🔁 Reorder service stopped (cancellation requested).");
            }
        }

        private async Task DoWorkAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var reorderService = scope.ServiceProvider.GetRequiredService<ReorderService>();

                _logger.LogDebug("🔍 [30s] Checking for items to auto-reorder...");
                await reorderService.CheckAndReorderAsync();
                _logger.LogDebug("✅ [30s] Auto-reorder check completed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [30s] Error in auto-reorder background service.");
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🛑 Reorder background service is stopping.");
            await base.StopAsync(stoppingToken);
        }
    }
}