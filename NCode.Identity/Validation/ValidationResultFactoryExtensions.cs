using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Contracts;

namespace NCode.Identity.Validation
{
    public static class ValidationResultFactoryExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValidationResult Error(this IValidationResultFactory factory, string errorCode, string errorDescription, int? statusCode)
            => factory.Error(new ErrorDetails { StatusCode = statusCode, ErrorCode = errorCode, ErrorDescription = errorDescription });

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValidationResult BadRequest(this IValidationResultFactory factory, string errorCode, string errorDescription)
            => factory.Error(errorCode, errorDescription, StatusCodes.Status400BadRequest);

        //

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValidationResult<TValue> Error<TValue>(this IValidationResultFactory factory, string errorCode, string errorDescription, int? statusCode)
            => factory.Error<TValue>(new ErrorDetails { StatusCode = statusCode, ErrorCode = errorCode, ErrorDescription = errorDescription });

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValidationResult<TValue> BadRequest<TValue>(this IValidationResultFactory factory, string errorCode, string errorDescription)
            => factory.Error<TValue>(errorCode, errorDescription, StatusCodes.Status400BadRequest);

        //

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValidationResult<TValue> FailedToDecodeJwt<TValue>(this IValidationResultFactory factory, string errorCode)
            => factory.BadRequest<TValue>(errorCode, "An error occurred while attempting to decode the JWT value.");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValidationResult<TValue> FailedToDeserializeJson<TValue>(this IValidationResultFactory factory, string errorCode)
            => factory.BadRequest<TValue>(errorCode, "An error occurred while attempting to deserialize the JSON value.");

        //

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValidationResult<TValue> InvalidRequest<TValue>(this IValidationResultFactory factory, string errorDescription, string errorCode = IdentityConstants.ErrorCodes.InvalidRequest)
            => factory.BadRequest<TValue>(errorCode, errorDescription);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValidationResult<TValue> MissingParameter<TValue>(this IValidationResultFactory factory, string key, string errorCode = IdentityConstants.ErrorCodes.InvalidRequest)
            => factory.BadRequest<TValue>(errorCode, $"The request is missing the {key} parameter.");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValidationResult<TValue> TooManyParameterValues<TValue>(this IValidationResultFactory factory, string key)
            => factory.InvalidRequest<TValue>($"The request includes the {key} parameter more than once.");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValidationResult<TValue> InvalidParameterValue<TValue>(this IValidationResultFactory factory, string key, string errorCode = IdentityConstants.ErrorCodes.InvalidRequest)
            => factory.BadRequest<TValue>(errorCode, $"The request includes an invalid value for the {key} parameter.");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValidationResult<TValue> UnauthorizedClient<TValue>(this IValidationResultFactory factory)
            => factory.BadRequest<TValue>(IdentityConstants.ErrorCodes.UnauthorizedClient, "The client is not allowed to request an access token using this authentication type.");
    }
}
