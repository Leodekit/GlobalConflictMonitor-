namespace GlobalConflictMonitor.API.BackgroundJobs
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using GlobalConflictMonitor.Infrastructure.External;
    using GlobalConflictMonitor.Application.Services;
    using GlobalConflictMonitor.Infrastructure.Persistence;
    using Microsoft.EntityFrameworkCore;

    public class EventIngestionWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EventIngestionWorker> _logger;
        private static readonly TimeSpan DelayInterval = TimeSpan.FromMinutes(15);

        public EventIngestionWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<EventIngestionWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "EventIngestionWorker started, running every {Minutes} minutes",
                DelayInterval.TotalMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();

                    var newsClient = scope.ServiceProvider.GetRequiredService<NewsApiClient>();
                    var normalization = scope.ServiceProvider.GetRequiredService<NormalizationService>();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var articles = await newsClient.FetchLatestEventsAsync();

                    foreach (var article in articles)
                    {
                        try
                        {
                            var normalized = normalization.Normalize(article);

                            if (normalized is null)
                                continue;

                            var exists = await db.Events
                                .AnyAsync(e => e.ExternalId == normalized.ExternalId, stoppingToken);

                            if (!exists)
                            {
                                await db.Events.AddAsync(normalized, stoppingToken);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex,
                                "Failed to process article {ExternalId}",
                                article.ExternalId);
                        }
                    }

                    await db.SaveChangesAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while ingesting events");
                }

                try
                {
                    await Task.Delay(DelayInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("EventIngestionWorker stopping");
        }
    }
}