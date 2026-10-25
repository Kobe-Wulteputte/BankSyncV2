# BankSync V2

Bank transaction sync (Enable Banking) with AI categorization, stored in PostgreSQL with field-level
encryption, served by a .NET 10 API and a Vue 3 front end behind Auth0.

See `docs/architecture.md` for the layout and `docs/api-contract.md` for the HTTP API.

## Prerequisites

- .NET 10 SDK, Node 22+, Docker Desktop
- `dotnet tool install -g dotnet-ef`
- `dotnet dev-certs https --trust` (the API listens on `https://localhost:8080` in Development, which is the Enable Banking redirect URL)

## First run (local)

1. **Database**
   ```bash
   docker compose up -d postgres
   ```
2. **Secrets** (user secrets keep them out of the repo):
   ```bash
   cd src/BS2.Api
   dotnet user-secrets set "Encryption:MasterKey" "$(openssl rand -base64 32)"
   dotnet user-secrets set "Auth0:Domain" "your-tenant.eu.auth0.com"
   dotnet user-secrets set "Auth0:Audience" "https://banksync.api"
   dotnet user-secrets set "Auth0:AllowedEmails:0" "you@example.com"
   dotnet user-secrets set "Auth0:AdminEmails:0" "you@example.com"   # may edit shared categories and import Excel
   dotnet user-secrets set "EnableBanking:AppKid" "<kid>"
   dotnet user-secrets set "EnableBanking:KeyPath" "C:/path/to/enablebanking.pem"
   dotnet user-secrets set "OpenAIServiceOptions:ApiKey" "sk-..."
   dotnet user-secrets set "Classification:Model" "ft:gpt-4.1-nano-2025-04-14:personal:banksync:..."
   dotnet user-secrets set "Sync:NotifyEmail" "you@example.com"
   dotnet user-secrets set "Mail:SmtpHost" "smtp.gmail.com"      # optional, enables expiry mails
   dotnet user-secrets set "Mail:SmtpUser" "..."
   dotnet user-secrets set "Mail:SmtpPassword" "..."
   dotnet user-secrets set "Mail:From" "..."
   ```
   Losing the master key means losing every encrypted column. Back it up.
3. **Migrations** apply automatically at startup. To create a new one after a model change:
   ```bash
   dotnet ef migrations add <Name> --project src/BS2.Infrastructure --startup-project src/BS2.Infrastructure --output-dir Persistence/Migrations
   ```
4. **API**
   ```bash
   dotnet run --project src/BS2.Api
   ```
5. **Client**
   ```bash
   cd client && cp .env.example .env   # fill VITE_AUTH0_*
   npm install && npm run dev          # http://localhost:5173, proxies /api to https://localhost:8080
   ```
6. Open the app, go to **Settings → Import V1 Excel** to load the historical workbook, then **Banks → Add bank**.

## Auth0 setup

1. Create an **API** (Applications → APIs): identifier = the `Auth0:Audience` value, RS256.
2. Create a **Single Page Application**: allowed callback/logout/web origins `http://localhost:5173` and your public origin. Its client id goes in `client/.env` as `VITE_AUTH0_CLIENT_ID`.
3. Access tokens carry no email by default. Either put your `sub` in `Auth0:AllowedSubjects` (and `Auth0:AdminSubjects`), or add a post-login Action:
   ```js
   exports.onExecutePostLogin = async (event, api) => {
     api.accessToken.setCustomClaim('https://banksync/email', event.user.email);
     api.accessToken.setCustomClaim('https://banksync/email_verified', event.user.email_verified);
   };
   ```
   and use `Auth0:AllowedEmails` (and `Auth0:AdminEmails`). An email without `email_verified: true` is ignored.
4. Categories and category classes are shared by all users. Only admins (`Auth0:AdminSubjects` / `Auth0:AdminEmails`) can change them or run the Excel import.

## Enable Banking

Register the redirect URL `{App:PublicOrigin}/api/banks/callback` in the Enable Banking control panel (locally `https://localhost:8080/api/banks/callback`). A new bank also has to be linked to your Enable Banking profile there, otherwise the session authorizes but exposes zero accounts.

## Edenred

myEdenred only logs in through a browser, so the server cannot fetch it. **Banks → Edenred** shows a bookmarklet: drag it to the bookmarks bar, log in on myedenred.be, click it, open any myEdenred page, then click *Send to BankSync*. It reuses the myEdenred tab's own API headers to fetch the operations and posts them to the BankSync tab, which imports them. The token stays in that tab. The same page also accepts the JSON pasted by hand. The scheduled sync does not touch Edenred.

## Run as a Windows service

```powershell
dotnet publish src/BS2.Api -c Release -o C:\BankSync
sc.exe create BankSync binPath= "C:\BankSync\BS2.Api.exe" start= auto
```
Configuration then comes from `C:\BankSync\appsettings.Production.json` or `BS2_`-prefixed environment variables (`BS2_Encryption__MasterKey`, ...).

## Run in a container / cloud

```bash
cp .env.example .env            # fill in
mkdir secrets && cp enablebanking.pem secrets/
docker compose --profile app up -d --build
```
To update a server that is already set up this way, run `bash deploy.sh` from Git Bash (`DEPLOY_HOST` overrides the default `kobew@192.168.68.63`). It replaces the code on the server, keeps `.env` and `secrets/`, rebuilds and checks the app answers.

The image serves the API and the built client on port 8080. Point it at any PostgreSQL via `BS2_ConnectionStrings__Postgres`. Terminate TLS in front of it (reverse proxy, container platform ingress) and set `BS2_App__PublicOrigin` to the public https origin.

## Tests

```bash
dotnet test tests/BS2.Tests
cd client && npm test -- --run
```
