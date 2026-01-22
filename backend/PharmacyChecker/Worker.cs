using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using PharmacyChecker.Models;
using System.Text.RegularExpressions;
using PharmacyChecker.Services;
using System.Text.Json;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ConfigLoader _loader;
    private readonly PharmacyScanner _scanner;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ChangeDetector _changeDetector;
    private readonly IConfiguration _config;
    public Worker(ILogger<Worker> logger, ConfigLoader loader, PharmacyScanner scanner, IHttpClientFactory httpFactory, IConfiguration config, ChangeDetector changeDetector)
    {
        _logger = logger;
        _loader = loader;
        _scanner = scanner;
        _httpFactory = httpFactory;
        _changeDetector = changeDetector;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PharmacyChecker worker starting.");

        var intervalSeconds = _config.GetValue<int?>("ScanIntervalSeconds") ?? 900;
        var runOnce = _config.GetValue<bool?>("RunOnce") ?? false;
        var products = _config.GetSection("Products").Get<string[]>() ?? Array.Empty<string>();

        while (!stoppingToken.IsCancellationRequested)
        {
                try
            {
                    var pharmacies = _loader.LoadAll("config/pharmacies").ToList();
                    _logger.LogInformation("Loaded {Count} pharmacies from config/pharmacies", pharmacies.Count);

                foreach (var p in pharmacies)
                {
                    foreach (var product in products)
                    {
                        _logger.LogInformation("Scanning {Pharmacy} for {Product}", p.Id, product);
                        var result = await _scanner.ScanAsync(p, product, stoppingToken);
                        
                        try
                        {
                            var client = _httpFactory.CreateClient("backend");

                            if (result.Raw != null && result.Raw.TryGetValue("rows", out var rowsObj) && rowsObj is List<Dictionary<string, string?>> rows)
                            {
                                foreach (var row in rows)
                                {
                                    var name = row.ContainsKey("name") ? row["name"] : null;
                                    var priceText = row.ContainsKey("priceText") ? row["priceText"] : null;
                                    var availText = row.ContainsKey("availabilityText") ? row["availabilityText"] : null;

                                    var slug = (name ?? "unknown").ToLowerInvariant();
                                    slug = Regex.Replace(slug, "[^a-z0-9]+", "-");
                                    slug = slug.Trim('-');
                                    var perPharmacyId = $"{p.Id}:{slug}";

                                    string status = "not_found";
                                    if (!string.IsNullOrEmpty(availText) && (availText.ToLowerInvariant().Contains("na sklade") || availText.ToLowerInvariant().Contains("skladom") || availText.ToLowerInvariant().Contains("áno") || availText.ToLowerInvariant().Contains(">0")))
                                    {
                                        status = "ok";
                                    }

                                    decimal? price = null;
                                    if (!string.IsNullOrEmpty(priceText))
                                    {
                                        var digits = new string(priceText.Where(c => char.IsDigit(c) || c == '.' || c == ',').ToArray());
                                        digits = digits.Replace(',', '.');
                                        if (decimal.TryParse(digits, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var pval))
                                        {
                                            price = pval;
                                        }
                                    }

                                    var details = new Dictionary<string, object?>
                                    {
                                        ["sourceName"] = name,
                                        ["priceText"] = priceText,
                                        ["availabilityText"] = availText,
                                        ["scannerRaw"] = result.Raw
                                    };

                                    var key = $"{perPharmacyId}:{product}";
                                    var hash = _changeDetector.ComputeHash(status, price, details);
                                    if (!_changeDetector.IsChangedAndUpdate(key, hash))
                                    {
                                        _logger.LogDebug("No change for {PharmacyRow}/{Product}, skipping POST", perPharmacyId, product);
                                        continue;
                                    }

                                    var payload = new
                                    {
                                        pharmacyId = perPharmacyId,
                                        product = product,
                                        timestamp = DateTime.UtcNow,
                                        status = status,
                                        price = price,
                                        details = details
                                    };

                                    try
                                    {
                                        var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload);
                                        _logger.LogInformation("Posting payload: {Payload}", payloadJson);
                                        var stateDir = Path.Combine(AppContext.BaseDirectory, "state");
                                        if (!Directory.Exists(stateDir)) Directory.CreateDirectory(stateDir);
                                        var outFile = Path.Combine(stateDir, "sent_payloads.ndjson");
                                        File.AppendAllText(outFile, payloadJson + Environment.NewLine);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogDebug(ex, "Failed to persist payload locally");
                                    }

                                    var resp = await client.PostAsJsonAsync("/internal/availability", payload, stoppingToken);
                                    if (!resp.IsSuccessStatusCode)
                                    {
                                        _logger.LogWarning("Backend returned {Status} for {PharmacyRow}/{Product}", resp.StatusCode, perPharmacyId, product);
                                    }
                                }
                            }
                            else
                            {
                                var key = $"{p.Id}:{product}";
                                var hash = _changeDetector.ComputeHash(result.Status, result.Price, result.Raw);
                                if (!_changeDetector.IsChangedAndUpdate(key, hash))
                                {
                                    _logger.LogDebug("No change for {Pharmacy}/{Product}, skipping POST", p.Id, product);
                                }
                                else
                                {
                                    var payload = new
                                    {
                                        pharmacyId = p.Id,
                                        product = product,
                                        timestamp = DateTime.UtcNow,
                                        status = result.Status,
                                        price = result.Price,
                                        details = result.Raw
                                    };

                                    try
                                    {
                                        var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload);
                                        _logger.LogInformation("Posting payload: {Payload}", payloadJson);
                                        var stateDir = Path.Combine(AppContext.BaseDirectory, "state");
                                        if (!Directory.Exists(stateDir)) Directory.CreateDirectory(stateDir);
                                        var outFile = Path.Combine(stateDir, "sent_payloads.ndjson");
                                        File.AppendAllText(outFile, payloadJson + Environment.NewLine);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogDebug(ex, "Failed to persist payload locally");
                                    }

                                    var resp = await client.PostAsJsonAsync("/internal/availability", payload, stoppingToken);
                                    if (!resp.IsSuccessStatusCode)
                                    {
                                        _logger.LogWarning("Backend returned {Status} for {Pharmacy}/{Product}", resp.StatusCode, p.Id, product);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to POST result for {Pharmacy}/{Product}", p.Id, product);
                        }

                        await Task.Delay(TimeSpan.FromSeconds(p.RateLimitSeconds ?? 2), stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker loop failed");
            }

            if (runOnce)
            {
                _logger.LogInformation("RunOnce set — exiting after single scan iteration.");
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }

        _logger.LogInformation("PharmacyChecker worker stopping.");
    }
}
