# PharmacyChecker

Playwright-based pharmacy availability checker with live web scraping and REST API.

## Overview

.NET 9 ASP.NET Core Web API service that:
- Scrapes pharmacy websites using Playwright/Chromium
- Provides `/api/scrape` endpoint for on-demand medication availability checks
- Posts availability data to ePatientApi backend
- Supports background scheduled scanning (optional)

## Quick Start (Local)

```powershell
cd backend/PharmacyChecker
dotnet restore
dotnet build
dotnet run
```

Service runs on: `http://localhost:5003`

## Configuration

### appsettings.json

```json
{
  "BackendApiUrl": "http://localhost:5000",
  "ScanIntervalSeconds": 3600,
  "Products": [],
  "Urls": "http://localhost:5003"
}
```

- **BackendApiUrl**: ePatientApi endpoint for posting availability data
- **ScanIntervalSeconds**: Background scan interval (3600 = 1 hour)
- **Products**: Array of products for automatic scanning (empty = API-only mode)
- **Urls**: Service listening address

### Pharmacy Configurations

Add YAML files in `config/pharmacies/`:

**Example: pilulka.yaml**
```yaml
id: "pilulka"
name: "Pilulka.sk"
searchUrlTemplate: "https://www.pilulka.sk/search?q={query}"
rateLimitSeconds: 5
selectors:
  title: ".product-name, h1"
  price: ".price, .product-price"
  availability: ".availability, .stock-status"
extraction:
  iterateRows: ".product-item"
  fields:
    title:
      selector: ".product-name"
      type: "innerText"
```

## API Endpoints

### POST /api/scrape

Trigger on-demand scraping for a specific medication.

**Request:**
```json
{
  "product": "paralen 500mg",
  "location": "Košice"
}
```

**Response:**
```json
{
  "success": true,
  "product": "paralen 500mg",
  "result": {
    "scannedPharmacies": 1,
    "results": [...]
  }
}
```

## Anti-Bot Detection

Built-in measures to avoid blocking:
- Chrome args: `--disable-blink-features=AutomationControlled`
- Realistic User-Agent: Chrome 131.0.0.0
- Slovak locale and timezone (Europe/Bratislava)
- HTTP headers: Accept-Language, Sec-Fetch-*, DNT
- JavaScript injection to hide `navigator.webdriver`
- Homepage-first navigation with random delays
- Rate limiting: 5 seconds between requests

## Docker Deployment

```bash
docker build -t pharmacychecker:latest .
docker run -p 5003:5003 \
  -e BackendApiUrl=http://host.docker.internal:5000 \
  pharmacychecker:latest
```

## Azure Deployment

See `AZURE_DEPLOYMENT.md` for complete Azure Container Instances setup.

**Recommended Configuration:**
- Azure Container Instance (2 vCPU, 2GB RAM)
- API-only mode (`Products: []`) for cost efficiency
- Environment variables: `BackendApiUrl`, `PLAYWRIGHT_BROWSERS_PATH`
- Estimated cost: ~$60/month

## Product Naming Strategy

**Important:** Use specific brand names instead of generic medication names to avoid bot detection:

✅ **Good:** "paralen 500mg", "nurofen 200mg", "ibuprofen nurofen"  
❌ **Avoid:** "ibuprofen", "paracetamol" (triggers redirect to blocked pages)

Generic searches may redirect to `/podla-ucinnej-latky-*` pages that trigger aggressive bot blocking.

## Troubleshooting

### Playwright Not Found
```bash
dotnet tool install --global Microsoft.Playwright.CLI
playwright install chromium --with-deps
```

### Exit Code 1
Check logs for specific errors. Common issues:
- Missing Playwright browsers
- Backend API unreachable
- Invalid pharmacy YAML configuration

### Bot Detection
If scraping fails with Error 500/403:
- Verify product names are specific (not generic)
- Check rate limiting settings
- Review anti-detection configuration in PharmacyScanner.cs

## Development

**Project Structure:**
```
PharmacyChecker/
├── Program.cs              # ASP.NET Core Web API setup
├── Worker.cs               # Background scanning service
├── appsettings.json        # Configuration
├── Dockerfile              # Production container
├── Services/
│   ├── PharmacyScanner.cs  # Playwright scraping engine
│   ├── ConfigLoader.cs     # YAML config parser
│   └── ChangeDetector.cs   # Duplicate detection
├── Models/
│   └── PharmacyConfig.cs   # Configuration models
└── config/
    └── pharmacies/
        ├── pilulka.yaml    # Active configuration
        ├── drmax.yaml.disabled
        └── chcemlieky.yaml.disabled
```

**Key Dependencies:**
- Microsoft.Playwright 1.52.0
- YamlDotNet
- .NET 9

## License

Part of ePatient application.
