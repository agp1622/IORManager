# IORManager Local IIS Deployment Setup Guide

## Prerequisites
- Windows 10/11 with Administrator privileges
- IIS installed and enabled
- SQL Server Express with SQL Server Management Studio
- .NET 9 SDK installed
- Node.js 18+ installed
- PowerShell 7+ (or Windows PowerShell with ExecutionPolicy set to RemoteSigned)

---

## Step 1: Create SQL Server Databases

Open **SQL Server Management Studio** and run:

```sql
-- Create dev database
CREATE DATABASE IORManager_dev;

-- Create prod database
CREATE DATABASE IORManager_prod;
```

Verify connection strings work:
- Dev: `Server=localhost\SQLEXPRESS;Database=IORManager_dev;Trusted_Connection=True;`
- Prod: `Server=localhost\SQLEXPRESS;Database=IORManager_prod;Trusted_Connection=True;`

---

## Step 2: Install GitHub Actions Self-Hosted Runner

### 2a. Download the Runner

1. Go to your GitHub repository → **Settings** → **Actions** → **Runners**
2. Click **New self-hosted runner**
3. Select **Windows** and **x64**
4. Download the ZIP file (e.g., `actions-runner-win-x64-2.x.x.zip`)
5. Extract it to a location (e.g., `C:\actions-runner`)

### 2b. Configure the Runner

Open **Command Prompt (Admin)** or **PowerShell (Admin)** and navigate to the runner directory:

```cmd
cd C:\actions-runner
```

Run the configuration script:

```cmd
.\config.cmd --url https://github.com/YOUR-USERNAME/IORManager --token YOUR-REGISTRATION-TOKEN
```

When prompted:
- **Enter the name of the runner**: `ior-manager-windows` (or any name)
- **Enter any labels** (optional): `windows,local-iis` (helpful for organizing runners)
- **Enter name of work folder**: `./_work` (default is fine)

### 2c. Run the Runner

**Option A: Run as an interactive service** (for testing):
```cmd
.\run.cmd
```

**Option B: Install and run as a Windows Service** (recommended for production):
```cmd
.\svc.cmd install
.\svc.cmd start
```

To check status:
```cmd
.\svc.cmd status
```

### 2d: Verify Runner is Online

Go back to GitHub → **Settings** → **Actions** → **Runners** and verify your runner shows as **Idle** (green).

---

## Step 3: Configure IIS Sites and App Pools

### 3a. Create App Pools

Open **IIS Manager** (inetmgr):

1. **Create App Pool for Frontend Dev**
   - Right-click **Application Pools** → **Add Application Pool**
   - Name: `IORManager-Frontend-Dev`
   - .NET CLR version: `No Managed Code` (important for ASP.NET Core)
   - Managed pipeline mode: `Integrated`

2. **Create App Pool for Backend Dev**
   - Name: `IORManager-Api-Dev`
   - .NET CLR version: `No Managed Code`
   - Managed pipeline mode: `Integrated`

3. **Create App Pool for Frontend Prod**
   - Name: `IORManager-Frontend-Prod`
   - .NET CLR version: `No Managed Code`
   - Managed pipeline mode: `Integrated`

4. **Create App Pool for Backend Prod**
   - Name: `IORManager-Api-Prod`
   - .NET CLR version: `No Managed Code`
   - Managed pipeline mode: `Integrated`

### 3b. Create IIS Sites

**Frontend Dev Site:**
1. Right-click **Sites** → **Add Website**
2. Site name: `IORManager-Frontend-Dev`
3. Application pool: `IORManager-Frontend-Dev`
4. Physical path: `C:\inetpub\IORManager-dev\frontend`
5. Binding: HTTP, IP: `*`, Port: `3000`, Host name: (leave blank)
6. Click OK

**Frontend Prod Site:**
1. Right-click **Sites** → **Add Website**
2. Site name: `IORManager-Frontend-Prod`
3. Application pool: `IORManager-Frontend-Prod`
4. Physical path: `C:\inetpub\IORManager-prod\frontend`
5. Binding: HTTP, IP: `*`, Port: `3001`, Host name: (leave blank)
6. Click OK

**Backend Dev Site:**
1. Right-click **Sites** → **Add Website**
2. Site name: `IORManager-Api-Dev`
3. Application pool: `IORManager-Api-Dev`
4. Physical path: `C:\inetpub\IORManager-dev\backend`
5. Binding: HTTP, IP: `*`, Port: `5031`, Host name: (leave blank)
6. Click OK

**Backend Prod Site:**
1. Right-click **Sites** → **Add Website**
2. Site name: `IORManager-Api-Prod`
3. Application pool: `IORManager-Api-Prod`
4. Physical path: `C:\inetpub\IORManager-prod\backend`
5. Binding: HTTP, IP: `*`, Port: `5030`, Host name: (leave blank)
6. Click OK

### 3c. Enable Web Server Features (if not already enabled)

Open **Windows Features**:
1. Press **Win+R** → type `OptionalFeatures.exe`
2. Enable:
   - ✓ Internet Information Services
   - ✓ IIS Management Console
   - ✓ Static Content
   - ✓ ASP.NET 4.8 (for management tools)

---

## Step 4: Configure PowerShell Execution Policy

Open **PowerShell (Admin)**:

```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

This allows the deployment scripts to run.

---

## Step 5: Create IIS Directories

Create the required directories for IIS to deploy to:

```cmd
mkdir C:\inetpub\IORManager-dev\frontend
mkdir C:\inetpub\IORManager-dev\backend
mkdir C:\inetpub\IORManager-prod\frontend
mkdir C:\inetpub\IORManager-prod\backend
```

Give **IIS_IUSRS** permissions to these directories:
1. Right-click each folder → **Properties** → **Security** → **Edit**
2. Select `IIS_IUSRS` and grant **Modify** permission
3. Click Apply

---

## Step 6: Test the Deployment Pipeline

### 6a. Manually Test Dev Deployment

1. Build the project locally:
   ```cmd
   cd backend
   dotnet publish -c Release
   cd ..\frontend
   npm run build
   ```

2. Run the deployment script:
   ```powershell
   cd scripts
   .\deploy-to-iis.ps1 -Environment dev -SourceDir "C:\Users\YOUR-USERNAME\Documents\GitHub\IORManager"
   ```

3. Check IIS Manager to verify files are deployed
4. Visit http://localhost:3000 (frontend) and http://localhost:5031/api (backend)

### 6b. Test GitHub Actions Trigger

1. Commit and push changes to the `dev` branch:
   ```cmd
   git add .
   git commit -m "ci: add deployment workflows"
   git push origin dev
   ```

2. Go to GitHub → **Actions** tab
3. Watch the `Deploy to Dev` workflow run
4. Check the logs for any errors
5. Verify the application is updated in IIS

---

## Step 7: Verify Everything Works

### Checklist:
- ✓ GitHub Actions runner shows as **Idle** in repository settings
- ✓ IIS sites are created and running (green in IIS Manager)
- ✓ Databases are created in SQL Server
- ✓ Frontend accessible at http://localhost:3000 (dev) and http://localhost:3001 (prod)
- ✓ Backend API responding at http://localhost:5031/api (dev) and http://localhost:5030/api (prod)
- ✓ Push to `dev` branch triggers `Deploy to Dev` workflow
- ✓ Push to `main` branch triggers `Deploy to Prod` workflow

---

## Troubleshooting

### Runner shows "Offline" or "Lost communication"
1. Check if the runner service is running: `.\svc.cmd status`
2. Restart the service: `.\svc.cmd stop` then `.\svc.cmd start`
3. Check firewall isn't blocking GitHub API connections

### IIS app pool keeps stopping
1. Check Event Viewer for errors (Windows Logs → Application)
2. Verify .NET 9 SDK is installed: `dotnet --version`
3. Check app pool identity has permissions to the deployment directories

### Database migrations fail
1. Verify connection strings in appsettings.json files
2. Check database exists in SQL Server
3. Verify Windows user has permissions to create tables
4. Run migrations manually: `cd backend && dotnet ef database update`

### PowerShell execution policy error
1. Set execution policy: `Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser`
2. Or run PowerShell as Administrator

### Frontend shows 404 on IIS
1. Verify `frontend/dist` folder exists after build
2. Check IIS site physical path points to correct location
3. Verify static content MIME types are configured in IIS

---

## Next Steps

Once verified:
1. **Commit everything**: `git add . && git commit -m "ci: setup local IIS deployment"`
2. **Push to dev**: `git push origin dev`
3. **Watch the deployment**: Check GitHub Actions and IIS for deployment
4. **Merge to main**: Create a PR and merge to `main` to trigger prod deployment

