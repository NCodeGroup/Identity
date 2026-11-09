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
