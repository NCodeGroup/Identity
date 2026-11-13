# The first use of a Production instance

- **Status:** Living guide — update it in the same change as anything it describes.
- **Audience:** an operator who brings up an `NCode.Identity.OpenId.Host` instance in Production and creates its first
  administrator.
- **Scope:** from an empty database to a signed-in root administrator, a registered management console and the grants
  the instance serves. The Helm chart's [README](../charts/ncode-identity-host/README.md) and the host's
  [README](../hosts/NCode.Identity.OpenId.Host/README.md) describe every setting.

A Production instance starts with an empty database and no administrator, and it ships no default credential. The
operator creates the first administrator, the root global administrator, once, through setup
([ADR-1512](decisions/1512-an-instance-is-set-up-once-through-a-one-time-setup-token-that-creates-a-root-global-administrator.md)).
The `BootstrapAdmin` configuration is a development convenience, and the host refuses it in Production
([ADR-1044](decisions/1044-the-management-api-authenticates-its-own-access-tokens-and-a-configured-client-bootstraps-the-first-globaladmin.md)).

## 1. Prerequisites

- **A Postgres database.** One database holds the host's schema and its Data Protection key ring.
- **A root key provider and a recovery key.** Production refuses the `ephemeral` root key provider. Create the root key
  and the recovery key pair before the first start, back up the root key, and keep the recovery private key offline:
  losing the root key without a recovery key loses every stored secret. See
  [Secret protection](../charts/ncode-identity-host/README.md#secret-protection).
- **Allowed hosts.** `AllowedHosts` (the chart's `allowedHosts`) lists the public host names. A missing value or `*` is
  refused.
- **TLS.** The host serves HTTPS only, and the setup wizard answers 404 to a request that is not HTTPS in Production.
  Give the pods a serving certificate.
- **Valkey** when the instance runs more than one replica, so replicas share single-use state.

## 2. Install

Install the chart, or run the host image with the same settings.

```bash
helm install identity charts/ncode-identity-host \
  --set 'allowedHosts={id.example.com}' \
  --set tenantResolution.staticSingle.issuer=https://id.example.com \
  --set database.parts.existingSecret=pg-app \
  --set secretProtection.rootKeyProvider=local --set secretProtection.local.keySecret=identity-root-key --set secretProtection.recovery.publicKeyConfigMap=identity-recovery-key
```

The chart refuses a release that lacks a required setting. The settings the image needs without the chart are in the
host's [Container image](../hosts/NCode.Identity.OpenId.Host/README.md#container-image) section.

## 3. Apply the schema

The host does not migrate on boot. The chart runs the migration worker from the host's image as a Helm hook Job before
the rollout, and a failed migration stops the release
([ADR-1180](decisions/1180-each-host-contexts-migrations-run-before-the-rollout-and-only-expand-the-schema.md)). Without
the chart, run `dotnet NCode.Identity.OpenId.Host.MigrationService.dll` from `/app/migrations` in the image with the
same database settings, or apply a generated idempotent script. The instance has no administrator and no setup state
until the schema exists.

## 4. Create the root administrator

Setup creates one local account in the control-plane tenant and gives it the `GlobalAdmin` role at the platform node,
which confers every management authority. It runs once: after it succeeds, every `/setup` route answers 404 and the
setup token is useless. Setup has two forms, an interactive wizard and a headless command.

### The setup token

The wizard is guarded by a setup token. Only the SHA-256 hash of the token is stored, never the token itself.

| Setting                         | Meaning                                                                                                          |
| ------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| `Setup__Token`                  | Optional. When set, the token is this value and it must be at least 32 characters. The host generates no token.  |
| `Setup__GeneratedTokenLifetime` | How long a generated token lives. The default is `01:00:00` and the range is 1 minute to 24 hours.               |
| `Setup__BundleRootPath`         | The folder of the wizard's bundle. The image sets it to `/app/setup`.                                            |

**A configured token.** Create the Secret before the install and name it in the chart. The chart never generates the
Secret, so the token is yours to choose and to keep out of a values file:

```bash
kubectl create secret generic identity-setup-token \
  --from-literal=token="$(openssl rand -base64 48 | tr '+/' '-_' | tr -d '=\n')"
```

Set `setup.token.existingSecret=identity-setup-token`. The chart passes the Secret's `setup.token.key` key (`token` by
default) as `Setup__Token`.

**A generated token.** When `Setup__Token` is unset, an uninitialized instance generates a 256-bit base64url token when
it starts and logs it once at Warning level, in the log of the one replica that stored it. Search every replica's log
for it. The token expires after `Setup__GeneratedTokenLifetime`, and an expired generated token needs a host restart to
issue a fresh one.

### Interactively

Browse to `https://<host>/setup/#token=<token>`. The token sits in the URL fragment, so it reaches neither the server's
logs nor a `Referer` header, and the wizard removes it from the address bar. The wizard also takes the token typed in.
It asks for the root administrator's user name, email address and password, and creates the account. The password needs
at least 15 characters and must satisfy the control-plane tenant's password policy. A third step optionally registers the
management console's client in the same save, as [The management console's client](#the-management-consoles-client)
describes.

The setup API is rate-limited to 10 requests per minute per client IP address. Behind a reverse proxy, configure the
host with forwarded headers, or every client shares the proxy's address and one person's attempts limit everyone's.

### Headless

The host's `setup` command creates the root administrator from configuration with no browser and needs no token,
because running the image with the database connection is already full authority over the instance. Run the host image
with `setup` as its first argument:

```sh
dotnet NCode.Identity.OpenId.Host.dll setup
```

It reads `Setup__Root__UserName`, `Setup__Root__Email` and `Setup__Root__Password`. Set `UserName` and `Password`
together or not at all. The exit code is `0` when the root administrator is created or the instance is already
initialized, `2` for a usage error, and `1` when the creation fails, so a Job can run the command on every release.

The chart runs it as an opt-in Job. Create a Secret that holds the account, with the keys `userName`, `email` and
`password`:

```bash
kubectl create secret generic identity-root-admin \
  --from-literal=userName=root \
  --from-literal=email=root@example.com \
  --from-literal=password='<at least 15 characters>'
```

Set `setup.job.enabled=true` and `setup.job.existingSecret=identity-root-admin`. `setup.job.userNameKey`, `emailKey`
and `passwordKey` rename the Secret's keys.

### The management console's client

Setup can also register the management console's confidential client in the control-plane tenant, in the same save as
the root administrator. If the registration is refused, setup saves nothing, so the root administrator is not created
either. The wizard's console step collects the values, and the headless command reads them from configuration:

| Setting                              | Meaning                                                                         |
| ------------------------------------ | ------------------------------------------------------------------------------- |
| `Setup__ManagementConsole__ClientId` | The client's `client_id`. The default is `mgmt-console`.                        |
| `Setup__ManagementConsole__BaseUrl`  | The console's public https origin, such as `https://console.example.com`.       |
| `Setup__ManagementConsole__Jwks`     | The console's public JWK Set, a JSON object with a non-empty `keys` array.      |

Set `BaseUrl` and `Jwks` together: setting either one registers the console, and both are then required. Setup imports
the JWK Set by value and never fetches a `jwks_uri`, so a console on a private network needs no route from the server.
Setup derives the redirect, post-logout and back-channel logout URIs from `BaseUrl`, and it applies the metadata that
the chart's [Management console](../charts/ncode-identity-host/README.md#management-console) section lists, except
`jwks_uri`. The registration passes the same validation as one made through the management API.

Derive the public JWK Set from the console's `mgmt-client.pem` key with `openssl`, `base64`, `tr`, `head` and `tail`.
The set holds one ES256 signing key: its `kid` is the key file's name without its extension (`mgmt-client`), its `use`
is `sig` and its `alg` is `ES256`, because the console signs its client assertions with that `kid`. The private key
never appears in it. The last 65 bytes of a P-256 SubjectPublicKeyInfo are `04 || X || Y`, so the block cuts `x` and
`y` from them:

```bash
openssl pkey -in mgmt-client.pem -pubout -outform DER -out mgmt-client.pub.der
b64url() { base64 | tr -d '=\n' | tr '+/' '-_'; }
x=$(tail -c 64 mgmt-client.pub.der | head -c 32 | b64url)
y=$(tail -c 32 mgmt-client.pub.der | b64url)
printf '{"keys":[{"kty":"EC","crv":"P-256","use":"sig","alg":"ES256","kid":"mgmt-client","x":"%s","y":"%s"}]}\n' "$x" "$y"
```

A running console publishes the same public set at `https://<console-host>/bff/jwks`
(`Bff:Frontends:mgmt:Jwks:Enabled`); it is public data and never holds the DPoP key.

With the chart, the setup Job passes these values from `setup.managementConsole.clientId`, `baseUrl` and `jwks`, and
emits them only when `baseUrl` is set.

### The automation client

The headless command can also register an automation client: a platform-owned `client_credentials` machine client that
authenticates with `private_key_jwt` and holds the `GlobalAdmin` role at the platform node. A GitOps install manages the
instance through it, without the development bootstrap client. It is registered in the same save as the root
administrator, so a refused registration saves nothing. The wizard never registers it.

| Setting                         | Meaning                                                                         |
| ------------------------------- | ------------------------------------------------------------------------------- |
| `Setup__Automation__ClientId`   | The client's `client_id`: 1 to 255 characters from `A-Z`, `a-z`, `0-9`, `.`, `_`, `~` and `-`, different from the console's. |
| `Setup__Automation__Jwks`       | The client's public JWK Set, a JSON object with a non-empty `keys` array.       |

Set both: setting either one registers the client, and both are then required. Setup imports the JWK Set by value. The
control-plane tenant must allow `client_credentials`, which [section 6](#6-enable-grants) describes, or setup fails as a
whole. With the chart, the setup Job passes these values from `setup.automation.clientId` and `setup.automation.jwks`,
and emits them only when `clientId` is set.

## 5. Sign in and register the management console

Sign in as the root administrator at the control-plane tenant's issuer. The management console is a confidential client
of the control-plane tenant, and its client has to exist before the console can sign anyone in.

Register the client during setup: fill in the wizard's console step, or set `Setup__ManagementConsole__BaseUrl` and
`Setup__ManagementConsole__Jwks` (and `Setup__ManagementConsole__ClientId` for an identifier other than `mgmt-console`)
for the headless command, as [The management console's client](#the-management-consoles-client) describes. Then deploy
the console with `managementConsole.enabled=true`.

To register a console added later, sign in and register `mgmt-console` through the management API with the metadata that
the chart's [Management console](../charts/ncode-identity-host/README.md#management-console) section lists
([ADR-1437](decisions/1437-the-management-console-is-a-confidential-client-whose-own-bff-host-serves-only-the-console.md)).

## 6. Enable grants

The platform's baseline grants are `authorization_code`, `refresh_token`,
`urn:ietf:params:oauth:grant-type:device_code` and `urn:openid:params:grant-type:ciba`, and omit `client_credentials`.
The `grant_types_enabled` setting adds its grants to the baseline, so name only the grants to add, one environment
variable per grant:

```yaml
extraEnv:
  - name: OpenId__Platform__Settings__grant_types_enabled__0
    value: client_credentials
```

The `grant_types_enabled` setting applies at the platform only. A `grant_types_supported` setting at any index is the
platform's whole list of grants, the baseline's included.

## 7. Remove the setup Secrets

The setup token and the root account's credentials have done their work once setup succeeds. Delete the token Secret and
the root-credential Secret, unset `setup.token` and `setup.job`, and roll out.

```bash
kubectl delete secret identity-setup-token identity-root-admin
```

Remove `setup.token` and `setup.job` from the release's values before the rollout, so no pod or Job references a deleted
Secret.

A configured token has no lifetime, so removing it is the operator's step. A setup request after the root exists
answers 404 whatever the token.

## References

- [ADR-1512](decisions/1512-an-instance-is-set-up-once-through-a-one-time-setup-token-that-creates-a-root-global-administrator.md):
  an instance is set up once through a one-time setup token.
- [ADR-1044](decisions/1044-the-management-api-authenticates-its-own-access-tokens-and-a-configured-client-bootstraps-the-first-globaladmin.md):
  the management API's authentication and the development-only bootstrap client.
- The chart's [README](../charts/ncode-identity-host/README.md).
