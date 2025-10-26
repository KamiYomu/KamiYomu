FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
# Puppeteer environment setup
ENV PUPPETEER_SKIP_CHROMIUM_DOWNLOAD=true
ENV PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium
ENV XDG_CONFIG_HOME=/tmp/.chromium
ENV XDG_CACHE_HOME=/tmp/.chromium

# Install Chromium and required libraries
RUN apt-get update \
  && apt-get install -y \
    libx11-6 libx11-xcb1 libatk1.0-0 libgtk-3-0 libcups2 libdrm2 \
    libxkbcommon0 libxcomposite1 libxdamage1 libxrandr2 libgbm1 \
    libpango-1.0-0 libcairo2 libasound2 libxshmfence1 libnss3 chromium \
  && apt-get autopurge -y \
  && apt-get autoclean -y

# Create folders for persistent data
RUN mkdir -p /manga /db /logs /agents

# Create non-root user
ARG UNAME=KamiYomu
ARG UID=1000
ARG GID=1000

RUN groupadd -g $GID -o $UNAME \
    && useradd -m -u $UID -g $GID -o -s /bin/bash $UNAME \
    && mkdir -p /manga /db /logs /agents \
    && chown -R $UID:$GID /manga /db /logs /agents \
    && chmod -R 775 /manga /db /logs /agents

# Switch to non-root user
USER $UNAME
WORKDIR /app

# Expose ports
EXPOSE 8080

# ─────────────────────────────────────────────────────────────
# Build stage
# ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG VERSION=1.0.0
ARG VERSION_SUFFIX=-development
WORKDIR /src

# Copy project file
COPY ["KamiYomu.Web/KamiYomu.Web.csproj", "KamiYomu.Web/"]

# Restore dependencies
RUN dotnet restore "./KamiYomu.Web/KamiYomu.Web.csproj"

# Copy full source
COPY . .

# Build project with full version metadata
WORKDIR "/src/KamiYomu.Web"
RUN dotnet build "./KamiYomu.Web.csproj" \
    -c ${BUILD_CONFIGURATION} \
    /p:Version=${VERSION} \
    /p:VersionSuffix=${VERSION_SUFFIX} \
    -o /app/build


# ─────────────────────────────────────────────────────────────
# Publish stage
# ─────────────────────────────────────────────────────────────
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG VERSION=1.0.0
ARG VERSION_SUFFIX=-development
WORKDIR /src/KamiYomu.Web

RUN dotnet publish "./KamiYomu.Web.csproj" \
    -c ${BUILD_CONFIGURATION} \
    /p:Version=${VERSION} \
    /p:VersionSuffix=${VERSION_SUFFIX} \
    /p:UseAppHost=false \
    -o /app/publish

# ─────────────────────────────────────────────────────────────
# Final runtime image
# ─────────────────────────────────────────────────────────────
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Health check (expects your app to expose /health endpoint)
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD curl --fail http://localhost:8080/healthz || exit 1

# Entrypoint
ENTRYPOINT ["dotnet", "KamiYomu.Web.dll"]