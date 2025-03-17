# Multi-stage build for SarmKadan.DistributedLock
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10 AS builder

WORKDIR /src

# Copy project files
COPY ["SarmKadan.DistributedLock.csproj", "."]
RUN dotnet restore "SarmKadan.DistributedLock.csproj"

# Copy source code
COPY . .

# Build the project
RUN dotnet build -c Release -o /app/build

# Stage 2: Package
FROM builder AS packager
WORKDIR /src

# Create NuGet package
RUN dotnet pack -c Release -o /app/packages

# Stage 3: Runtime (for demo/test applications)
FROM mcr.microsoft.com/dotnet/runtime:10 AS runtime

WORKDIR /app

# Copy built application from builder
COPY --from=builder /app/build .

# Copy license and documentation
COPY ["LICENSE", "README.md", "CHANGELOG.md", "./"]

# Health check (basic validation)
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD dotnet --info || exit 1

# Set entrypoint
ENTRYPOINT ["dotnet"]
CMD ["SarmKadan.DistributedLock.dll"]
