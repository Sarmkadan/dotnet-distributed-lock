# =============================================================================
# Multi-stage Dockerfile for SarmKadan.DistributedLock
# =============================================================================

# Stage 1: Restore dependencies
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS restore
WORKDIR /src
COPY ["SarmKadan.DistributedLock.csproj", "."]
RUN dotnet restore "SarmKadan.DistributedLock.csproj"

# Stage 2: Build and publish
FROM restore AS build
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore \
    /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

RUN addgroup -S appgroup && adduser -S appuser -G appgroup

WORKDIR /app

COPY --from=build /app/publish .

RUN chown -R appuser:appgroup /app

USER appuser

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "SarmKadan.DistributedLock.dll"]
