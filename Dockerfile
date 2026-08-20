# ==========================================
# 1. Build Stage (.NET 8 SDK)
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["kingbox-API/kingbox-API.csproj", "kingbox-API/"]
RUN dotnet restore "kingbox-API/kingbox-API.csproj"

# Copy entire source and publish
COPY . .
WORKDIR "/src/kingbox-API"
RUN dotnet publish "kingbox-API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ==========================================
# 2. Runtime Stage (ASP.NET 8 + yt-dlp + FFmpeg)
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install system dependencies, FFmpeg, Python3, and yt-dlp
RUN apt-get update && apt-get install -y --no-install-recommends \
    ffmpeg \
    python3 \
    ca-certificates \
    curl \
    && curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp -o /usr/local/bin/yt-dlp \
    && chmod a+rx /usr/local/bin/yt-dlp \
    && rm -rf /var/lib/apt/lists/*

# Create temporary processing directory with full permissions
RUN mkdir -p /app/storage/temp && chmod 777 /app/storage/temp

# Copy published application
COPY --from=build /app/publish .

# Expose standard ASP.NET 8 container port
EXPOSE 8080

# Configure default runtime environment variables
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    MediaSettings__YtDlpPath=yt-dlp \
    MediaSettings__FfmpegPath=ffmpeg \
    MediaSettings__TempDirectory=/app/storage/temp

ENTRYPOINT ["dotnet", "KingBox.Api.dll"]
