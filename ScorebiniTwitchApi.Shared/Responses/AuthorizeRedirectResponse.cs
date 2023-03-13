namespace ScorebiniTwitchApi.Shared.Responses
{
    public record class AuthorizeRedirectResponse(
        ResponseCommonMetadata Meta,
        string RedirectUri
        );
}
