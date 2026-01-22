using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Microsoft.Extensions.Logging;
using PharmacyChecker.Models;

namespace PharmacyChecker.Services;

public class ConfigLoader
{
    private readonly IDeserializer _deserializer;
    private readonly ILogger<ConfigLoader> _logger;

    public ConfigLoader(ILogger<ConfigLoader> logger)
    {
        _logger = logger;
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public IEnumerable<PharmacyConfig> LoadAll(string folderPath)
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, folderPath);
        _logger.LogInformation("ConfigLoader using base path: {BasePath}", basePath);
        if (!Directory.Exists(basePath))
        {
            basePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), folderPath));
            _logger.LogInformation("ConfigLoader fallback base path: {BasePath}", basePath);
        }

        if (!Directory.Exists(basePath))
            return Array.Empty<PharmacyConfig>();

        var files = Directory.GetFiles(basePath, "*.yaml")
            .Where(f => !Path.GetFileName(f).Contains(".disabled") && !Path.GetFileName(f).StartsWith("sample", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        _logger.LogInformation("ConfigLoader found {Count} yaml file(s) in {BasePath}", files.Length, basePath);
        var list = new List<PharmacyConfig>();
        foreach (var f in files)
        {
            try
            {
                var yaml = File.ReadAllText(f);
                var cfg = _deserializer.Deserialize<PharmacyConfig>(yaml);
                if (cfg != null)
                {
                    list.Add(cfg);
                    _logger.LogInformation("Successfully loaded config: {ConfigId} from {File}", cfg.Id ?? "unknown", Path.GetFileName(f));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load config from {File}", Path.GetFileName(f));
            }
        }

        return list;
    }
}
