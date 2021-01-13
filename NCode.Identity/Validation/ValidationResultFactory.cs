using System.Runtime.CompilerServices;

namespace NCode.Identity.Validation
{
    public interface IValidationResultFactory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ValidationResult Error(IErrorDetails errorDetails);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ValidationResult<TValue> Success<TValue>(TValue value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ValidationResult<TValue> Error<TValue>(IErrorDetails errorDetails);
    }

    public class ValidationResultFactory : IValidationResultFactory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValidationResult Error(IErrorDetails errorDetails) => new(errorDetails);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValidationResult<TValue> Success<TValue>(TValue value) => new(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValidationResult<TValue> Error<TValue>(IErrorDetails errorDetails) => new(errorDetails);
    }
}
