# Purchase confirmation email

## Delivery design

The Paystack verification endpoint and signed webhook use the same successful-payment transition. When that transition newly persists an order as paid, it adds one `EmailNotifications` outbox record in the same SQL transaction. The unique order and idempotency-key indexes prevent repeat verification or a simultaneous webhook from creating another confirmation. The background worker claims due records with a time-limited lease, sends responsive HTML plus plain text, and retries up to five attempts with delays of 1, 5, 15, and 60 minutes. A failed record remains visible to Admins at `GET /api/admin/email-notifications?status=Failed`; an Admin can requeue it with `POST /api/admin/email-notifications/{id}/retry`.

The email is rendered from the saved order and order-item snapshot, including saved prices and selected options. Customer-provided content is HTML-escaped. It includes no payment reference, guest capability, password, or authentication token. The tracking link uses the existing public order-number route and only includes the unguessable order number. SMTP errors record an error type, not message content, address, or credentials.

The outbox provides at-least-once delivery. If the process stops after the SMTP server accepts a message but before the database records `Sent`, a retry can cause a duplicate email; SMTP does not provide a cross-system exactly-once transaction. Normal repeated payment verification and concurrent webhook/verification requests create only one outbox record.

## Development configuration

The backend has a .NET User Secrets store. Run these commands from `Belah-Backend`; replace placeholders locally and never paste real values into source files or logs:

```sh
dotnet user-secrets set "Email:Smtp:Host" "<smtp-host>"
dotnet user-secrets set "Email:Smtp:Port" "587"
dotnet user-secrets set "Email:Smtp:Username" "<smtp-username>"
dotnet user-secrets set "Email:Smtp:Password" "<smtp-password>"
dotnet user-secrets set "Email:Smtp:FromAddress" "<verified-sender-address>"
dotnet user-secrets set "Email:Smtp:FromName" "Belah"
dotnet user-secrets set "Email:Smtp:SecureSocketOptions" "StartTls"
dotnet user-secrets set "Email:SupportEmail" "<customer-support-address>"
dotnet user-secrets set "Email:StorefrontBaseUrl" "http://localhost:3000"
```

`SecureSocketOptions` accepts `StartTls` (recommended for port 587), `StartTlsWhenAvailable`, `SslOnConnect` (commonly port 465), or `None` (local development only). Username and password must either both be set or both be empty. `FromAddress` must be a valid mailbox. Configure the sender domain/address with the SMTP provider before production use.

In production, provide equivalent settings through the deployment secret manager/environment, using names such as `Email__Smtp__Host`, `Email__Smtp__Port`, `Email__Smtp__Username`, `Email__Smtp__Password`, `Email__Smtp__FromAddress`, `Email__Smtp__FromName`, `Email__Smtp__SecureSocketOptions`, `Email__SupportEmail`, and `Email__StorefrontBaseUrl`. Do not commit an `appsettings` file containing credentials.

Without valid SMTP settings, Development still starts and checkout is unaffected. Newly confirmed orders retain a `Pending` outbox record and are not falsely marked sent. The worker emits one generic configuration warning and does not attempt delivery. Configure SMTP and restart the backend to resume processing.

## Migration and safe enablement

Migration `20261013150747_AddPurchaseConfirmationEmailOutbox` adds the outbox table, order relationship, unique idempotency/order indexes, and processing index. It is additive; it does not backfill historical paid orders or modify existing orders, payment rows, users, roles, products, or variants. The migration is generated but **has not been applied**. The SQL Server connection was unavailable during this work, so the live migration history/schema and target database could not be confirmed. Do not apply it until the configured target is independently verified and a fresh backup has been created and verified.

From the repository root, first inspect the migration status against the intended Development connection without printing the connection string:

```sh
dotnet ef migrations list --project Belah-Backend/Belah-Backend.csproj --startup-project Belah-Backend/Belah-Backend.csproj --context ApplicationDbContext
```

Check the full pending sequence; `dotnet ef database update` applies every pending migration, not only the email migration. Before any update, take a fresh SQL Server backup and verify it with `RESTORE VERIFYONLY`, then copy it to protected storage outside the SQL Server container. After the update, verify `EmailNotifications`, its foreign key/indexes, and `__EFMigrationsHistory`. Do not run these database-changing steps against a production database without the normal backup/approval process.

## Manual end-to-end check

Use a local/test database, an SMTP sandbox or controlled test mailbox, and Paystack **sandbox** credentials only. Confirm the API points to that local/test database, apply the reviewed pending migration sequence only after a verified backup, set the configuration above to the sandbox sender/mailbox, and restart the API with the existing HTTP profile:

```sh
dotnet run --project Belah-Backend/Belah-Backend.csproj --launch-profile http
```

Place a low-value order using the storefront with the test mailbox, then complete it through Paystack sandbox checkout. Verify the order shows Paid and exactly one notification transitions from Pending/Processing to Sent. Check the inbox and spam folder for the saved order number, items, selected options, quantities, saved item prices, ZAR total, delivery details, and tracking link. Confirm that pending, failed, expired, amount-mismatched, and invalid-signature cases do not create a confirmation. For retry testing, use a deliberately invalid SMTP sandbox endpoint, confirm the paid order stays Paid and the notification retries/fails, then configure the valid sandbox and have an Admin retry the failed notification. Do not test against a real buyer address or initiate live payment.
