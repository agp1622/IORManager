# IOR Manager frontend

This React application provides a dashboard for exploring and creating invoices, purchase orders, and receipts exposed by the IOR Manager ASP.NET Core API.

## Getting started

1. Install dependencies:
   ```bash
   npm install
   ```
2. Start the development server (expects the API to be running on http://localhost:5031):
   ```bash
   npm run dev
   ```
3. Open the provided URL (usually http://localhost:5173) in your browser.

The API base URL can be adjusted by updating the `VITE_API_BASE_URL` value in `.env.development`.

## Production build

To generate an optimized build output, run:

```bash
npm run build
```

The compiled assets will be written to `dist/`.
