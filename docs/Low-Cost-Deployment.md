# Low-cost Belah staging deployment

This guide prepares a **staging deployment** using Cloudflare Pages for the React site and Azure Container Apps for the .NET API. It does not create cloud resources, move the local SQL database, or authorize live payments. Azure free allowances have usage limits; expect cold starts or pauses, and set a budget alert before provisioning anything.

## What is prepared in the repository

- `Belah-Backend/Dockerfile` builds a .NET 8 API image listening on port 8080, without applying EF migrations.
- `.dockerignore` keeps build output and local environment files out of the container build context.
- `belah-website/public/_redirects` routes React Router paths to `index.html` on Cloudflare Pages.
- `Cors:AllowedOrigins` can be set from Azure environment configuration. `Uploads:Path` can point to a mounted directory for product uploads; when unset, the API preserves its local `wwwroot/uploads` behavior.

## Cloudflare Pages settings

Create a Pages project connected to the repository with:

- Root directory: `belah-website`
- Build command: `npm ci && npm run build`
- Build output directory: `build`
- Environment variable: `REACT_APP_API_URL=https://<your-api-host>`
- Node.js: use a supported active LTS version compatible with the locked React Scripts toolchain.

Set the production build environment variable in Cloudflare Pages before the first deploy. React embeds it into the compiled JavaScript, so changing the value requires a new frontend build/deploy. The value must be the public HTTPS API origin, with no `/api` suffix.

## API image

Build from the repository root:

```sh
docker build -f Belah-Backend/Dockerfile -t belah-api:staging .
```

Push this image to a registry accessible to Azure Container Apps, then deploy it with ingress enabled and target port `8080`. Do not put secrets in the Dockerfile, image, build arguments, or repository. No EF migration command is part of container startup.

## API configuration

Use the Azure secret store or Container Apps secret references for these values:

- `ConnectionStrings__DefaultConnection`: connection to the **staging copy** of BelahDB, not the local production-like database.
- `Jwt__Key`: newly generated staging key of at least 32 UTF-8 bytes.
- `Jwt__Issuer` and `Jwt__Audience`: stable staging-specific values.
- `Paystack__SecretKey`: Paystack sandbox secret only.
- `Paystack__CallbackUrl`: HTTPS storefront checkout callback.
- `Cors__AllowedOrigins__0`: exact Cloudflare Pages staging origin, e.g. `https://<project>.pages.dev`.
- `Email__StorefrontBaseUrl` and `Email__SupportEmail`.
- `Email__Smtp__Host`, `Email__Smtp__Port`, `Email__Smtp__Username`, `Email__Smtp__Password`, and `Email__Smtp__FromAddress` for a controlled test inbox/provider.
- Optional `Uploads__Path`: path to a mounted persistent directory for uploaded product images.

Set `ASPNETCORE_ENVIRONMENT=Production` and keep Swagger disabled as it currently is outside Development. Use a dedicated staging SQL login with only the permissions the API needs; reserve schema-owner credentials for a separately controlled migration step.

The current upload implementation writes local files and serves `/uploads/*`. `Uploads__Path` lets the API use a mounted directory while preserving those URLs. Without a persistent mount, uploaded images can disappear after a restart or redeploy. This does not yet integrate Cloudflare R2; keep image uploads out of staging until durable storage is mounted. Before real sales, choose and validate durable object/file storage and backup behavior.

## Data and payment safeguards

1. Take and verify a database backup, then restore/import a separate staging copy. Do not connect staging to the local database or copy real customer data unless it is necessary and protected appropriately.
2. Confirm all migration IDs and expected columns on the staging database. This repository's current local database migration history was reported complete, but a cloud database is a separate target.
3. Add only test products or listings with confirmed inventory. Do not run either Development seeder in production.
4. Complete hosted checkout with sandbox credentials, verify the order and one-time stock deduction, and test duplicate webhook delivery. Do not enable live Paystack credentials for this staging exercise.
5. Verify email delivery only to a controlled test inbox. Exercise `RefundRequired` handling without issuing a real refund.

## Before a public production launch

- Decide whether Azure free-tier cold starts and database auto-pause are acceptable. If not, budget for always-on API and database service.
- Configure a stable API domain and allow that exact origin in CORS and the Pages build variable.
- Implement or mount durable image storage and test persistence across a redeploy.
- Configure automated backups, alerts, log retention, and a tested restore procedure.
- Confirm product identity, image rights, sale prices, and physical inventory.
- Switch Paystack to live mode only after a successful end-to-end sandbox review and operational readiness check.
