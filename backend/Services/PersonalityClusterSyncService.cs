using GrawiaaApp.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GrawiaaApp.API.Services
{
    public class PersonalityClusterSyncService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PersonalityClusterSyncService> _logger;

        public PersonalityClusterSyncService(
            IServiceProvider serviceProvider,
            ILogger<PersonalityClusterSyncService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try { await SyncClusterMemberCountsAsync(stoppingToken); }
                catch (Exception ex) { _logger.LogError(ex, "[ClusterSync] Error"); }

                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        private async Task SyncClusterMemberCountsAsync(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var counts = await context.AdminChildrenPersonalityClusters
                .Where(a => a.IsCurrent)
                .GroupBy(a => a.PersonalityClusterId)
                .Select(g => new { ClusterId = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            int updated = 0;
            foreach (var c in counts)
            {
                var cluster = await context.PersonalityClusters.FindAsync(c.ClusterId);
                if (cluster != null && cluster.MemberCount != c.Count)
                {
                    cluster.MemberCount = c.Count;
                    updated++;
                }
            }

            if (updated > 0)
            {
                await context.SaveChangesAsync(ct);
                _logger.LogInformation("[ClusterSync] Updated MemberCount on {Count} clusters.", updated);
            }
        }
    }
}