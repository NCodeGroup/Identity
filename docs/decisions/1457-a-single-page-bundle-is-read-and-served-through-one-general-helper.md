# 1457. A single-page bundle is read and served through one general helper

- **Status:** Accepted (amends
  [ADR-1254](1254-the-interactive-bundles-hashed-files-are-served-immutable-without-resolving-the-tenant.md) decision
  2: the assets-rooted file provider is `SinglePageBundle<TIndex>`'s)
- **Date:** 2026-11-06
- **Deciders:** NCode Group

## Context

`apps/auth` and `apps/mgmt` are Vite bundles. Each host reads the bundle's `index.html` once per version of the file and
serves the bundle's `assets` directory immutable. Two copies of that code drift (issue
[#612](https://github.com/NCodeGroup/Identity/issues/612)). The surfaces differ only in what they keep of the index:

- The interactive surface splits the index at its mount point once per version.
- The management console renders a nonce into the index for every response.

## Decision

**One `SinglePageBundle<TIndex>` in `NCode.AspNetCore` reads a bundle's index and serves its assets, and each surface
registers one instance with its own index type.**

1. **`NCode.AspNetCore` holds `SinglePageBundle<TIndex>` and `SinglePageBundleDefaults`.** The defaults name the
   `assets` directory and the `index.html` file. The bundle takes the absolute path of its root and a parser from the
   index's text to `TIndex`. It caches the parsed index for a version of the file, identified by last-write time and
   length, and its file provider is rooted at `assets`, so nothing outside `assets` is servable. `ServeAsset` answers a
   file with `Cache-Control: public, max-age=31536000, immutable`, its validators and range processing, and answers `404`
   for no file or an unknown content type.
2. **Each surface registers one instance with its index type.** The interactive surface registers
   `SinglePageBundle<InteractiveBundleShell>` and the management console registers
   `SinglePageBundle<ManagementConsolePage>`. Each resolves its configured root against the content root.
3. **A change to the index is seen by revalidating file metadata, not by a file watcher.** A request compares the
   index's last-write time and length with the cached version.

## Options considered

- **A copy in each surface.** Rejected: the copies drift.
- **Cache the raw text and parse it for every request.** Rejected: the interactive surface's split is the same for every
  request of a version.
- **Static-file middleware.** Rejected for the reasons of
  [ADR-1254](1254-the-interactive-bundles-hashed-files-are-served-immutable-without-resolving-the-tenant.md).
- **A file watcher.** Rejected: it holds an operating-system handle for the lifetime of the host and behaves differently
  across container file systems.

## Consequences

- One implementation, tested in `NCode.AspNetCore.Tests`, serves both bundles.
- `assets` stays the contract with `@ncode/vite-config`
  ([ADR-1157](1157-every-frontend-workspace-builds-and-tests-from-one-vite-preset.md)).
- A surface adds only its index type and its parser.

## References

- [ADR-1254](1254-the-interactive-bundles-hashed-files-are-served-immutable-without-resolving-the-tenant.md),
  [ADR-1355](1355-general-aspnet-core-helpers-live-in-a-dependency-free-package.md)
- `src/NCode.AspNetCore/SinglePageBundle.cs`, `src/NCode.AspNetCore/SinglePageBundleDefaults.cs`
- Issue [#612](https://github.com/NCodeGroup/Identity/issues/612)
