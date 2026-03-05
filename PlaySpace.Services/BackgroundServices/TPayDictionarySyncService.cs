using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Services.BackgroundServices;

public class TPayDictionarySyncService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TPayDictionarySyncService> _logger;
    private readonly TimeSpan _syncInterval = TimeSpan.FromHours(24); // Sync every 24 hours

    public TPayDictionarySyncService(IServiceProvider serviceProvider, ILogger<TPayDictionarySyncService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TPay Dictionary Sync Service started");

        // Wait 30 seconds after startup before first sync attempt
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        // Perform initial seed if needed
        await PerformInitialSeed();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformScheduledSync();
                
                // Wait for the next sync interval
                await Task.Delay(_syncInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in TPay Dictionary Sync Service");
                
                // Wait 1 hour before retrying on error
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        _logger.LogInformation("TPay Dictionary Sync Service stopped");
    }

    private async Task PerformInitialSeed()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dictionaryService = scope.ServiceProvider.GetRequiredService<ITPayDictionaryService>();

            _logger.LogInformation("Checking if initial dictionary seed is required");
            
            var isRequired = await dictionaryService.IsInitialSeedRequiredAsync();
            if (isRequired)
            {
                _logger.LogInformation("Performing initial dictionary seed");
                await dictionaryService.SeedInitialDataAsync();
                _logger.LogInformation("Initial dictionary seed completed");
            }
            else
            {
                _logger.LogInformation("Initial dictionary seed not required");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during initial dictionary seed");
        }
    }

    private async Task PerformScheduledSync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dictionaryService = scope.ServiceProvider.GetRequiredService<ITPayDictionaryService>();

            _logger.LogInformation("Performing scheduled TPay dictionary sync");
            await dictionaryService.PerformScheduledSyncAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during scheduled dictionary sync");
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TPay Dictionary Sync Service is stopping");
        await base.StopAsync(stoppingToken);
    }
}