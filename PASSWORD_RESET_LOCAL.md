# Password reset: local and Gmail verification

Implementation date: 2026-09-18. Only ordinary User identities (Member, Trainer,
Creator) participate; AdminAccount is separate.

## Run locally

From this clone, start the backend with an isolated database:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Logging__EventLog__LogLevel__Default = 'None'
$env:ConnectionStrings__Default = 'Data Source=App_Data/colearnx-member-next-local.db'
dotnet run --project .\CoLearnX.Server\CoLearnX.Server.csproj --no-launch-profile --urls http://localhost:5088
```

In another terminal, from `colearnx.client`:

```powershell
$env:ASPNETCORE_URLS = 'http://localhost:5088'
$env:DEV_SERVER_PORT = '55128'
npm.cmd run dev -- --host localhost
```

Open https://localhost:55128/login and choose **Forgot password?**. The emailed
link opens `/reset-password#token=...`; the page removes the fragment from the
address bar after loading. Do not refresh that page before completing the reset;
if necessary, reopen the original email link.

## Gmail SMTP

The user's current choice is Gmail, replacing the earlier QQ plan. No QQ script
or QQ credentials are required. The same SMTP implementation supports Gmail.

Enable Google 2-Step Verification and create an app password:
https://support.google.com/mail/answer/185833?hl=en

Run the following in your own PowerShell, entering the app password at the hidden
prompt. Do not paste it into chat, source files or a command argument.

```powershell
.\scripts\Configure-GmailPasswordReset.ps1 -Email 'jianglingmuse@gmail.com'
```

This stores these settings in local .NET user-secrets (outside the repository):

| Key | Value |
| --- | --- |
| PasswordReset:DeliveryMode | Smtp |
| PasswordReset:SmtpHost | smtp.gmail.com |
| PasswordReset:SmtpPort | 587 |
| PasswordReset:SmtpUsername | Your Gmail address |
| PasswordReset:FromAddress | Your Gmail address |
| PasswordReset:SmtpPassword | Google app password |
| PasswordReset:ClientBaseUrl | https://localhost:55128 |

Restart the backend after configuration. The user-secrets ID is inherited from
the project; other clones with the same ID can see the same configuration. Do not
run their mail flows during this verification. Production should use environment
variables or its secret store with a public HTTPS ClientBaseUrl.

Default Development delivery captures `.eml` files in `CoLearnX.Server/App_Data/mail`.
Setting DeliveryMode=Smtp explicitly enables real mail in Development. Automated
Testing hosts always capture or inject a test sender, even if SMTP is configured.
Production requires a configured SMTP host and sender and does not use pickup.
Credentials, captured mail, local databases and verification account state are
under ignored locations; none should be committed.

## API/security behavior

- POST `/api/auth/forgot-password` accepts `{ email }`. Unknown, inactive, Admin,
  throttled and delivery-failure requests return the same 200 message. Delivery
  failures log only a generic configuration/delivery warning.
- POST `/api/auth/reset-password` accepts `{ token, newPassword }`. Tokens contain
  256 random bits; only SHA-256 hashes are persisted. Default lifetime is 30
  minutes, limited by configuration to 1–60 minutes.
- One current token row per ordinary user; persisted per-account cooldown defaults
  to 60 seconds. IP request limiting is 8 forgot-password attempts per 10 minutes.
- Conditional database UPDATE claims the token once. Password update and a new
  SessionStamp commit in the same transaction. Old password, old JWT, expired,
  forged and already-used credentials are rejected.
- New passwords use existing PasswordRules; no email-based identity is accepted
  from the reset request itself. The token determines the account.
- A uniform API response is not proof of mail delivery. Verify the real inbox and
  complete a reset and subsequent login before marking external mail accepted.

## Database compatibility

Fresh SQLite and SQL Server databases include PasswordResetTokens via the EF model.
Existing SQLite databases at this clone's baseline gain the table/index on startup
without deleting user data. A test drops only that table in a disposable temporary
database and verifies two startup passes preserve users and password hashes.

For an existing SQL Server database, startup fails clearly if the new table is
absent. Review/apply `scripts/PasswordReset.SqlServer.sql` before startup. It is
idempotent and does not modify Users. SQL Server execution has not been tested in
this local SQLite run. This is not a repository-wide migrations conversion.

## Current verification status

- Password flows, role coverage, expiry, forged token, replay, four simultaneous
  consumers, generic responses and old-session invalidation passed automated API tests.
- Gmail SMTP TCP port 587 was reachable on this computer.
- A local Member test account exists for the authorized Gmail address.
- On 2026-09-19, local user-secrets readiness was verified without printing the
  app password; the backend was restarted with SMTP delivery enabled.
- A real request from the Forgot password browser page delivered "Reset your
  CoLearnX password" to jianglingmuse@gmail.com at 16:36 Asia/Shanghai. Receipt
  was verified in Gmail, rather than inferred from the uniform API response.
- The email link opened the local Set a new password page, and its token fragment
  was removed from the address bar. The page is handed to the user for password
  entry and submission. That account's completed reset, subsequent login and
  old-credential rejection remain pending this user step.
