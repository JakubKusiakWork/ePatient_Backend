FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5003

# Install Playwright dependencies (Chromium browser and system libraries)
RUN apt-get update && apt-get install -y \
    libnss3 \
    libnspr4 \
    libdbus-1-3 \
    libatk1.0-0 \
    libatk-bridge2.0-0 \
    libcups2 \
    libdrm2 \
    libxkbcommon0 \
    libxcomposite1 \
    libxdamage1 \
    libxfixes3 \
    libxrandr2 \
    libgbm1 \
    libpango-1.0-0 \
    libcairo2 \
    libasound2 \
    libatspi2.0-0 \
    wget \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["PharmacyChecker.csproj", "./"]
RUN dotnet restore "PharmacyChecker.csproj"
COPY . ./
RUN dotnet publish "PharmacyChecker.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish ./

ENV PLAYWRIGHT_BROWSERS_PATH=/app/.playwright
ENV PATH="/root/.dotnet/tools:${PATH}"
RUN dotnet tool install --global Microsoft.Playwright.CLI && \
    playwright install chromium --with-deps

ENV DOTNET_EnableDiagnostics=0
ENV ASPNETCORE_URLS=http://+:5003

ENTRYPOINT ["dotnet", "PharmacyChecker.dll"]
