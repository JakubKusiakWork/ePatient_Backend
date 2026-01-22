using ePatientApi.Interfaces;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using System.Text.RegularExpressions;
using System.Security.AccessControl;

namespace ePatientApi.Services
{
    public sealed class VsZPService : IVsZPService
    {
        private const string VszpUrl = "https://www.epobocka.com/ipep-web/#!/narokZS";

        public async Task<bool> CheckAsync(string birthNumber, DateTime date, CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(birthNumber))
            {
                return false;
            }

            var rC = birthNumber.Replace("/", "").Trim();

            try
            {
                using var playwright = await Playwright.CreateAsync();
                await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = true,
                    Args = new[]
                    {
                        "--no-sandbox",
                        "--disable-dev-shm-usage",
                        "--window-size=1920,1080"
                    }
                });

                var context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
                });

                var page = await context.NewPageAsync();

                await page.GotoAsync(VszpUrl, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.NetworkIdle,
                    Timeout = 30_000
                });

                await page.Locator("#rodneCislo").FillAsync(rC, new LocatorFillOptions
                {
                    Timeout = 10_000
                });

                await page.GetByRole(AriaRole.Button, new() { Name = "Overiť" })
                    .ClickAsync(new LocatorClickOptions { Timeout = 10_000 });

                var resultLocator = page.Locator("div.third:has(label:text-is('Poistený:')) + div.twothird label.ng-binding");

                await Expect(resultLocator).ToHaveTextAsync(new Regex(@"\S"), new() { Timeout = 10_000 });
                
                var rawText = (await resultLocator.InnerTextAsync()).Trim();
                var lower = rawText.ToLowerInvariant();

                Console.WriteLine($"[VSZP] result text: {rawText}");

                if(lower.Contains("áno"))
                {
                    return true;
                }

                if (lower.Contains("nie"))
                {
                    return false;
                }

                Console.WriteLine("[VSZP] unknown result text, returnin false.");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[VSZP] Playwright error: " + ex.Message);
                return false;
            }
        }
    }
}
