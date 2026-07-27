#!/usr/bin/env bash
# Simple build script for the dotnet-distributed-lock repository.
# It runs the unit tests and exits with the appropriate status code.

set -e

# Ensure the dotnet SDK is available
if ! command -v dotnet >/dev/null 2>&1; then
    echo "Error: dotnet SDK is not installed or not in PATH."
    exit 1
fi

# Restore and build the solution
dotnet restore
dotnet build --configuration Release

# Run all tests
dotnet test --no-build --configuration Release

# If we reach this point, everything succeeded
exit 0
