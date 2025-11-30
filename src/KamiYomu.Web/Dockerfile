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

# Install Fonts
RUN apt-get update && apt-get install -y --no-install-recommends \
        curl fontconfig && \
    rm -rf /var/lib/apt/lists/*

RUN apt-get update && apt-get install -y --no-install-recommends \
        curl unzip fontconfig \
        fonts-dejavu \
        fonts-liberation \
        fonts-noto \
        fonts-noto-cjk \
    && rm -rf /var/lib/apt/lists/*

# Create fonts folder
RUN mkdir -p /usr/share/fonts/truetype/lato
RUN cd /usr/share/fonts/truetype/lato && \
    curl -L -O https://fonts.gstatic.com/s/lato/v24/S6uyw4BMUTPHjx4wWw.ttf && \
    curl -L -O https://fonts.gstatic.com/s/lato/v24/S6uyw4BMUTPHjx4wWA.ttf && \
    curl -L -O https://fonts.gstatic.com/s/lato/v24/S6uyw4BMUTPHjx4wWy.ttf && \
    curl -L -O https://fonts.gstatic.com/s/lato/v24/S6uyw4BMUTPHjx4wWQ.ttf
   
RUN fc-cache -f -v


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

# Restore .NET dependencies
RUN dotnet restore "./KamiYomu.Web/KamiYomu.Web.csproj"

# Install LibMan CLI globally
RUN dotnet tool install -g Microsoft.Web.LibraryManager.Cli

# Add LibMan to PATH
ENV PATH="$PATH:/root/.dotnet/tools"

# Copy full source
COPY . .

# Restore LibMan libraries
WORKDIR "/src/KamiYomu.Web"
RUN libman restore

# Build project with full version metadata
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

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD curl --fail http://localhost:8080/healthz || exit 1

# Entrypoint
ENTRYPOINT ["dotnet", "KamiYomu.Web.dll"]