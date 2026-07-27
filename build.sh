#!/usr/bin/env bash
# This wrapper script exists to satisfy the build command used by the
# task-factory tooling, which expects a build.sh at the path:
#   /home/redrocket/task-factory/workdir/sql-index-advisor/build.sh
#
# The actual build logic for the dotnet-distributed-lock project lives in
# the repository root's build.sh. We simply delegate to that script.

# Resolve the repository root (the directory containing this wrapper).
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Path to the real build script.
REAL_BUILD_SCRIPT="${REPO_ROOT}/build.sh"

if [[ ! -x "${REAL_BUILD_SCRIPT}" ]]; then
    echo "Error: Real build script not found or not executable at ${REAL_BUILD_SCRIPT}"
    exit 1
fi

# Execute the real build script with the same arguments.
exec "${REAL_BUILD_SCRIPT}" "$@"
