using System.Runtime.CompilerServices;

namespace NCode.Identity.Validation
{
    public interface IValidationResultFactory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ValidationResult Error(IErrorDetails error);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ValidationResult<T> Success<T>(T value);
    }

    public class ValidationResultFactory : IValidationResultFactory
    {
        public static ValidationResultFactory Instance { get; } = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValidationResult Error(IErrorDetails error) => new(error);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValidationResult<T> Success<T>(T value) => new(value);
    }
}
