using AuthKit.Contracts;
using LocalizationKit.Services;
namespace AuthKitTest.Api.Auth.localization
{
    public class AuthLocalizer : IAuthMessageLocalizer
    {
        private readonly ILocalizationService _loc; 
        public AuthLocalizer(ILocalizationService loc) => _loc = loc;
        public string? Localize(string key, object[]? args)
        {
            var result= args?.Length >0 ? _loc.T(key,args) : _loc.T(key);
            return result == key ? null : result;
        }
    }
}
