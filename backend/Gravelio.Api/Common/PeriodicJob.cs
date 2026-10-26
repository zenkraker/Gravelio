using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Common;

/// A job that runs on a timer in the background. A PostgreSQL advisory lock makes sure only one replica runs a given job at a time:
/// the others skip that tick (https://www.postgresql.org/docs/current/explicit-locking.html#ADVISORY-LOCKS).
public abstract class PeriodicJob(IServiceScopeFactory scopes, TimeProvider time, ILogger logger) : BackgroundService
{
    protected TimeProvider Time => time;

    /// Identifies the job to the database lock; unique per job.
    protected abstract int LockKey { get; }

    protected abstract TimeSpan Interval { get; }

    protected virtual bool Enabled => true;

    protected abstract Task RunAsync(IServiceProvider services, CancellationToken ct);

    /// One pass, as the timer runs it. Public so tests can run it without waiting.
    public async Task RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        // The lock belongs to the connection, so keep one open for the whole pass.
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var connection = db.Database.GetDbConnection();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "select pg_try_advisory_lock(@key)";
                var key = command.CreateParameter();
                key.ParameterName = "key";
                key.Value = LockKey;
                command.Parameters.Add(key);
                if (await command.ExecuteScalarAsync(ct) is not true) return;
            }
            try
            {
                await RunAsync(scope.ServiceProvider, ct);
            }
            finally
            {
                await using var unlock = connection.CreateCommand();
                unlock.CommandText = $"select pg_advisory_unlock({LockKey})";
                await unlock.ExecuteScalarAsync(CancellationToken.None);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Enabled) return;
        using var timer = new PeriodicTimer(Interval, time);
        try
        {
            do
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    logger.LogError(e, "Background job {Job} failed", GetType().Name);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }
}
