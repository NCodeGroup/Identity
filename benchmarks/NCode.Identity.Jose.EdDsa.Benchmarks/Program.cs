// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Running;

namespace NCode.Identity.Jose.EdDsa.Benchmarks;

internal static class Program
{
    private static int Main(string[] args)
    {
        var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

        var failed = summaries.Any(summary =>
            summary.HasCriticalValidationErrors || summary.Reports.Any(report => !report.Success)
        );

        return failed ? 1 : 0;
    }
}
