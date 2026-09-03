# Mail Triage setup

The app is built and runs (`dotnet run --project ITMartinMailTriage.Server`,
opens at http://localhost:8080 by default, or pick another `--urls`). Claude
scoring already works - it reuses the suite's existing API key from
`magic.env`. Two things only you can do before syncing will work:

## 1. Gmail

1. Go to https://console.cloud.google.com/ -> create or pick a project.
2. **APIs & Services -> Library** -> enable "Gmail API".
3. **APIs & Services -> Credentials -> Create Credentials -> OAuth client ID**.
   - If prompted, configure the consent screen first (External, add yourself
     as a test user - no Google review needed for personal use).
   - Application type: **Desktop app**.
4. Download the resulting JSON, save it as
   `ITMartinMailTriage.Server/data/gmail-credentials.json` (the `data/`
   folder is gitignored, safe to put secrets there).
5. First "Sync & Score" run opens a browser to sign in and consent - after
   that the refresh token is cached under `data/gmail-token/`, no more
   prompts.

## 2. Outlook

1. Go to https://entra.microsoft.com/ -> **App registrations -> New
   registration**.
   - Name: anything, e.g. "ITMartin Mail Triage".
   - Supported account types: your choice (personal Microsoft account works
     with "Accounts in any organizational directory and personal Microsoft
     accounts").
   - Redirect URI: platform **Mobile and desktop applications**,
     `http://localhost`.
2. **API permissions -> Add a permission -> Microsoft Graph -> Delegated
   permissions -> Mail.Read** -> add it (admin consent not needed for a
   personal mailbox).
3. Copy the **Application (client) ID** from the registration's Overview
   page.
4. Put it in `ITMartinMailTriage.Server/appsettings.Development.json` under
   `MailTriage:Outlook:ClientId` (already has a placeholder there).
5. First "Sync & Score" run opens a browser to sign in and consent - MSAL
   caches the token itself after that.

## Running it

```
cd ITMartinMailTriage.Server
dotnet run
```

Click "Sync & Score" on the home page. It fetches up to 50 recent emails per
account, scores up to 200 unscored emails per run (10 Claude calls x 20
emails/call - a hard cap per CLAUDE.md's cost-discipline rules), and shows
them sorted by relevance with "needs response" flagged. Edit the "what
matters to you" text box any time and save - it takes effect on the next run.
