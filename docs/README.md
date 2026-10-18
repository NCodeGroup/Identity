# Documentation

Project documentation for the **NCode.Identity** library family.

## Architecture Decision Records (ADRs)

Architecturally significant decisions are captured as ADRs under [`adr/`](./adr). See the
[ADR index](./adr/README.md) for the list and the process we follow.

## Hosting

[`hosting-analysis.md`](./hosting-analysis.md) compares entry-level hosting vendors (Kubernetes, load balancer,
PostgreSQL, Valkey, KMS) with verified monthly pricing, compute sizing, and Traefik mTLS guidance.

[`platform-bootstrap.md`](./platform-bootstrap.md) is the step-by-step runbook for the manual operations that create the
hosted platform from scratch — accounts, the infrastructure repository, credentials, and registrar delegation.
