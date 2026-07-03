# ITMartin Test Suite — Setup Guide

## Run tests locally

```powershell
# All tests
dotnet test ITMartinTests

# Only smoke (fast — no browser, ~30 sec)
dotnet test ITMartinTests --filter "Category=Smoke"

# Only flow (Playwright browser, ~3 min)
dotnet test ITMartinTests --filter "Category=Flow"
```

First time only — install Playwright browser:
```powershell
dotnet build ITMartinTests
pwsh ITMartinTests\bin\Debug\net8.0\playwright.ps1 install chromium
```

k6 load test:
```powershell
k6 run ITMartinTests/k6/load-concurrent.js
```

---

## GitHub setup (one-time)

### 1. Push the repo to GitHub

```powershell
cd C:\Users\hvidb\RiderProjects\ITMartinSuite
git init
git add .
git commit -m "Initial commit"
gh repo create ITMartinSuite --private --source=. --push
```

### 2. Enable GitHub Pages

Go to your repo → **Settings → Pages** → Source: **Deploy from a branch** → Branch: `gh-pages`.

The dashboard will be available at:
`https://YOUR_GITHUB_USERNAME.github.io/ITMartinSuite/`

### 3. Add GitHub Secrets

Go to repo → **Settings → Secrets and variables → Actions → New repository secret**:

| Secret | Value | Purpose |
|---|---|---|
| `POLL_ADMIN_PIN` | `1234` | Admin login for stem.itmartin.dk |
| `GALLERY_PASSWORD_MIE` | `8670Låsby` | Mie gallery password |
| `DAILYBRIEF_URL` | `https://nyheder.itmartin.dk` | URL for DailyBrief (confirm the real domain) |

### 4. (Optional) SSH to NAS for automatic container start/stop

This lets the nightly run start all containers before testing and stop them after.

**a. Generate an SSH key:**
```powershell
ssh-keygen -t ed25519 -C "github-actions" -f github-actions-key
```

**b. Copy public key to NAS:**
```powershell
cat github-actions-key.pub | ssh martinhvidberg@10.0.0.126 "cat >> ~/.ssh/authorized_keys"
```

**c. Add GitHub Secrets:**

| Secret | Value |
|---|---|
| `NAS_SSH_KEY` | Contents of `github-actions-key` (the private key) |
| `NAS_SSH_USER` | `martinhvidberg` |

**d. Tailscale (so GitHub can reach the NAS):**

Go to Tailscale admin → Settings → OAuth clients → Create client:
- Scopes: `devices:write`
- Tags: `tag:ci` (add this tag in your Tailscale ACL policy first)

| Secret | Value |
|---|---|
| `TS_OAUTH_CLIENT_ID` | From Tailscale OAuth client |
| `TS_OAUTH_SECRET`    | From Tailscale OAuth client |

---

## How test results are reported

| Where | What |
|---|---|
| **GitHub Pages** | Full dashboard — green/red per app, response times, load test |
| **GitHub Actions UI** | Per-test results in the "ITMartin Tests" check (click Details on any run) |
| **Email** | GitHub sends an email automatically when any workflow run fails |
| **Artifacts** | Raw XML + k6 JSON kept for 60 days per run |

---

## When nightly fails: diagnose locally

If GitHub Actions reports failures at 04:00, run the same tests from your PC:

```powershell
dotnet test ITMartinTests --filter "Category=Smoke" -v normal
```

- **Tests also fail locally** → NAS or Cloudflare is genuinely down
- **Tests pass locally** → GitHub runner can't reach itmartin.dk (rare, usually transient)

---

## Adding tests for a new app

1. Add an entry to `AppRegistry.cs`
2. Smoke test is automatic (HttpClient hits the URL)
3. For a flow test, add a file in `Flows/` extending `PageTest`, tagged `[Category("Flow")]`
