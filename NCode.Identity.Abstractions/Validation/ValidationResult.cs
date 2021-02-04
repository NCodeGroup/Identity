using System.Runtime.CompilerServices;

namespace NCode.Identity.Validation
{
    public readonly struct ValidationResult
    {
        public static ValidationResult SuccessResult => default;

        public static IValidationResultFactory Factory { get; } = ValidationResultFactory.Instance;

        public bool HasError
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ErrorDetails != null;
        }

        public IErrorDetails ErrorDetails
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
            return new(ErrorDetails);
        }
    }

    public readonly struct ValidationResult<TValue>
    {
        private readonly ValidationResult BaseResult;

        public bool HasError
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ErrorDetails != null;
        }

        public IErrorDetails ErrorDetails
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => BaseResult.ErrorDetails;
        }

        public TValue Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValidationResult(TValue value)
        {
            BaseResult = default;
            Value = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValidationResult(IErrorDetails error)
        {
            BaseResult = new ValidationResult(error);
            Value = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ValidationResult(ValidationResult<TValue> result) => result.BaseResult;
    }
}
