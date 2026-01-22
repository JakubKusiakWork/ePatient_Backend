namespace PharmacyChecker.Models;

public class PharmacyConfig
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? SearchUrlTemplate { get; set; }
    public SelectorConfig? Selectors { get; set; }
    public int? RateLimitSeconds { get; set; }
    public List<string>? PreActions { get; set; }
    public List<NavigationStep>? NavigationFlow { get; set; }
    public NetworkConfig? Network { get; set; }
    public GeolocationConfig? Geolocation { get; set; }
    public MetadataConfig? Metadata { get; set; }
    public ExtractionConfig? Extraction { get; set; }
}

public class ExtractionConfig
{
    public string? IterateRows { get; set; }
    public Dictionary<string, FieldConfig>? Fields { get; set; }
    public string? FallbackIterateRows { get; set; }
    public Dictionary<string, FieldConfig>? FallbackFields { get; set; }
}

public class FieldConfig
{
    public string? Selector { get; set; }
    public string? Type { get; set; }
    public string? Description { get; set; }
}

public class MetadataConfig
{
    public bool? RequiresJavascript { get; set; }
    public bool? Spa { get; set; }
}

public class SelectorConfig
{
    public string? Title { get; set; }
    public string? Price { get; set; }
    public string? Availability { get; set; }
    public string? NoResults { get; set; }
    public string? Input { get; set; }
    public string? InputFallback { get; set; }
    public string? Results { get; set; }
    public string? ResultItem { get; set; }
    public string? ResultsTable { get; set; }
    public string? ResultRow { get; set; }
    public string? SuggestionIdAttribute { get; set; }
    public string? WaitForResponseRegex { get; set; }
    public bool? PersistAvailabilityApiJson { get; set; }
    public int? ScanTimeoutMs { get; set; }
}

public class ScanResult
{
    public string Status { get; set; } = "error";
    public decimal? Price { get; set; }
    public Dictionary<string, object?> Raw { get; set; } = new();
}

public class NavigationStep
{
    public string Action { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Selector { get; set; }
    public bool? ClearFirst { get; set; }
    public string? ValueFromInput { get; set; }
    public int? TimeoutMs { get; set; }
    public string? MatchRegex { get; set; }
    public ExtractAttribute? ExtractAttribute { get; set; }
    public string? Comment { get; set; }
    public string? Prompt { get; set; }
}

public class ExtractAttribute
{
    public string? Name { get; set; }
    public string? Attribute { get; set; }
}

public class NetworkConfig
{
    public string? WaitForResponseRegex { get; set; }
    public bool? PersistAvailabilityApiJson { get; set; }
}

public class GeolocationConfig
{
    public bool Enable { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Origin { get; set; }
}
