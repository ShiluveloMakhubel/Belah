# Belah implementation status — Batches 1 and 2

## Implemented

- Customer orders use the authenticated stable user ID. Guest orders receive a 256-bit capability once; only its SHA-256 hash is stored. `GET /api/orders/my-orders` derives the owner from validated JWT claims and only returns the current customer’s orders.
- `/my-account` shows the existing username/email/role profile fields and owned order history with totals, dates, payment and fulfilment states, and item details. `GET /api/User/me` returns only the authenticated account’s existing profile fields.
- Payment initialization and verification run through the backend and Paystack hosted checkout. The persisted transaction reference is unique; verification checks exact reference, status, amount, currency, customer email, transaction ID and transaction uniqueness. Stock is conditionally deducted once within a database transaction. A successful payment that cannot be allocated becomes `RefundRequired` for manual handling.
- New unpaid orders expire after 30 minutes. Expiry is enforced lazily at payment initialization and verification; no worker reserves stock for pending orders.
- Product create, edit, upload, stock, and activation APIs are Admin-only. Creation uses a DTO without a caller-controlled database ID; create and update DTOs map explicitly to the EF entity. Product create/edit validate required fields, price precision/range, stock, URL shape and lengths. Duplicate SKUs are rejected by application validation and a new filtered unique database index migration protects against concurrent duplicate creates.
- The image upload endpoint accepts JPEG, PNG, WebP, or GIF up to 5 MiB, checks the content signature, uses generated names, and returns a backend-root-relative URL. The Admin editor supports upload and URL, validates before submit, supports editing by existing ID, and shows API feedback.
- Public catalogue and detail pages fetch active records from the API, search and filter actual results, show availability, use the database ID for detail routes, and provide loading/empty/error/image fallback states. Details no longer require generic phone color/storage options. Purchase order creation rejects inactive products and quantities above current stock; payment verification allocates available stock atomically.
- Admin dashboard has product search, order search/date/payment/fulfilment filters, delivery details, separate payment and fulfilment labels, stock and active controls, valid fulfilment transitions, and a visible manual `RefundRequired` warning. No refund API is implemented.
- Paystack `charge.success` webhooks validate the HMAC-SHA512 signature against the raw body, then reuse the server-side Paystack verification and transactional stock-allocation path. Repeated delivery is idempotent through the persisted order payment state and provider transaction ID.
- Login and registration endpoints use a per-client IP fixed-window limit of 20 requests per minute. Registration no longer returns a user ID or distinguishes an existing valid account from a newly-created account in its response.
- Obsolete `/postproduct` now resolves through the Admin dashboard, and API authorization remains enforced independently of React navigation.
- Homepage has a restrained collection hero and API-backed featured products; the previous hard-coded unrelated phone deals were removed. Authentication retains the existing localStorage JWT approach, clears the UI session on expiry/logout, and has its storage risk documented in README.

## Migrations and database safety

Order migrations created in Batch 1:

- `20261010120110_SecureOrderPaymentsAndOwnership` — nullable account ownership, guest capability hash, provider transaction ID, foreign key, and filtered unique indexes for payment reference, provider transaction ID, and guest capability hash.
- `20261010123901_AddPaymentExpiry` — nullable pending payment expiry.

Added during this audit and **not applied**:

- `20261010153852_EnforceUniqueProductSku` — adds a filtered unique index to non-null product SKUs. It does not modify rows. Before applying it, run `SELECT Sku, COUNT_BIG(*) FROM dbo.Products WHERE Sku IS NOT NULL GROUP BY Sku HAVING COUNT_BIG(*) > 1;`; any duplicates must be reviewed and resolved before the index can be created.

Apply migrations in timestamp order after reviewing schema and backing up the target database. Before the unique payment-reference/provider-transaction indexes, query for duplicate non-null values and reconcile them through an approved maintenance process (queries are in `README.md`). EF Core 9 packages are compatible with the .NET 8 target. Product lifecycle work made no schema/snapshot change.

## Configuration

Set configuration outside source control: `ConnectionStrings__DefaultConnection`, `Jwt__Key` (at least 32 UTF-8 bytes), optional `Jwt__Issuer`/`Jwt__Audience`, `Paystack__SecretKey` (sandbox only during testing), secure absolute `Paystack__CallbackUrl`, and frontend `REACT_APP_API_URL`. Update backend CORS origins for local frontend/backend hosts. The browser does not need a Paystack key for hosted checkout.

A Paystack test secret was reportedly exposed in a prior chat. Rotate it in Paystack before further sandbox activity if that has not already been done; do not copy it into source, logs, or this document.

Public registration always assigns `Customer`; provision Admin accounts only through a controlled operator/database process. No public privilege-escalation route exists.

## Remaining work and limits

- Automated refunds and refund resolution are not implemented. `RefundRequired` means operational review is needed; the application does not claim a refund was requested, submitted, or confirmed.
- The webhook endpoint is implemented, but Paystack test mode still needs a publicly reachable HTTPS webhook URL configured in its dashboard. Localhost alone cannot receive Paystack callbacks. The handler verifies transactions against Paystack before changing payment state.
- When deployed behind a reverse proxy, configure trusted forwarded-header handling and review rate-limit partitioning so requests are attributed to the real client safely; do not trust arbitrary forwarded IP headers.
- Customer order-confirmation and fulfilment notifications are not implemented; the application does not send messages.
- Expiration is lazy: expired pending rows remain until accessed, though they cannot be paid through the API and hold no stock.
- No persistent product-variant/options model currently exists; unsupported generic options were removed from product details.
- Browser JWTs remain in `localStorage`, which carries XSS theft risk. A server-issued expiry is used for UI session cleanup, not as an authorization boundary.
- Manual sandbox end-to-end validation remains: reviewed migrations on disposable SQL Server; Admin create/edit/upload; public active/inactive visibility; stock rejection; hosted sandbox success and repeat callback; failed/cancelled/pending/expired/refund-required behavior; customer order ownership; Admin fulfilment transitions. Do not use live payment credentials.

## Checks

Run from the repository root unless noted:

```sh
dotnet build Belah-Backend/Belah-Backend.csproj
dotnet test Belah-Backend.Tests/Belah-Backend.Tests.csproj
cd belah-website && npm test -- --watchAll=false && npm run build
```

- `dotnet build Belah-Backend/Belah-Backend.csproj --no-restore` — passed, 0 warnings and 0 errors.
- `dotnet test Belah-Backend.Tests/Belah-Backend.Tests.csproj --no-restore` — passed, 64 tests, 0 failures (includes payment, inventory, webhook, in-memory ASP.NET authorization middleware, registration privacy, and seeder coverage).
- `npm test -- --watchAll=false` from `belah-website` — passed, 1 suite and 3 tests. CRA/Jest reports existing React test utility and undeclared Babel plugin warnings.
- `npm run build` from `belah-website` — passed, optimized production bundle generated with no project ESLint warnings. CRA reports existing Browserslist and Babel maintenance notices.

## Mock catalogue and storefront presentation

- `Belah-Backend/Services/DevelopmentMockProductSeeder.cs` defines 14 development sample products across Wigs, Bundles, Closures, Frontals, and Hair Care & Accessories. The descriptions identify them as samples, use stable reserved SKUs, and point only to existing local frontend images. No image binaries, product schema, or migrations were added for this feature.
- Seeding is opt-in through `ASPNETCORE_ENVIRONMENT=Development dotnet run --project Belah-Backend/Belah-Backend.csproj -- --seed-mock-products`. It refuses non-Development execution, requires the configured existing SQL Server, performs no migration/schema creation, adds absent sample SKUs, refreshes only rows bearing the seeder marker, skips collisions without that marker, and never deletes products. Do not point it at production or real inventory.
- The shared typography, palette, navigation, footer, controls, and responsive component styles are defined in the frontend global styles. Seeded `/images/...` paths resolve to the React app's same-origin public assets; backend upload paths continue resolving against the API origin.
- Seeder coverage uses EF Core InMemory and checks the 14-row catalogue, repeated-run refresh behavior, collision preservation, and environment guard. This does not substitute for the later manual check against a disposable SQL Server database.

## Live order-schema diagnosis — 2026-10-10

The earlier Development API inspection found this migration history. A newer user-provided report says the two order migrations have since been applied and the database now contains 14 products, 2 users, and 4 orders. Docker is unavailable in this environment, so that later state could not be independently verified. Preserve all four reported orders and the existing `thembi` Admin role; no user role or database records were changed during this audit.

1. `20241210174731_InitialCreate` — applied
2. `20241211120152_AddCategoryAndSpecificationToProduct` — applied
3. `20261003065424_AddSkuAndStockToProduct` — applied
4. `20261003072107_AddOrdersAndOrderItems` — applied
5. `20261010092124_AddUsersTable` — applied
6. `20261010100001_AddUserRole` — applied
7. `20261010120110_SecureOrderPaymentsAndOwnership` — pending at earlier inspection; user later reports applied, not independently verified
8. `20261010123901_AddPaymentExpiry` — pending at earlier inspection; user later reports applied, not independently verified
9. `20261010153852_EnforceUniqueProductSku` — added during this audit; pending and not applied

At the earlier inspection the database had 14 products (reserved mock SKUs, no duplicate SKU groups), 2 users, and 0 orders. The current user-provided report says there are now four orders. The earlier inspection found the existing Orders schema lacked the order ownership/payment columns; current schema is not verifiable here. The existing OrderItems foreign keys were present at the earlier inspection.

The exact pending sequence is `20261010120110_SecureOrderPaymentsAndOwnership`, then `20261010123901_AddPaymentExpiry`. The first adds nullable guest hash, provider transaction ID, and user ownership columns; adds a restricted FK from `Orders.UserId` to `Users.Id`; adds the UserId index and filtered unique indexes for guest hashes and provider transaction IDs; and replaces the payment-reference index with a filtered unique index. The second adds nullable `PaymentExpiresAt`. Because there are currently no order rows, these changes have no existing order data to backfill or constrain. They do not alter Products or Users. Migrations were not applied during this diagnostic task.

Before any migration, take and verify a restorable backup outside the repository. Example for the SQL Server container (set `DB_USER` to the existing database login; `sqlcmd` prompts for the password because no password argument is supplied):

```sh
read -r -p "SQL Server login: " DB_USER
BACKUP_NAME="BelahDB_pre_order_schema_$(date +%Y%m%d_%H%M%S).bak"
docker exec -it belah-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U "$DB_USER" -C -d master -Q "BACKUP DATABASE [BelahDB] TO DISK = N'/var/opt/mssql/data/$BACKUP_NAME' WITH COPY_ONLY, CHECKSUM;"
docker exec -it belah-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U "$DB_USER" -C -d master -Q "RESTORE VERIFYONLY FROM DISK = N'/var/opt/mssql/data/$BACKUP_NAME' WITH CHECKSUM;"
docker cp "belah-sqlserver:/var/opt/mssql/data/$BACKUP_NAME" "/tmp/$BACKUP_NAME"
```

After reviewing and approving the plan and confirming the backup, run EF from a shell with the same `ConnectionStrings__DefaultConnection` and Development configuration as the running backend. On this Linux setup, the connection setting can be copied from the running backend process into the current shell without echoing it (if `/proc` permissions allow):

```sh
BACKEND_PID="$(pgrep -n -x Belah-Backend)"
export ConnectionStrings__DefaultConnection="$(tr '\0' '\n' <"/proc/$BACKEND_PID/environ" | sed -n 's/^ConnectionStrings__DefaultConnection=//p')"
```

Do not substitute the checked-in fallback if it points to a different authentication mode/database. The target migration applies both pending migrations in order:

```sh
ASPNETCORE_ENVIRONMENT=Development dotnet ef database update 20261010123901_AddPaymentExpiry --project Belah-Backend/Belah-Backend.csproj --startup-project Belah-Backend/Belah-Backend.csproj --context ApplicationDbContext
```

Verify `dotnet ef migrations list` marks both migrations applied and query `INFORMATION_SCHEMA.COLUMNS` for the four columns above. Then restart the backend from the same IDE launch profile or configured terminal that supplies its existing connection and JWT settings (stop the old process with Ctrl+C, then run `ASPNETCORE_ENVIRONMENT=Development dotnet run --project Belah-Backend/Belah-Backend.csproj`). Finally, `GET http://localhost:5037/api/products` should still return 14 products. After approval, a one-item `POST /api/orders` smoke test can be run without initializing payment:

```sh
PRODUCT_ID="$(curl -fsS http://localhost:5037/api/products | python3 -c 'import json,sys; print(json.load(sys.stdin)[0]["id"])')"
curl -sS -o /dev/null -w 'POST /api/orders HTTP %{http_code}\n' \
  -H 'Content-Type: application/json' \
  -d "{\"customerName\":\"Schema Verification\",\"email\":\"schema-test@example.invalid\",\"phone\":\"0000000000\",\"address\":\"Local test\",\"city\":\"Test City\",\"province\":\"Test Province\",\"postalCode\":\"0000\",\"items\":[{\"productId\":$PRODUCT_ID,\"quantity\":1}]}" \
  http://localhost:5037/api/orders
```

Expected result is HTTP 201. This creates one pending test order, so run it only when that local data write is desired. Do not call `/api/payments/initialize` for this schema check. The checkout now presents readable, accessible errors for JSON and non-JSON server failures; a failed order POST does not advance to the payment step.
