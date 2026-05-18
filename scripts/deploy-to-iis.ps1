param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('dev', 'prod')]
    [string]$Environment,

    [Parameter(Mandatory=$true)]
    [string]$SourceDir
)

$ErrorActionPreference = "Stop"

# Configuration based on environment
if ($Environment -eq 'dev') {
    $FrontendPath = "C:\inetpub\IORManager-dev\frontend"
    $BackendPath = "C:\inetpub\IORManager-dev\backend"
    $FrontendAppPool = "IORManager-Frontend-Dev"
    $BackendAppPool = "IORManager-Api-Dev"
    $BackendPort = 5031
} else {
    $FrontendPath = "C:\inetpub\IORManager-prod\frontend"
    $BackendPath = "C:\inetpub\IORManager-prod\backend"
    $FrontendAppPool = "IORManager-Frontend-Prod"
    $BackendAppPool = "IORManager-Api-Prod"
    $BackendPort = 5030
}

Write-Host "Deploying to $Environment environment..."
Write-Host "Frontend path: $FrontendPath"
Write-Host "Backend path: $BackendPath"

# Create directories if they don't exist
if (!(Test-Path $FrontendPath)) {
    New-Item -ItemType Directory -Path $FrontendPath -Force | Out-Null
    Write-Host "Created directory: $FrontendPath"
}

if (!(Test-Path $BackendPath)) {
    New-Item -ItemType Directory -Path $BackendPath -Force | Out-Null
    Write-Host "Created directory: $BackendPath"
}

# Stop app pools
Write-Host "Stopping app pools..."
try {
    Stop-WebAppPool -Name $FrontendAppPool -Force -ErrorAction SilentlyContinue
    Stop-WebAppPool -Name $BackendAppPool -Force -ErrorAction SilentlyContinue
} catch {
    Write-Host "Warning: Error stopping app pools: $_"
}

# Deploy frontend (dist files)
Write-Host "Deploying frontend..."
$FrontendDistPath = Join-Path $SourceDir "frontend\dist"
if (Test-Path $FrontendDistPath) {
    Remove-Item "$FrontendPath\*" -Recurse -Force -ErrorAction SilentlyContinue
    Copy-Item "$FrontendDistPath\*" -Destination $FrontendPath -Recurse -Force
    Write-Host "✓ Frontend deployed successfully"
} else {
    Write-Error "Frontend dist directory not found at: $FrontendDistPath"
}

# Deploy backend (published files)
Write-Host "Deploying backend..."
$BackendPublishPath = Join-Path $SourceDir "backend\bin\Release\net9.0\publish"
if (Test-Path $BackendPublishPath) {
    Remove-Item "$BackendPath\*" -Recurse -Force -ErrorAction SilentlyContinue
    Copy-Item "$BackendPublishPath\*" -Destination $BackendPath -Recurse -Force
    Write-Host "✓ Backend deployed successfully"
} else {
    Write-Error "Backend publish directory not found at: $BackendPublishPath"
}

# Start app pools
Write-Host "Starting app pools..."
try {
    Start-WebAppPool -Name $FrontendAppPool -ErrorAction SilentlyContinue
    Start-WebAppPool -Name $BackendAppPool -ErrorAction SilentlyContinue
    Write-Host "✓ App pools started"
} catch {
    Write-Host "Warning: Error starting app pools: $_"
}

# Wait for app pools to start
Start-Sleep -Seconds 3

Write-Host "✓ Deployment to $Environment complete!"
