#!/usr/bin/env bash
# Root build script for the dotnet-distributed-lock repository.
# It restores NuGet packages, builds the solution, and runs all tests.

set -e

# Verify that the dotnet SDK is available.
if ! command -v dotnet >/dev/null 2>&1; then
    echo "Error: dotnet SDK is not installed or not in PATH."
    exit 1
fi

# Restore packages
dotnet restore

# Build the solution in Release configuration
dotnet build --configuration Release

# Run all tests without rebuilding
dotnet test --no-build --configuration Release

# If we reach this point, everything succeeded.
exit 0
