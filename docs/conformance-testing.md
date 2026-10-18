# OIDC / OAuth conformance testing — plan

Status: **harness committed; nightly CI pipeline deferred.** The committed, runnable automation lives in
[`build/conformance/`](../build/conformance) (orchestration `run-conformance.ps1` + config templates +
baselines), summarized in [`docs/conformance/external-suite.md`](conformance/external-suite.md); the
appendices below are the reference material it was lifted from. This document remains the design and work plan
for validating the NCode.Identity OpenID Provider (OP) against an **independent**, spec-author-maintained
conformance harness, and for driving that harness **fully automated** in CI. It is linked from
[`BACKLOG.md`](../BACKLOG.md) (group `T`) so the backlog stays lean.

## Why an external harness (not just our own tests)

Our own integration tests ([`NCode.Identity.OpenId.IntegrationTests`](../tests/NCode.Identity.OpenId.IntegrationTests))
exercise the endpoints over the wire via `WebApplicationFactory`/`TestServer`, but they encode **our own
reading** of the specs — the same reading that produced the implementation, so a shared misunderstanding
passes both. An independent oracle removes that blind spot.

The industry-standard oracle is the **OpenID Foundation Conformance Suite** — free, open source (MIT,
Apache-2 for libs), and self-hostable:

- Source / wiki: <https://gitlab.com/openid/conformance-suite>
- Hosted instance (free to run; fee only for the certification mark): <https://www.certification.openid.net/>
- Automated-testing tutorial: <https://gitlab.com/openid/conformance-suite-automated-testing-tutorial>

**Local by default — the OP is never exposed publicly.** The committed harness
([`build/conformance/run-conformance.ps1`](../build/conformance/run-conformance.ps1)) **self-hosts** the suite from
prebuilt Docker images on the developer's machine; the suite reaches the OP over `host.docker.internal:5001`, so no
public ingress is required. The OpenID Foundation's **hosted** instance (`https://www.certification.openid.net/`) is an
optional alternative that _would_ require a publicly reachable OP — it is not the default path and not a prerequisite of
backlog `T1`.

It is what Auth0, Okta, Keycloak, Ping, and Duende IdentityServer use to self-certify. There is **no
separate official OAuth 2.0/2.1-only** suite; the OIDC **Basic/Config** OP profiles exercise the OAuth
2.0 authorization-code and token endpoints, and we supplement with targeted RFC checks (6749/6750, 7636
PKCE, 8414 metadata, 7009 revocation, 7662 introspection, 8628 device).

## How the suite works (architecture)

- **Stack:** a Java/Spring Boot server + MongoDB + an nginx TLS front end, all run via Docker Compose.
  Prebuilt images exist (`docker-compose-prebuilt.yml`) so **no Java build is required** — this is the
  path we use. Default external base URL is `https://localhost.emobix.co.uk:8443` (a hostname that
  resolves to `127.0.0.1`; it must be added to the hosts file).
- **Role reversal:** for OP testing the **suite acts as the Relying Party / client**; our host is
  the **OP under test**. The suite drives the browser through the authorize flow, then calls our token /
  userinfo / jwks / discovery endpoints directly and asserts on every response.
- **Redirect URI:** the suite's RP callback the OP must accept is
  `https://<suite-base>/test/a/<ALIAS>/callback`, where `<ALIAS>` is a unique string we pick (e.g.
  `ncode`). For the hosted instance the base is `https://www.certification.openid.net`.
- **Pass criteria:** every test in a profile must be `PASSED`, `REVIEW`, `WARNING`, or `SKIPPED`. Any
  `FAILED` or `INTERRUPTED` result blocks certification.

### Browser automation (the crux of full automation)

The suite completes the interactive login/consent via a `BrowserControl` driven by headless Selenium.
It is configured by a `browser` array in the test config JSON: each entry has a URL `match` and an
ordered list of `tasks`; each task has its own `match` plus `commands` such as
`["text","id","<field>","<value>"]`, `["click","name","<btn>"]`, and a final verification task that
waits for the callback (`["wait","id","submission_complete",10]`). Element location uses Selenium
selectors (`id` / `name` / `xpath` / `css` / `class`).

**Implication for us:** our login and consent pages must expose **stable, scriptable element ids/names**
and there must be a **deterministic seeded test user**. Without this, full automation is impossible even
though the protocol is correct.

### CI driver

`scripts/run-test-plan.py` (async Python) drives plans headlessly against the suite's REST API and
**exits non-zero on any unexpected failure** — the CI gate. Key mechanics:

- Invocation: `run-test-plan.py <test-plan-name> <config-file> [<plan> <config> ...]`.
- Environment: `CONFORMANCE_SERVER` (suite base URL), `CONFORMANCE_TOKEN` (API token; omitted in
  dev/`--fintechlabs.devmode=true`), optional `DISABLE_SSL_VERIFY`, `CONFORMANCE_DEV_MODE`.
- Config JSON supports placeholders (`{BASEURL}`, `{EXTERNALBASEURL}`, `{HOSTNAME}`, …) substituted at
  run time.
- **Baselines:** `--expected-failures-file` and `--expected-skips-file` take JSON lists that codify
  _known, intentional_ deviations (matched by test-name / variant / config / block / condition). This is
  how we keep a green gate while we have documented non-support — unexpected _new_ failures still fail
  the run, and stale baseline entries that no longer fire also fail it.
- `--export-dir` saves per-plan result zips; `compare-results.py` diffs runs.

## Target profiles (phased)

Selected under "Test an OpenID Provider" in the suite:

1. **Basic OP** (`oidcc-basic-certification-test-plan`) — authorization-code flow. **First target.**
2. **Config OP** (`oidcc-config-certification-test-plan`) — validates `/.well-known/openid-configuration`.
3. **Form Post OP** — only if `response_mode=form_post` is supported; run per auth profile.
4. **Implicit OP / Hybrid OP** — only after confirming `id_token` / `token` / `code id_token` response
   types are fully implemented at the authorize endpoint (constants exist; flow support must be verified).
5. **Dynamic OP** — requires Dynamic Client Registration (RFC 7591), **not yet implemented** (gap `F6`).
6. **FAPI 1/2** — deferred; only if we target open-finance ecosystems.

## What "full automation" requires in this repo

Prerequisites, roughly in dependency order:

1. **Reachable HTTPS host.** Run the host on a real Kestrel HTTPS endpoint the suite's Selenium/nginx
   can reach (not `TestServer`). The existing
   [`HostApplicationFactory`](../tests/NCode.Identity.OpenId.IntegrationTests/Infrastructure/HostApplicationFactory.cs)
   is in-process only; CI needs a launched process (Docker container or `dotnet run`) addressable from
   the suite. On Windows/Docker, the suite reaches the host via `host.docker.internal`.
2. **Automatable login + consent UI** with stable selectors and a **seeded deterministic conformance
   user** (fixed username/password). Auto-consent (or a scripted consent task) for the test client.
3. **Deterministic conformance tenant + clients.** Either:
    - seed the required clients (the manual path needs **three**: two `client_secret_basic`, one
      `client_secret_post` — the second basic client exists so the suite can prove auth codes are bound to
      the issuing client), **or**
    - implement **Dynamic Client Registration** (gap `F6`), which removes manual seeding _and_ lets the
      suite run plan modules in parallel (it serialises when an `alias`/static client is used).
4. **Discovery metadata alignment.** Ensure
   [`DefaultDiscoveryEndpointHandler`](../src/NCode.Identity.OpenId.Authentication/Endpoints/Discovery/DefaultDiscoveryEndpointHandler.cs)
   advertises exactly what is supported: `response_types_supported`, `grant_types_supported`,
   `code_challenge_methods_supported` (`S256`), `token_endpoint_auth_methods_supported`,
   `scopes_supported` (incl. `openid`, `profile`, `email`), `subject_types_supported`,
   `id_token_signing_alg_values_supported` (`RS256`), and the jwks URI.
5. **TLS trust.** The suite calls our OP over TLS (dev cert) and uses a snake-oil cert itself;
   `--fintechlabs.devmode=true` / `DISABLE_SSL_VERIFY` relax verification for local runs.
6. **Config templates** (one per profile) with the `browser` automation block and our alias.
7. **Orchestration script** (PowerShell) that: launches the host on the reachable URL → `docker compose
-f docker-compose-prebuilt.yml up` → waits for readiness → runs `run-test-plan.py` with the config(s)
   and the expected-failures/-skips baseline → exports result zips → tears everything down → surfaces the
   exit code.
8. **Baseline files** (`expected-failures.json`, `expected-skips.json`) encoding intentional non-support,
   kept in the repo next to the configs and reviewed on every change.
9. **CI wiring:** a **separate, opt-in/nightly** pipeline — **not** [`build/dod.ps1`](../build/dod.ps1),
   whose inner loop must stay fast and hermetic. The job needs Docker + Python 3; no local Java (prebuilt
   images). Gate it on the baseline so a regression turns the pipeline red.

### Windows developer notes

- Add `127.0.0.1  localhost.emobix.co.uk` and `127.0.0.1  host.docker.internal` to
  `C:\Windows\System32\drivers\etc\hosts` (a fresh Docker Desktop install may not write the latter, and the
  host-side readiness probe / pre-flight reach the OP issuer through it).
- Install a real Python 3 via the Python install manager (see
  [`build/conformance/README.md`](../build/conformance/README.md)) — the Microsoft Store `python` alias is on
  PATH but fails when run.
- MongoDB bind-mounts are unreliable on Windows; use a named Docker volume (per the suite's Windows
  compose snippet).
- If a profile needs port 443, map it with `netsh interface portproxy add v4tov4 listenport=443
connectaddress=localhost connectport=8443` (reset with `netsh interface portproxy reset`).

## Known prerequisite gaps

- **`F6` (new) — Dynamic Client Registration (RFC 7591).** No `registration_endpoint`. Not required for
  Basic/Config (manual 3-client seeding works) but strongly desirable: it removes manual seeding and
  unlocks parallel plan execution; it is also its own certification profile (Dynamic OP).
- **Automatable login/consent + seeded conformance user.** The interactive UI must expose stable
  selectors and a deterministic test identity for `BrowserControl`.
- **Implicit/Hybrid response-type support** must be confirmed before those profiles are attempted.

## Proposed work breakdown

- **Phase A — Local harness:** stand up the suite via prebuilt Docker images; confirm it reaches the
  host discovery + jwks endpoints.
- **Phase B — Conformance-ready host:** reachable HTTPS host; seeded tenant/clients + test user;
  automatable login/consent; discovery metadata aligned.
- **Phase C — First plans (manual trigger):** run Basic OP + Config OP; triage every result into
  _bug-to-fix_ vs _documented non-support_.
- **Phase D — Full automation:** config templates with `browser` blocks + `run-test-plan.py` +
  orchestration script + baseline files; green locally end to end.
- **Phase E — CI:** nightly/opt-in pipeline gated on the baseline; result zips published as artifacts.
- **Phase F — Record outcomes:** close real gaps in code; capture residual items back in
  [`BACKLOG.md`](../BACKLOG.md); if the tooling choice/baseline policy needs justification, add an
  [ADR](adr/).

## Open decisions

1. **DCR first, or manual 3-client seeding first?** DCR is more work up front but simplifies and
   parallelises everything after; manual seeding gets Basic/Config green sooner.
2. **Profile scope:** Basic + Config only initially (recommended), deferring Implicit/Hybrid/FAPI.
3. **Consent automation:** auto-approve for the conformance client vs. a scripted consent `BrowserControl`
   task.

---

# Appendices — copy-ready reference templates

Everything below is the **reference material** these artifacts were lifted from. The templates have now been
realized as committed, runnable infrastructure under [`build/conformance/`](../build/conformance) (resolved
against the real `AddConformanceSeed` values); the appendices are retained as the annotated source-of-record
for why each config value is shaped the way it is. Placeholders are written as `<LIKE_THIS>`.

## Appendix A — Discovery metadata checklist

The Config OP profile reads `/.well-known/openid-configuration` and fails on anything advertised that the
OP cannot actually honour. Verify each entry below against
[`DefaultDiscoveryEndpointHandler`](../src/NCode.Identity.OpenId.Authentication/Endpoints/Discovery/DefaultDiscoveryEndpointHandler.cs)
and the backing settings in
[`OpenIdSettingKeys`](../src/NCode.Identity.OpenId.Abstractions/Settings/OpenIdSettingKeys.cs).

| Metadata field                          | Must advertise (Basic/Config)                  | Note                                                          |
| --------------------------------------- | ---------------------------------------------- | ------------------------------------------------------------- |
| `issuer`                                | exact base URL the suite uses                  | **Must byte-match** the discovery host (see Appendix D)       |
| `authorization_endpoint`                | `<issuer>/oauth2/authorize`                    | —                                                             |
| `token_endpoint`                        | `<issuer>/oauth2/token`                        | —                                                             |
| `jwks_uri`                              | `<issuer>/oauth2/jwks`                         | Must serve the active `RS256` signing key                     |
| `userinfo_endpoint`                     | `<issuer>/oauth2/userinfo`                     | Required by Basic OP                                          |
| `response_types_supported`              | `["code"]`                                     | Add `id_token`/`code id_token` only when those flows verified |
| `grant_types_supported`                 | `["authorization_code","refresh_token"]`       | Match the seeded client's allowed grants                      |
| `subject_types_supported`               | `["public","pairwise"]`                        | Both implemented (ADR-0093)                                   |
| `id_token_signing_alg_values_supported` | `["RS256"]`                                    | Must match the key the suite fetches from `jwks_uri`          |
| `scopes_supported`                      | incl. `openid`, `profile`, `email`             | Suite requests these in standard claims tests                 |
| `claims_supported`                      | the claims actually returned                   | Over-advertising triggers warnings                            |
| `token_endpoint_auth_methods_supported` | `["client_secret_basic","client_secret_post"]` | Must cover both to certify Basic OP                           |
| `code_challenge_methods_supported`      | `["S256"]`                                     | PKCE; omit `plain` unless intentionally supported             |

## Appendix B — Login/consent automation contract

For `BrowserControl` to drive the flow headlessly, the host login and consent pages must expose
**stable element locators** and there must be a **seeded deterministic test user**. Treat the locators
below as a contract the UI must satisfy (ids are suggestions — keep whatever the config in Appendix D
references in sync with the actual markup).

| Page    | Element           | Suggested locator         | Used for                         |
| ------- | ----------------- | ------------------------- | -------------------------------- |
| Login   | username field    | `id=username`             | `["text","id","username", ...]`  |
| Login   | password field    | `id=password`             | `["text","id","password", ...]`  |
| Login   | submit button     | `name=login` / `id=login` | `["click","id","login"]`         |
| Consent | allow/approve btn | `id=consent-allow`        | `["click","id","consent-allow"]` |
| Consent | deny button       | `id=consent-deny`         | negative-path tests              |

Seeded conformance user (deterministic, dev-only): e.g. `conformance-user` / fixed password, created on
startup in the conformance tenant. Prefer an **auto-approve** path for the conformance client so the
consent task can be omitted (simpler, fewer flaky selectors), with the deny button still present for the
negative tests.

## Appendix C — Client seeding checklist (manual, non-DCR path)

The Basic OP plan needs **three** client registrations (two may coincide if one client supports both auth
methods). All must register the suite callback as a redirect URI.

- **`client` (primary):** `token_endpoint_auth_method=client_secret_basic`; grants
  `authorization_code` + `refresh_token`; scopes `openid profile email`.
- **`client2` (second confidential):** same as above — a _distinct_ `client_id`, used to prove auth codes
  are bound to the issuing client.
- **`client_secret_post` client:** `token_endpoint_auth_method=client_secret_post` (may be the same record
  as `client` if it accepts either method).
- **Redirect URI (all):** `https://<SUITE_BASE>/test/a/<ALIAS>/callback`
  (local: `https://localhost.emobix.co.uk:8443/test/a/<ALIAS>/callback`;
  hosted: `https://www.certification.openid.net/test/a/<ALIAS>/callback`).
- **PKCE:** allow `S256`.

> With DCR (gap `F6`) implemented, skip all of the above — remove `alias` from the config and the suite
> registers clients itself and runs modules in parallel.

## Appendix D — Config template: Basic OP (`oidcc-basic.json`)

Issuer must match the host the suite reaches. Running the suite in Docker, the OP is reached at
`host.docker.internal`; the host must therefore **issue discovery with that same host** (configure
the issuer/public origin accordingly) so `issuer` byte-matches `server.discoveryUrl`'s origin.

```json
{
    "alias": "<ALIAS>",
    "description": "NCode.Identity OP — Basic OP certification",
    "server": {
        "discoveryUrl": "https://host.docker.internal:5001/.well-known/openid-configuration"
    },
    "client": {
        "client_id": "<CLIENT_ID>",
        "client_secret": "<CLIENT_SECRET>"
    },
    "client2": {
        "client_id": "<CLIENT2_ID>",
        "client_secret": "<CLIENT2_SECRET>"
    },
    "browser": [
        {
            "match": "https://host.docker.internal:5001/oauth2/authorize*",
            "tasks": [
                {
                    "task": "Login",
                    "match": "https://host.docker.internal:5001/*login*",
                    "commands": [
                        ["text", "id", "username", "<TEST_USER>"],
                        ["text", "id", "password", "<TEST_PASSWORD>"],
                        ["click", "id", "login"]
                    ]
                },
                {
                    "task": "Consent",
                    "match": "https://host.docker.internal:5001/*consent*",
                    "optional": true,
                    "commands": [["click", "id", "consent-allow"]]
                },
                {
                    "task": "Verify Complete",
                    "match": "https://*/test/a/<ALIAS>/callback*",
                    "commands": [["wait", "id", "submission_complete", 10]]
                }
            ]
        }
    ]
}
```

Variants (`client_auth_type`, `response_type`, `response_mode`) are selected per plan, not in this file.
Add `"browser_verbose": true` at the root to debug Selenium selector issues.

## Appendix E — Config template: Config OP (`oidcc-config.json`)

The Config OP plan only reads discovery metadata, so no `browser` block or clients are required.

```json
{
    "alias": "<ALIAS>",
    "description": "NCode.Identity OP — Config OP certification",
    "server": {
        "discoveryUrl": "https://host.docker.internal:5001/.well-known/openid-configuration"
    }
}
```

## Appendix F — Baseline files

Start empty (`[]`) and add an entry only for a **documented, intentional** deviation. Each entry is
matched by test name / variant / config / block / condition; unexpected new failures still fail the run,
and a baseline entry that stops firing also fails the run (so the baseline cannot silently rot).

`expected-failures.json`:

```json
[]
```

Example of a single codified deviation (shape only — fill from the `--verbose` template the runner
prints for any unexpected failure):

```json
[
    {
        "test-name": "<module-name>",
        "variant": "*",
        "configuration-filename": "oidcc-basic.json",
        "current-block": "<block name from the log>",
        "condition": "<ConditionClassName>",
        "expected-result": "failure",
        "comment": "Why this deviation is intentional; link to BACKLOG item or ADR."
    }
]
```

`expected-skips.json`:

```json
[]
```

## Appendix G — Orchestration script skeleton (`run-conformance.ps1`)

Reference skeleton for the execution phase. It assumes the conformance-suite repo is available for its
`scripts/` (`run-test-plan.py` + `conformance.py` + `test_plan_parser.py`) and that Docker + Python 3 are
installed. **Not** part of `dod.ps1`.

```powershell
#requires -Version 7
[CmdletBinding()]
param(
    [string] $Alias          = 'ncode',
    [string] $SuiteBase      = 'https://localhost.emobix.co.uk:8443',
    [string] $SuiteRepo      = "$PSScriptRoot/.conformance-suite",   # git clone target (gitignored)
    [string] $ConfigDir      = $PSScriptRoot,
    [string] $ExportDir      = "$PSScriptRoot/.results",
    [string[]] $Plans        = @('oidcc-config-certification-test-plan', 'oidcc-basic-certification-test-plan'),
    [string[]] $Configs      = @('oidcc-config.json',                  'oidcc-basic.json')
)
$ErrorActionPreference = 'Stop'

# 1) Launch the OpenID host on a host-reachable HTTPS URL (host.docker.internal:5001).
#    TODO: start via `dotnet run` or a container; capture the process/container id for teardown.

# 2) Bring up the conformance suite (prebuilt images; no Java build).
docker compose -f "$SuiteRepo/docker-compose-prebuilt.yml" up -d

# 3) Wait for the suite to answer, then for the OP discovery document.
#    TODO: poll "$SuiteBase/" and the OP's /.well-known/openid-configuration until ready.

# 4) Drive the plans headlessly. DISABLE_SSL_VERIFY tolerates dev certs on both ends.
$env:CONFORMANCE_SERVER = "$SuiteBase/"
$env:DISABLE_SSL_VERIFY = '1'
New-Item -ItemType Directory -Force -Path $ExportDir | Out-Null

$failed = $false
for ($i = 0; $i -lt $Plans.Count; $i++) {
    python "$SuiteRepo/scripts/run-test-plan.py" `
        $Plans[$i] (Join-Path $ConfigDir $Configs[$i]) `
        --expected-failures-file (Join-Path $ConfigDir 'expected-failures.json') `
        --expected-skips-file    (Join-Path $ConfigDir 'expected-skips.json') `
        --export-dir $ExportDir --verbose
    if ($LASTEXITCODE -ne 0) { $failed = $true }
}

# 5) Teardown.
docker compose -f "$SuiteRepo/docker-compose-prebuilt.yml" down
# TODO: stop the OpenID host started in step 1.

if ($failed) { throw 'Conformance run failed — see exported results and log-detail links above.' }
Write-Host 'Conformance run passed.'
```

## Appendix H — Windows Docker Compose notes

On Windows the suite's MongoDB bind-mount is unreliable; use a **named volume**. Minimal override aligned
with the suite's documented Windows snippet (pin image tags to a specific release for reproducibility):

```yaml
services:
    mongodb:
        image: mongo:6.0.13
        volumes:
            - mongodata:/data/db
    server:
        command: >
            java -jar /server/fapi-test-suite.jar
            --fintechlabs.base_url=https://localhost.emobix.co.uk:8443
            --fintechlabs.devmode=true
volumes:
    mongodata:
```

Also required on Windows:

- `hosts` entries: `127.0.0.1  localhost.emobix.co.uk` and `127.0.0.1  host.docker.internal`.
- The suite (in Docker) reaches the OP via `host.docker.internal`; ensure the host listens on a port
  mapped/visible to the container and issues discovery with that host.

## Appendix I — First run, step by step

**Hosted suite (quickest smoke test, manual login):**

1. Seed the three clients (Appendix C) with redirect
   `https://www.certification.openid.net/test/a/<ALIAS>/callback`.
2. Open <https://www.certification.openid.net/>, sign in, **Create a new test plan**.
3. Under **Test an OpenID Provider**, pick **OpenID Connect Core: Config Certification** (no login needed)
   first, then **Basic Certification**.
4. Fill the form (or paste the JSON from Appendix D/E), set your `ALIAS`, start the plan, run each module;
   complete login/consent in the popped browser when prompted.
5. All modules must end `PASSED/REVIEW/WARNING/SKIPPED`.

**Local suite, fully automated:**

1. Clone the suite; run `docker compose -f docker-compose-prebuilt.yml up` (Appendix H on Windows).
2. Start the host on `host.docker.internal:5001` with issuer set to match.
3. Fill the Appendix D/E configs + baseline files; run `run-conformance.ps1` (Appendix G).
4. Inspect exit code and the exported result zips / `log-detail` links.
