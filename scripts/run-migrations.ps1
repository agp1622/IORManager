param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('dev', 'prod')]
    [string]$Environment
)

$ErrorActionPreference = "Stop"

# Configuration based on environment
if ($Environment -eq 'dev') {
    $ConnectionString = "Server=localhost\SQLEXPRESS;Database=IORManager_dev;Trusted_Connection=True;"
} else {
    $ConnectionString = "Server=localhost\SQLEXPRESS;Database=IORManager_prod;Trusted_Connection=True;"
}

Write-Host "Running database migrations for $Environment environment..."
Write-Host "Connection string: $ConnectionString"

# Get the backend directory
$BackendDir = Join-Path (Split-Path -Parent $PSScriptRoot) "backend"
Set-Location $BackendDir

# Check if dotnet ef tools are installed
Write-Host "Checking Entity Framework tools..."
try {
    dotnet ef --version | Out-Null
} catch {
    Write-Host "Installing Entity Framework tools..."
    dotnet tool install --global dotnet-ef
}

# Run migrations
Write-Host "Applying migrations..."
try {
    $env:ASPNETCORE_ENVIRONMENT = $Environment
    dotnet ef database update --configuration Release
    Write-Host "✓ Database migrations applied successfully"
} catch {
    Write-Error "Failed to apply migrations: $_"
}
