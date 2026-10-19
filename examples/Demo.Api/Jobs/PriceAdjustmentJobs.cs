using System.Collections.Concurrent;
using System.Threading.Channels;
using CachedQueries.Abstractions;
using Demo.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Demo.Api.Jobs;

public enum JobState
{
    Queued,
    Completed,
    Failed
}

// ExplicitInvalidation = false shows what happens without explicit tenant invalidation:
// the tenant's cached goods stay stale.
public record PriceAdjustmentJob(Guid Id, string TenantId, string Category, decimal Percent, bool ExplicitInvalidation);

/// <summary>
///     In-memory job queue + status store (enough for a demo; use a durable queue in production).
/// </summary>
public sealed class PriceAdjustmentQueue
{
    private readonly Channel<PriceAdjustmentJob> _channel = Channel.CreateUnbounded<PriceAdjustmentJob>();
    private readonly ConcurrentDictionary<Guid, JobState> _states = new();

    public ChannelReader<PriceAdjustmentJob> Reader => _channel.Reader;

    public PriceAdjustmentJob Enqueue(string tenantId, string category, decimal percent, bool explicitInvalidation)
    {
        var job = new PriceAdjustmentJob(Guid.NewGuid(), tenantId, category, percent, explicitInvalidation);
        _states[job.Id] = JobState.Queued;
        _channel.Writer.TryWrite(job);
        return job;
    }

    public JobState? GetState(Guid id)
    {
        return _states.TryGetValue(id, out var state) ? state : null;
    }

    public void SetState(Guid id, JobState state)
    {
        _states[id] = state;
    }
}

/// <summary>
///     Background service changing tenant data outside of any HTTP request.
/// </summary>
/// <remarks>
///     There is no ambient tenant here, so <see cref="TenantCacheContextProvider" /> returns null and the automatic
///     invalidation run by SaveChanges only reaches <b>global</b> cache entries. The tenant's own entries (e.g. its
///     cached goods list) must be invalidated explicitly with the tenant key.
/// </remarks>
public sealed class PriceAdjustmentWorker(
    PriceAdjustmentQueue queue,
    IServiceScopeFactory scopeFactory,
    ICacheInvalidator invalidator,
    ILogger<PriceAdjustmentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await RunAsync(job, stoppingToken);
                queue.SetState(job.Id, JobState.Completed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Price adjustment job {JobId} failed", job.Id);
                queue.SetState(job.Id, JobState.Failed);
            }
        }
    }

    private async Task RunAsync(PriceAdjustmentJob job, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var factor = 1 + job.Percent / 100m;
        var goods = await db.Goods
            .Where(g => g.TenantId == job.TenantId && g.Category == job.Category)
            .ToListAsync(cancellationToken);

        foreach (var good in goods)
        {
            good.Price = Math.Round(good.Price * factor, 2);
        }

        // Auto-invalidation runs, but without a tenant context it only invalidates global entries.
        await db.SaveChangesAsync(cancellationToken);

        if (job.ExplicitInvalidation)
        {
            // Reach the tenant's entries by passing its context key explicitly.
            await invalidator.InvalidateAsync([typeof(Good)], job.TenantId, cancellationToken);
        }

        logger.LogInformation("Adjusted {Count} prices for tenant {TenantId}", goods.Count, job.TenantId);
    }
}

public static class JobEndpoints
{
    public static void MapJobEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/jobs").WithTags("Jobs");

        // POST /api/jobs/price-adjustment — enqueue a background price change for the current tenant
        group.MapPost("/price-adjustment", (PriceAdjustmentRequest req, PriceAdjustmentQueue queue, HttpContext http) =>
        {
            var job = queue.Enqueue(http.GetTenantId(), req.Category, req.Percent, req.ExplicitInvalidation);
            return Results.Accepted($"/api/jobs/{job.Id}", new { jobId = job.Id });
        });

        // GET /api/jobs/{id} — job status
        group.MapGet("/{id:guid}", (Guid id, PriceAdjustmentQueue queue) =>
        {
            var state = queue.GetState(id);
            return state is null ? Results.NotFound() : Results.Ok(new { id, state = state.ToString() });
        });
    }

    public record PriceAdjustmentRequest(string Category, decimal Percent, bool ExplicitInvalidation = true);
}
