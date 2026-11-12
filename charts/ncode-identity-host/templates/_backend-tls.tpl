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
The name of the Secret that the chart's ingress Certificate writes: the ingress controller's own client certificate and
the CA that signed the pods' certificates.
*/}}
{{- define "identity-host.backendTls.ingressSecretName" -}}
{{- printf "%s-ingress" (include "identity-host.fullname" .) }}
{{- end }}

{{/*
The name of the ingress controller's Secret for a Service: its ca.crt verifies the pods' certificate and its tls.crt and
tls.key are the controller's client certificate. Takes a dict with root and service (the Service's name). For the
management console it is managementConsole.backendTls.ingressSecret, else backendTls.ingressSecret, else the chart's
ingress Secret; for the server it is backendTls.ingressSecret, else the chart's ingress Secret. The pods' serving
Secret is never a fallback, so the controller never reads the pods' private key.
*/}}
{{- define "identity-host.backendTls.verificationSecretName" -}}
{{- $root := .root }}
{{- if eq .service (include "identity-host.managementConsole.fullname" $root) }}
{{- $root.Values.managementConsole.backendTls.ingressSecret | default $root.Values.backendTls.ingressSecret | default (include "identity-host.backendTls.ingressSecretName" $root) }}
{{- else }}
{{- $root.Values.backendTls.ingressSecret | default (include "identity-host.backendTls.ingressSecretName" $root) }}
{{- end }}
{{- end }}

{{/*
Prints true when a component's route uses the ingress controller's Secret, and nothing otherwise: a Traefik Ingress
verifies the pods against it, and any route presents its client certificate when backendTls.clientCertificate.required
is true. Takes a dict with root and console (true for the management console).
*/}}
{{- define "identity-host.backendTls.controllerSecretUsed" -}}
{{- $values := .root.Values }}
{{- $ingress := $values.ingress.enabled }}
{{- $route := or $values.ingress.enabled $values.httpRoute.enabled }}
{{- if .console }}
{{- $console := $values.managementConsole }}
{{- $ingress = and $console.enabled $console.ingress.enabled }}
{{- $route = and $console.enabled (or $console.ingress.enabled $console.httpRoute.enabled) }}
{{- end }}
{{- if or (and $ingress (eq $values.ingress.controller "traefik")) (and $route $values.backendTls.clientCertificate.required) }}
{{- print "true" }}
{{- end }}
{{- end }}

{{/*
Prints true when the chart issues the ingress Secret: a component's route uses the controller's Secret and no
ingressSecret of the operator's names it. Prints nothing otherwise.
*/}}
{{- define "identity-host.backendTls.ingressCertificateNeeded" -}}
{{- $server := and (include "identity-host.backendTls.controllerSecretUsed" (dict "root" . "console" false)) (not .Values.backendTls.ingressSecret) }}
{{- $console := and (include "identity-host.backendTls.controllerSecretUsed" (dict "root" . "console" true)) (not .Values.managementConsole.backendTls.ingressSecret) (not .Values.backendTls.ingressSecret) }}
{{- if or $server $console }}
{{- print "true" }}
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
The Certificate that issues the ingress controller's client certificate. Takes a dict with root and labels (the
rendered label block).
*/}}
{{- define "identity-host.backendTls.ingressCertificate" -}}
{{- $certManager := .root.Values.backendTls.certManager }}
apiVersion: cert-manager.io/v1
kind: Certificate
metadata:
  name: {{ include "identity-host.backendTls.ingressSecretName" .root }}
  labels:
    {{- .labels | nindent 4 }}
spec:
  secretName: {{ include "identity-host.backendTls.ingressSecretName" .root }}
  commonName: {{ .root.Values.backendTls.clientCertificate.commonName }}
  duration: {{ $certManager.duration }}
  renewBefore: {{ $certManager.renewBefore }}
  privateKey:
    algorithm: {{ $certManager.privateKey.algorithm }}
    size: {{ $certManager.privateKey.size }}
    encoding: PKCS8
    rotationPolicy: Always
  usages:
    - digital signature
    - client auth
  issuerRef:
    {{- include "identity-host.backendTls.issuerRef" .root | trim | nindent 4 }}
{{- end }}

{{/*
The environment of a pod that serves HTTPS: the one HTTPS endpoint and the certificate files the mounted Secret holds.
When backendTls.clientCertificate.required is true the pod also requires the ingress controller's client certificate:
one that chains to the mounted ca.crt and whose subject is CN=<backendTls.clientCertificate.commonName>.
*/}}
{{- define "identity-host.backendTls.env" -}}
- name: ASPNETCORE_URLS
  value: "https://+:8443"
- name: Kestrel__Certificates__Default__Path
  value: /etc/ncode-identity/tls/tls.crt
- name: Kestrel__Certificates__Default__KeyPath
  value: /etc/ncode-identity/tls/tls.key
{{- if .Values.backendTls.clientCertificate.required }}
- name: ProxyClientCertificate__Required
  value: "true"
- name: ProxyClientCertificate__CertificateAuthorityPath
  value: /etc/ncode-identity/tls/ca.crt
- name: ProxyClientCertificate__AllowedSubjects__0
  value: {{ printf "CN=%s" .Values.backendTls.clientCertificate.commonName | quote }}
{{- end }}
{{- end }}

{{/*
The Service annotations that make the Traefik controller speak HTTPS to a Service through the release's
ServersTransport, which verifies the pods' certificate and, when backendTls.clientCertificate.required is true,
presents the controller's client certificate. Takes a dict with root and name (the ServersTransport's name).
*/}}
{{- define "identity-host.backendTls.traefikServiceAnnotations" -}}
traefik.ingress.kubernetes.io/service.serversscheme: https
traefik.ingress.kubernetes.io/service.serverstransport: {{ printf "%s-%s@kubernetescrd" .root.Release.Namespace .name | quote }}
{{- end }}

{{/*
The name of the ConfigMap that holds the CA a Gateway API implementation verifies a Service's pods against:
backendTls.caConfigMap, or <release>-ca when backendTls.caPublisher.enabled publishes it; the management console's
Service takes managementConsole.backendTls.caConfigMap first. Empty when none is set. Takes a dict with root and service
(the Service's name).
*/}}
{{- define "identity-host.backendTls.caConfigMap" -}}
{{- $server := .root.Values.backendTls.caConfigMap }}
{{- if and (not $server) .root.Values.backendTls.caPublisher.enabled }}
{{- $server = printf "%s-ca" (include "identity-host.fullname" .root) }}
{{- end }}
{{- if eq .service (include "identity-host.managementConsole.fullname" .root) }}
{{- .root.Values.managementConsole.backendTls.caConfigMap | default $server }}
{{- else }}
{{- $server }}
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
{{- $console := .Values.managementConsole }}
{{- $consoleOn := $console.enabled }}
{{- if and (or .Values.ingress.enabled (and $consoleOn $console.ingress.enabled)) (not .Values.ingress.controller) }}
{{- fail "ingress.controller must be traefik or other when an Ingress is enabled: the pods serve HTTPS only, so the controller must speak HTTPS to the Service and verify the pods' certificate." }}
{{- end }}
{{- if .Values.httpRoute.enabled }}
{{- if not .Values.httpRoute.parentRefs }}
{{- fail "httpRoute.parentRefs must name a Gateway listener when httpRoute.enabled is true: an HTTPRoute without a parent attaches to nothing." }}
{{- end }}
{{- if not .Values.httpRoute.hostnames }}
{{- fail "httpRoute.hostnames must not be empty when httpRoute.enabled is true: the route needs a host to match." }}
{{- end }}
{{- if not (include "identity-host.backendTls.caConfigMap" (dict "root" . "service" (include "identity-host.fullname" .))) }}
{{- fail "backendTls.caConfigMap must be set, or backendTls.caPublisher.enabled with the chart's private CA, when httpRoute.enabled is true: the BackendTLSPolicy verifies the pods' certificate against the CA in that ConfigMap." }}
{{- end }}
{{- end }}
{{- if and .Values.backendTls.caPublisher.enabled (or .Values.backendTls.certManager.issuerRef.name .Values.backendTls.existingSecret) }}
{{- fail "backendTls.caPublisher.enabled publishes the chart's private CA to the server's CA ConfigMap, so the server must take its certificate from that CA: backendTls.certManager.issuerRef.name and backendTls.existingSecret must be empty." }}
{{- end }}
{{- if and .Values.backendTls.caPublisher.enabled $consoleOn $console.backendTls.existingSecret (not $console.backendTls.caConfigMap) }}
{{- fail "managementConsole.backendTls.caConfigMap must name the CA of managementConsole.backendTls.existingSecret when backendTls.caPublisher.enabled is true: the console's CA ConfigMap otherwise falls back to the one the publisher writes, which holds only the chart's private CA." }}
{{- end }}
{{- if and $consoleOn $console.httpRoute.enabled }}
{{- if not $console.httpRoute.parentRefs }}
{{- fail "managementConsole.httpRoute.parentRefs must name a Gateway listener when managementConsole.httpRoute.enabled is true: an HTTPRoute without a parent attaches to nothing." }}
{{- end }}
{{- if not (include "identity-host.backendTls.caConfigMap" (dict "root" . "service" (include "identity-host.managementConsole.fullname" .))) }}
{{- fail "managementConsole.backendTls.caConfigMap or backendTls.caConfigMap must be set when managementConsole.httpRoute.enabled is true: the BackendTLSPolicy verifies the console pods' certificate against the CA in that ConfigMap." }}
{{- end }}
{{- end }}
{{- if not $certManager.issuerRef.name }}
{{- if and (include "identity-host.backendTls.controllerSecretUsed" (dict "root" . "console" false)) .Values.backendTls.existingSecret (not .Values.backendTls.ingressSecret) }}
{{- fail "backendTls.ingressSecret is required when backendTls.existingSecret is set without backendTls.certManager.issuerRef and a route uses the ingress controller's Secret: the chart does not know the CA that signed your certificate." }}
{{- end }}
{{- if and (include "identity-host.backendTls.controllerSecretUsed" (dict "root" . "console" true)) $console.backendTls.existingSecret (not $console.backendTls.ingressSecret) (not .Values.backendTls.ingressSecret) }}
{{- fail "managementConsole.backendTls.ingressSecret or backendTls.ingressSecret is required when managementConsole.backendTls.existingSecret is set without backendTls.certManager.issuerRef and a route uses the ingress controller's Secret: the chart does not know the CA that signed your certificate." }}
{{- end }}
{{- end }}
{{- end }}
