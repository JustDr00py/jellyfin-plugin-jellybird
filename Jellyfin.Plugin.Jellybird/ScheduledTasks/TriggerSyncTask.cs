using Jellyfin.Plugin.Jellybird.Client;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.Jellybird.ScheduledTasks;

/// <summary>
/// Asks jellybird to resync now. jellybird already resyncs on its own
/// interval (its own <c>sync.interval</c> config, default 10 minutes, plus
/// <c>run_on_start</c>) — this task is for on-demand or chained triggers
/// (e.g. right after a Jellyfin library scan), not a replacement for
/// jellybird's schedule. It returns as soon as jellybird acknowledges the
/// request (HTTP 202); it does not wait for the sync itself to finish.
/// </summary>
public class TriggerSyncTask : IScheduledTask
{
    private readonly IJellybirdClient _client;

    public TriggerSyncTask(IJellybirdClient client)
    {
        _client = client;
    }

    /// <inheritdoc />
    public string Name => "Trigger Sync";

    /// <inheritdoc />
    public string Key => "JellybirdTriggerSync";

    /// <inheritdoc />
    public string Description =>
        "Asks jellybird to resync now. Returns immediately once jellybird acknowledges " +
        "the request — jellybird already syncs on its own schedule independently of this task.";

    /// <inheritdoc />
    public string Category => "Jellybird";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        progress.Report(0);
        await _client.TriggerSyncAsync(cancellationToken).ConfigureAwait(false);
        progress.Report(100);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // No default trigger: jellybird already owns its own sync cadence.
        // Auto-enabling a Jellyfin-side cron too would just duplicate load
        // for no benefit. The admin opts in from Dashboard > Scheduled
        // Tasks, or fires it manually / chains it after a library scan.
        return [];
    }
}
