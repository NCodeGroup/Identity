// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.CodeAnalysis;
using Xunit;

namespace NCode.Identity.Analyzers;

public class AsyncMethodShapeAnalyzerTests
{
    private const string TaskReturnId = "NCID0002";
    private const string CancellationTokenId = "NCID0003";

    private static async ValueTask AssertReportsAsync(
        string source,
        string[] expectedIds,
        CancellationToken cancellationToken
    )
    {
        var diagnostics = await AnalyzerHarness.AnalyzeAsync(
            source,
            new AsyncMethodShapeAnalyzer(),
            cancellationToken
        );

        Assert.All(
            diagnostics,
            diagnostic => Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity)
        );
        Assert.Equal(
            expectedIds.Order(StringComparer.Ordinal),
            diagnostics.Select(diagnostic => diagnostic.Id).Order(StringComparer.Ordinal)
        );
    }

    private static ValueTask AssertReportsNothingAsync(
        string source,
        CancellationToken cancellationToken
    ) => AssertReportsAsync(source, [], cancellationToken);

    #region Initialize Tests

    [Fact]
    public async Task Initialize_AsyncTaskMethodWithoutToken_ReportsBoth()
    {
        await AssertReportsAsync(
            "class C { public async System.Threading.Tasks.Task RunAsync() { await System.Threading.Tasks.Task.Yield(); } }",
            [TaskReturnId, CancellationTokenId],
            CancellationToken.None
        );
    }

    [Fact]
    public async Task Initialize_TaskOfTMethodWithToken_ReportsTaskReturn()
    {
        await AssertReportsAsync(
            "class C { public System.Threading.Tasks.Task<int> GetAsync(System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.Task.FromResult(1); }",
            [TaskReturnId],
            CancellationToken.None
        );
    }

    [Fact]
    public async Task Initialize_ValueTaskMethodWithoutToken_ReportsToken()
    {
        await AssertReportsAsync(
            "class C { public System.Threading.Tasks.ValueTask RunAsync() => default; }",
            [CancellationTokenId],
            CancellationToken.None
        );
    }

    [Fact]
    public async Task Initialize_TokenNotLast_ReportsToken()
    {
        await AssertReportsAsync(
            "class C { public System.Threading.Tasks.ValueTask RunAsync(System.Threading.CancellationToken cancellationToken, int count) => default; }",
            [CancellationTokenId],
            CancellationToken.None
        );
    }

    [Fact]
    public async Task Initialize_InterfaceMemberAndImplementation_ReportsBoth()
    {
        await AssertReportsAsync(
            """
            interface I { System.Threading.Tasks.Task RunAsync(); }
            class C : I { public System.Threading.Tasks.Task RunAsync() => System.Threading.Tasks.Task.CompletedTask; }
            """,
            [TaskReturnId, TaskReturnId, CancellationTokenId, CancellationTokenId],
            CancellationToken.None
        );
    }

    [Fact]
    public async Task Initialize_MethodGroupPassedToFamilyMethod_ReportsMethod()
    {
        await AssertReportsAsync(
            """
            class C
            {
                static void Run(System.Func<System.Threading.Tasks.Task> f) { }
                static System.Threading.Tasks.Task Work() => System.Threading.Tasks.Task.CompletedTask;
                static void Start() => Run(C.Work);
            }
            """,
            [TaskReturnId, CancellationTokenId],
            CancellationToken.None
        );
    }

    [Fact]
    public async Task Initialize_ValueTaskOfTMethodWithTrailingToken_ReportsNothing()
    {
        await AssertReportsNothingAsync(
            "class C { public System.Threading.Tasks.ValueTask<int> GetAsync(System.Threading.CancellationToken cancellationToken) => new(1); }",
            CancellationToken.None
        );
    }

    [Fact]
    public async Task Initialize_SynchronousMethod_ReportsNothing()
    {
        await AssertReportsNothingAsync(
            "class C { public int Run() => 1; }",
            CancellationToken.None
        );
    }

    [Fact]
    public async Task Initialize_MainReturningTask_ReportsNothing()
    {
        await AssertReportsNothingAsync(
            "class C { public static System.Threading.Tasks.Task Main() => System.Threading.Tasks.Task.CompletedTask; }",
            CancellationToken.None
        );
    }

    [Fact]
    public async Task Initialize_AsyncMain_ReportsNothing()
    {
        await AssertReportsNothingAsync(
            "static class P { static async System.Threading.Tasks.Task Main() { } }",
            CancellationToken.None
        );
    }

    [Fact]
    public async Task Initialize_ImplicitDisposeAsync_ReportsNothing()
    {
        await AssertReportsNothingAsync(
            "class C : System.IAsyncDisposable { public System.Threading.Tasks.ValueTask DisposeAsync() => default; }",
            CancellationToken.None
        );
    }

    [Fact]
    public async Task Initialize_ExplicitDisposeAsync_ReportsNothing()
    {
        await AssertReportsNothingAsync(
            "class C : System.IAsyncDisposable { System.Threading.Tasks.ValueTask System.IAsyncDisposable.DisposeAsync() => default; }",
            CancellationToken.None
        );
    }

    [Fact]
    public async Task Initialize_OverrideOfFrameworkMember_ReportsNothing()
    {
        await AssertReportsNothingAsync(
            "class C : System.Net.Http.HttpMessageHandler { protected override System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken cancellationToken) => throw null; }",
            CancellationToken.None
        );
    }

    [Fact]
    public async Task Initialize_DisposeAsyncCore_ReportsNothing()
    {
        await AssertReportsNothingAsync(
            "class C { protected virtual System.Threading.Tasks.ValueTask DisposeAsyncCore() => default; }",
            CancellationToken.None
        );
    }

    [Fact]
    public async Task Initialize_FactTestMethod_ReportsNothing()
    {
        await AssertReportsNothingAsync(
            """
            namespace Xunit { public class FactAttribute : System.Attribute { } }
            class C { [Xunit.Fact] public async System.Threading.Tasks.Task T() { } }
            """,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task Initialize_DerivedFactTestMethod_ReportsNothing()
    {
        await AssertReportsNothingAsync(
            """
            namespace Xunit { public class FactAttribute : System.Attribute { } }
            public sealed class BuiltFactAttribute : Xunit.FactAttribute { }
            class C { [BuiltFact] public async System.Threading.Tasks.Task T() { } }
            """,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task Initialize_MiddlewareInvokeAsync_ReportsNothing()
    {
        await AssertReportsNothingAsync(
            """
            namespace Microsoft.AspNetCore.Http { public class HttpContext { } }
            class C { public System.Threading.Tasks.Task InvokeAsync(Microsoft.AspNetCore.Http.HttpContext context) => System.Threading.Tasks.Task.CompletedTask; }
            """,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task Initialize_MethodGroupPassedToAspNetCoreMethod_ReportsNothing()
    {
        await AssertReportsNothingAsync(
            """
            using Microsoft.AspNetCore.Builder;
            namespace Microsoft.AspNetCore.Http { public class HttpContext { } }
            namespace Microsoft.AspNetCore.Builder
            {
                public static class RouteExtensions
                {
                    public static void MapGet(this object routes, string pattern, System.Delegate handler) { }
                }
            }
            class C
            {
                static System.Threading.Tasks.Task Handle(Microsoft.AspNetCore.Http.HttpContext c) => System.Threading.Tasks.Task.CompletedTask;
                static void Map() => new object().MapGet("/", C.Handle);
            }
            """,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task Initialize_MethodGroupAssignedToAspNetCoreDelegate_ReportsNothing()
    {
        await AssertReportsNothingAsync(
            """
            namespace Microsoft.AspNetCore.Http
            {
                public class HttpContext { }
                public delegate System.Threading.Tasks.Task RequestDelegate(HttpContext context);
            }
            class C
            {
                static System.Threading.Tasks.Task Handle(Microsoft.AspNetCore.Http.HttpContext c) => System.Threading.Tasks.Task.CompletedTask;
                static Microsoft.AspNetCore.Http.RequestDelegate Create() => C.Handle;
            }
            """,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task Initialize_Constructor_ReportsNothing()
    {
        await AssertReportsNothingAsync(
            "class C { public C(System.Threading.Tasks.Task work) { } }",
            CancellationToken.None
        );
    }

    [Fact]
    public async Task Initialize_PropertyAccessor_ReportsNothing()
    {
        await AssertReportsNothingAsync(
            "class C { public System.Threading.Tasks.Task P { get; } = System.Threading.Tasks.Task.CompletedTask; }",
            CancellationToken.None
        );
    }

    [Fact]
    public async Task Initialize_AsyncLambda_ReportsNothing()
    {
        await AssertReportsNothingAsync(
            "class C { public int Run() { System.Func<System.Threading.Tasks.Task> f = async () => { await System.Threading.Tasks.Task.Yield(); }; return 1; } }",
            CancellationToken.None
        );
    }

    #endregion
}
