using System;
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
            get => ErrorOrDefault != null;
        }

        public IErrorDetails Error
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ErrorOrDefault ?? throw new InvalidOperationException();
        }

        public IErrorDetails? ErrorOrDefault
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValidationResult(IErrorDetails error)
        {
            ErrorOrDefault = error;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValidationResult<TValue> AsError<TValue>()
        {
            return new(Error);
        }
    }

    public readonly struct ValidationResult<TValue>
    {
        private readonly ValidationResult _inner;
        private readonly TValue _value;

        public bool HasError
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ErrorOrDefault != null;
        }

        public IErrorDetails Error
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ErrorOrDefault ?? throw new InvalidOperationException();
        }

        public IErrorDetails? ErrorOrDefault
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _inner.ErrorOrDefault;
        }

        public TValue Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => HasError ? throw new InvalidOperationException() : _value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValidationResult(TValue value)
        {
            _inner = default;
            _value = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValidationResult(IErrorDetails error)
        {
            _inner = new ValidationResult(error);
            _value = default!;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValidationResult<T> AsError<T>()
        {
            return new(Error);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ValidationResult(ValidationResult<TValue> result) => result._inner;
    }
}
