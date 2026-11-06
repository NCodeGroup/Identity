---
description: "NCode.Identity PowerShell conventions — the build scripts under build/ and their fixture tests."
applyTo: "**/*.ps1"
---

# PowerShell scripts

The build scripts under `build/` are PowerShell 7 and run under `Set-StrictMode -Version Latest` with
`$ErrorActionPreference = 'Stop'`.

- **An `if`, `switch` or loop used as a value unrolls its output.** An empty array it yields becomes `$null`, and a
  single item becomes that item, so `$x.Count` throws under strict mode. Wrap the whole statement in `@(...)` when the
  value must be an array: `$projects = @(if ($all) { $every } else { $selected })`. Wrapping each branch is not enough.
- **A script checks its prerequisites after it knows it has work.** A run that selects nothing, such as
  `./build/e2e.ps1 -Affected` with no suite affected, reports its verdict without requiring Docker or a build.
- **A script's fixture test** lives under `build/tests/` as `<script>.tests.ps1`, and `build/dod.ps1` runs it as an
  early, build-free stage. A fixture is a temporary directory, and a git repository when the script reads git, that the
  test deletes in a `finally` block. Run a script whose `return` or `throw` the test asserts in a child
  `pwsh -NoProfile -File` process.
