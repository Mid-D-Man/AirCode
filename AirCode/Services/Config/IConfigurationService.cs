namespace AirCode.Services.Config;

public interface IConfigurationService
{
    Task<string> GetCryptoKeyAsync();
    Task<string> GetCryptoIVAsync();
    Task<CryptographyConfig> GetCryptographyConfigAsync();
    Task PushConfigToJavaScriptAsync();
    bool IsProduction { get; }
    
    // AirCode specific configuration methods
    Task<string> GetAirCodeKeyAsync();
    Task<string> GetAirCodeIVAsync();
    Task<AirCodeConfig> GetAirCodeConfigAsync();
    Task<bool> ValidateAirCodeKeysAsync();
}

/// <summary>
/// Configuration model for AirCode specific settings
/// </summary>
public class AirCodeConfig
{
    public string DefaultAirCode_KEY { get; set; } = string.Empty;
    public string DefaultAirCode_IV { get; set; } = string.Empty;
    public QRCodeEncryptionConfig QRCodeEncryption { get; set; } = new();
}

/// <summary>
/// QR Code encryption specific configuration
/// </summary>
public class QRCodeEncryptionConfig
{
    public string Algorithm { get; set; } = "AES-256-CBC";
    public int KeyLength { get; set; } = 32;
    public int IVLength { get; set; } = 16;
    public bool UseConfiguredKeys { get; set; } = true;
    public bool AllowInsecureKeys { get; set; } = false;
}