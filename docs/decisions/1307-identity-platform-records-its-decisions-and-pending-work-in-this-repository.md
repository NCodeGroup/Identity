# 1307. identity-platform records its decisions and pending work in this repository

- **Status:** Accepted
- **Date:** 2026-11-01
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0010](project-template/0010-a-change-consumers-can-observe-is-decided-before-it-is-built.md)

## Context

[`NCodeGroup/identity-platform`](https://github.com/NCodeGroup/identity-platform) holds the hosted platform's
infrastructure as code in a public repository of its own
([ADR-1132](1132-platform-infrastructure-as-code-is-public-on-github.md)). It is made from the same project template
as this repository. The template makes each repository the home of its own decisions
([project-template ADR-0010](project-template/0010-a-change-consumers-can-observe-is-decided-before-it-is-built.md))
and pending work ([project-template ADR-0007](project-template/0007-pending-work-is-tracked-as-issues.md)), and its
merge-method record rules out squash merges
([project-template ADR-0013](project-template/0013-pull-requests-merge-with-a-merge-commit-or-by-rebasing.md); template
v0.49.0 and earlier set the method in
[ADR-0005](project-template/0005-the-default-branch-accepts-changes-only-through-pull-requests.md)). The platform's
decisions, such as its environments, deployment artifacts and resource tags
([ADR-1131](1131-hosted-platform-environment-model-and-hostname-scheme.md) to
[ADR-1136](1136-provenance-is-attested-on-workloads-and-audited-for-infrastructure.md)), build on the library's and
cite them, and its work is planned beside the library's.

## Decision

identity-platform keeps no decision records and no issues of its own: both live in this repository. In
identity-platform, whose `AGENTS.md` lists them as overrides, that supersedes three of the template's records:

1. **Decisions (project-template ADR-0010).** A change identity-platform's consumers can observe starts as an issue
   here, and its decision is recorded as an ADR in this repository's `docs/decisions/`, before or with the
   identity-platform pull request that builds it. identity-platform's `docs/decisions/` holds only the template's
   records, and `/analyze-decision` is not used there.
2. **Pending work (project-template ADR-0007).** identity-platform's pending work is issues here, under
   [ADR-1306](1306-pending-work-is-tracked-as-github-issues-linked-by-address-in-files.md). Its `/ship-plan` files a
   plan's follow-ups here (`follow-ups: NCodeGroup/Identity`), and its pull requests end with
   `Closes NCodeGroup/Identity#<number>` or `Refs NCodeGroup/Identity#<number>`.
3. **Merge method (project-template ADR-0013).** identity-platform's `main` takes only squash merges, with linear
   history: the pull request's title, a conventional commit subject, and its description become the one commit on
   `main`, and the required `Commit attribution` check keeps agent trailers out of both, for the reasons
   [ADR-1304](1304-pull-requests-into-main-merge-by-squash-through-the-merge-queue.md) gives for this repository.

In this repository, the record refines project-template ADR-0010 and ADR-1306: `docs/decisions/` and the issues also
serve identity-platform.

## Options considered

- **Decisions and issues in identity-platform, as the template sets out.** Rejected: the platform's decisions build on
  the library's, so most records would link across repositories, and the work would be split across two queues that
  `/triage-issues` and `/run-issues` read separately.
- **Decisions here and issues in identity-platform.** Rejected: every issue would link a record in another repository,
  and a pull request here could not close the item it completes.
- **Both here (chosen).** One index of decisions and one work queue for the product and the platform that hosts it.

## Consequences

- An identity-platform change that needs a decision takes two pull requests: the record here, then the change there
  that cites it.
- An issue here may be completed by a pull request in identity-platform, which closes it with
  `Closes NCodeGroup/Identity#<number>`.
- When identity-platform takes a template version that moves one of these rules to another record, its `AGENTS.md`
  names the new record, and this record still states the decision.

## References

- [ADR-1132](1132-platform-infrastructure-as-code-is-public-on-github.md) — identity-platform is a repository of its
  own.
- [ADR-1304](1304-pull-requests-into-main-merge-by-squash-through-the-merge-queue.md) and
  [ADR-1306](1306-pending-work-is-tracked-as-github-issues-linked-by-address-in-files.md) — this repository's merge
  method and pending-work records.
- identity-platform's [`AGENTS.md`](https://github.com/NCodeGroup/identity-platform/blob/main/AGENTS.md), which lists
  the overrides.
- [#438](https://github.com/NCodeGroup/Identity/issues/438).
