using Microsoft.Extensions.Options;

namespace ScorebiniTwitchApi
{
    public class TwitchOptionsValidation : IValidateOptions<TwitchOptions>
    {
        public ValidateOptionsResult Validate(string? name, TwitchOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.AppClientId))
            {
                return ValidateOptionsResult.Fail("Empty AppClientId");
            }
            if (string.IsNullOrWhiteSpace(options.AppClientSecret))
            {
                return ValidateOptionsResult.Fail("Empty AppClientSecret");
            }
            if (options.AppClientId == "<insert_client_id>")
            {
                return ValidateOptionsResult.Fail("Configure the AppClientId");
            }
            if (options.AppClientSecret == "<insert_client_secret>")
            {
                return ValidateOptionsResult.Fail("Configure the AppClientSecret");
            }
            return ValidateOptionsResult.Success;
        }
    }
}
