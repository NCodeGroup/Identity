{{/*
The name of the Secret that holds the server pods' serving certificate: backendTls.existingSecret, or else the
Secret the chart's Certificate writes.
*/}}
{{- define "identity-host.backendTls.secretName" -}}
{{- .Values.backendTls.existingSecret | default (printf "%s-tls" (include "identity-host.fullname" .)) }}
{{- end }}

{{/*
The name of the Secret that holds the management console pods' serving certificate.
*/}}
{{- define "identity-host.managementConsole.backendTls.secretName" -}}
{{- .Values.managementConsole.backendTls.existingSecret | default (printf "%s-tls" (include "identity-host.managementConsole.fullname" .)) }}
{{- end }}

{{/*
The name of the Secret from which an ingress controller reads the CA that verifies the pods' certificate. Takes a dict
with root and service (the Service's name). For the management console it is
managementConsole.backendTls.ingressCaSecret, else backendTls.ingressCaSecret, else the console's serving Secret; for
the server it is backendTls.ingressCaSecret, else the serving Secret. A CA-only Secret keeps the pods' private key from
the controller.
*/}}
{{- define "identity-host.backendTls.verificationSecretName" -}}
{{- $root := .root }}
{{- if eq .service (include "identity-host.managementConsole.fullname" $root) }}
{{- $root.Values.managementConsole.backendTls.ingressCaSecret | default $root.Values.backendTls.ingressCaSecret | default (include "identity-host.managementConsole.backendTls.secretName" $root) }}
{{- else }}
{{- $root.Values.backendTls.ingressCaSecret | default (include "identity-host.backendTls.secretName" $root) }}
{{- end }}
{{- end }}

{{/*
The DNS name an ingress controller verifies the pods' certificate against. Takes a dict with root (the chart's root
context) and service (the Service's name).
*/}}
{{- define "identity-host.backendTls.serverName" -}}
{{- printf "%s.%s.svc" .service .root.Release.Namespace }}
{{- end }}

{{/*
The DNS names of a Service's certificate, as a YAML list. Takes a dict with root, service and extra (a list of further
names).
*/}}
{{- define "identity-host.backendTls.dnsNames" -}}
- {{ .service }}
- {{ printf "%s.%s" .service .root.Release.Namespace }}
- {{ include "identity-host.backendTls.serverName" . }}
- {{ printf "%s.%s.svc.cluster.local" .service .root.Release.Namespace }}
{{- range .extra }}
- {{ . }}
{{- end }}
{{- end }}

{{/*
The issuer of the pods' certificates: backendTls.certManager.issuerRef, or else the release's private CA Issuer.
*/}}
{{- define "identity-host.backendTls.issuerRef" -}}
{{- $issuerRef := .Values.backendTls.certManager.issuerRef }}
{{- if $issuerRef.name }}
name: {{ $issuerRef.name }}
kind: {{ $issuerRef.kind }}
group: {{ $issuerRef.group }}
{{- else }}
name: {{ printf "%s-ca" (include "identity-host.fullname" .) }}
kind: Issuer
group: cert-manager.io
{{- end }}
{{- end }}

{{/*
The Certificate that issues a Service's serving certificate. Takes a dict with root, name (the Certificate's name),
secretName, service, extra and labels (the rendered label block).
*/}}
{{- define "identity-host.backendTls.certificate" -}}
{{- $certManager := .root.Values.backendTls.certManager }}
apiVersion: cert-manager.io/v1
kind: Certificate
metadata:
  name: {{ .name }}
  labels:
    {{- .labels | nindent 4 }}
spec:
  secretName: {{ .secretName }}
  duration: {{ $certManager.duration }}
  renewBefore: {{ $certManager.renewBefore }}
  privateKey:
    algorithm: {{ $certManager.privateKey.algorithm }}
    size: {{ $certManager.privateKey.size }}
    encoding: PKCS8
    rotationPolicy: Always
  usages:
    - digital signature
    - server auth
  dnsNames:
    {{- include "identity-host.backendTls.dnsNames" . | nindent 4 }}
  issuerRef:
    {{- include "identity-host.backendTls.issuerRef" .root | trim | nindent 4 }}
{{- end }}

{{/*
The environment of a pod that serves HTTPS: the one HTTPS endpoint and the certificate files the mounted Secret holds.
*/}}
{{- define "identity-host.backendTls.env" -}}
- name: ASPNETCORE_URLS
  value: "https://+:8443"
- name: Kestrel__Certificates__Default__Path
  value: /etc/ncode-identity/tls/tls.crt
- name: Kestrel__Certificates__Default__KeyPath
  value: /etc/ncode-identity/tls/tls.key
{{- end }}

{{/*
The Ingress annotations that make the ingress-nginx controller speak HTTPS to a Service and verify its pods'
certificate against the Secret that identity-host.backendTls.verificationSecretName chooses. Takes a dict with root and
service.
*/}}
{{- define "identity-host.backendTls.ingressNginxAnnotations" -}}
nginx.ingress.kubernetes.io/backend-protocol: "HTTPS"
nginx.ingress.kubernetes.io/proxy-ssl-verify: "on"
nginx.ingress.kubernetes.io/proxy-ssl-secret: {{ printf "%s/%s" .root.Release.Namespace (include "identity-host.backendTls.verificationSecretName" .) | quote }}
nginx.ingress.kubernetes.io/proxy-ssl-name: {{ include "identity-host.backendTls.serverName" . | quote }}
nginx.ingress.kubernetes.io/proxy-ssl-server-name: "on"
{{- end }}

{{/*
The Service annotations that make the Traefik controller speak HTTPS to a Service through the release's
ServersTransport. Takes a dict with root and name (the ServersTransport's name).
*/}}
{{- define "identity-host.backendTls.traefikServiceAnnotations" -}}
traefik.ingress.kubernetes.io/service.serversscheme: https
traefik.ingress.kubernetes.io/service.serverstransport: {{ printf "%s-%s@kubernetescrd" .root.Release.Namespace .name | quote }}
{{- end }}

{{/*
The name of the ConfigMap that holds the CA verifying a Service's pods, as backendTls.caConfigMap names it; the
management console's Service takes managementConsole.backendTls.caConfigMap first. Empty when none is set. Takes a dict
with root and service (the Service's name).
*/}}
{{- define "identity-host.backendTls.caConfigMap" -}}
{{- if eq .service (include "identity-host.managementConsole.fullname" .root) }}
{{- .root.Values.managementConsole.backendTls.caConfigMap | default .root.Values.backendTls.caConfigMap }}
{{- else }}
{{- .root.Values.backendTls.caConfigMap }}
{{- end }}
{{- end }}

{{/*
The rootCAs item of a ServersTransport: the CA ConfigMap when one is set, otherwise the Service's serving Secret. Takes a
dict with root and service (the Service's name).
*/}}
{{- define "identity-host.backendTls.traefikRootCA" -}}
{{- $caConfigMap := include "identity-host.backendTls.caConfigMap" . }}
{{- if $caConfigMap }}
configMap: {{ $caConfigMap }}
{{- else if eq .service (include "identity-host.managementConsole.fullname" .root) }}
secret: {{ include "identity-host.managementConsole.backendTls.secretName" .root }}
{{- else }}
secret: {{ include "identity-host.backendTls.secretName" .root }}
{{- end }}
{{- end }}

{{/*
The BackendTLSPolicy that makes a Gateway API implementation speak HTTPS to a Service and verify its pods' certificate
against the CA ConfigMap. Takes a dict with root, service (the Service's name) and labels (the rendered label block).
*/}}
{{- define "identity-host.backendTls.policy" -}}
apiVersion: gateway.networking.k8s.io/v1
kind: BackendTLSPolicy
metadata:
  name: {{ .service }}
  labels:
    {{- .labels | nindent 4 }}
spec:
  targetRefs:
    - group: ""
      kind: Service
      name: {{ .service }}
      sectionName: https
  validation:
    caCertificateRefs:
      - group: ""
        kind: ConfigMap
        name: {{ include "identity-host.backendTls.caConfigMap" . }}
    hostname: {{ include "identity-host.backendTls.serverName" . | quote }}
{{- end }}

{{/*
The HTTPRoute that sends every path to a Service's HTTPS port. Takes a dict with root, service (the Service's name),
labels (the rendered label block), annotations (a map), parentRefs (a list), hostnames (a list) and port.
*/}}
{{- define "identity-host.httpRoute" -}}
apiVersion: gateway.networking.k8s.io/v1
kind: HTTPRoute
metadata:
  name: {{ .service }}
  labels:
    {{- .labels | nindent 4 }}
  {{- with .annotations }}
  annotations:
    {{- toYaml . | nindent 4 }}
  {{- end }}
spec:
  parentRefs:
    {{- toYaml .parentRefs | nindent 4 }}
  hostnames:
    {{- toYaml .hostnames | nindent 4 }}
  rules:
    - matches:
        - path:
            type: PathPrefix
            value: /
      backendRefs:
        - name: {{ .service }}
          port: {{ .port }}
{{- end }}

{{/*
Refuses a certificate the chart's Certificate could not describe to cert-manager. Rendered for its side effect only.
*/}}
{{- define "identity-host.backendTls.validate" -}}
{{- $certManager := .Values.backendTls.certManager }}
{{- if ge (int (trimSuffix "h" $certManager.renewBefore)) (int (trimSuffix "h" $certManager.duration)) }}
{{- fail "backendTls.certManager.renewBefore must be shorter than backendTls.certManager.duration." }}
{{- end }}
{{- if and (eq $certManager.privateKey.algorithm "ECDSA") (gt (int $certManager.privateKey.size) 521) }}
{{- fail "backendTls.certManager.privateKey.size must be 256, 384 or 521 for an ECDSA key." }}
{{- end }}
{{- if and (eq $certManager.privateKey.algorithm "RSA") (lt (int $certManager.privateKey.size) 2048) }}
{{- fail "backendTls.certManager.privateKey.size must be at least 2048 for an RSA key." }}
{{- end }}
{{- end }}
