// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace NCode.Identity.Analyzers;

/// <summary>
/// Identifies the methods whose shape a framework contract fixes, so the family's own rule for async methods does not
/// apply to them.
/// </summary>
// See ADR-1496.
internal static class MethodExemptions
{
    private const string FactAttributeName = "Xunit.FactAttribute";
    private const string HttpContextName = "Microsoft.AspNetCore.Http.HttpContext";

    // The assemblies built from this repository. A member one of them declares is the family's own, so an implementer
    // has the family's shape; a member any other assembly declares is a framework contract.
    private static readonly string[] FamilyAssemblyNames =
    [
        "NCode.Identity",
        "NCode.AspNetCore",
        "NCode.Json",
        "NCode.Persistence",
        "NCode.Registration",
        "NCode.Time",
    ];

    /// <summary>
    /// Determines whether a framework contract fixes a method's shape.
    /// </summary>
    /// <param name="method">The method to examine.</param>
    /// <param name="compilationAssemblyName">The name of the assembly being compiled, which is always the family's.</param>
    /// <returns>
    /// <see langword="true"/> when the method is an entry point, the dispose pattern's <c>DisposeAsyncCore</c>, an xUnit
    /// test method, an ASP.NET Core middleware <c>Invoke</c> or <c>InvokeAsync</c> method, or overrides or implements a
    /// member that an assembly outside the family declares; otherwise <see langword="false"/>.
    /// </returns>
    public static bool IsFrameworkContract(IMethodSymbol method, string? compilationAssemblyName)
    {
        return IsEntryPoint(method)
            || IsDisposePattern(method)
            || IsTestMethod(method)
            || IsMiddlewareInvoke(method)
            || OverridesForeignMember(method, compilationAssemblyName)
            || ImplementsForeignMember(method, compilationAssemblyName);
    }

    private static bool IsEntryPoint(IMethodSymbol method) =>
        method.IsStatic
        && (method.Name == "Main" || method.Name == "<Main>$")
        && method.ContainingType.TypeKind != TypeKind.Interface;

    // The dispose pattern's protected virtual DisposeAsyncCore() is a convention of IAsyncDisposable, whose
    // DisposeAsync() takes no token.
    private static bool IsDisposePattern(IMethodSymbol method) =>
        method.Name == "DisposeAsyncCore" && method.Parameters.Length == 0;

    /// <summary>
    /// Determines whether a reference to a method hands it to an ASP.NET Core API: as the argument of a method or
    /// constructor that an ASP.NET Core assembly declares, such as <c>MapGet</c>, or as a value of a delegate type that
    /// one declares, such as <c>RequestDelegate</c>.
    /// </summary>
    /// <param name="reference">The reference to examine.</param>
    /// <returns><see langword="true"/> when an ASP.NET Core API takes the referenced method as a delegate.</returns>
    public static bool IsPassedToAspNetCore(IMethodReferenceOperation reference)
    {
        var current = reference.Parent;
        while (current is IConversionOperation or IDelegateCreationOperation)
        {
            if (IsAspNetCore(current.Type))
            {
                return true;
            }

            current = current.Parent;
        }

        return current is IArgumentOperation argument
            && argument.Parent switch
            {
                IInvocationOperation invocation => IsAspNetCore(invocation.TargetMethod),
                IObjectCreationOperation creation => IsAspNetCore(creation.Constructor),
                _ => false,
            };
    }

    private static bool IsAspNetCore(ISymbol? symbol)
    {
        var name = symbol?.ContainingNamespace?.ToDisplayString();
        return name is not null
            && (
                name == "Microsoft.AspNetCore"
                || name.StartsWith("Microsoft.AspNetCore.", StringComparison.Ordinal)
            );
    }

    private static bool IsTestMethod(IMethodSymbol method)
    {
        foreach (var attribute in method.GetAttributes())
        {
            for (var type = attribute.AttributeClass; type is not null; type = type.BaseType)
            {
                if (type.ToDisplayString() == FactAttributeName)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsMiddlewareInvoke(IMethodSymbol method) =>
        (method.Name == "Invoke" || method.Name == "InvokeAsync")
        && method.Parameters.Length > 0
        && method.Parameters[0].Type.ToDisplayString() == HttpContextName;

    private static bool OverridesForeignMember(
        IMethodSymbol method,
        string? compilationAssemblyName
    )
    {
        for (
            var overridden = method.OverriddenMethod;
            overridden is not null;
            overridden = overridden.OverriddenMethod
        )
        {
            if (!IsFamily(overridden.ContainingAssembly, compilationAssemblyName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ImplementsForeignMember(
        IMethodSymbol method,
        string? compilationAssemblyName
    )
    {
        if (method.ExplicitInterfaceImplementations.Length > 0)
        {
            foreach (var implemented in method.ExplicitInterfaceImplementations)
            {
                if (!IsFamily(implemented.ContainingAssembly, compilationAssemblyName))
                {
                    return true;
                }
            }

            return false;
        }

        if (method.IsStatic || method.ContainingType.TypeKind == TypeKind.Interface)
        {
            return false;
        }

        foreach (var contract in method.ContainingType.AllInterfaces)
        {
            if (IsFamily(contract.ContainingAssembly, compilationAssemblyName))
            {
                continue;
            }

            foreach (var member in contract.GetMembers(method.Name))
            {
                if (
                    member is IMethodSymbol contractMethod
                    && SymbolEqualityComparer.Default.Equals(
                        method.ContainingType.FindImplementationForInterfaceMember(contractMethod),
                        method
                    )
                )
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsFamily(IAssemblySymbol? assembly, string? compilationAssemblyName)
    {
        var name = assembly?.Identity.Name;
        if (name is null)
        {
            return false;
        }

        if (name == compilationAssemblyName)
        {
            return true;
        }

        foreach (var prefix in FamilyAssemblyNames)
        {
            if (name == prefix || name.StartsWith(prefix + ".", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
