// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.CodeAnalysis;

namespace NCode.Identity.Analyzers;

/// <summary>
/// Identifies the fields that a property cannot replace by their declaration alone.
/// </summary>
internal static class FieldExemptions
{
    public static bool IsExempt(IFieldSymbol field)
    {
        if (
            field.IsImplicitlyDeclared
            || field.AssociatedSymbol is not null
            || field.IsConst
            || field.RefKind != RefKind.None
            || field.IsFixedSizeBuffer
            || field.IsVolatile
            || field.ContainingType.TypeKind == TypeKind.Enum
        )
        {
            return true;
        }

        return field.ContainingType.TypeKind == TypeKind.Struct
            && HasLayoutContract(field.ContainingType);
    }

    private static bool HasLayoutContract(INamedTypeSymbol type)
    {
        foreach (var attribute in type.GetAttributes())
        {
            var name = attribute.AttributeClass?.ToDisplayString();
            if (
                name == "System.Runtime.InteropServices.StructLayoutAttribute"
                || name == "System.Runtime.CompilerServices.InlineArrayAttribute"
            )
            {
                return true;
            }
        }

        return false;
    }
}
