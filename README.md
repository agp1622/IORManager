# IORManager

This repository now contains two independent projects: the ASP.NET Core backend under `backend/` and the React+Vite frontend under `frontend/`.  Each project can be opened, built, and deployed separately while still sharing a single Git repository for coordination.

## Project layout
- `backend/` – ASP.NET Core 9.0 Web API, Entity Framework Core migrations, and the Visual Studio solution (`IORManager.sln`).
- `frontend/` – React application generated with Vite. The default Vite README is still available inside this folder for framework-specific tips.
- `.github/`, `.vscode/`, `.idea/`, `.gitignore` – shared repository tooling that applies to both projects.

## Backend
```bash
cd backend
# restore dependencies
 dotnet restore
# run EF Core migrations (optional)
 dotnet ef database update
# start the API with hot reload
 dotnet watch run
```

The API listens on the usual ASP.NET Core ports (e.g., `https://localhost:7000` / `http://localhost:5031`). Update `appsettings*.json` inside `backend/` to point to the desired SQL Server instance.

## Frontend
```bash
cd frontend
npm install
npm run dev
```

Create a `.env` file in `frontend/` to point the UI at a different backend instance:
```
VITE_API_BASE_URL=http://localhost:5031/api
```

## Working with both projects
Run the backend and frontend in separate terminals. The backend already enables CORS for the Vite dev server (`http://localhost:5173`). When publishing, deploy the `/backend` project as a standard ASP.NET Core service and the `/frontend` project as a static site bundle from `frontend/dist/`.
