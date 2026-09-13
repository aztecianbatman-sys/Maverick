using System.Collections.Concurrent;
using System.Diagnostics;

namespace Maverick.Core;

public sealed record TrackedProcess(
    int Pid,
    string Name,
    string? ExecutablePath,
    DateTimeOffset StartedUtc);

public sealed class ProcessTracker
{
    private readonly ConcurrentDictionary<int, TrackedProcess> recent = new();

    public void Add(int pid, string name, string? executablePath)
    {
        recent[pid] = new TrackedProcess(pid, name, executablePath, DateTimeOffset.UtcNow);
        Trim();
    }

    public IReadOnlyList<TrackedProcess> Recent(TimeSpan age)
    {
        Trim();
        var cutoff = DateTimeOffset.UtcNow - age;
        return recent.Values
            .Where(x => x.StartedUtc >= cutoff)
            .OrderByDescending(x => x.StartedUtc)
            .ToList();
    }

    public void Remove(int pid) => recent.TryRemove(pid, out _);

    private void Trim()
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-5);
        foreach (var pair in recent)
        {
            if (pair.Value.StartedUtc < cutoff)
                recent.TryRemove(pair.Key, out _);
        }
    }
}
