using System.Runtime.InteropServices.Marshalling;
using ePatientApi.Interfaces;
using Microsoft.Playwright;

namespace ePatientApi.Services
{
    public sealed class DoveraService : IDoveraService
    {
        private  const string DoveraUrl = "https://www.dovera.sk/overenia/overit-poistenca";

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
                        "--window-size=1920, 1080"
                    }
                });

                var context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    ViewportSize = new ViewportSize { Width = 1920, Height = 1080}
                });

                var page = await context.NewPageAsync();

                await page.GotoAsync(DoveraUrl, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 60_000
                });

                var input = page.Locator("#rodne_cislo");
                await input.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 10_000
                });
                await input.FillAsync(rC);

                await page.Locator("input[value='Overiť']").ClickAsync(new LocatorClickOptions
                {
                    Timeout = 10_000
                });

                var resultStrong = page.Locator("p.mb-xxsmall strong").First;

                await resultStrong.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 10_000
                });

                await page.WaitForFunctionAsync(
                    @"() => {
                        const el = document.querySelector('p.mb-xxsmall strong');
                        return el && el.textContent.trim().length > 0;
                        }",
                        null,
                        new PageWaitForFunctionOptions { Timeout = 10_000}
                );

                var resultText = (await resultStrong.InnerTextAsync()).Trim();
                Console.WriteLine($"[DOVERA] result text: {resultText}");

                if (resultText.Contains("Nie je poistencom", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (resultText.Contains("Je poistencom", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                Console.WriteLine("[DOVERA] unknown result text, returnin false.");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[DOVERA] Playwright error: " + ex.Message);
                return false;
            }
        }
    }
}