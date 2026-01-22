using ePatientApi.Interfaces;
using Microsoft.Playwright;

namespace ePatientApi.Services
{
    public sealed class UnionService : IUnionService
    {
        private const string UnionUrl = "https://portal.unionzp.sk/pub/overenie-poistneho-vztahu";

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
                    ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
                });

                var page = await context.NewPageAsync();

                await page.GotoAsync(UnionUrl, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.NetworkIdle,
                    Timeout = 30_000
                });

                var input = page.Locator("input.v-field__input").First;
                await input.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10_000 });
                await input.FillAsync(rC);

                await page.Locator("span.v-btn__content", new PageLocatorOptions { HasTextString = "Overiť" })
                    .First
                    .ClickAsync(new LocatorClickOptions { Timeout = 10_000 });

                var result = page.Locator("div.v-row.align-center.justify-center h3").First;
                await result.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10_000 });

                await page.WaitForFunctionAsync(
                    @"() => {
                        const el = document.querySelector('div.v-row.align-center.justify-center h3');
                        return el && el.innerText.trim().length > 0;
                        }",
                        null,
                        new PageWaitForFunctionOptions { Timeout = 10_000 }
                );

                var rawText = (await result.InnerTextAsync()).Trim();
                var lower = rawText.ToLowerInvariant();

                Console.WriteLine($"[UNION] result text: {rawText}");

                if (lower.Contains("nie je poistenec"))
                {
                    return false;
                }

                if (lower.Contains("je poistenec"))
                {
                    return true;
                }

                Console.WriteLine("[UNION] unknown result text, returning false.");
                return false;
            }

            catch (Exception ex)
            {
                Console.WriteLine("[UNION] Playwright error: " + ex.Message);
                return false;
            }
        }
    }
}