# 210. The reference hosts log to standard output

- **Status:** Accepted (amended by
  [ADR-0248](0248-the-host-image-writes-one-line-json-logs.md): the host image selects one-line JSON)
- **Date:** 2026-10-22
- **Deciders:** NCode Group

## Context

`WebApplication.CreateBuilder` and `Host.CreateApplicationBuilder` register the console, debug, and EventSource logging
providers, and on Windows the Event Log provider as well. The cloud-native principles say that logs go to standard
output
([ADR-0134](0134-cloud-native-operating-principles.md) principle 4), but the defaults choose the sinks by platform.

On Windows, every warning and error from a developer host or a test host went to the machine's Application log,
thousands of entries a day on a machine that runs the integration suite. The Event Log provider also throws on a write
after it is disposed, and the integration fixtures failed at random when a host's last warning reached it during
teardown (issue [#245](https://github.com/NCodeGroup/Identity/issues/245), [ADR-0209](0209-a-replica-adds-each-usage-batch-to-the-ledger-once.md)).

## Decision

1. **Both reference hosts choose the same providers.** `NCode.Identity.OpenId.Host` and
   `NCode.Identity.OpenId.Host.MigrationService` call
   `builder.Logging.ClearProviders().AddConsole().AddEventSourceLogger()`, so the same providers run on every operating
   system.
2. **The console writes to standard output.** Its format is configuration (`Logging:Console:FormatterName`).
3. **EventSource writes nothing until a tool attaches.** `dotnet-monitor` and `dotnet-trace` collect logs through it.
4. **A library never registers or removes a logging provider.** A host that needs another sink, such as the Event Log of
   a Windows service, adds it in its own composition root.

## Options considered

- **Keep the defaults.** Rejected: the sinks depend on the platform, and the Windows sink pollutes the machine's Event
  Log.
- **Remove only the Event Log provider.** Rejected: the debug provider writes only to an attached debugger, which a
  container never has.
- **A package extension that chooses the providers.** Rejected: a library would be choosing the sinks of its host.
- **OTLP log export.** Not decided here: the OpenTelemetry pipeline of
  [ADR-0197](0197-protocol-metrics-have-one-owner-per-signal-and-only-bounded-dimensions.md) exports metrics.

## Consequences

- Nothing is written to the Windows Event Log by a reference host or by the integration tests; `ProgramTests` pins the
  set of providers.
- The console provider's default `simple` format spreads one entry over several lines; the host image selects one-line
  JSON ([ADR-0248](0248-the-host-image-writes-one-line-json-logs.md)).

## References

- [ADR-0134](0134-cloud-native-operating-principles.md) — logs go to standard output.
- [ADR-0197](0197-protocol-metrics-have-one-owner-per-signal-and-only-bounded-dimensions.md) — the OpenTelemetry
  pipeline exports metrics.
- [ADR-0209](0209-a-replica-adds-each-usage-batch-to-the-ledger-once.md) — the final flush that the Event Log provider
  failed under.
- Code: `src/NCode.Identity.OpenId.Host/Program.cs`, `src/NCode.Identity.OpenId.Host.MigrationService/Program.cs`,
  `tests/NCode.Identity.OpenId.IntegrationTests/ProgramTests.cs`.
