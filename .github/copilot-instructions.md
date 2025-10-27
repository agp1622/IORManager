## Quick orientation

This is an ASP.NET Core Web API (net9.0) with a small React frontend (Vite). Key pieces:

- Backend entry: `Program.cs` — DI, CORS, Swagger (enabled in Development), DbContext and repository registration.
- EF Core DbContext: `Data/IORManagerContext.cs` — DbSets for Invoices, PurchaseOrders, Receipts; discriminator mapping for FinancialDocument; DateOnly & ValueConverter usage.
- Repository pattern: `Repositories/IFinancialDocumentRepository<T>` and `Repositories/EfFinancialDocumentRepository<TDocument>` (uses an include function to load navigation properties).
- In-memory alternative: `Repositories/InMemoryFinancialDocumentRepository<TDocument>` (useful for tests or lightweight runs).
- Controllers: `Controllers/InvoicesController.cs`, `Controllers/PurchaseOrdersController.cs`, `Controllers/ReceiptsController.cs` — expose CRUD and `{id}/pdf` endpoints.
- PDF generation service: `Services/QuestPdfFinancialDocumentPdfService.cs` (registered as a singleton in `Program.cs`, uses QuestPDF).
- Frontend: `frontend/` — Vite + React. Dev server usually at `http://localhost:5173`. Frontend README documents `VITE_API_BASE_URL` usage.

## How to run locally (exact, reproducible)

- Start the API (development):

  dotnet run

  - The API binds to `http://localhost:5031` (see `Properties/launchSettings.json`). Swagger UI is available in Development.
  - The DB connection string is read from `appsettings.json` / `appsettings.Development.json` using the key `DefaultConnection` (see `Program.cs` where `UseSqlServer` is configured).

- Start the frontend:

  cd frontend
  npm install
  npm run dev

  - Frontend expects the API base URL in `.env.development` via `VITE_API_BASE_URL`. Default dev port is `5173` (see `frontend/README.md`).

## Project-specific conventions & patterns (do not break these)

- Generic repository pattern: repositories implement `IFinancialDocumentRepository<TDocument>` with three methods: `GetAll()`, `GetById(Guid)`, and `Add(TDocument)`.
  - When registering EF repositories in `Program.RegisterRepositories`, an include function is supplied so the repository knows how to eagerly load related navigation properties (e.g., invoice.Lines or receipt.Payments). If you add a new navigation property, update the include lambda in `RegisterRepositories`.

- Polymorphic FinancialDocument model: `IORManagerContext.OnModelCreating` configures a discriminator `DocumentType` that maps `Invoice`, `PurchaseOrder`, and `Receipt`. When adding a new document subtype, update that mapping here.

- Date handling: `DateOnly` properties are stored with a `ValueConverter` to `DateTime` and use a custom `ValueComparer`. Respect `DateOnly` types when creating DTOs or mapping to models.

- PDF service is registered as a singleton: `IFinancialDocumentPdfService -> QuestPdfFinancialDocumentPdfService`. QuestPDF requires `QuestPDF.Settings.License = LicenseType.Community` (already configured in the service static constructor).

## Important files to edit for common tasks

- Add a new document type: update `Models/` (new subclass), `IORManagerContext.OnModelCreating` (discriminator), and `Program.RegisterRepositories` (add EfFinancialDocumentRepository registration and include lambda).
- Add related navigation properties: update `Data/IORManagerContext.cs` relationships and the include passed into the EF repository registration.
- Change DB provider or connection: edit `IORManager.csproj` (packages) and `Program.cs` (UseSqlServer call) and `appsettings*.json` for `DefaultConnection`.

## Endpoints quick reference (useful for tests & agents)

- GET /api/invoices
- GET /api/invoices/{id}
- POST /api/invoices
- GET /api/invoices/{id}/pdf

Same pattern for `/api/purchaseorders` and `/api/receipts` (see respective controllers).

## Integration points & external dependencies

- Database: Microsoft.EntityFrameworkCore.SqlServer — connection string key `DefaultConnection`.
- PDF generation: QuestPDF package (configured in `Services/QuestPdfFinancialDocumentPdfService.cs`).
- Frontend: Vite React app in `frontend/` (dev server on 5173). Frontend uses `VITE_API_BASE_URL` to point to the API (set to `http://localhost:5031` in development by default).

## Small examples the agent can use

- To load invoices with lines using EF repo registration pattern:

  // See Program.RegisterRepositories for include example
  new EfFinancialDocumentRepository<Invoice>(context, q => q.Include(i => i.Lines));

- When creating a PDF endpoint implementer, follow controllers' pattern: fetch by id via repository, call `_pdfService.GenerateInvoicePdf(model)` and return `File(bytes, "application/pdf", filename)`.

## Quick gotchas / checks for PRs

- If you add a navigation property or change model relationships, update the EF include used in `RegisterRepositories` or tests will see incomplete graphs.
- When changing DateOnly or time zone handling, confirm `IORManagerContext`'s `ValueConverter` logic remains correct and test serialization/deserialization.
- To debug locally, use `dotnet run` and open Swagger at the API URL (Development only) to exercise endpoints quickly.

## If something is missing

If the instructions above leave gaps, tell me what you want added (for example: sample integration test, EF migrations workflow, or a list of DTO-to-model mapping helpers). I can iterate quickly.
