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

## Fragrance and flexible catalogue adaptation — 2026-10-13

The customer-facing copy is now category-neutral and initially highlights fragrances while remaining suitable for accessories, beauty, and other merchandise. Storefront search now includes brand; category and brand filters and ZAR price ranges operate on API products. Product cards and detail pages show brands and variant-aware starting prices/stock. Customers select an available size/option before adding a variant to the cart; the selected variant ID and server-priced amount flow through order creation. Cart and checkout display the selected option.

The EF model and APIs now support product brand/type, featured status, UTC create/update timestamps, editable persistent categories, and product variants with distinct name, SKU, price, stock, active state, and optional image. Admin product management can create/edit variants and manage category names/visibility. Category and product mutations remain Admin-only. Existing single-price/stock products and order lines remain supported. Payment verification still validates inventory server-side; a successful variant purchase conditionally decrements that variant and the product's aggregate stock in the existing transaction, so duplicate verification cannot deduct twice. Order lines keep their selected variant name as a historical snapshot.

New migration: `20261013124621_AddFlexibleCatalogueAndProductVariants`. It adds nullable product brand/type/category ID, featured flag, UTC timestamps, nullable variant ID and selected-variant snapshot on order lines, and `ProductCategories`/`ProductVariants` tables. It adds unique category name/slug, filtered unique non-null variant SKU, unique product/variant name, product/category and order-line/variant indexes, a category FK with `SET NULL`, product-to-variant cascade, and a restricted order-line-to-variant FK. Existing category text and order lines are retained; the new category ID and variant ID are nullable, and timestamp defaults populate existing products with UTC time. The migration was generated only and was not applied.

The SQL Server instance was not reachable during the EF tooling connection check, so this turn did not inspect live migration history or live table data. No schema or database rows were changed. Before applying the new migration, independently verify the earlier reviewed migration sequence and database state, take and verify a restorable backup, then review this generated migration. Applying it creates new tables and nullable columns; it does not drop or overwrite Products, Users, Orders, payment rows, or existing order lines.

The ten supplied fragrance entries are prepared for intake in [`docs/Fragrance-Catalogue-Intake.md`](docs/Fragrance-Catalogue-Intake.md). They were not inserted into SQL Server or activated: the request supplies no Belah prices or physical stock counts, and several supplied names do not identify a specific fragrance variant. No image binaries were downloaded. The storefront placeholder remains in use until Belah has authorized product imagery. Confirm the bottle/box identity, local sale price, stock, size, and image rights before creating and publishing each entry.

The development-only hair mock seeder remains a test fixture and was not run or changed. It still creates its explicit sample prices/stock in Development only; do not use those sample listings as Belah fragrance inventory.

### Fragrance intake implementation — 2026-10-13

- Added a Development-only `--seed-fragrance-catalogue` command for the ten exact intake entries. It creates stable `BELAH-FRAGRANCE-INTAKE-*` SKUs, reuses/creates the Fragrances category, uses the local placeholder, and skips existing SKUs or matching product names without changing them. It never deletes products and does not touch users, roles, orders, or payment rows.
- Intake listings are public (`IsActive = true`) but use zero stock with `IsStockConfirmed = false`; purchase creation and payment verification reject them until an Admin confirms physical stock. `IsPriceApproved = false` similarly blocks checkout. Numeric retailer references are clearly labelled indicative; unresolved entries show price pending approval. Admin product edit now exposes approval/stock confirmation controls.
- Added migration `20261013135332_AddCatalogueApprovalState`, which adds the two non-null `Products` flags with default `true` to preserve existing inventory behavior. It follows `20261013124621_AddFlexibleCatalogueAndProductVariants`. Neither migration was applied in this task because the SQL Server container and live database were inaccessible and a fresh verified backup could not be made.
- Image rights were not established for any found product photos. All intake products use existing `/images/product-placeholder.svg`; the image/source and indicative-price registers are in [`docs/product-image-sources.md`](docs/product-image-sources.md) and [`docs/fragrance-catalogue-pricing-sources.md`](docs/fragrance-catalogue-pricing-sources.md). No fragrance photos were copied or downloaded.
- The local API at `localhost:5037` was not running during this task. Docker access to `/var/run/docker.sock` was denied. Therefore the seeder was not run, database contents and migration history were not verified, and no claim is made that the live catalogue has changed.
- Verification: backend tests passed (73); backend build passed with 0 warnings and 0 errors; frontend tests passed (6) with existing React/Jest deprecation warnings; frontend production build passed with existing Browserslist/Babel maintenance notices. EF reports no pending model changes relative to the added migration snapshot.

After reviewing/backup and applying the required migrations in order, use the Development shell already configured for the correct local SQL Server connection:

```sh
ASPNETCORE_ENVIRONMENT=Development dotnet run --project Belah-Backend/Belah-Backend.csproj -- --seed-fragrance-catalogue
```

Expected seeder output on a fresh catalogue is `10 added, 0 existing products skipped`. This command writes only catalogue/category rows, so first verify that the configured connection targets local `BelahDB` and that the migration backup is verified.

### Purchase confirmation email outbox — 2026-10-13

- Added SMTP-backed purchase confirmation delivery with plain-text and responsive HTML versions, a database outbox, five bounded delivery attempts, safe error-type logging, leases, and Admin-only notification inspection/retry. Both verification and signed webhook processing enqueue through the same newly-paid transition. Unique indexes enforce one confirmation per order and idempotency key; existing paid orders are not backfilled.
- Email content uses saved order-item names, options, quantities and prices. The existing public order-number tracking route is linked without exposing guest capabilities or payment references. Missing SMTP settings leave notifications Pending and do not block checkout. See [`docs/Transactional-Email.md`](docs/Transactional-Email.md) for setup and manual sandbox procedure.
- Added migration `20261013150747_AddPurchaseConfirmationEmailOutbox`. It is additive and was generated, not applied. SQL Server was unreachable during EF connection checks, so the live database target/schema/history and backup could not be verified. Before applying, verify the intended connection, review the complete pending migration sequence, and create and verify a fresh backup. No database rows or schema were changed and no email or live payment was sent.
- Verification: backend tests passed (81); frontend tests passed (8). Frontend production build passed. `git diff --check` passed. Frontend test output includes existing React testing/router warnings; build output includes existing Node/Browserslist maintenance warnings.

## Fixed delivery fee and product sale pricing — 2026-10-13

- New orders include a fixed R99 delivery fee. The cart and checkout show the item subtotal, delivery fee, and payable total before payment. The API calculates the fee and product prices, persists the delivery fee on the order, and uses the resulting total for Paystack initialization and verification; browser-supplied totals are not trusted.
- Admin product editing supports optional sale prices for products and individual variants. Sale prices must be positive, lower than the regular price, and use no more than two decimal places. The storefront displays the regular price struck through beside the sale price and savings; order lines snapshot the sale price actually charged.
- Generated migration `20261013181604_AddFixedDeliveryFeeAndProductSalePricing` adds nullable sale-price columns and a non-null order delivery-fee column with a zero default for existing orders. It is not applied. The existing database history/schema was not inspected in this task, so do not assume this or earlier generated migrations are pending or applied. Verify live migration history, back up the target, and review the full sequence before applying anything.
- No database data or schema was changed. Existing orders retain their prior total and receive a stored delivery fee of zero from the migration default; newly-created orders use R99.

### Local SQL password handling — 2026-10-13

- Repeated local startup logs showed SQL Server error 18456 for `sa`. The container login succeeded through interactive `sqlcmd`, and its published port was confirmed as 1433, but the .NET connection still rejected the password. Added optional `BELAH_SQL_PASSWORD` handling in `Program.cs`; it uses `SqlConnectionStringBuilder` to set the password as a raw value rather than manually escaping it in a shell connection string. See the zsh command in [README.md](README.md).
- Backend build passed with 0 warnings and 0 errors; `git diff --check` passed. This environment cannot access the user's Docker socket or SQL process, so the new startup path has not yet been verified against the live container. No database or credential was changed.

## Publication readiness review — 2026-10-14

The repository contains a functioning storefront and API implementation, but a production release is not yet verified. This review changed documentation only; it did not change application code, credentials, database records, or schema.

### Implemented in source

- API-backed product catalogue, product details, Admin product/category/variant editing, customer accounts and order history, Admin order management, inventory checks, Paystack server-side verification and signed webhook handling, R99 delivery pricing, optional sale pricing, and transactional purchase-confirmation email outbox.
- README descriptions were corrected to match the current source for webhooks, variants, SKU indexing, delivery/sale pricing, and email. It lists all 13 repository migrations and distinguishes migration-history evidence from direct schema verification.

### Release blockers to resolve, in order

1. **Database state and migrations:** user-provided read-only queries confirm `dbo.__EFMigrationsHistory` contains all 13 repository migration IDs in `BelahDB`, and the sampled order/catalogue/email columns and tables exist; `IX_Products_Sku` is unique with the expected non-null filter. Other indexes, foreign keys, and complete schema integrity have not been checked. Do not infer schema integrity from migration history, build, or EF snapshot output.
2. **Hosting and public configuration:** a .NET container build and Cloudflare Pages SPA fallback are now prepared, but no Azure resources or infrastructure provisioning are configured. Choose API and frontend hosting, configure HTTPS/domain/DNS, production CORS, a production `REACT_APP_API_URL`, database network access, secret management, monitoring, and backup/restore ownership.
3. **Durable image storage:** Admin uploads support a configurable `Uploads:Path` mount and static serving from that path. Production hosting must provide durable storage or replace this with object storage; ephemeral deployment storage risks losing uploaded product images.
4. **Payment and customer operations:** configure Paystack sandbox callback/webhook on public HTTPS and complete sandbox verification before live credentials. Define the operator process for `RefundRequired` orders; refunds are not automated. Verify SMTP delivery and retries using a controlled test inbox.
5. **Merchandise readiness:** confirm fragrance identity, licensed product images, final prices, and physical stock before making the intake catalogue purchasable.

The last recorded automated checks are in the sections above; they are source/build checks, not proof of the live database's physical schema, a deployed site, or production checkout. No production deployment or live payment has been performed.

### User-provided live migration-history result — 2026-10-14

The user ran a read-only `SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId` against `BelahDB` inside `belah-sqlserver` and provided the output. It contains all 13 IDs listed in README, through `20261013181604_AddFixedDeliveryFeeAndProductSalePricing`. This confirms the database's EF history table records the complete repository migration chain as applied. It does **not** independently prove that every expected column, index, and foreign key is still present or unaltered; inspect those separately before deployment. No migration was run and no data or schema was changed in this session.

The user subsequently ran a read-only SQL Server metadata query against `BelahDB` and provided the result. The checked columns were present with expected nullability: `Orders.DeliveryFee` is non-null; guest hash, payment expiry, Paystack transaction ID, and user ID are nullable; product `CategoryId` is nullable; product approval flags are non-null; product and variant `SalePrice` are nullable. The `ProductCategories`, `ProductVariants`, and `EmailNotifications` tables exist. `IX_Products_Sku` exists, is unique, and has filter `([Sku] IS NOT NULL)`. These outputs verify the queried schema elements, not every table, FK, index, or production configuration. No migration, write query, or database change was performed in this session.

### Low-cost staging preparation — 2026-10-15

- Added a .NET 8 API container definition at `Belah-Backend/Dockerfile`, root `.dockerignore`, and Cloudflare Pages SPA fallback at `belah-website/public/_redirects`. The image listens on port 8080 and does not apply migrations at startup.
- Made API CORS origins configurable through `Cors:AllowedOrigins` while retaining current local and existing site defaults. Product uploads can now use `Uploads:Path`; files at that location are served under `/uploads` so the frontend's existing API-origin URL resolution continues to work.
- Added [`docs/Low-Cost-Deployment.md`](docs/Low-Cost-Deployment.md) with Cloudflare Pages build settings, API environment requirements, a staging-only data/payment sequence, and limits. No cloud resource was created, no database was copied, and no payment or email was sent.
- Backend build passed using `/tmp/belah-dotnet-artifacts` so tracked generated-file deletions were not recreated: 0 errors and 2 NU1900 warnings because this environment could not reach the NuGet vulnerability feed. Frontend production build passed to `/tmp/belah-website-build`; the Cloudflare `_redirects` file is present in the output. CRA reported its existing outdated Browserslist data notice. `git diff --check` passed and all 428 pre-existing staged deletions remain staged.
- The Docker image itself was not built or deployed. Durable object storage (including R2) is not integrated; use a mounted persistent path or keep uploads disabled for staging, and do not treat ephemeral container storage as production-safe.
