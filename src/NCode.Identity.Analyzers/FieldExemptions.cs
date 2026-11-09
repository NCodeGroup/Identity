// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.CodeAnalysis;

namespace NCode.Identity.Analyzers;

/// <summary>
/// Identifies the fields that a property cannot replace by their declaration alone.
/// </summary>
internal static class FieldExemptions
{
    /// <summary>
    /// Determines whether a field's declaration alone lets it stay a field.
    /// </summary>
    /// <param name="field">The field to examine.</param>
    /// <returns>
    /// <see langword="true"/> when the field is compiler-declared, a <c>const</c>, an enum member, a <c>ref</c>,
    /// fixed-size buffer or <c>volatile</c> field, or a field of a struct with <c>[StructLayout]</c> or
    /// <c>[InlineArray]</c>; otherwise <see langword="false"/>.
    /// </returns>
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
