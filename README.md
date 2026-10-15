# Belah

Belah is a React storefront backed by an ASP.NET Core 8 API, EF Core, and SQL Server. Products are read from the database through `GET /api/products`; Admins create and edit products through protected API endpoints. Customer orders are priced from saved product records. Paystack checkout uses a server-created hosted authorization URL and server-side transaction verification.

## Local setup

Prerequisites: .NET 8 SDK, Node.js/npm, and a local SQL Server database. Configure the backend using user secrets, environment variables, or an untracked local settings file. Do not put real credentials in source control.

Required backend configuration names:

- `ConnectionStrings__DefaultConnection` — local SQL Server connection string.
- `BELAH_SQL_PASSWORD` — optional raw SQL login password, read separately and safely inserted with `SqlConnectionStringBuilder`; when set, SQL authentication is enabled and `BELAH_SQL_USER` defaults to `sa`.
- `Jwt__Key` — random signing key with at least 32 UTF-8 bytes.
- `Jwt__Issuer` and `Jwt__Audience` — optional values; defaults are `BelahApi` and `BelahWebsite`.
- `Paystack__SecretKey` — use a Paystack sandbox secret key only for local manual checkout.
- `Paystack__CallbackUrl` — absolute HTTPS callback URL; HTTP is accepted only on localhost.
- `REACT_APP_API_URL` — frontend build-time backend base URL (default `http://localhost:5037`).
- CORS origins can be configured with `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, etc. If none are set, the API retains the local and existing `https://simplyluxe.co.za` defaults.
- `Uploads__Path` — optional path for product uploads on a mounted persistent directory; when unset, uploads use `wwwroot/uploads`.
- SMTP settings under `Email:Smtp:*` plus `Email:SupportEmail` and `Email:StorefrontBaseUrl` for transactional purchase confirmations.

Example local run commands:

```sh
cd Belah-Backend
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<local SQL Server connection string>"
dotnet user-secrets set "Jwt:Key" "<random key of at least 32 UTF-8 bytes>"
dotnet user-secrets set "Paystack:SecretKey" "<Paystack sandbox secret key>"
dotnet user-secrets set "Paystack:CallbackUrl" "http://localhost:3000/checkout"
dotnet run
```

For local SQL Server password authentication, keep the connection string free of a password and enter the password through a hidden prompt before starting the API. The backend uses `SqlConnectionStringBuilder` to insert it, so shell metacharacters do not need manual escaping:

```zsh
read -r -s "BELAH_SQL_PASSWORD?Enter SQL Server password: "
printf '\n'
export BELAH_SQL_PASSWORD
export ConnectionStrings__DefaultConnection='Server=127.0.0.1,1433;Database=BelahDB;User Id=sa;Encrypt=True;TrustServerCertificate=True;'
DOTNET_ROOT=/home/kali/.dotnet PATH=/home/kali/.dotnet:$PATH \
  dotnet run --project Belah-Backend/Belah-Backend.csproj --launch-profile http
unset BELAH_SQL_PASSWORD
```

The password remains in the current shell environment while the API runs and is unset when it exits. Do not enable shell tracing (`set -x`) while entering or using secrets.

In another terminal:

```sh
cd belah-website
npm install
npm start
```

### Optional development catalogue samples

To populate a **development database you have explicitly configured**, run the opt-in seeder from the repository root:

```sh
ASPNETCORE_ENVIRONMENT=Development dotnet run --project Belah-Backend/Belah-Backend.csproj -- --seed-mock-products
```

The command requires `ConnectionStrings:DefaultConnection`, refuses to run outside the `Development` environment, and never applies migrations or creates a schema. Point it only at a disposable/local database whose schema is already current. It inserts 14 catalogue sample rows using the existing product table and stable `BELAH-DEV-MOCK-*` SKUs. Re-running refreshes rows that still carry the seeder's development-sample description marker, adds missing rows, and skips reserved-SKU conflicts without that marker; it does not delete rows. These are mock listings and stock values, not real inventory. The sample image paths use the existing local frontend assets. Public product APIs show them while the API runs in Development.

For production frontend output, run `npm run build` from `belah-website`. Backend tests run with `dotnet test Belah-Backend.Tests/Belah-Backend.Tests.csproj` from the repository root.

Build/test commands:

```sh
dotnet build Belah-Backend/Belah-Backend.csproj
dotnet test Belah-Backend.Tests/Belah-Backend.Tests.csproj
cd belah-website
npm test -- --watchAll=false
npm run build
```

## Database migrations

The repository contains these migrations in order. **The current live database migration history has not been independently verified**, so do not assume all are pending or applied. Query `dbo.__EFMigrationsHistory` against the exact target database and compare it with this list before deciding what to apply. Back up and verify a restorable copy before any schema change.

1. `20241210174731_InitialCreate`
2. `20241211120152_AddCategoryAndSpecificationToProduct`
3. `20261003065424_AddSkuAndStockToProduct`
4. `20261003072107_AddOrdersAndOrderItems`
5. `20261010092124_AddUsersTable`
6. `20261010100001_AddUserRole`
7. `20261010120110_SecureOrderPaymentsAndOwnership`
8. `20261010123901_AddPaymentExpiry`
9. `20261010153852_EnforceUniqueProductSku`
10. `20261013124621_AddFlexibleCatalogueAndProductVariants`
11. `20261013135332_AddCatalogueApprovalState`
12. `20261013150747_AddPurchaseConfirmationEmailOutbox`
13. `20261013181604_AddFixedDeliveryFeeAndProductSalePricing`

Inspect duplicate non-null payment references before creating the filtered unique index:

```sql
SELECT PaymentReference, COUNT(*) AS DuplicateCount
FROM Orders WHERE PaymentReference IS NOT NULL
GROUP BY PaymentReference HAVING COUNT(*) > 1;

```

Resolve duplicate references through an approved data-maintenance process before migration. The provider transaction ID column is newly added nullable, so existing rows begin null; its unique filtered index permits nulls. Do not use `EnsureCreated` against the production database. Standard migration commands, only after confirming the target, reviewing the pending operations, and verifying a backup, are `dotnet ef migrations list` and `dotnet ef database update`. Do not run `database update` against production until the live history and pending sequence have been reviewed.

EF Core 9 packages are used by the .NET 8 project; EF Core 9 supports .NET 8. The flexible catalogue, approval state, email outbox, and delivery/sale pricing migrations add persistent schema used by the current application. The SKU migration adds a filtered unique index; check existing duplicate non-null SKUs before applying it.

## Admin provisioning and security

Public registration always assigns the `Customer` role. Provision an Admin through a controlled database/operator process after verifying the account; there is no public role-promotion route. The React dashboard is only a convenience boundary: Admin role authorization is enforced by the API.

JWTs are currently persisted in browser `localStorage`. This is vulnerable to token theft if an XSS flaw is introduced. Logout removes the saved token, and the UI clears expired sessions, but this is not equivalent to an HttpOnly cookie design. Do not store Paystack provider references or guest order capabilities in logs or analytics.

## Payment and manual verification

Checkout redirects to the HTTPS Paystack authorization URL returned by the backend. The callback only asks the backend to verify; it is not proof of payment. The backend also implements a signed `charge.success` webhook and re-verifies the transaction with Paystack before changing payment state. Configure a public HTTPS webhook URL in Paystack and test duplicate delivery in sandbox before launch. New unpaid orders expire lazily after 30 minutes when payment initialization or verification is attempted. Stock is not reserved before payment. A successfully charged order that cannot be allocated is marked `RefundRequired`; refunds are not automatic and need manual operations handling.

Before sandbox end-to-end testing, verify:

1. Apply the reviewed migration sequence to a disposable SQL Server database first.
2. Set local origins, callback URL, and sandbox credentials; confirm no live key is configured.
3. Admin creates and edits an active product; check its saved ID and image URL.
4. Confirm the active product appears in catalogue and detail, and inactive products do not.
5. Attempt out-of-stock checkout and verify it is rejected without stock changes.
6. Complete a sandbox hosted payment and confirm server verification, one stock deduction, and order history.
7. Exercise failed, cancelled, pending, expired, repeated callback, and refund-required flows without retrying a charge blindly.
8. Confirm account history only shows the signed-in user’s orders and Admin workflows keep payment and fulfilment states distinct.

## Current limits

- Refund initiation/resolution is not implemented. `RefundRequired` orders need an operator process and provider confirmation; the application does not claim a refund was completed.
- Pending expiry is lazy; it does not use a background worker.
- Product variants, editable categories, sale prices, fixed delivery fees, and the purchase-confirmation email outbox are implemented in source. Their migrations must be checked against the live database before deploying this version.
- Admin image uploads are written to the backend's `wwwroot/uploads` directory. A production host must provide persistent storage or an object-storage integration; ephemeral container filesystems can lose uploads on redeploy.
- The fragrance intake still contains placeholder photography and records that need product identity, image rights, final price, and physical stock approval before they are made purchasable.
- The backend stores no deployment manifests. Hosting, domain/DNS, TLS, persistent SQL Server, backups, monitoring, and secret management must be configured for the selected hosting provider.
- The production frontend must be built with `REACT_APP_API_URL` set to the public HTTPS API origin; the local default `http://localhost:5037` is only appropriate for local development.
- Existing Create React App dependencies report maintenance, Browserslist data, and Babel plugin warnings during builds.
- The opt-in development seeders require a local database with the existing schema already applied; they are never run automatically during application startup and must not be used to populate production inventory.

## Production release checklist

1. Confirm the exact production database and read `dbo.__EFMigrationsHistory`; compare it with the migration list above. Take a fresh backup, verify it, and rehearse the pending migration sequence against a restored copy before applying it.
2. Provision production SQL Server with private network access, automated backups and a tested restore procedure. Configure the API's connection string, JWT signing key, Paystack live secret, SMTP credentials, and callback URL through the hosting provider's secret manager. Never use Development seeders or local sample credentials in production.
3. Deploy the API behind HTTPS. Configure the frontend origin in backend CORS, the Paystack callback and webhook URLs, and a persistent destination for uploaded product images. Confirm the deployed health/API endpoints without exposing secrets.
4. Build the frontend with `REACT_APP_API_URL=https://<public-api-origin>`, then deploy it to the chosen static hosting provider and attach the production domain/TLS.
5. Complete sandbox checkout and webhook tests before switching to live Paystack credentials. Verify order persistence, one-time inventory allocation, email delivery, duplicate webhook handling, Admin authorization, and the manual `RefundRequired` operating procedure.
6. Publish only products with approved names, images, prices and verified stock. After launch, monitor API errors, payment reconciliation, email retries, low inventory and backup/restore status.

No production deployment, live payment, migration, or live database verification is claimed by this repository documentation.

For the prepared low-cost staging setup, see [`docs/Low-Cost-Deployment.md`](docs/Low-Cost-Deployment.md). It covers Cloudflare Pages settings, the API container, required environment configuration, and data/payment safeguards. No cloud resources are provisioned by the guide.
