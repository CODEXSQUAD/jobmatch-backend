#!/bin/sh
set -eu

# The existing CLI applies migrations, seeds missing demo records, then exits.
# Stop on failure: do not start an API against a partially initialized database.
dotnet JobMatch.Api.dll --seed-demo-data
exec dotnet JobMatch.Api.dll
