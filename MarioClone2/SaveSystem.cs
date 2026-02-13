using System.Text.Json;

namespace MarioClone2;

// Persisted run-state snapshot used for lightweight milestone saves.
internal sealed class SaveData
{
    // Increment when save shape/semantics change in a non-backward-compatible way.
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public int UnlockedLevelIndex { get; set; }
    public int CurrentLevelIndex { get; set; }
    public int Score { get; set; }
    public int CoinCount { get; set; }
    public int Lives { get; set; } = 3;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

    // Produces a known-safe baseline so load failures never block startup.
    public static SaveData CreateDefault()
    {
        return new SaveData
        {
            Version = CurrentVersion,
            UnlockedLevelIndex = 0,
            CurrentLevelIndex = 0,
            Score = 0,
            CoinCount = 0,
            Lives = 3,
            LastUpdatedUtc = DateTime.UtcNow
        };
    }
}

// File-backed save helper with defensive load/save behavior.
internal static class SaveStore
{
    // Human-readable JSON is acceptable here because writes are infrequent milestones.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static SaveData LoadOrDefault()
    {
        var path = GetSavePath();
        if (!File.Exists(path))
        {
            return SaveData.CreateDefault();
        }

        try
        {
            var json = File.ReadAllText(path);
            var save = JsonSerializer.Deserialize<SaveData>(json, JsonOptions);
            return save ?? SaveData.CreateDefault();
        }
        catch
        {
            // Corrupt/partial files are ignored so the game can still boot.
            return SaveData.CreateDefault();
        }
    }

    public static void Save(SaveData data)
    {
        try
        {
            data.Version = SaveData.CurrentVersion;
            data.LastUpdatedUtc = DateTime.UtcNow;

            var path = GetSavePath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(path, json);
        }
        catch
        {
            // Keep save failures non-fatal.
        }
    }

    public static string GetSavePath()
    {
        // Store under LocalAppData so saves are user-scoped and writable without admin rights.
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "MarioClone2", "save.json");
    }
}
