# Quick Reference: IORManager Deployment Commands

## GitHub Actions Runner Management

```powershell
# Install runner as Windows service
cd C:\actions-runner
.\svc.cmd install

# Start runner service
.\svc.cmd start

# Stop runner service
.\svc.cmd stop

# Check runner status
.\svc.cmd status

# Uninstall runner service
.\svc.cmd uninstall
```

## Manual Build Commands

```bash
# Backend
cd backend
dotnet restore
dotnet build -c Release
dotnet publish -c Release -o bin/Release/net9.0/publish

# Frontend
cd frontend
npm install
npm run build
```

## Database Management

```powershell
# Run migrations (dev)
$env:ASPNETCORE_ENVIRONMENT = "Development"
cd backend
dotnet ef database update

# Run migrations (prod)
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet ef database update
```

## IIS Deployment

```powershell
# Deploy to dev environment
cd scripts
.\deploy-to-iis.ps1 -Environment dev -SourceDir "C:\path\to\IORManager"

# Deploy to prod environment
.\deploy-to-iis.ps1 -Environment prod -SourceDir "C:\path\to\IORManager"
```

## IIS App Pool Management

```powershell
# Stop app pool
Stop-WebAppPool -Name "IORManager-Api-Dev"

# Start app pool
Start-WebAppPool -Name "IORManager-Api-Dev"

# Restart app pool
Restart-WebAppPool -Name "IORManager-Api-Dev"

# Get app pool status
Get-WebAppPool -Name "IORManager-Api-Dev"
```

## Testing Deployments

```powershell
# Test backend connectivity
Invoke-WebRequest -Uri "http://localhost:5031/api" -UseBasicParsing

# Test frontend connectivity
Invoke-WebRequest -Uri "http://localhost:3000" -UseBasicParsing

# Check app logs in Windows Event Viewer
Get-EventLog -LogName Application -Newest 20
```

## Git Deployment Workflow

```bash
# Dev environment (triggers on dev branch push)
git checkout dev
git add .
git commit -m "your changes"
git push origin dev

# Prod environment (triggers on main branch push)
git checkout main
git merge dev
git push origin main
```

