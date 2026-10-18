# Platform bootstrap runbook

- **Status:** Living runbook — update it in the same change as anything it describes.
- **Audience:** anyone reproducing the hosted platform from scratch — the maintainers, or a self-hoster running the
  published stacks ([ADR-0133](adr/0133-we-ship-and-run-the-same-deployment-artifacts.md)).
- **Scope:** the **manual** operations only — the steps no API can do (creating accounts, proving identity, payment,
  registrar delegation, clicking verification links). Everything after them is the OpenTofu bootstrap stack and then
  GitOps ([ADR-0132](adr/0132-platform-infrastructure-as-code-is-public-on-github.md),
  [ADR-0134](adr/0134-cloud-native-operating-principles.md)).

Each step says **what** to do, **why** it is manual, and how to **verify** it. Placeholders are in angle brackets; the
values used by the NCode hosted platform are listed once here.

| Placeholder        | Meaning                                         | NCode value                          |
| ------------------ | ----------------------------------------------- | ------------------------------------ |
| `<domain>`         | Apex domain for the platform                    | `nidentity.io`                       |
| `<github-org>`     | GitHub organization                             | `NCodeGroup`                         |
| `<infra-repo>`     | Public infrastructure repository                | `identity-platform`                  |
| `<aws-region>`     | Home region of the platform's AWS account       | `us-west-2` (Oregon)                 |
| `<oci-region>`     | Home region of the lab's OCI tenancy            | `us-phoenix-1` (Phoenix)             |
| `<recovery-email>` | An **off-domain** mailbox you control           | the owner's personal mailbox         |

**Region choice.** The AWS account holds only state and backups, so `us-west-2` puts them in a different geography from
the Dallas production cluster — a regional outage can't take both. OCI has no Oregon region; Phoenix is the closest OCI
region to Denver and has three availability domains to spread the lab's nodes across.

## 0. Before you start

- **Accounts security baseline** (applies to every step): a password manager, a passkey or hardware security key for
  every account that supports one, and recovery codes stored offline.
- **Recovery email rule:** the registrar, DNS, cloud, and GitHub owner accounts use `<recovery-email>` — **never** an
  address at `<domain>`. If the domain or its DNS breaks, those are the accounts you need to fix it. Other vendor
  accounts may use per-purpose `@<domain>` routing addresses.
- **Local tools:** `git`; OpenTofu through [tenv](https://github.com/tofuutils/tenv) (`scoop install tenv` /
  `brew install tenv`), which picks the version in the infrastructure repository's `.opentofu-version` (`tenv tofu
  install` in the repository) and verifies its signature; AWS CLI v2;
  OCI CLI (lab); `kubectl`, Flux CLI, Helm; `gitleaks` (pre-commit). Optional: GitHub CLI `gh`.
- **Expected cost:** see the [hosting analysis](hosting-analysis.md#2-summary-and-ranked-recommendations) — the lab is
  within OCI's free allowance (~$0–4/month); the AWS account costs cents per month.

## 1. Domain registrar (Porkbun)

Manual because a registrar credential can hand over the whole domain; it is never automated.

1. Turn on account 2FA (passkey / hardware key), **auto-renew**, and **registrar lock** for `<domain>`.
2. Leave **API access off** for the domain, keep the **Cloudflare integration disconnected** (it would store a standing
   Cloudflare credential at the registrar), and turn **off** "Allow Afternic fast transfer opt-ins" so third-party
   attempts to list the domain for sale are rejected automatically.
3. Do **not** change nameservers yet — that is step 7, after the bootstrap stack has created the zone.

**Verify:** RDAP for `<domain>` shows `client transfer prohibited`
(`Invoke-RestMethod https://rdap.org/domain/<domain> | Select-Object -ExpandProperty status`).

## 2. GitHub organization and repository

Manual because the repository must exist before any code can be pushed to it. The bootstrap stack then manages its
settings declaratively.

1. **Organization** (`<github-org>`):
   - Settings → Authentication security → **Require two-factor authentication** for everyone, and **Only allow secure
     two-factor methods**.
   - Member privileges → base permission **Read**; repository creation unchecked (owners only); uncheck **Allow members
     to change repository visibilities** and **Allow members to delete or transfer repositories**; App access requests
     → **Members only**.
   - Personal access tokens → fine-grained tokens **allowed** with **administrator approval** and a **maximum lifetime
     of 30 days**; tokens (classic) **restricted**.
2. **Create the repository:** New repository → owner `<github-org>`, name `<infra-repo>`, **Public**, description
   "Public infrastructure as code for the NCode.Identity hosted platform: reusable OpenTofu and Kubernetes components,
   opinionated stacks, and the live configuration we run.", and **no** README, `.gitignore`, license, or Copilot starter
   prompt — the first commit adds the files. The website (`https://<domain>`) and topics are managed by the bootstrap
   stack (an empty repository has no About panel to set them in).
3. **Harden it before the first push** (the bootstrap stack takes these over afterwards):
   - Settings → Actions → General:
     - **Actions permissions** → *Allow `<github-org>`, and select non-`<github-org>`, actions and reusable workflows*,
       with *Allow actions created by GitHub* and *Allow actions by Marketplace verified creators* checked, and
       `opentofu/setup-opentofu@*` in *Allow or block specified actions* (the CI workflow needs it and its publisher
       isn't a verified creator; the bootstrap stack manages this list afterwards). Turn on **Require actions to be
       pinned to a full-length commit SHA** — a moved tag can't swap in different code.
     - **Approval for running fork pull request workflows from contributors** → *Require approval for all external
       contributors*.
     - **Workflow permissions** → *Read repository contents and packages permissions*; leave *Allow GitHub Actions to
       create and approve pull requests* **unchecked**.
   - Settings → Advanced Security: **Enable** private vulnerability reporting; turn on **Dependency graph**,
     **Dependabot alerts**, **Malware alerts**, **Dependabot security updates**, and **Grouped security updates**; under
     **Secret Protection** click **Enable**, then turn on **Push protection**. After the first push, set up **CodeQL**
     (default setup) — it analyzes the GitHub Actions workflows.
   - Settings → General: turn on **Enable release immutability** (releases and their tags can't be changed after
     publishing — [ADR-0133](adr/0133-we-ship-and-run-the-same-deployment-artifacts.md)); disable **Wikis**; under Pull
     Requests, allow **squash merging** only and **automatically delete head branches**.
4. **Clone it:** `git clone https://github.com/<github-org>/<infra-repo>.git`.

Only one new repository is needed: components, stacks, and live configuration share it, and the host's Helm chart lives
in the library repository ([ADR-0133](adr/0133-we-ship-and-run-the-same-deployment-artifacts.md)).

**Verify:** the repository is public and empty; a test push of a file containing a fake AWS key is **blocked** by push
protection.

## 3. Cloudflare (DNS and inbound email)

Manual because account creation and the first API token can't be created by an API.

1. Create the account with `<recovery-email>` and 2FA (security key). No payment method is needed.
2. Note the **account ID** (the long hex value in the dashboard URL, or Account home → ⋯ → *Copy account ID*). It is an
   identifier, not a secret.
3. Create a short-lived **bootstrap token**: My Profile → API Tokens → *Create Custom Token*:
   - Permissions: Account → *Account API Tokens* → Edit (to mint the scoped CI tokens); Account → *Email Routing
     Addresses* → Edit; Zone → *Zone* → Edit; Zone → *DNS* → Edit; Zone → *Zone Settings* → Edit; Zone → *Email Routing
     Rules* → Edit. _(Cloudflare renames permission groups occasionally — match by meaning.)_
   - Account resources: only your account. Zone resources: all zones from that account.
   - Client IP filtering: your current public IP. TTL: **expires tomorrow**.
4. Keep the token only in an environment variable for the bootstrap run (`$env:CLOUDFLARE_API_TOKEN`); never write it to
   a file in the repository.

**Verify:**
`Invoke-RestMethod https://api.cloudflare.com/client/v4/user/tokens/verify -Headers @{ Authorization = "Bearer $env:CLOUDFLARE_API_TOKEN" }`
returns `status: active`.

## 4. AWS (state, backups, vendor tokens)

Manual because AWS accounts and the identity used to administer them must exist before any automation can authenticate.

1. **Organization:** in your existing AWS account (it becomes the *management* account), open AWS Organizations →
   *Create an organization*. Make sure its root user has MFA.
2. **Member account:** Organizations → *Add an AWS account* → *Create an AWS account*: name `nidentity-platform`, email
   a unique alias of `<recovery-email>` (for example a `+aws-platform` sub-address), IAM role name left as
   `OrganizationAccountAccessRole`. It is billed to the management account — no new payment method.
3. **Root credentials:** in IAM (management account) enable **centralized root access** for member accounts and remove
   the new account's root credentials, so it has no root password to steal. _(If unavailable, set a strong root
   password through "Forgot password" and enable MFA instead.)_
4. **IAM Identity Center** in `<aws-region>` (its home region is hard to change later): enable it, create your user with
   **MFA required**, create a permission set `AdministratorAccess` with a **1-hour** session, and assign it to you on
   the `nidentity-platform` account. Create **no IAM users or access keys** — CI uses GitHub OIDC
   ([ADR-0132](adr/0132-platform-infrastructure-as-code-is-public-on-github.md)).
5. **Local profile:** `aws configure sso` → profile `nidentity-platform-admin`, region `<aws-region>`; then
   `aws sso login --profile nidentity-platform-admin`.

**Verify:** `aws sts get-caller-identity --profile nidentity-platform-admin` returns the member account ID and an
`AWSReservedSSO_AdministratorAccess_…` role.

## 5. OCI (the lab)

Manual because tenancy sign-up requires identity and card verification, and the home region is chosen once.

1. **Sign up** with `<recovery-email>`; choose home region **`<oci-region>`** — it **cannot** be changed later.
2. **Upgrade to Pay As You Go** (Billing → Upgrade). It keeps the Always Free resources, adds the monthly Ampere A1
   allowance (≈ 4 OCPU / 24 GB always-on), and exempts the tenancy from idle-resource reclamation.
3. Enable **MFA** for your console user (Identity → Domains → Default → Security → MFA).
4. Create an **API signing key** for the local bootstrap: Profile → *API keys* → *Add API key* → generate, download the
   private key to `~/.oci/`, and paste the shown configuration into `~/.oci/config` (or run `oci setup config`).

**Verify:** `oci iam region-subscription list` lists `<oci-region>` as the home region.

## 6. Run the bootstrap stack

The bootstrap stack lives in the infrastructure repository (`live/bootstrap`); its
[README](https://github.com/NCodeGroup/identity-platform/tree/main/live/bootstrap) has the exact commands. It is the
one root run by hand, from your machine, with the short-lived credentials above: it creates the identity CI uses, so CI
can't create it. Everything after it runs from GitHub Actions.

1. **Prepare.** Commit the member account ID as `aws_account_id` in `live/bootstrap/terraform.tfvars` (not a secret;
   the AWS provider refuses any other account). Generate a **state passphrase** of at least 32 random characters and
   keep it in your password manager — it encrypts this root's state and plans, and the same passphrase is needed on
   every later run.
2. **Credentials for this shell only:** `$env:AWS_PROFILE = 'nidentity-platform-admin'`, the Cloudflare bootstrap
   token in `$env:CLOUDFLARE_API_TOKEN`, a fine-grained GitHub token in `$env:GITHUB_TOKEN` (resource owner
   `<github-org>`, only `<infra-repo>`, permissions *Administration*, *Environments*, and *Variables* read/write, expiry
   1 day), and the passphrase in `$env:TF_VAR_state_passphrase` — enter secrets with `Read-Host -MaskInput` so they
   never reach shell history.
3. **First apply on local state.** The state bucket can't hold this root's state before this root creates it, so the
   first run uses a local backend: create the git-ignored `backend_override.tf` (`terraform { backend "local" {} }`),
   then `tofu init` and `tofu apply`. The local state file is already encrypted with the passphrase. The apply creates
   the S3 state and backup buckets, the KMS keys, the GitHub OIDC provider and roles, the Cloudflare zone, DNSSEC, and
   scoped tokens (stored as SSM SecureStrings), the budget alert, and the repository's rulesets and `lab` /
   `production` environments — importing the repository from step 2 rather than creating it. If DNSSEC fails because
   the zone is still pending, continue and re-run `tofu apply` after step 7.
4. **Move the state into the bucket it created:** delete `backend_override.tf`, run `tofu init -migrate-state`
   (answer `yes`), then delete the local `terraform.tfstate` and `terraform.tfstate.backup`.
5. **Revoke** the Cloudflare bootstrap token and the GitHub token; let the AWS session expire.

**Verify:** `tofu plan` (with fresh credentials) shows no changes; the state object exists in the bucket with
versioning enabled.

**Re-run it at least every 90 days** with fresh credentials (no override file) to rotate the CI Cloudflare tokens; they
expire 30 days after their scheduled rotation. Re-run it too whenever `live/bootstrap` changes.

## 7. Delegate the domain

Manual because it is a registrar change (step 1).

1. In Porkbun, set the nameservers of `<domain>` to the two Cloudflare nameservers from the bootstrap outputs.
2. Add the DNSSEC **DS record** from the bootstrap outputs (key tag, algorithm, digest type, digest).

**Verify:** `Resolve-DnsName <domain> -Type NS -Server 1.1.1.1` returns the Cloudflare nameservers;
[DNSViz](https://dnsviz.net/) shows a secure chain for `<domain>`.

## 8. Confirm inbound email

1. Click the Cloudflare **destination verification** link sent to `<recovery-email>`.
2. In the mailbox, add a filter for mail to `*@<domain>`: never send to spam, apply a label.

**Verify:** a test message to `security@<domain>` arrives in the mailbox.

## 9. Close out

- [ ] Bootstrap tokens revoked; no credentials in shell history or files.
- [ ] 2FA confirmed on Porkbun, GitHub, Cloudflare, AWS (Identity Center), and OCI.
- [ ] Budget alerts present on AWS and OCI (created by the stacks).
- [ ] Record what changed in this runbook if any step differed from it.
