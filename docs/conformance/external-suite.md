# T6 / T1 — External conformance suite as the oracle (full automation)

Status: **implemented (committed harness); nightly CI pipeline deferred.** Detailed plan for the remaining
Phase 4 of backlog `T6` and Phase D of `T1`. Parent design:
[`docs/conformance-testing.md`](../conformance-testing.md). Decision record:
[ADR-0084](../adr/0084-spec-coverage-conformance-harness.md) (the `browser` / `probe-runner` / `external-suite`
exercise boundary). Catalog entry: [`BACKLOG.md`](../../BACKLOG.md) group `T`.

## Goal

Wire the external **OpenID Foundation Conformance Suite** — the certified, spec-author-maintained oracle — as
the authoritative gate behind the fast in-house probe-runner pre-flight, as one committed, runnable command.
This lifts the copy-ready templates in [`docs/conformance-testing.md`](../conformance-testing.md) Appendices
D–H out of the doc and into version-controlled, executable infrastructure.

## What was implemented

The committed harness is [`build/conformance/`](../../build/conformance):

- **[`run-conformance.ps1`](../../build/conformance/run-conformance.ps1)** — the orchestration (Phase D). It
  launches the Playground OP on the reachable HTTPS origin (`host.docker.internal:5001`, the `Conformance`
  launch profile — `T3`), asserts the discovery `issuer` byte-matches that origin, runs the in-house probe
  runner as a **fast pre-flight** that fails early before the slow external suite is started, brings the suite
  up from **prebuilt Docker images** (no Java build), drives the Config OP + Basic OP plans headlessly via the
  suite's `run-test-plan.py` gated on the committed baselines, and tears everything down — surfacing one
  non-zero exit code on any unexpected failure.
- **Config templates** resolved against the deterministic seed (`T4`):
  [`oidcc-basic.json`](../../build/conformance/oidcc-basic.json) (with the `browser` automation block whose
  selectors — `username` / `password` / `login` / `consent-allow` — match the `apps/auth` surface, `T2`) and
  [`oidcc-config.json`](../../build/conformance/oidcc-config.json).
- **Baselines** [`expected-failures.json`](../../build/conformance/expected-failures.json) /
  [`expected-skips.json`](../../build/conformance/expected-skips.json) (start empty; an entry codifies only a
  documented, intentional deviation and must link a `BACKLOG.md` item or ADR).

The pre-flight is the already-shipped `@ncode/app-conformance-runner` (ADR-0084 Phase 3), which exercises the
confidential and server-initiated legs a browser cannot (`client_credentials`, introspection/revocation with
client auth, device-code polling, CIBA, `private_key_jwt`, DPoP/mTLS, back-channel logout) and exits non-zero
on any failed probe. Running it first gives fast local feedback before the heavier suite spins up.

## Exercise boundary (why both)

Per [ADR-0084](../adr/0084-spec-coverage-conformance-harness.md), coverage is split across three actors and the
harness runs the first two locally, pre-flighting the third:

| Actor            | Drives                                                                    | In this harness                                  |
| ---------------- | ------------------------------------------------------------------------- | ------------------------------------------------ |
| `browser`        | CORS-reachable, credential-free discovery/authorize legs                  | `apps/conformance` dashboard (not scripted here) |
| `probe-runner`   | confidential / server-initiated legs (secrets, device/CIBA, back-channel) | `run-conformance.ps1` step 2 (pre-flight)        |
| `external-suite` | the certified spec catalog (the authoritative judgment)                   | `run-conformance.ps1` steps 3–4                  |

## Not part of `dod.ps1`

The suite needs Docker + Python 3 and is slow, so the harness is an **opt-in / nightly** gate, not part of the
hermetic [`build/dod.ps1`](../../build/dod.ps1) inner loop — consistent with
[`docs/conformance-testing.md`](../conformance-testing.md) prerequisite #9.

## Remaining

- **Nightly CI pipeline** (a `T6` remainder). No `.github/workflows` exists yet; a separate, opt-in /
  nightly job (`docker compose` + Python 3, no local Java) that invokes `run-conformance.ps1` and publishes the
  `.results/` zips as artifacts is the last step. It is intentionally deferred from the committed harness so the
  wiring is introduced alongside the repo's broader CI bootstrap rather than as a one-off.
- **Basic OP now runs end-to-end under Dynamic Client Registration.** The **Config OP** plan passes cleanly
  (`44 successes, 0 failures, 0 warnings`). The **Basic OP** browser profile originally interrupted at
  `Unable to locate element with ID: 'username'` (the suite's default `BrowserControl` is a no-JavaScript
  **HtmlUnitDriver** and the login surface was a client-rendered SPA); that is **fixed** by the no-JS progressive
  `<form>`. The config was then switched to **Dynamic Client Registration** (dropping the static `alias` + `client`/
  `client2` so the suite registers a fresh client per module — [ADR-0063](../adr/0063-dynamic-client-registration.md),
  with `registration_endpoint_allow_anonymous=true` in `appsettings.Conformance.json`), which removed the
  `Stopping test due to alias conflict` serialization. The DCR switch surfaced and **fixed** two further backend bugs:
  the deregistration `DELETE /oauth2/register/{client_id}` failing on a client that still owned a secret
  (`ClientStore.RemoveAsync` guard was blind to same-unit-of-work pending deletes), and **generated `client_secret`s
  failing `invalid_client`** because a symmetric secret was stored as raw bytes instead of the UTF-8 bytes client
  authentication compares against (new `ISecretGenerator.GenerateSharedSecret` —
  [ADR-0110](../adr/0110-shared-secret-vs-cryptographic-key-storage.md)). Two conformance-config fixes were also needed:
  the `Verify Complete` callback match uses `/test/*/callback*` (DCR has no `/a/<alias>/` segment), and the `Login` task
  is `optional` (so no-login `prompt=none` / `max_age` flows do not error). **First full Basic OP (DCR) run:**
  `18 PASSED, 11 WARNING, 4 SKIPPED, 4 FAILED` across 38 modules with no mass-abort. Triaging those dispositions is
  tracked as backlog `T8`.
- **Profile expansion** beyond Basic + Config (Form Post / Dynamic OP / FAPI) as those capabilities are
  verified end-to-end.
