{{/*
The chart name, overridable by nameOverride.
*/}}
{{- define "identity-host.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
The name of the migration Job. The fully qualified name is shortened first so the name, with its suffix, fits the
63-character label value Kubernetes copies it into (batch.kubernetes.io/job-name).
*/}}
{{- define "identity-host.migrationsJobName" -}}
{{- printf "%s-migrations" (include "identity-host.fullname" . | trunc 52 | trimSuffix "-") }}
{{- end }}

{{/*
The name of the secret protection Job, which carries the release revision so each upgrade can run its own. The fully
qualified name is shortened first so the name, with its suffix, fits the 63-character label value Kubernetes copies it
into (batch.kubernetes.io/job-name).
*/}}
{{- define "identity-host.secretProtectionJobName" -}}
{{- printf "%s-secret-protection-%d" (include "identity-host.fullname" . | trunc 38 | trimSuffix "-") (int .Release.Revision) }}
{{- end }}

{{/*
The fully qualified name of the release's resources.
*/}}
{{- define "identity-host.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{- define "identity-host.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
The labels every resource of the release carries.
*/}}
{{- define "identity-host.commonLabels" -}}
helm.sh/chart: {{ include "identity-host.chart" . }}
app.kubernetes.io/name: {{ include "identity-host.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/*
The labels that select the server's pods. The component keeps the migration Job's pod out of the Service and the
PodDisruptionBudget.
*/}}
{{- define "identity-host.selectorLabels" -}}
app.kubernetes.io/name: {{ include "identity-host.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/component: server
{{- end }}

{{- define "identity-host.labels" -}}
{{ include "identity-host.commonLabels" . }}
app.kubernetes.io/component: server
{{- end }}

{{- define "identity-host.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "identity-host.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

{{- define "identity-host.image" -}}
{{- if .Values.image.digest }}
{{- printf "%s@%s" .Values.image.repository .Values.image.digest }}
{{- else }}
{{- printf "%s:%s" .Values.image.repository (default .Chart.AppVersion .Values.image.tag) }}
{{- end }}
{{- end }}

{{/*
The Host header the probes send: probes.host, or else the first allowed host.
*/}}
{{- define "identity-host.probeHost" -}}
{{- .Values.probes.host | default (first .Values.allowedHosts) }}
{{- end }}

{{/*
Whether the release shares single-use state through Valkey.
*/}}
{{- define "identity-host.valkeyEnabled" -}}
{{- if .Values.valkey.existingSecret }}true{{ end }}
{{- end }}

{{/*
Refuses a release the host would refuse, or one that would run replicas without shared state, before anything is
applied. Rendered for its side effect only.
*/}}
{{- define "identity-host.validate" -}}
{{- if not .Values.allowedHosts }}
{{- fail "allowedHosts must list the host names the host answers to." }}
{{- end }}
{{- if has "*" .Values.allowedHosts }}
{{- fail "allowedHosts must not contain '*': it accepts every Host header." }}
{{- end }}
{{- if contains "*" (include "identity-host.probeHost" .) }}
{{- fail "probes.host must name a concrete host that allowedHosts accepts: the first allowed host is a wildcard." }}
{{- end }}
{{- if not .Values.database.existingSecret }}
{{- fail "database.existingSecret must name the Secret holding the openiddb connection string." }}
{{- end }}
{{- if and (eq .Values.tenantResolution.strategyCode "StaticSingle") (not .Values.tenantResolution.staticSingle.issuer) }}
{{- fail "tenantResolution.staticSingle.issuer must be the public issuer URL." }}
{{- end }}
{{- if and (eq .Values.secretProtection.rootKeyProvider "aws-kms") (not .Values.secretProtection.awsKms.keyArn) }}
{{- fail "secretProtection.awsKms.keyArn must name the KMS key or alias that holds the root key." }}
{{- end }}
{{- if and (eq .Values.secretProtection.rootKeyProvider "aws-kms") .Values.secretProtection.awsKms.keyArn (not (regexMatch "^arn:aws[a-z-]*:kms:[a-z0-9-]+:[0-9]{12}:(key/[A-Za-z0-9-]+|alias/[A-Za-z0-9/_-]+)$" .Values.secretProtection.awsKms.keyArn)) }}
{{- fail "secretProtection.awsKms.keyArn must be the ARN of a KMS key or alias, such as arn:aws:kms:us-west-2:111122223333:alias/ncode-identity-root." }}
{{- end }}
{{- if eq .Values.secretProtection.rootKeyProvider "azure-key-vault" }}
{{- $azureKeyVault := .Values.secretProtection.azureKeyVault }}
{{- if not (regexMatch "^https://[A-Za-z0-9-]+([.][A-Za-z0-9-]+)+/keys/[A-Za-z0-9-]{1,127}(/[A-Za-z0-9]{32})?$" $azureKeyVault.keyId) }}
{{- fail "secretProtection.azureKeyVault.keyId must be the identifier of a Key Vault or Managed HSM key, such as https://contoso.vault.azure.net/keys/ncode-identity-root." }}
{{- end }}
{{- end }}
{{- if eq (include "identity-host.usesKeyWrapper" (dict "root" . "name" "azure-key-vault")) "true" }}
{{- $azureKeyVault := .Values.secretProtection.azureKeyVault }}
{{- if ne (empty $azureKeyVault.workloadIdentity.tenantId) (empty $azureKeyVault.workloadIdentity.clientId) }}
{{- fail "secretProtection.azureKeyVault.workloadIdentity needs both tenantId and clientId, or neither." }}
{{- end }}
{{- end }}
{{- if eq .Values.secretProtection.rootKeyProvider "google-cloud-kms" }}
{{- $googleCloudKms := .Values.secretProtection.googleCloudKms }}
{{- if not (regexMatch "^projects/[a-z0-9.:-]+/locations/[a-z0-9-]+/keyRings/[A-Za-z0-9_-]{1,63}/cryptoKeys/[A-Za-z0-9_-]{1,63}$" $googleCloudKms.keyName) }}
{{- fail "secretProtection.googleCloudKms.keyName must be the resource name of a Cloud KMS crypto key, such as projects/my-project/locations/us-central1/keyRings/ncode-identity/cryptoKeys/root." }}
{{- end }}
{{- end }}
{{- if eq (include "identity-host.usesKeyWrapper" (dict "root" . "name" "google-cloud-kms")) "true" }}
{{- $googleCloudKms := .Values.secretProtection.googleCloudKms }}
{{- if and $googleCloudKms.workloadIdentityFederation.provider (not (regexMatch "^projects/[0-9]+/locations/global/workloadIdentityPools/[a-z0-9-]+/providers/[a-z0-9-]+$" $googleCloudKms.workloadIdentityFederation.provider)) }}
{{- fail "secretProtection.googleCloudKms.workloadIdentityFederation.provider must be the resource name of a workload identity pool provider, such as projects/123456789012/locations/global/workloadIdentityPools/ncode-identity/providers/cluster." }}
{{- end }}
{{- end }}
{{- if .Values.secretProtection.rewrapJob.enabled }}
{{- if and (eq .Values.secretProtection.rewrapJob.command "rewrap-recovery") (not .Values.secretProtection.recovery.publicKeyConfigMap) }}
{{- fail "secretProtection.rewrapJob.command rewrap-recovery replaces the recovery copies, so secretProtection.recovery.publicKeyConfigMap must name the recovery public key ConfigMap." }}
{{- end }}
{{- end }}
{{- if not .Values.secretProtection.rootKeyProvider }}
{{- fail "secretProtection.rootKeyProvider is required: local, aws-kms, azure-key-vault or google-cloud-kms, or ephemeral outside Production." }}
{{- end }}
{{- if eq .Values.environment "Production" }}
{{- $secretProtection := .Values.secretProtection }}
{{- if eq $secretProtection.rootKeyProvider "ephemeral" }}
{{- fail "secretProtection.rootKeyProvider must name a root key held outside the pod in Production: local, aws-kms, azure-key-vault or google-cloud-kms." }}
{{- end }}
{{- if and (eq $secretProtection.rootKeyProvider "local") (not $secretProtection.local.keySecret) }}
{{- fail "secretProtection.local.keySecret must name the Secret holding the local root key." }}
{{- end }}
{{- if and (not $secretProtection.recovery.publicKeyConfigMap) (not $secretProtection.recovery.disabled) }}
{{- fail "secretProtection.recovery must name the recovery public key ConfigMap, or set disabled to accept that losing the root key loses every stored secret." }}
{{- end }}
{{- end }}
{{- if and (gt (int .Values.replicaCount) 1) (not (include "identity-host.valkeyEnabled" .)) }}
{{- fail "replicaCount above 1 requires valkey.existingSecret: replicas share single-use state through Valkey." }}
{{- end }}
{{- end }}

{{/*
The pod security context of every pod the chart runs: the restricted Pod Security Standard as the image's non-root
'app' user, whose group can read the mounted secrets.
*/}}
{{- define "identity-host.podSecurityContext" -}}
runAsNonRoot: true
runAsUser: 1654
runAsGroup: 1654
fsGroup: 1654
seccompProfile:
  type: RuntimeDefault
{{- end }}

{{/*
The security context of every container the chart runs.
*/}}
{{- define "identity-host.containerSecurityContext" -}}
allowPrivilegeEscalation: false
readOnlyRootFilesystem: true
capabilities:
  drop:
    - ALL
{{- end }}

{{/*
The image pull secrets and scheduling constraints of every pod the chart runs, as pod spec fields.
*/}}
{{- define "identity-host.imagePullSecrets" -}}
{{- with .Values.imagePullSecrets }}
imagePullSecrets:
  {{- toYaml . | nindent 2 }}
{{- end }}
{{- end }}

{{- define "identity-host.scheduling" -}}
{{- with .Values.nodeSelector }}
nodeSelector:
  {{- toYaml . | nindent 2 }}
{{- end }}
{{- with .Values.affinity }}
affinity:
  {{- toYaml . | nindent 2 }}
{{- end }}
{{- with .Values.tolerations }}
tolerations:
  {{- toYaml . | nindent 2 }}
{{- end }}
{{- end }}

{{/*
Whether the release uses the named key wrapper, as the root key provider or as a provider a tenant may bring a root key
from (secretProtection.ownerRootKeys.providers), so that the pod carries that provider's workload credentials. Takes a
dict with the chart context as root and the key wrapper name as name; renders "true" or nothing.
*/}}
{{- define "identity-host.usesKeyWrapper" -}}
{{- if or (eq .root.Values.secretProtection.rootKeyProvider .name) (has .name .root.Values.secretProtection.ownerRootKeys.providers) }}true{{ end }}
{{- end }}

{{/*
The external_account credential configuration that Google Application Default Credentials read: it exchanges the
projected service-account token with Google STS, and impersonates a service account when one is named. Empty unless the
release uses Google Cloud KMS, for the root key or for a tenant's root key, through workload identity federation. The
token path must match the volume deployment.yaml mounts.
*/}}
{{- define "identity-host.googleCloudCredentialConfiguration" -}}
{{- $googleCloudKms := .Values.secretProtection.googleCloudKms }}
{{- $federation := $googleCloudKms.workloadIdentityFederation }}
{{- if and (eq (include "identity-host.usesKeyWrapper" (dict "root" . "name" "google-cloud-kms")) "true") $federation.provider }}
{{- $configuration := dict "type" "external_account" "audience" (printf "//iam.googleapis.com/%s" $federation.provider) "subject_token_type" "urn:ietf:params:oauth:token-type:jwt" "token_url" "https://sts.googleapis.com/v1/token" "credential_source" (dict "file" "/var/run/secrets/tokens/google/token" "format" (dict "type" "text")) }}
{{- if $federation.serviceAccountEmail }}
{{- $_ := set $configuration "service_account_impersonation_url" (printf "https://iamcredentials.googleapis.com/v1/projects/-/serviceAccounts/%s:generateAccessToken" $federation.serviceAccountEmail) }}
{{- end }}
{{- $configuration | toPrettyJson }}
{{- end }}
{{- end }}

{{/*
The environment of the host container, which the Deployment and the secret protection Job share, so a Job reads the
same configuration the pods it rewraps keys for read.
*/}}
{{- define "identity-host.hostEnv" -}}
{{- $valkey := include "identity-host.valkeyEnabled" . }}
{{- $secretProtectionRoot := "/etc/ncode-identity/secret-protection" }}
{{- $awsWebIdentityRoot := "/var/run/secrets/tokens/aws" }}
{{- /* A pod that uses AWS KMS neither for the root key nor for a tenant's root key never carries AWS credentials, whatever the web identity values say. */}}
{{- $awsWebIdentity := and (eq (include "identity-host.usesKeyWrapper" (dict "root" . "name" "aws-kms")) "true") .Values.secretProtection.awsKms.webIdentity.roleArn }}
{{- $azureWorkloadIdentityRoot := "/var/run/secrets/tokens/azure" }}
{{- /* A pod that uses Azure Key Vault neither for the root key nor for a tenant's root key never carries Azure credentials, whatever the workload identity values say. */}}
{{- $azureWorkloadIdentity := and (eq (include "identity-host.usesKeyWrapper" (dict "root" . "name" "azure-key-vault")) "true") .Values.secretProtection.azureKeyVault.workloadIdentity.clientId }}
{{- $googleCloudCredentials := include "identity-host.googleCloudCredentialConfiguration" . }}
{{- $googleCloudTokenRoot := "/var/run/secrets/tokens/google" }}
- name: ASPNETCORE_ENVIRONMENT
  value: {{ .Values.environment | quote }}
- name: AllowedHosts
  value: {{ join ";" .Values.allowedHosts | quote }}
- name: OpenId__TenantResolution__StrategyCode
  value: {{ .Values.tenantResolution.strategyCode | quote }}
{{- with .Values.tenantResolution.staticSingle.issuer }}
- name: OpenId__TenantResolution__StaticSingle__Issuer
  value: {{ . | quote }}
{{- end }}
{{- range $index, $network := .Values.forwardedHeaders.knownNetworks }}
- name: ForwardedHeaders__KnownNetworks__{{ $index }}
  value: {{ $network | quote }}
{{- end }}
{{- range $index, $proxy := .Values.forwardedHeaders.knownProxies }}
- name: ForwardedHeaders__KnownProxies__{{ $index }}
  value: {{ $proxy | quote }}
{{- end }}
- name: ForwardedHeaders__ForwardLimit
  value: {{ .Values.forwardedHeaders.forwardLimit | quote }}
- name: ConnectionStrings__openiddb
  valueFrom:
    secretKeyRef:
      name: {{ .Values.database.existingSecret }}
      key: {{ .Values.database.connectionStringKey }}
{{- if $valkey }}
- name: ConnectionStrings__valkey
  valueFrom:
    secretKeyRef:
      name: {{ .Values.valkey.existingSecret }}
      key: {{ .Values.valkey.connectionStringKey }}
{{- end }}
{{- with .Values.secretProtection.rootKeyProvider }}
- name: SecretProtection__RootKeyProvider
  value: {{ . | quote }}
{{- end }}
{{- if .Values.secretProtection.local.keySecret }}
- name: SecretProtection__RootKeyProviders__Local__KeyPath
  value: {{ printf "%s/root-key/key" $secretProtectionRoot }}
{{- end }}
{{- range $index, $secret := .Values.secretProtection.local.previousKeySecrets }}
- name: SecretProtection__RootKeyProviders__Local__PreviousKeyPaths__{{ $index }}
  value: {{ printf "%s/previous-%d/key" $secretProtectionRoot $index }}
{{- end }}
{{- if eq .Values.secretProtection.rootKeyProvider "aws-kms" }}
- name: SecretProtection__RootKeyProviders__AwsKms__KeyArn
  value: {{ .Values.secretProtection.awsKms.keyArn | quote }}
# The SDK reads the region from the environment; the key's ARN names it.
- name: AWS_REGION
  value: {{ index (splitList ":" .Values.secretProtection.awsKms.keyArn) 3 | quote }}
{{- end }}
{{- if $awsWebIdentity }}
# The SDK exchanges the projected token for short-lived credentials of this role.
- name: AWS_ROLE_ARN
  value: {{ .Values.secretProtection.awsKms.webIdentity.roleArn | quote }}
- name: AWS_WEB_IDENTITY_TOKEN_FILE
  value: {{ printf "%s/token" $awsWebIdentityRoot }}
- name: AWS_ROLE_SESSION_NAME
  value: {{ include "identity-host.fullname" . | quote }}
{{- end }}
{{- if eq .Values.secretProtection.rootKeyProvider "azure-key-vault" }}
- name: SecretProtection__RootKeyProviders__AzureKeyVault__KeyId
  value: {{ .Values.secretProtection.azureKeyVault.keyId | quote }}
{{- end }}
{{- if $azureWorkloadIdentity }}
# Azure.Identity exchanges the projected token for an Entra token of this identity, and tries no other
# credential.
- name: AZURE_TENANT_ID
  value: {{ .Values.secretProtection.azureKeyVault.workloadIdentity.tenantId | quote }}
- name: AZURE_CLIENT_ID
  value: {{ .Values.secretProtection.azureKeyVault.workloadIdentity.clientId | quote }}
- name: AZURE_FEDERATED_TOKEN_FILE
  value: {{ printf "%s/token" $azureWorkloadIdentityRoot }}
- name: AZURE_TOKEN_CREDENTIALS
  value: WorkloadIdentityCredential
{{- end }}
{{- if eq .Values.secretProtection.rootKeyProvider "google-cloud-kms" }}
- name: SecretProtection__RootKeyProviders__GoogleCloudKms__KeyName
  value: {{ .Values.secretProtection.googleCloudKms.keyName | quote }}
{{- end }}
{{- if $googleCloudCredentials }}
# Application Default Credentials read this configuration, which exchanges the projected token with Google
# STS.
- name: GOOGLE_APPLICATION_CREDENTIALS
  value: {{ printf "%s/credential-configuration.json" $googleCloudTokenRoot }}
{{- end }}
{{- range $index, $provider := .Values.secretProtection.ownerRootKeys.providers }}
- name: SecretProtection__OwnerRootKeys__Providers__{{ $index }}
  value: {{ $provider | quote }}
{{- end }}
{{- if .Values.secretProtection.recovery.publicKeyConfigMap }}
- name: SecretProtection__Recovery__PublicKeyPath
  value: {{ printf "%s/recovery/recovery-public.pem" $secretProtectionRoot }}
{{- end }}
{{- if .Values.secretProtection.recovery.disabled }}
- name: SecretProtection__Recovery__Disabled
  value: "true"
{{- end }}
{{- with .Values.extraEnv }}
{{- toYaml . | nindent 0 }}
{{- end }}
{{- end }}

{{/*
The volume mounts of the host container, which the Deployment and the secret protection Job share.
*/}}
{{- define "identity-host.hostVolumeMounts" -}}
{{- $secretProtectionRoot := "/etc/ncode-identity/secret-protection" }}
{{- $awsWebIdentityRoot := "/var/run/secrets/tokens/aws" }}
{{- /* A pod that uses AWS KMS neither for the root key nor for a tenant's root key never carries AWS credentials, whatever the web identity values say. */}}
{{- $awsWebIdentity := and (eq (include "identity-host.usesKeyWrapper" (dict "root" . "name" "aws-kms")) "true") .Values.secretProtection.awsKms.webIdentity.roleArn }}
{{- $azureWorkloadIdentityRoot := "/var/run/secrets/tokens/azure" }}
{{- /* A pod that uses Azure Key Vault neither for the root key nor for a tenant's root key never carries Azure credentials, whatever the workload identity values say. */}}
{{- $azureWorkloadIdentity := and (eq (include "identity-host.usesKeyWrapper" (dict "root" . "name" "azure-key-vault")) "true") .Values.secretProtection.azureKeyVault.workloadIdentity.clientId }}
{{- $googleCloudCredentials := include "identity-host.googleCloudCredentialConfiguration" . }}
{{- $googleCloudTokenRoot := "/var/run/secrets/tokens/google" }}
# The root filesystem is read-only; the runtime's diagnostics socket and temporary files go here.
- name: tmp
  mountPath: /tmp
{{- if .Values.secretProtection.local.keySecret }}
- name: secret-protection-root-key
  mountPath: {{ printf "%s/root-key" $secretProtectionRoot }}
  readOnly: true
{{- end }}
{{- range $index, $secret := .Values.secretProtection.local.previousKeySecrets }}
- name: secret-protection-previous-{{ $index }}
  mountPath: {{ printf "%s/previous-%d" $secretProtectionRoot $index }}
  readOnly: true
{{- end }}
{{- if $awsWebIdentity }}
- name: aws-web-identity
  mountPath: {{ $awsWebIdentityRoot }}
  readOnly: true
{{- end }}
{{- if $azureWorkloadIdentity }}
- name: azure-workload-identity
  mountPath: {{ $azureWorkloadIdentityRoot }}
  readOnly: true
{{- end }}
{{- if $googleCloudCredentials }}
- name: google-cloud-credentials
  mountPath: {{ $googleCloudTokenRoot }}
  readOnly: true
{{- end }}
{{- if .Values.secretProtection.recovery.publicKeyConfigMap }}
- name: secret-protection-recovery
  mountPath: {{ printf "%s/recovery" $secretProtectionRoot }}
  readOnly: true
{{- end }}
{{- with .Values.extraVolumeMounts }}
{{- toYaml . | nindent 0 }}
{{- end }}
{{- end }}

{{/*
The volumes of the host pod, which the Deployment and the secret protection Job share.
*/}}
{{- define "identity-host.hostVolumes" -}}
{{- /* A pod that uses AWS KMS neither for the root key nor for a tenant's root key never carries AWS credentials, whatever the web identity values say. */}}
{{- $awsWebIdentity := and (eq (include "identity-host.usesKeyWrapper" (dict "root" . "name" "aws-kms")) "true") .Values.secretProtection.awsKms.webIdentity.roleArn }}
{{- /* A pod that uses Azure Key Vault neither for the root key nor for a tenant's root key never carries Azure credentials, whatever the workload identity values say. */}}
{{- $azureWorkloadIdentity := and (eq (include "identity-host.usesKeyWrapper" (dict "root" . "name" "azure-key-vault")) "true") .Values.secretProtection.azureKeyVault.workloadIdentity.clientId }}
{{- $googleCloudCredentials := include "identity-host.googleCloudCredentialConfiguration" . }}
- name: tmp
  emptyDir:
    sizeLimit: 256Mi
{{- with .Values.secretProtection.local.keySecret }}
- name: secret-protection-root-key
  secret:
    secretName: {{ . }}
    defaultMode: 0440
{{- end }}
{{- range $index, $secret := .Values.secretProtection.local.previousKeySecrets }}
- name: secret-protection-previous-{{ $index }}
  secret:
    secretName: {{ $secret }}
    defaultMode: 0440
{{- end }}
{{- if $awsWebIdentity }}
# The pod does not mount the default service account token, so this one is projected explicitly, for AWS only.
- name: aws-web-identity
  projected:
    sources:
      - serviceAccountToken:
          audience: {{ .Values.secretProtection.awsKms.webIdentity.audience | quote }}
          expirationSeconds: {{ .Values.secretProtection.awsKms.webIdentity.tokenExpirationSeconds }}
          path: token
{{- end }}
{{- if $azureWorkloadIdentity }}
# The pod does not mount the default service account token, so this one is projected explicitly, for Azure only.
- name: azure-workload-identity
  projected:
    sources:
      - serviceAccountToken:
          audience: {{ .Values.secretProtection.azureKeyVault.workloadIdentity.audience | quote }}
          expirationSeconds: {{ .Values.secretProtection.azureKeyVault.workloadIdentity.tokenExpirationSeconds }}
          path: token
{{- end }}
{{- if $googleCloudCredentials }}
# The token and the configuration that exchanges it, projected together, for Google Cloud only.
- name: google-cloud-credentials
  projected:
    sources:
      - serviceAccountToken:
          audience: {{ printf "https://iam.googleapis.com/%s" .Values.secretProtection.googleCloudKms.workloadIdentityFederation.provider | quote }}
          expirationSeconds: {{ .Values.secretProtection.googleCloudKms.workloadIdentityFederation.tokenExpirationSeconds }}
          path: token
      - configMap:
          name: {{ include "identity-host.fullname" . }}-google-cloud
          items:
            - key: credential-configuration.json
              path: credential-configuration.json
{{- end }}
{{- with .Values.secretProtection.recovery.publicKeyConfigMap }}
- name: secret-protection-recovery
  configMap:
    name: {{ . }}
    defaultMode: 0440
{{- end }}
{{- with .Values.extraVolumes }}
{{- toYaml . | nindent 0 }}
{{- end }}
{{- end }}

{{/*
The fully qualified name of the management console's resources. The release's fully qualified name is shortened first so
the name, with its suffix, fits the 63-character limit.
*/}}
{{- define "identity-host.managementConsole.fullname" -}}
{{- printf "%s-management-console" (include "identity-host.fullname" . | trunc 44 | trimSuffix "-") }}
{{- end }}

{{/*
The labels that select the management console's pods.
*/}}
{{- define "identity-host.managementConsole.selectorLabels" -}}
app.kubernetes.io/name: {{ include "identity-host.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/component: management-console
{{- end }}

{{/*
The labels every resource of the management console carries.
*/}}
{{- define "identity-host.managementConsole.labels" -}}
{{ include "identity-host.commonLabels" . }}
app.kubernetes.io/component: management-console
{{- end }}

{{/*
The name of the management console's ServiceAccount.
*/}}
{{- define "identity-host.managementConsole.serviceAccountName" -}}
{{- if .Values.managementConsole.serviceAccount.create }}
{{- default (include "identity-host.managementConsole.fullname" .) .Values.managementConsole.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.managementConsole.serviceAccount.name }}
{{- end }}
{{- end }}

{{/*
The management console's container image: the digest when one is set, else the tag, which defaults to the chart's
appVersion.
*/}}
{{- define "identity-host.managementConsole.image" -}}
{{- if .Values.managementConsole.image.digest }}
{{- printf "%s@%s" .Values.managementConsole.image.repository .Values.managementConsole.image.digest }}
{{- else }}
{{- printf "%s:%s" .Values.managementConsole.image.repository (default .Chart.AppVersion .Values.managementConsole.image.tag) }}
{{- end }}
{{- end }}

{{/*
The authority of the console's backend-for-frontend: managementConsole.authority, else the server's issuer when the
release resolves its tenant with StaticSingle, else empty.
*/}}
{{- define "identity-host.managementConsole.authority" -}}
{{- if .Values.managementConsole.authority }}
{{- .Values.managementConsole.authority }}
{{- else if eq .Values.tenantResolution.strategyCode "StaticSingle" }}
{{- .Values.tenantResolution.staticSingle.issuer }}
{{- end }}
{{- end }}

{{/*
The URL the console calls the server's management API through: managementConsole.managementApiUrl, else /api on the
console's host.
*/}}
{{- define "identity-host.managementConsole.managementApiUrl" -}}
{{- .Values.managementConsole.managementApiUrl | default (printf "https://%s/api" .Values.managementConsole.host) }}
{{- end }}

{{/*
Whether the management console shares its single-use state through its own Valkey.
*/}}
{{- define "identity-host.managementConsole.valkeyEnabled" -}}
{{- if .Values.managementConsole.valkey.existingSecret }}true{{ end }}
{{- end }}

{{/*
Refuses a management console the host would refuse, or one that would run replicas without shared state, before anything
is applied. Rendered for its side effect only.
*/}}
{{- define "identity-host.managementConsole.validate" -}}
{{- $console := .Values.managementConsole }}
{{- if not $console.host }}
{{- fail "managementConsole.host must be the console's public host name, such as manage.example.com." }}
{{- end }}
{{- if contains "*" $console.host }}
{{- fail "managementConsole.host must be a concrete host name, not a wildcard." }}
{{- end }}
{{- if not (has $console.host .Values.allowedHosts) }}
{{- fail "allowedHosts must list managementConsole.host: the management host routes /api to the server." }}
{{- end }}
{{- $authority := include "identity-host.managementConsole.authority" . }}
{{- if not (hasPrefix "https://" $authority) }}
{{- fail "managementConsole.authority must be the issuer's https URL; it defaults to tenantResolution.staticSingle.issuer when tenantResolution.strategyCode is StaticSingle." }}
{{- end }}
{{- if not $console.keys.clientKeySecret }}
{{- fail "managementConsole.keys.clientKeySecret must name the Secret holding the client's private key under mgmt-client.pem." }}
{{- end }}
{{- if not $console.keys.dpopKeySecret }}
{{- fail "managementConsole.keys.dpopKeySecret must name the Secret holding the DPoP private key under mgmt-dpop.pem." }}
{{- end }}
{{- if and (gt (int $console.replicaCount) 1) (not (include "identity-host.managementConsole.valkeyEnabled" .)) }}
{{- fail "managementConsole.replicaCount above 1 requires managementConsole.valkey.existingSecret: replicas share their sessions through Valkey." }}
{{- end }}
{{- end }}

{{/*
The environment of the management console container.
*/}}
{{- define "identity-host.managementConsole.env" -}}
{{- $console := .Values.managementConsole }}
{{- $keyRoot := "/etc/ncode-identity/management-console" }}
- name: ASPNETCORE_ENVIRONMENT
  value: {{ .Values.environment | quote }}
- name: AllowedHosts
  value: {{ $console.host | quote }}
{{- range $index, $network := .Values.forwardedHeaders.knownNetworks }}
- name: ForwardedHeaders__KnownNetworks__{{ $index }}
  value: {{ $network | quote }}
{{- end }}
{{- range $index, $proxy := .Values.forwardedHeaders.knownProxies }}
- name: ForwardedHeaders__KnownProxies__{{ $index }}
  value: {{ $proxy | quote }}
{{- end }}
- name: ForwardedHeaders__ForwardLimit
  value: {{ .Values.forwardedHeaders.forwardLimit | quote }}
- name: Bff__Frontends__mgmt__Authority
  value: {{ include "identity-host.managementConsole.authority" . | quote }}
- name: Bff__Frontends__mgmt__PublicOrigin
  value: {{ printf "https://%s" $console.host | quote }}
- name: Bff__KeyRings__mgmt-client__PemFiles__0
  value: {{ printf "%s/client-key/mgmt-client.pem" $keyRoot | quote }}
- name: Bff__KeyRings__mgmt-dpop__PemFiles__0
  value: {{ printf "%s/dpop-key/mgmt-dpop.pem" $keyRoot | quote }}
- name: ManagementConsole__ManagementApiUrl
  value: {{ include "identity-host.managementConsole.managementApiUrl" . | quote }}
{{- if include "identity-host.managementConsole.valkeyEnabled" . }}
- name: ConnectionStrings__valkey
  valueFrom:
    secretKeyRef:
      name: {{ $console.valkey.existingSecret }}
      key: {{ $console.valkey.connectionStringKey }}
{{- end }}
{{- with $console.extraEnv }}
{{- toYaml . | nindent 0 }}
{{- end }}
{{- end }}
