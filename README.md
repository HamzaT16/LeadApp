# LeadApp — Partner MKF kreditni upiti

Blazor Web App (.NET 10, Interactive Server) that collects loan inquiries and talks to the partner CRM.
Implements the Web → CRM operations of the *CX360 API specifikacija*:

* **section 1**: `POST /api/sales-opportunity/web/create` (lead → sales opportunity)
* **section 6**: `POST /api/campaign/online` (online campaign)

Sections 2–5 (CRM ↔ iMicro) are out of scope.

```
src/LeadApp            web app
  Components/Pages     Home (/ and /k/{slug}), Account/Login, Admin/* (dashboard, leads, campaigns, account)
  Services             CRM client, lead submission, Firestore/in-memory storage, auth, Excel export
  Data                 opcina.csv (cityId), mjesna_zajednica.csv (localCommunityId)
tests/LeadApp.Tests    xUnit
firestore.rules        deny-all for client SDKs
```

## Run locally

```powershell
cd src/LeadApp
dotnet user-secrets set "Admin:Email" "you@partner.ba"
dotnet user-secrets set "Admin:InitialPassword" "<at least 12 chars>"
# optional — without these, Development uses an in-memory store (data is lost on restart):
dotnet user-secrets set "Firebase:CredentialsPath" "C:\secure\mkf-partner-app-service-account.json"
dotnet run --launch-profile https
```

In Development the CRM is faked (`Crm:UseFake=true` in `appsettings.Development.json`) and the top bar shows an
**Admin DEV** chip. In every other environment that chip is not rendered: open `/admin` directly.

Tests: `dotnet test` from the repo root.

## Configuration

| Key | Where | Notes |
|---|---|---|
| `Crm:BaseUrl` | appsettings / env | e.g. `https://crm.partner.ba` |
| `Crm:ApiKey` | **secret** | sent as `X-PARTNER-API-KEY: App <key>` |
| `Crm:DefaultSourceCode` | appsettings | sourceCode for leads without a campaign (default `EMAIL`) |
| `Crm:CreateCampaignPath` | appsettings | default `/api/campaign/online` |
| `Crm:DefaultEmployeeId` | appsettings | prefills `createdByEmployeeId` (ROW_ID from employee.csv) in the campaign form |
| `Firebase:ProjectId` | appsettings | `mkf-partner-app` |
| `Firebase:CredentialsPath` or `Firebase:CredentialsJson` | **secret** | service-account key; required outside Development |
| `Admin:Email`, `Admin:InitialPassword` | **secret** | seeds the single admin only when no admin exists yet |
| `LeadForm:*` | appsettings | amount range/step, currency, submit rate limit |

Production: use environment variables (`Crm__ApiKey`, `Firebase__CredentialsJson`, …) or Azure Key Vault.
After the first start, change the seeded password under **Admin → Račun** and remove `Admin:InitialPassword`.

## How it works

* **Public form** — FL/PL, amount slider, općina → mjesna zajednica cascade from the CSVs, purpose, consent.
  Validation follows the spec, but stricter on contact: **phone is mandatory**, e-mail optional (≤255/≤128 chars, MZ must belong to the općina, amount > 0).
  Phones are normalised to `+387…` so the CRM can match contacts.
* **Submission** — the lead is saved first (sequential number via a Firestore transaction), then sent to the CRM.
  A CRM failure never loses the lead: it is marked *Greška* and can be resent from the admin panel.
  POSTs are never retried automatically (a retry could create a duplicate opportunity).
* **Campaigns** — each gets a slug, link `/k/{slug}`. Leads from the link carry the slug as `urlId` and the
  campaign's lead source as `sourceCode`. Views are counted once per visit, leads are counted on submit,
  and inactive or expired campaigns show a notice.
* **Campaign → CRM (section 6)** — on create the campaign is saved locally, then registered with
  `POST /api/campaign/online`. The request carries name, description, FL/PL, target group, `channelCode`, `urlId`,
  cost (EUR) and the creator's employee ID. Dates are sent as ISO 8601 with the Europe/Sarajevo offset: the start
  at 00:00:00, the end and `endTimeLimit` at 23:59:59.
  * `channelCode` (promotion channel) and `sourceCode` (lead source) are separate codebooks. The lead source defaults
    from the channel where there's a natural match (Viber/SMS_VIBER→VIBER, SMS→SMS, EMAIL→EMAIL,
    PHONE→CALL_CENTER, BRANCH→BRANCH_OFFICE) and can be changed.
  * A CRM failure keeps the campaign, marks it *Nije u CRM-u* and offers **Pošalji u CRM**. A `409` (urlId taken)
    reopens the form on the urlId field; a `404` means the employee ID is unknown to the CRM.
  * Once registered, the CRM fields are read-only (the spec has no update endpoint). Product, lead source and
    status stay editable because they're local.
* **Admin** — cookie login (no registration), 5 failed attempts → 15 min lockout, login endpoint rate-limited.
  Dashboard, leads grid (filters, details drawer, resend), campaigns (create/edit/activate/delete/copy link),
  password change, and Excel export.

## Security notes (Sigurnosne preporuke)

| Recommendation | Status |
|---|---|
| API key not in the browser | ✅ CRM is called only from the server; key comes from secrets |
| Firebase rules | ✅ Browsers no longer use Firebase at all; `firestore.rules` denies everything. Deploy with `firebase deploy --only firestore:rules` |
| Bots and duplicates | ✅ honeypot, per-IP limit (5 per 10 min), button locked while sending. Captcha was not added (your decision) |
| XSS / Excel formulas | ✅ Razor encodes all output; exported text cells are strings with Excel's quote prefix for `= + - @` |
| Old libraries, CSP, SRI | ✅ No CDN scripts (SheetJS and the Firebase JS SDK are gone); strict CSP and security headers |
| Data location | ⚠️ Check the Firestore region of `mkf-partner-app` against BiH data-protection rules |
| Entra ID SSO + MFA | ⚠️ Not done (you chose a local admin account). Cookie auth makes swapping to OpenID Connect straightforward later |

Behind a reverse proxy, enable `ForwardedHeaders` so the per-IP limit sees real client addresses.
