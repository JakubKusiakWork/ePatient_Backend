using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PharmacyChecker.Services;

public class ChangeDetector
{
    private readonly string _stateFile;
    private readonly ConcurrentDictionary<string, string> _hashes = new();

    public ChangeDetector()
    {
        _stateFile = Path.Combine(AppContext.BaseDirectory, "state", "last_hashes.json");
        Load();
    }

    private void Load()
    {
        try
        {
            var dir = Path.GetDirectoryName(_stateFile) ?? AppContext.BaseDirectory;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            if (File.Exists(_stateFile))
            {
                var json = File.ReadAllText(_stateFile);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (dict != null)
                {
                    foreach (var kv in dict)
                        _hashes[kv.Key] = kv.Value;
                }
            }
        }
        catch
        {
        }
    }

    private void Persist()
    {
        try
        {
            var dir = Path.GetDirectoryName(_stateFile) ?? AppContext.BaseDirectory;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var dict = _hashes.ToDictionary(kv => kv.Key, kv => kv.Value);
            File.WriteAllText(_stateFile, JsonSerializer.Serialize(dict));
        }
        catch
        {
        }
    }

    public string ComputeHash(string status, decimal? price, object? details)
    {
        var payload = new { status, price, details };
        var json = JsonSerializer.Serialize(payload);
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }

    public bool IsChangedAndUpdate(string key, string hash)
    {
        if (_hashes.TryGetValue(key, out var existing))
        {
            if (existing == hash)
                return false;
        }

        _hashes[key] = hash;
        Task.Run(() => Persist());
        return true;
    }
}
