namespace NCode.Identity.Flows.Authorization
{
	public enum AuthorizationStage
	{
		PreInit,
		Init,
		PostInit,

        PreValidate,

        PreValidateRequestValues,
		ValidateRequestValues,
        PostValidateRequestValues,

        PreValidateRequestObject,
		ValidateRequestObject,
        PostValidateRequestObject,

        ValidateClient,
        // https://github.com/IdentityServer/IdentityServer4/blob/main/src/IdentityServer4/src/Validation/Default/AuthorizeRequestValidator.cs#L297

        PostValidate
    }
}
