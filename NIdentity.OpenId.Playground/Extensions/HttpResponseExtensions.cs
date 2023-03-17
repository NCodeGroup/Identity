namespace NIdentity.OpenId.Playground.Extensions;

internal static class HttpResponseExtensions
{
    public static void SetNoCache(this HttpResponse response)
    {
        if (response == null)
            throw new ArgumentNullException(nameof(response));

        response.Headers["Pragma"] = "no-cache";
        response.Headers["Cache-Control"] = "no-store, no-cache, max-age=0";
    }
}
