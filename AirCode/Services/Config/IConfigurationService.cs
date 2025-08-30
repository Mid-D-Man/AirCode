namespace AirCode.Services.Config;


public interface IConfigurationService
{
    Task<string> GetCryptoKeyAsync();
    Task<string> GetCryptoIVAsync();
    Task<CryptographyConfig> GetCryptographyConfigAsync();
    Task PushConfigToJavaScriptAsync();
    bool IsProduction { get; }
}
