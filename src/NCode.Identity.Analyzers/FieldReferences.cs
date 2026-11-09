// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace NCode.Identity.Analyzers;

/// <summary>
/// Identifies the uses of a field that need the field itself rather than a property.
/// </summary>
internal static class FieldReferences
{
    /// <summary>
    /// Determines whether a reference to a field needs the field itself rather than a property.
    /// </summary>
    /// <param name="reference">The reference to the field.</param>
    /// <returns>
    /// <see langword="true"/> when the reference is an argument to a <c>ref</c>, <c>out</c>, <c>in</c> or
    /// <c>ref readonly</c> parameter, the operand of <c>ref</c> or <c>&amp;</c>, or a mutable struct that a
    /// non-<c>readonly</c> call, a write or a by-reference use of one of its members changes through the field;
    /// otherwise <see langword="false"/>.
    /// </returns>
    public static bool IsByReference(IFieldReferenceOperation reference)
    {
        if (
            reference.Parent is IArgumentOperation
            {
                Parameter.RefKind: RefKind.Ref
                    or RefKind.Out
                    or RefKind.In
                    or RefKind.RefReadOnlyParameter
            }
        )
        {
            return true;
        }

        if (reference.Parent is IAddressOfOperation)
        {
            return true;
        }

        if (reference.Syntax.Parent is RefExpressionSyntax)
        {
            return true;
        }

        return MutatesInPlace(reference);
    }

    private static bool MutatesInPlace(IFieldReferenceOperation reference)
    {
        if (!reference.Field.Type.IsValueType || reference.Field.Type.IsReadOnly)
        {
            return false;
        }

        return reference.Parent switch
        {
            IInvocationOperation invocation => invocation.Instance == reference
                && !invocation.TargetMethod.IsReadOnly,
            IPropertyReferenceOperation property => property.Instance == reference
                && IsWritten(property),
            IFieldReferenceOperation field => field.Instance == reference
                && (IsWritten(field) || IsByReference(field)),
            _ => false,
        };
    }

    private static bool IsWritten(IOperation member)
    {
        return member.Parent switch
        {
            IAssignmentOperation assignment => assignment.Target == member,
            IIncrementOrDecrementOperation => true,
            _ => false,
        };
    }
}
