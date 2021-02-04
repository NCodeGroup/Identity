using System.Runtime.CompilerServices;

namespace NCode.Identity.Validation
{
    public static class ValidationResultFactoryExtensions
    {
        private const int Status400BadRequest = 400;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValidationResult Error(this IValidationResultFactory factory, string errorCode, string errorDescription, int statusCode) =>
            factory.Error(new ErrorDetails { StatusCode = statusCode, ErrorCode = errorCode, ErrorDescription = errorDescription });

        //

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValidationResult BadRequest(this IValidationResultFactory factory, string errorCode, string errorDescription) =>
            factory.Error(errorCode, errorDescription, Status400BadRequest);

        //

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValidationResult FailedToDecodeJwt(this IValidationResultFactory factory, string errorCode) =>
            factory.BadRequest(errorCode, "An error occurred while attempting to decode the JWT value.");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValidationResult FailedToDeserializeJson(this IValidationResultFactory factory, string errorCode) =>
            factory.BadRequest(errorCode, "An error occurred while attempting to deserialize the JSON value.");

        //

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValidationResult InvalidRequest(this IValidationResultFactory factory, string errorDescription, string errorCode = IdentityConstants.ErrorCodes.InvalidRequest) =>
            factory.BadRequest(errorCode, errorDescription);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValidationResult MissingParameter(this IValidationResultFactory factory, string key, string errorCode = IdentityConstants.ErrorCodes.InvalidRequest) =>
            factory.BadRequest(errorCode, $"The request is missing the {key} parameter.");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValidationResult TooManyParameterValues(this IValidationResultFactory factory, string key) =>
            factory.InvalidRequest($"The request includes the {key} parameter more than once.");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValidationResult InvalidParameterValue(this IValidationResultFactory factory, string key, string errorCode = IdentityConstants.ErrorCodes.InvalidRequest) =>
            factory.BadRequest(errorCode, $"The request includes an invalid value for the {key} parameter.");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValidationResult UnauthorizedClient(this IValidationResultFactory factory) =>
            factory.BadRequest(IdentityConstants.ErrorCodes.UnauthorizedClient, "The client is not allowed to request an access token using this authentication type.");
    }
}
