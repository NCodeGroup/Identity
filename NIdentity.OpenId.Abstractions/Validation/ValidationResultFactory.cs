using System.Runtime.CompilerServices;

namespace NIdentity.OpenId.Validation
{
    public interface IValidationResultFactory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ValidationResult Error(IErrorDetails error);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ValidationResult<TValue> Error<TValue>(IErrorDetails error);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ValidationResult<TValue> Success<TValue>(TValue value);
    }

    public class ValidationResultFactory : IValidationResultFactory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValidationResult Error(IErrorDetails error) => new(error);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValidationResult<TValue> Error<TValue>(IErrorDetails error) => new(error);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValidationResult<TValue> Success<TValue>(TValue value) => new(value);
    }
}
