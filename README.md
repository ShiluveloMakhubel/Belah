# Belah

Belah is a React storefront backed by an ASP.NET Core 8 API, EF Core, and SQL Server. Products are read from the database through `GET /api/products`; Admins create and edit products through protected API endpoints. Customer orders are priced from saved product records. Paystack checkout uses a server-created hosted authorization URL and server-side transaction verification.

## Local setup

Prerequisites: .NET 8 SDK, Node.js/npm, and a local SQL Server database. Configure the backend using user secrets, environment variables, or an untracked local settings file. Do not put real credentials in source control.

Required backend configuration names:

- `ConnectionStrings__DefaultConnection` — local SQL Server connection string.
- `Jwt__Key` — random signing key with at least 32 UTF-8 bytes.
- `Jwt__Issuer` and `Jwt__Audience` — optional values; defaults are `BelahApi` and `BelahWebsite`.
- `Paystack__SecretKey` — use a Paystack sandbox secret key only for local manual checkout.
- `Paystack__CallbackUrl` — absolute HTTPS callback URL; HTTP is accepted only on localhost.
- `REACT_APP_API_URL` — frontend build-time backend base URL (default `http://localhost:5037`).
- CORS origins in `Belah-Backend/Program.cs` must include the selected frontend origin.

Example local run commands:

```sh
cd Belah-Backend
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<local SQL Server connection string>"
dotnet user-secrets set "Jwt:Key" "<random key of at least 32 UTF-8 bytes>"
dotnet user-secrets set "Paystack:SecretKey" "<Paystack sandbox secret key>"
dotnet user-secrets set "Paystack:CallbackUrl" "http://localhost:3000/checkout"
dotnet run
```

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

Migrations are **not applied** by this implementation. Review the existing database and take a restorable backup before applying anything. Apply the checked-in chain in this order:

1. `20261003065424_AddSkuAndStockToProduct`
2. `20261003072107_AddOrdersAndOrderItems`
3. `20261010092124_AddUsersTable`
4. `20261010100001_AddUserRole`
5. `20261010120110_SecureOrderPaymentsAndOwnership`
6. `20261010123901_AddPaymentExpiry`

Inspect duplicate non-null payment references before creating the filtered unique index:

```sql
SELECT PaymentReference, COUNT(*) AS DuplicateCount
FROM Orders WHERE PaymentReference IS NOT NULL
GROUP BY PaymentReference HAVING COUNT(*) > 1;

```

Resolve duplicate references through an approved data-maintenance process before migration. The provider transaction ID column is newly added nullable, so existing rows begin null; its unique filtered index permits nulls. Do not use `EnsureCreated` against the production database. Standard local migration commands, after review and backup, are `dotnet ef migrations list` and `dotnet ef database update`; neither was run against a database here.

EF Core 9 packages are used by the .NET 8 project; EF Core 9 supports .NET 8. No product schema migration was needed for the product lifecycle work. SKU duplicate checks are currently enforced by API validation, not a database unique index.

## Admin provisioning and security

Public registration always assigns the `Customer` role. Provision an Admin through a controlled database/operator process after verifying the account; there is no public role-promotion route. The React dashboard is only a convenience boundary: Admin role authorization is enforced by the API.

JWTs are currently persisted in browser `localStorage`. This is vulnerable to token theft if an XSS flaw is introduced. Logout removes the saved token, and the UI clears expired sessions, but this is not equivalent to an HttpOnly cookie design. Do not store Paystack provider references or guest order capabilities in logs or analytics.

## Payment and manual verification

Checkout redirects to the HTTPS Paystack authorization URL returned by the backend. The callback only asks the backend to verify; it is not proof of payment. New unpaid orders expire lazily after 30 minutes when payment initialization or verification is attempted. Stock is not reserved before payment. A successfully charged order that cannot be allocated is marked `RefundRequired`; refunds are not automatic and need manual operations handling. The integration has no webhook reconciliation yet.

Before sandbox end-to-end testing, verify:

1. Apply reviewed migrations to a disposable local SQL Server database only.
2. Set local origins, callback URL, and sandbox credentials; confirm no live key is configured.
3. Admin creates and edits an active product; check its saved ID and image URL.
4. Confirm the active product appears in catalogue and detail, and inactive products do not.
5. Attempt out-of-stock checkout and verify it is rejected without stock changes.
6. Complete a sandbox hosted payment and confirm server verification, one stock deduction, and order history.
7. Exercise failed, cancelled, pending, expired, repeated callback, and refund-required flows without retrying a charge blindly.
8. Confirm account history only shows the signed-in user’s orders and Admin workflows keep payment and fulfilment states distinct.

## Current limits

- Refund initiation/resolution and provider webhooks are not implemented.
- Pending expiry is lazy; it does not use a background worker.
- There is no persistent product variant/options model; the storefront accepts products without fabricated color/storage options.
- Existing Create React App dependencies report maintenance, Browserslist data, and Babel plugin warnings during builds.
- The opt-in development seeder requires a local database with the existing schema already applied; it is never run automatically during application startup.
