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
{{- if and (eq .Values.environment "Production") (not .Values.keyProtection.certificateSecret) }}
{{- fail "keyProtection.certificateSecret must name the TLS Secret whose certificate encrypts the key ring in Production." }}
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
