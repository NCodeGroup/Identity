using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NIdentity.OpenId.Validation
{
    public readonly struct ValidationResult
    {
        public static ValidationResult SuccessResult => default;

        public static IValidationResultFactory Factory { get; } = new ValidationResultFactory();

        public bool HasError
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ErrorDetails != null;
        }

        public IErrorDetails? ErrorDetails
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValidationResult(IErrorDetails errorDetails)
        {
            ErrorDetails = errorDetails;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValidationResult<TValue> As<TValue>()
        {
            Debug.Assert(ErrorDetails != null);
            return new ValidationResult<TValue>(ErrorDetails);
        }
    }

    public readonly struct ValidationResult<TValue>
    {
        private readonly ValidationResult _inner;

        public bool HasError
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ErrorDetails != null;
        }

        public IErrorDetails? ErrorDetails
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _inner.ErrorDetails;
        }

        public TValue? Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValidationResult(TValue value)
        {
            _inner = default;
            Value = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValidationResult(IErrorDetails errorDetails)
        {
            _inner = new ValidationResult(errorDetails);
            Value = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ValidationResult(ValidationResult<TValue> result) => result._inner;
    }
}
