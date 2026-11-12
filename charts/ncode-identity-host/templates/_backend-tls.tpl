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
The name of the Secret from which an ingress controller reads the CA that verifies the pods' certificate and its own
client certificate. Takes a dict with root and service (the Service's name). For the management console it is
managementConsole.backendTls.ingressSecret, else backendTls.ingressSecret, else the chart's ingress Secret; for the
server it is backendTls.ingressSecret, else the chart's ingress Secret. The pods' serving Secret is never a fallback,
so the controller never reads the pods' private key.
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
Prints true when the chart issues the ingress Secret: the ingress controller is not none and an enabled Ingress has no
ingressSecret of the operator's. Prints nothing otherwise.
*/}}
{{- define "identity-host.backendTls.ingressCertificateNeeded" -}}
{{- if ne .Values.backendTls.ingressController "none" }}
{{- if or (and .Values.ingress.enabled (not .Values.backendTls.ingressSecret)) (and .Values.managementConsole.enabled .Values.managementConsole.ingress.enabled (not .Values.managementConsole.backendTls.ingressSecret) (not .Values.backendTls.ingressSecret)) }}
{{- print "true" }}
{{- end }}
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
The Ingress annotations that make the ingress-nginx controller speak HTTPS to a Service, verify its pods' certificate
and authenticate to them, all through the Secret that identity-host.backendTls.verificationSecretName chooses: the
proxy-ssl-secret Secret holds the CA in ca.crt and the controller's client certificate in tls.crt and tls.key. Takes a
dict with root and service.
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
ServersTransport, which verifies the pods' certificate and, when backendTls.clientCertificate.required is true,
presents the controller's client certificate. Takes a dict with root and name (the ServersTransport's name).
*/}}
{{- define "identity-host.backendTls.traefikServiceAnnotations" -}}
traefik.ingress.kubernetes.io/service.serversscheme: https
traefik.ingress.kubernetes.io/service.serverstransport: {{ printf "%s-%s@kubernetescrd" .root.Release.Namespace .name | quote }}
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
{{- if and (ne .Values.backendTls.ingressController "none") (not $certManager.issuerRef.name) }}
{{- if and .Values.ingress.enabled .Values.backendTls.existingSecret (not .Values.backendTls.ingressSecret) }}
{{- fail "backendTls.ingressSecret is required when backendTls.existingSecret is set without backendTls.certManager.issuerRef: the chart does not know the CA that signed your certificate." }}
{{- end }}
{{- if and .Values.managementConsole.enabled .Values.managementConsole.ingress.enabled .Values.managementConsole.backendTls.existingSecret (not .Values.managementConsole.backendTls.ingressSecret) (not .Values.backendTls.ingressSecret) }}
{{- fail "managementConsole.backendTls.ingressSecret or backendTls.ingressSecret is required when managementConsole.backendTls.existingSecret is set without backendTls.certManager.issuerRef: the chart does not know the CA that signed your certificate." }}
{{- end }}
{{- end }}
{{- end }}
