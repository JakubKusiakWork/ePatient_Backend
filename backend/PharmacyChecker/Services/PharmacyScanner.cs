using Microsoft.Playwright;
using PharmacyChecker.Models;
using Microsoft.Extensions.Logging;

namespace PharmacyChecker.Services;

public class PharmacyScanner
{
    private readonly ILogger<PharmacyScanner> _logger;

    public PharmacyScanner(ILogger<PharmacyScanner> logger)
    {
        _logger = logger;
    }

    public async Task<ScanResult> ScanAsync(PharmacyConfig pharmacy, string product, CancellationToken ct = default)
    {
        var result = new ScanResult();

        try
        {
            using var playwright = await Playwright.CreateAsync();
            var headless = true;
            var envHeadless = Environment.GetEnvironmentVariable("PHARMACY_HEADLESS");
            if (!string.IsNullOrEmpty(envHeadless) && envHeadless.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                headless = false;
            }

            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = headless,
                Args = new[] { 
                    "--no-sandbox", 
                    "--disable-setuid-sandbox",
                    "--disable-blink-features=AutomationControlled",
                    "--disable-dev-shm-usage",
                    "--disable-web-security",
                    "--disable-features=IsolateOrigins,site-per-process"
                }
            });

            var contextOptions = new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
                Locale = "sk-SK",
                TimezoneId = "Europe/Bratislava"
            };

            try
            {
                if (pharmacy.Geolocation?.Enable == true && pharmacy.Geolocation.Latitude.HasValue && pharmacy.Geolocation.Longitude.HasValue)
                {
                    contextOptions.Geolocation = new Geolocation { Latitude = (float)pharmacy.Geolocation.Latitude.Value, Longitude = (float)pharmacy.Geolocation.Longitude.Value };
                    contextOptions.Permissions = new[] { "geolocation" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to configure geolocation for {Id}", pharmacy.Id);
            }

            var context = await browser.NewContextAsync(contextOptions);

            var page = await context.NewPageAsync();
            
            await page.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
            {
                ["Accept-Language"] = "sk-SK,sk;q=0.9,en-US;q=0.8,en;q=0.7",
                ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8",
                ["Accept-Encoding"] = "gzip, deflate, br",
                ["DNT"] = "1",
                ["Upgrade-Insecure-Requests"] = "1",
                ["Sec-Fetch-Dest"] = "document",
                ["Sec-Fetch-Mode"] = "navigate",
                ["Sec-Fetch-Site"] = "none",
                ["Sec-Fetch-User"] = "?1"
            });
            
            await page.AddInitScriptAsync(@"
                Object.defineProperty(navigator, 'webdriver', { get: () => false });
                Object.defineProperty(navigator, 'plugins', { get: () => [1, 2, 3, 4, 5] });
                Object.defineProperty(navigator, 'languages', { get: () => ['sk-SK', 'sk', 'en-US', 'en'] });
                window.chrome = { runtime: {} };
                Object.defineProperty(navigator, 'permissions', {
                    get: () => ({
                        query: () => Promise.resolve({ state: 'granted' })
                    })
                });
            ");

            string url = pharmacy.SearchUrlTemplate?.Replace("{query}", Uri.EscapeDataString(product)) ?? product;

            try
            {
                var homepageUrl = new Uri(url).GetLeftPart(UriPartial.Authority);
                await page.GotoAsync(homepageUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 10000 });
                await Task.Delay(new Random().Next(2000, 4000));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to visit homepage first for {Product}", product);
            }

            await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 15000 });
            
            await Task.Delay(1000);
            
            var pageContent = await page.ContentAsync();
            if (pageContent.Contains("Error 500") || pageContent.Contains("Error 403") || pageContent.Contains("Access Denied"))
            {
                _logger.LogWarning("Detected error page for {Product}: {Url}", product, page.Url);
                result.Status = "error";
                result.Raw["error"] = "Website returned error page (possible bot detection)";
                await browser.CloseAsync();
                return result;
            }

            var defaultTimeout = 8000;
            var timeoutMs = pharmacy.Selectors?.ScanTimeoutMs ?? defaultTimeout;

            if (pharmacy.NavigationFlow != null && pharmacy.NavigationFlow.Count > 0)
            {
                foreach (var step in pharmacy.NavigationFlow)
                {
                    var act = (step.Action ?? string.Empty).ToLowerInvariant();
                    try
                    {
                        switch (act)
                        {
                            case "navigate":
                                if (!string.IsNullOrEmpty(step.Url))
                                {
                                    await page.GotoAsync(step.Url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 30000 });
                                }
                                break;
                            case "waitforselector":
                            case "wait_for_selector":
                                if (!string.IsNullOrEmpty(step.Selector))
                                {
                                    await page.WaitForSelectorAsync(step.Selector, new PageWaitForSelectorOptions { Timeout = step.TimeoutMs ?? timeoutMs });
                                }
                                break;
                            case "type":
                                if (!string.IsNullOrEmpty(step.Selector))
                                {
                                    var value = string.Empty;
                                    if (!string.IsNullOrEmpty(step.ValueFromInput) && step.ValueFromInput == "searchTerm") value = product;
                                    if (step.ClearFirst == true)
                                        await page.FillAsync(step.Selector, "");
                                    try
                                    {
                                        await page.Locator(step.Selector).PressSequentiallyAsync(value, new LocatorPressSequentiallyOptions { Delay = 50 });
                                    }
                                    catch
                                    {
                                        await page.FillAsync(step.Selector, value);
                                    }
                                }
                                break;
                            case "click":
                                if (!string.IsNullOrEmpty(step.Selector))
                                {
                                    var el = await page.QuerySelectorAsync(step.Selector);
                                    if (el != null)
                                    {
                                        await el.ClickAsync();
                                        if (step.ExtractAttribute != null && !string.IsNullOrEmpty(step.ExtractAttribute.Attribute))
                                        {
                                            try
                                            {
                                                var attr = await el.GetAttributeAsync(step.ExtractAttribute.Attribute);
                                                if (!string.IsNullOrEmpty(step.ExtractAttribute.Name) && attr != null)
                                                    result.Raw[step.ExtractAttribute.Name] = attr;
                                            }
                                            catch { }
                                        }
                                    }
                                }
                                break;
                            case "waitforresponse":
                            case "wait_for_response":
                                {
                                    System.Text.RegularExpressions.Regex? rx = null;
                                    var pat = step.MatchRegex ?? pharmacy.Network?.WaitForResponseRegex ?? pharmacy.Selectors?.WaitForResponseRegex;
                                    if (!string.IsNullOrEmpty(pat))
                                    {
                                        var patTrim = pat.Trim();
                                        if (patTrim.StartsWith("/") && patTrim.EndsWith("/")) patTrim = patTrim.Substring(1, patTrim.Length - 2);
                                        try { rx = new System.Text.RegularExpressions.Regex(patTrim, System.Text.RegularExpressions.RegexOptions.Compiled); } catch { }
                                    }

                                    try
                                    {
                                        var response = await page.WaitForResponseAsync(r =>
                                        {
                                            try
                                            {
                                                if (rx != null) return rx.IsMatch(r.Url) && r.Status == 200;
                                                return r.Url.Contains("/api/public/product/") && r.Url.Contains("/availability") && r.Status == 200;
                                            }
                                            catch { return false; }
                                        }, new PageWaitForResponseOptions { Timeout = step.TimeoutMs ?? timeoutMs });

                                        if (response != null)
                                        {
                                            try
                                            {
                                                var text = await response.TextAsync();
                                                if (!string.IsNullOrEmpty(text))
                                                {
                                                    if (pharmacy.Network?.PersistAvailabilityApiJson == true || pharmacy.Selectors?.PersistAvailabilityApiJson == true)
                                                        result.Raw["availabilityApiJsonRaw"] = text;

                                                    try
                                                    {
                                                        var doc = System.Text.Json.JsonDocument.Parse(text);
                                                        result.Raw["availabilityApiJson"] = doc.RootElement.Clone();
                                                    }
                                                    catch { }
                                                }
                                            }
                                            catch { }
                                        }
                                    }
                                    catch { }
                                }
                                break;
                            default:
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Navigation step failed for {Id}: {Action}", pharmacy.Id, step.Action);
                    }
                }

                if (!string.IsNullOrEmpty(pharmacy.Selectors?.ResultsTable) && !string.IsNullOrEmpty(pharmacy.Selectors?.ResultRow))
                {
                    try { await page.WaitForSelectorAsync(pharmacy.Selectors.ResultsTable, new PageWaitForSelectorOptions { Timeout = timeoutMs }); } catch { }

                    var rows = await page.QuerySelectorAllAsync(pharmacy.Selectors.ResultRow);
                    var rowList = new List<Dictionary<string, string?>>();
                    if (rows != null)
                    {
                        foreach (var r in rows)
                        {
                            try
                            {
                                var nameEl = await r.QuerySelectorAsync(pharmacy.Selectors.Title ?? "td.name");
                                var priceEl = await r.QuerySelectorAsync(pharmacy.Selectors.Price ?? "td.price");
                                var availEl = await r.QuerySelectorAsync(pharmacy.Selectors.Availability ?? "td.stock");

                                var name = nameEl != null ? (await nameEl.InnerTextAsync())?.Trim() : null;
                                var rowPriceText = priceEl != null ? (await priceEl.InnerTextAsync())?.Trim() : null;
                                var rowAvailText = availEl != null ? (await availEl.InnerTextAsync())?.Trim() : null;

                                rowList.Add(new Dictionary<string, string?> {
                                    ["name"] = name,
                                    ["priceText"] = rowPriceText,
                                    ["availabilityText"] = rowAvailText
                                });
                            }
                            catch { }
                        }
                    }
                    result.Raw["rows"] = rowList;
                }
            }

            if (!string.IsNullOrEmpty(pharmacy.Selectors?.Input))
            {
                try
                {
                    var inputSel = pharmacy.Selectors.Input;
                    try
                    {
                        await page.WaitForSelectorAsync(inputSel, new PageWaitForSelectorOptions { Timeout = timeoutMs });
                    }
                    catch
                    {
                        if (!string.IsNullOrEmpty(pharmacy.Selectors.InputFallback))
                        {
                            try
                            {
                                await page.WaitForSelectorAsync(pharmacy.Selectors.InputFallback, new PageWaitForSelectorOptions { Timeout = timeoutMs });
                                inputSel = pharmacy.Selectors.InputFallback;
                            }
                            catch { }
                        }
                    }

                    if (!string.IsNullOrEmpty(pharmacy.Selectors.Results) && !string.IsNullOrEmpty(pharmacy.Selectors.ResultItem))
                    {
                        try
                        {
                            await page.Locator(inputSel).PressSequentiallyAsync(product, new LocatorPressSequentiallyOptions { Delay = 50 });
                        }
                        catch
                        {
                            await page.FillAsync(inputSel, product);
                        }

                        try { await page.WaitForSelectorAsync(pharmacy.Selectors.Results, new PageWaitForSelectorOptions { Timeout = timeoutMs }); } catch { }

                        var items = await page.QuerySelectorAllAsync(pharmacy.Selectors.ResultItem);
                        if (items != null && items.Count > 0)
                        {
                            IElementHandle? pick = null;
                            foreach (var it in items)
                            {
                                try
                                {
                                    var t = (await it.InnerTextAsync())?.Trim() ?? string.Empty;
                                    if (!string.IsNullOrEmpty(t) && t.Contains(product, StringComparison.OrdinalIgnoreCase))
                                    {
                                        pick = it;
                                        break;
                                    }
                                }
                                catch { }
                            }
                            pick ??= items.FirstOrDefault();
                            if (pick != null)
                            {
                                try
                                {
                                    await pick.ClickAsync();
                                }
                                catch
                                {
                                    try
                                    {
                                        var inputEl = await page.QuerySelectorAsync(inputSel);
                                        if (inputEl != null)
                                        {
                                            await inputEl.FocusAsync();
                                            await page.Keyboard.PressAsync("ArrowDown");
                                            await page.Keyboard.PressAsync("Enter");
                                        }
                                    }
                                    catch { }
                                }

                                try
                                {
                                    var attrName = pharmacy.Selectors?.SuggestionIdAttribute ?? "data-id";
                                    var prodId = await pick.GetAttributeAsync(attrName);
                                    if (string.IsNullOrEmpty(prodId))
                                        prodId = await pick.GetAttributeAsync("data-product-id");
                                    if (!string.IsNullOrEmpty(prodId))
                                        result.Raw["selectedProductId"] = prodId;
                                }
                                    catch { }
                                }

                            try
                            {
                                System.Text.RegularExpressions.Regex? rx = null;
                                if (!string.IsNullOrEmpty(pharmacy.Selectors?.WaitForResponseRegex))
                                {
                                    var pat = pharmacy.Selectors.WaitForResponseRegex.Trim();
                                    if (pat.StartsWith("/") && pat.EndsWith("/")) pat = pat.Substring(1, pat.Length - 2);
                                    try { rx = new System.Text.RegularExpressions.Regex(pat, System.Text.RegularExpressions.RegexOptions.Compiled); } catch { rx = null; }
                                }

                                var response = await page.WaitForResponseAsync(r =>
                                {
                                    try
                                    {
                                        if (rx != null) return rx.IsMatch(r.Url) && r.Status == 200;
                                        return r.Url.Contains("/api/public/product/") && r.Url.Contains("/availability") && r.Status == 200;
                                    }
                                    catch { return false; }
                                }, new PageWaitForResponseOptions { Timeout = timeoutMs });

                                if (response != null)
                                {
                                    try
                                    {
                                        var text = await response.TextAsync();
                                        if (!string.IsNullOrEmpty(text))
                                        {
                                            if (pharmacy.Selectors?.PersistAvailabilityApiJson == true)
                                                result.Raw["availabilityApiJsonRaw"] = text;

                                            try
                                            {
                                                var doc = System.Text.Json.JsonDocument.Parse(text);
                                                result.Raw["availabilityApiJson"] = doc.RootElement.Clone();
                                            }
                                            catch { }
                                        }
                                    }
                                    catch { }
                                }
                            }
                            catch { }

                            if (!string.IsNullOrEmpty(pharmacy.Selectors?.ResultsTable) && !string.IsNullOrEmpty(pharmacy.Selectors?.ResultRow))
                            {
                                try { await page.WaitForSelectorAsync(pharmacy.Selectors.ResultsTable!, new PageWaitForSelectorOptions { Timeout = timeoutMs }); } catch { }

                                var rows = await page.QuerySelectorAllAsync(pharmacy.Selectors.ResultRow);
                                var rowList = new List<Dictionary<string, string?>>();
                                if (rows != null)
                                {
                                    foreach (var r in rows)
                                    {
                                        try
                                        {
                                            var nameEl = await r.QuerySelectorAsync(pharmacy.Selectors.Title ?? "td.name");
                                            var priceEl = await r.QuerySelectorAsync(pharmacy.Selectors.Price ?? "td.price");
                                            var availEl = await r.QuerySelectorAsync(pharmacy.Selectors.Availability ?? "td.stock");

                                            var name = nameEl != null ? (await nameEl.InnerTextAsync())?.Trim() : null;
                                            var rowPriceText = priceEl != null ? (await priceEl.InnerTextAsync())?.Trim() : null;
                                            var rowAvailText = availEl != null ? (await availEl.InnerTextAsync())?.Trim() : null;

                                            rowList.Add(new Dictionary<string, string?> {
                                                ["name"] = name,
                                                ["priceText"] = rowPriceText,
                                                ["availabilityText"] = rowAvailText
                                            });
                                        }
                                        catch { }
                                    }
                                }
                                result.Raw["rows"] = rowList;
                            }
                        }
                    }
                    else
                    {
                        await page.FillAsync(inputSel, product);
                        try
                        {
                            var inputEl = await page.QuerySelectorAsync(inputSel);
                            if (inputEl != null)
                            {
                                await inputEl.PressAsync("Enter");
                                try { await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = timeoutMs }); } catch { }
                            }
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Typeahead flow failed for {Id}", pharmacy.Id);
                }
            }

            if (pharmacy.Extraction != null && !string.IsNullOrEmpty(pharmacy.Extraction.IterateRows))
            {
                try
                {
                    var currentUrl = page.Url;
                    var isSearchResultsPage = currentUrl.Contains("/search") || 
                                             currentUrl.Contains("/vyhladavanie") ||
                                             currentUrl.Contains("?q=") ||
                                             currentUrl.Contains("/podla-ucinnej-latky");
                    
                    var isProductDetailPage = !isSearchResultsPage && 
                                             !currentUrl.EndsWith(".sk/") && 
                                             !currentUrl.Contains("kategoria");
                    
                    if (isProductDetailPage)
                    {
                        _logger.LogInformation("Detected product detail page: {Url}, treating as single product result", currentUrl);
                    }
                    else
                    {
                        await page.WaitForSelectorAsync(pharmacy.Extraction.IterateRows, new PageWaitForSelectorOptions { Timeout = 3000 });
                    
                    var rows = await page.QuerySelectorAllAsync(pharmacy.Extraction.IterateRows);
                    var rowList = new List<Dictionary<string, string?>>();
                    
                    if (rows != null && rows.Count > 0)
                    {
                        _logger.LogInformation("Found {Count} product rows for {Pharmacy}", rows.Count, pharmacy.Id);
                        
                        foreach (var row in rows)
                        {
                            var rowData = new Dictionary<string, string?>();
                            
                            if (pharmacy.Extraction.Fields != null)
                            {
                                foreach (var field in pharmacy.Extraction.Fields)
                                {
                                    try
                                    {
                                        var fieldEl = await row.QuerySelectorAsync(field.Value?.Selector ?? "");
                                        if (fieldEl != null)
                                        {
                                            var value = field.Value?.Type?.ToLower() == "href" 
                                                ? await fieldEl.GetAttributeAsync("href")
                                                : await fieldEl.InnerTextAsync();
                                            rowData[field.Key] = value?.Trim() ?? string.Empty;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogDebug(ex, "Failed to extract field {Field}", field.Key);
                                    }
                                }
                            }
                            
                            if (rowData.Count > 0)
                            {
                                rowList.Add(rowData);
                            }
                        }
                        
                        result.Raw["extractedRows"] = rowList;
                        result.Raw["rowCount"] = rowList.Count;
                        
                        if (rowList.Count > 0)
                        {
                            var firstRow = rowList[0];
                            result.Raw["title"] = firstRow.GetValueOrDefault("title");
                            result.Raw["priceText"] = firstRow.GetValueOrDefault("price");
                            result.Raw["availabilityText"] = firstRow.GetValueOrDefault("stockStatus");
                            
                            var stockText = firstRow.GetValueOrDefault("stockStatus")?.ToLowerInvariant() ?? "";
                            if (stockText.Contains("skladom") || stockText.Contains("na sklade") || stockText.Contains("dostupn"))
                            {
                                result.Status = "ok";
                            }
                            else if (stockText.Contains("nie je") || stockText.Contains("nedostupn"))
                            {
                                result.Status = "not_found";
                            }
                            else if (!string.IsNullOrEmpty(firstRow.GetValueOrDefault("title")))
                            {
                                result.Status = "ok";
                            }
                        }
                    }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Extraction failed for {Pharmacy}, falling back to legacy selectors", pharmacy.Id);
                }
            }
            
            if (result.Raw.ContainsKey("title") && !string.IsNullOrEmpty(result.Raw["title"]?.ToString()))
            {
                _logger.LogInformation("Extraction succeeded, found product: {Title}", result.Raw["title"]);
            }
            else
            {
                if (!string.IsNullOrEmpty(pharmacy.Selectors?.NoResults))
                {
                    var noRes = await page.QuerySelectorAsync(pharmacy.Selectors.NoResults);
                    if (noRes != null)
                    {
                        result.Status = "not_found";
                        result.Raw["note"] = "no_results_selector_matched";
                        await browser.CloseAsync();
                        return result;
                    }
                }
                
                var noResultsPageContent = await page.ContentAsync();
                var lowerContent = noResultsPageContent.ToLowerInvariant();
                if (lowerContent.Contains("nenašli sme žiadne") || 
                    lowerContent.Contains("nenašli sa žiadne") ||
                    lowerContent.Contains("žiadne výsledky") ||
                    lowerContent.Contains("no results") ||
                    lowerContent.Contains("nenájdené"))
                {
                    result.Status = "not_found";
                    result.Raw["note"] = "no_results_text_found";
                    await browser.CloseAsync();
                    return result;
                }
                
                if (!result.Raw.ContainsKey("title"))
            {
                if (!string.IsNullOrEmpty(pharmacy.Selectors?.Title))
                {
                    try
                    {
                        await page.WaitForSelectorAsync(pharmacy.Selectors.Title, new PageWaitForSelectorOptions { Timeout = 2000 });
                    }
                    catch { }
                }
                
                string? legacyTitle = null;
                if (!string.IsNullOrEmpty(pharmacy.Selectors?.Title))
                {
                    var el = await page.QuerySelectorAsync(pharmacy.Selectors.Title);
                    if (el != null)
                        legacyTitle = (await el.InnerTextAsync())?.Trim();
                }

                string? legacyPriceText = null;
                if (!string.IsNullOrEmpty(pharmacy.Selectors?.Price))
                {
                    var el = await page.QuerySelectorAsync(pharmacy.Selectors.Price);
                    if (el != null)
                        legacyPriceText = (await el.InnerTextAsync())?.Trim();
                }

                string? legacyAvailText = null;
                if (!string.IsNullOrEmpty(pharmacy.Selectors?.Availability))
                {
                    var el = await page.QuerySelectorAsync(pharmacy.Selectors.Availability);
                    if (el != null)
                        legacyAvailText = (await el.InnerTextAsync())?.Trim();
                }

                result.Raw["title"] = legacyTitle;
                result.Raw["priceText"] = legacyPriceText;
                result.Raw["availabilityText"] = legacyAvailText;
                }
            }

            var title = result.Raw.GetValueOrDefault("title")?.ToString();
            var priceText = result.Raw.GetValueOrDefault("priceText")?.ToString();
            var availText = result.Raw.GetValueOrDefault("availabilityText")?.ToString();

            _logger.LogInformation("Extraction results for {Product}: title='{Title}', price='{Price}', avail='{Avail}'", 
                product, title ?? "null", priceText ?? "null", availText ?? "null");

            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(priceText))
            {
                result.Status = "not_found";
                result.Raw["note"] = "missing_title_or_price";
                result.Raw["pageUrl"] = page.Url;
                _logger.LogWarning("Missing title or price for {Product} at {Url}. Tried selectors: title={TitleSel}, price={PriceSel}", 
                    product, page.Url, pharmacy.Selectors?.Title, pharmacy.Selectors?.Price);
                await browser.CloseAsync();
                return result;
            }
            
            var searchLower = product.ToLowerInvariant();
            var titleLower = title.ToLowerInvariant();
            
            var searchKey = searchLower
                .Replace("tablety", "").Replace("tablet", "")
                .Replace("kapsula", "").Replace("kapsuly", "")
                .Replace("mg", "").Replace("ml", "")
                .Trim();
            
            var isRelevant = titleLower.Contains(searchLower) || 
                           searchLower.Contains(titleLower) ||
                           (searchKey.Length >= 4 && titleLower.Contains(searchKey));
            
            if (!isRelevant)
            {
                result.Status = "not_found";
                result.Raw["note"] = "product_name_mismatch";
                _logger.LogInformation("Product mismatch: searched for '{Search}' (key: '{Key}') but found '{Title}'", product, searchKey, title);
                await browser.CloseAsync();
                return result;
            }

            if (!string.IsNullOrEmpty(availText) && (availText.ToLowerInvariant().Contains("nie je") || availText.ToLowerInvariant().Contains("zadne") || availText.ToLowerInvariant().Contains("nedostupn")))
            {
                result.Status = "not_found";
            }
            else if (!string.IsNullOrEmpty(availText) && (availText.ToLowerInvariant().Contains("na sklade") || availText.ToLowerInvariant().Contains("skladom") || availText.ToLowerInvariant().Contains("dostupn")))
            {
                result.Status = "ok";
            }
            else
            {
                result.Status = "ok";
            }

            if (!string.IsNullOrEmpty(priceText))
            {
                var digits = new string(priceText.Where(c => char.IsDigit(c) || c == '.' || c == ',').ToArray());
                digits = digits.Replace(',', '.');
                if (decimal.TryParse(digits, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var p))
                {
                    result.Price = p;
                }
            }

            await browser.CloseAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scan failed for {Id}", pharmacy.Id);
            result.Status = "error";
            result.Raw["error"] = ex.Message;
        }

        return result;
    }

    public async Task<Dictionary<string, object>> CheckSingleProductAsync(string product, string location, string backendApiUrl, ConfigLoader configLoader)
    {
        _logger.LogInformation("On-demand scan requested for product: {Product}, location: {Location}", product, location);

        var configs = configLoader.LoadAll("config/pharmacies")
            .Where(c => c.Id == "pilulka")
            .ToList();
        var results = new List<Dictionary<string, object>>();

        foreach (var config in configs)
        {
            try
            {
                var scanResult = await ScanAsync(config, product, CancellationToken.None);

                if (scanResult.Status == "ok" || scanResult.Status == "not_found")
                {
                    results.Add(new Dictionary<string, object>
                    {
                        ["pharmacyId"] = config.Id ?? "unknown",
                        ["status"] = scanResult.Status,
                        ["price"] = scanResult.Price ?? 0m,
                        ["details"] = scanResult.Raw
                    });

                    try
                    {
                        using var httpClient = new HttpClient { BaseAddress = new Uri(backendApiUrl) };
                        var payload = new
                        {
                            productName = product.ToLowerInvariant(),
                            pharmacyExternalId = config.Id,
                            status = scanResult.Status,
                            price = scanResult.Price,
                            details = scanResult.Raw
                        };

                        var response = await httpClient.PostAsJsonAsync("/api/pharmacy/availability", payload);
                        if (response.IsSuccessStatusCode)
                        {
                            _logger.LogInformation("Successfully posted availability to backend for {Product} at {Pharmacy}", product, config.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to post availability to backend for {Product}", product);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to scan {Pharmacy} for product {Product}", config.Id, product);
                results.Add(new Dictionary<string, object>
                {
                    ["pharmacyId"] = config.Id ?? "unknown",
                    ["status"] = "error",
                    ["error"] = ex.Message
                });
            }
        }

        return new Dictionary<string, object>
        {
            ["product"] = product,
            ["location"] = location,
            ["scannedPharmacies"] = results.Count,
            ["results"] = results
        };
    }

    private static int LevenshteinDistance(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1)) return s2?.Length ?? 0;
        if (string.IsNullOrEmpty(s2)) return s1.Length;

        var d = new int[s1.Length + 1, s2.Length + 1];

        for (int i = 0; i <= s1.Length; i++)
            d[i, 0] = i;
        for (int j = 0; j <= s2.Length; j++)
            d[0, j] = j;

        for (int i = 1; i <= s1.Length; i++)
        {
            for (int j = 1; j <= s2.Length; j++)
            {
                int cost = (s2[j - 1] == s1[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[s1.Length, s2.Length];
    }
}
