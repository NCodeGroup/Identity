# 248. The host image writes one-line JSON logs

- **Status:** Accepted
- **Date:** 2026-10-24
- **Deciders:** NCode Group

## Context

The reference hosts write their logs to standard output, and the console provider formats an entry as `simple` unless
the configuration names another formatter
([ADR-0210](0210-the-reference-hosts-log-to-standard-output.md)). The `simple` format spreads one entry over several
lines: a line for the level and the category, a line for the message, and the lines of an exception and its stack
trace. A container log collector reads standard output a line at a time, so it splits one entry into several records,
and the exception that explains an error is in another record than the one that reports it (issue [#265](https://github.com/NCodeGroup/Identity/issues/265)).

The image is the artifact that the hosted platform and a self-hoster both run
([ADR-0133](0133-we-ship-and-run-the-same-deployment-artifacts.md),
[ADR-0178](0178-the-host-image-is-chiseled-non-root-multi-arch-and-carries-the-login-bundle.md)), so the format that a
collector expects belongs to the image, not to one deployment of it.

## Decision

**The `final` stage of the host image selects the console's `json` formatter with UTC timestamps in round-trip format,
so every log entry is one JSON object on one line.**

1. **Three environment variables select it.** The `final` stage sets `Logging__Console__FormatterName=json`,
   `Logging__Console__FormatterOptions__UseUtcTimestamp=true` and
   `Logging__Console__FormatterOptions__TimestampFormat=O`. The console provider reads them as the `Logging:Console`
   configuration that ADR-0210 leaves to the host.
2. **One object holds one entry.** The object holds the timestamp, the level, the category, the event ID, the message,
   the state and the exception, whose stack trace is an escaped string, so a line break inside an exception is never a
   line break of the output. The timestamp is in UTC with the `+00:00` offset, so entries of replicas in different time
   zones sort together.
3. **The migration worker logs the same way.** It runs from the same image, from `/app/migrations`, and registers the
   console provider as the host does, so it reads the same environment.
4. **The `base` stage and runs outside the image keep `simple`.** The `base` stage serves Visual Studio's debugging, and
   a run under the AppHost or with `dotnet run` writes to a console that a person reads.
5. **An operator overrides through the environment.** Setting `Logging__Console__FormatterName=simple` writes text. The
   Helm chart sets nothing of its own, so an operator sets it through `extraEnv`.
6. **CI reads the image's environment.** The `Host image` workflow loads the `linux/amd64` image and fails unless
   `docker image inspect` lists each of the three settings.

## Options considered

- **Set the format in the chart.** Rejected: the same image run another way, with `docker run` or on another
  orchestrator, would write text, and the artifact that is shipped would differ from the one that is run
  ([ADR-0133](0133-we-ship-and-run-the-same-deployment-artifacts.md)).
- **A package extension that chooses the format.** Rejected: a library never chooses the sinks of its host
  ([ADR-0210](0210-the-reference-hosts-log-to-standard-output.md)), and the format is part of the sink.
- **OTLP log export.** Not decided here: the OpenTelemetry pipeline exports metrics, as ADR-0210 records.

## Consequences

- A collector that reads standard output keeps an entry and its exception in one record, and can index the level, the
  category and the event ID as fields.
- The output of `docker logs` for the image is JSON, which is harder to read by eye; an operator who reads it directly
  sets `Logging__Console__FormatterName=simple`.
- The three settings are a contract of the image. A change that drops one fails the `Host image` workflow, and
  `ProgramTests` pins that the formatter they select writes an entry with a multi-line exception as one line that parses
  as JSON.
- This implements issue [#265](https://github.com/NCodeGroup/Identity/issues/265).

## References

- [ADR-0133](0133-we-ship-and-run-the-same-deployment-artifacts.md) — the shipped artifact is the one that is run.
- [ADR-0178](0178-the-host-image-is-chiseled-non-root-multi-arch-and-carries-the-login-bundle.md) — the host image.
- [ADR-0210](0210-the-reference-hosts-log-to-standard-output.md) — logs go to standard output, and their format is
  configuration.
- Code: `src/NCode.Identity.OpenId.Host/Dockerfile`, `.github/workflows/host-image.yml`,
  `tests/NCode.Identity.OpenId.IntegrationTests/ProgramTests.cs`.
