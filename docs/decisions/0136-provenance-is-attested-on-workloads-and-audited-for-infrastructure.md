# 136. Provenance is attested on workloads and audited for infrastructure

- **Status:** Accepted
- **Date:** 2026-10-18
- **Deciders:** NCode Group

## Context

Every deployed thing should be traceable to the reviewed code and the pipeline run that produced it. The obvious place
to record that — a `commit` or `pipeline_run` tag on every cloud resource — conflicts with stable tags
([ADR-0135](0135-one-resource-tagging-strategy-across-every-vendor.md)): each apply would rewrite tags on every
resource, plans would never report "no changes", scheduled drift detection would lose its signal, and a resource the
apply didn't otherwise touch would carry a SHA that says nothing about the code that defines it. The platform deploys
two kinds of things through different paths: **workloads** (container images and Helm charts, rolled out by Flux) and
**infrastructure** (cloud resources, applied by OpenTofu from GitHub Actions)
([ADR-0132](0132-platform-infrastructure-as-code-is-public-on-github.md),
[ADR-0133](0133-we-ship-and-run-the-same-deployment-artifacts.md)).

## Decision

**Workloads carry verifiable provenance; infrastructure carries stable ownership tags and gets its change provenance
from audit trails.**

1. **Workloads.** Every image, chart, and published module artifact is built in GitHub Actions with a build-provenance
   attestation and a keyless Sigstore signature, and is referenced by digest. Flux verifies the signature against the
   building workflow's identity before it applies a release, and the cluster admits only signed images from the
   platform's registry. The provenance travels with the artifact, so it never causes drift.
2. **Infrastructure — what owns it.** Static tags only: `source` (repository and root) and `managed_by` name the code
   that defines a resource; release tags (`?ref=vX.Y.Z`) name the component version. No commit, run, or timestamp tags.
3. **Infrastructure — who changed it, when, from which commit.**
   - CI assumes its AWS role with the session name `gh-<run_id>-<run_attempt>`, so CloudTrail attributes every API
     call to the exact workflow run (and through it the commit and approver). The role trust policies require that
     form with the `sts:RoleSessionName` condition, so CI can't obtain credentials without naming its run.
   - Each CI scope uses its own named Cloudflare token, so Cloudflare's audit log attributes each change to a pipeline
     scope.
   - Every apply job runs in a GitHub environment, so GitHub records a deployment with the commit, run, and approval —
     the answer to "which commit is live in production".
   - The owner-run bootstrap root is attributed to the owner's IAM Identity Center session.

## Options considered

- **Commit and run tags on every resource.** Visible on the resource itself, but rewrites every resource on every
  apply and blinds drift detection. Rejected.
- **A "last deployed" record managed by OpenTofu** (an SSM parameter holding the commit). Always shows a diff, for the
  same reason. Rejected; GitHub deployments already answer it.
- **Provenance only by convention** (trusting that `main` is what runs). No verification at the point of deployment.
  Rejected for workloads.

## Consequences

- Plans stay quiet unless the code or the real world changed, so a non-empty scheduled plan always means drift.
- Tracing an infrastructure change goes resource → CloudTrail or Cloudflare audit log → workflow run → commit, instead
  of reading a tag.
- The workload build pipeline needs attestation, signing, and Flux verification before the first production rollout;
  the `aws-github-oidc` component needs the `sts:RoleSessionName` condition, and the deploy workflow the session name.

## References

- [ADR-0133](0133-we-ship-and-run-the-same-deployment-artifacts.md) — signed OCI artifacts and the member rule.
- [ADR-0135](0135-one-resource-tagging-strategy-across-every-vendor.md) — static tag values.
- [GitHub artifact attestations](https://docs.github.com/actions/security-for-github-actions/using-artifact-attestations).
- [Flux OCI artifact verification](https://fluxcd.io/flux/components/source/ocirepositories/#verification).
