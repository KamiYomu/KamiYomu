FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base

# --- Puppeteer/Chromium Setup ---
ENV PUPPETEER_SKIP_CHROMIUM_DOWNLOAD=true
ENV PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium
ENV XDG_CONFIG_HOME=/tmp/.chromium
ENV XDG_CACHE_HOME=/tmp/.chromium

# --- Locales and Encoding for QuestPDF/SkiaSharp ---
# Ensure locale is set for text rendering consistency
ENV LANG=en_US.UTF-8
ENV LC_ALL=en_US.UTF-8

# --- Install Dependencies and Fonts ---
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        # **Core Packages for BOTH (Puppeteer/QuestPDF)**
        chromium \
        libgdiplus \
        fontconfig \
        locales \
        curl \
        # **Minimal Runtime Dependencies (Focus on non-Chromium dependencies)**
        libgbm1 \
        libxshmfence1 \
        fonts-dejavu \
\
    # --- Custom Font Installation (Combined for single RUN) ---
    && mkdir -p /usr/share/fonts/truetype/lato \
    && cd /usr/share/fonts/truetype/lato \
    # Lato Fonts
    && curl -L -O https://fonts.gstatic.com/s/lato/v24/S6uyw4BMUTPHjx4wWw.ttf \
    && curl -L -O https://fonts.gstatic.com/s/lato/v24/S6uyw4BMUTPHjx4wWA.ttf \
    && curl -L -O https://fonts.gstatic.com/s/lato/v24/S6uyw4BMUTPHjx4wWy.ttf \
    && curl -L -O https://fonts.gstatic.com/s/lato/v24/S6uyw4BMUTPHjx4wWQ.ttf \
    # Gloria Hallelujah Font
    && curl -L -O https://fonts.gstatic.com/s/gloriahallelujah/v24/LYjYdHv3kUk9BMV96EIswT9DIbW-MLSy3TKEvkCF.ttf \
\
    # --- Cleanup ---
    && fc-cache -f -v \
    && apt-get autopurge -y \
    && apt-get autoclean -y \
    && rm -rf /var/lib/apt/lists/*

# --- Create Folders ---
RUN mkdir -p /manga /db /logs /agents

# Locale
RUN locale-gen en_US.UTF-8
ENV LANG=en_US.UTF-8
ENV LANGUAGE=en_US:en
ENV LC_ALL=en_US.UTF-8
   
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