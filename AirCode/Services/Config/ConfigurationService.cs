namespace AirCode.Services.Config;
// Services/ConfigurationService.cs
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;
using System.Text.Json;

    public class ConfigurationService : IConfigurationService
    {
        private readonly IConfiguration _configuration;
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<ConfigurationService> _logger;
        private CryptographyConfig? _cryptoConfig;

        public ConfigurationService(
            IConfiguration configuration, 
            IJSRuntime jsRuntime,
            ILogger<ConfigurationService> logger)
        {
            _configuration = configuration;
            _jsRuntime = jsRuntime;
            _logger = logger;
        }

        public bool IsProduction => 
            _configuration.GetValue<bool>("Cryptography:IsProduction", false) ||
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production";

        public async Task<string> GetCryptoKeyAsync()
        {
            var config = await GetCryptographyConfigAsync();
            
            if (IsProduction && config.AllowInsecureKeys == false)
            {
                // In production, you might want to get this from a secure source
                // like Azure Key Vault, AWS Secrets Manager, etc.
                throw new InvalidOperationException("Secure key retrieval not implemented for production");
            }
            
            return config.DefaultKey;
        }

        public async Task<string> GetCryptoIVAsync()
        {
            var config = await GetCryptographyConfigAsync();
            return config.DefaultIV;
        }

        public async Task<CryptographyConfig> GetCryptographyConfigAsync()
        {
            if (_cryptoConfig == null)
            {
                _cryptoConfig = new CryptographyConfig();
                _configuration.GetSection("Cryptography").Bind(_cryptoConfig);

                // Validate configuration
                await ValidateConfigurationAsync(_cryptoConfig);
            }

            return _cryptoConfig;
        }

        public async Task PushConfigToJavaScriptAsync()
        {
            try
            {
                var config = await GetCryptographyConfigAsync();
                
                // Only push non-sensitive configuration to JavaScript
                var jsConfig = new
                {
                    Algorithm = config.Algorithm,
                    KeyLength = config.KeyLength,
                    IVLength = config.IVLength,
                    IsProduction = IsProduction,
                    AllowInsecureKeys = config.AllowInsecureKeys,
                    // Only include keys in development
                    DefaultKey = IsProduction ? null : config.DefaultKey,
                    DefaultIV = IsProduction ? null : config.DefaultIV
                };

                await _jsRuntime.InvokeVoidAsync("window.setAirCodeConfig", jsConfig);
                
                _logger.LogInformation("Configuration pushed to JavaScript successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to push configuration to JavaScript");
                throw;
            }
        }

        private async Task ValidateConfigurationAsync(CryptographyConfig config)
        {
            var errors = new List<string>();

            // Validate key
            if (string.IsNullOrEmpty(config.DefaultKey))
            {
                errors.Add("DefaultKey is missing");
            }
            else
            {
                try
                {
                    var keyBytes = Convert.FromBase64String(config.DefaultKey);
                    if (keyBytes.Length != config.KeyLength)
                    {
                        errors.Add($"DefaultKey must be {config.KeyLength} bytes, but is {keyBytes.Length} bytes");
                    }
                }
                catch (FormatException)
                {
                    errors.Add("DefaultKey is not valid Base64");
                }
            }

            // Validate IV
            if (string.IsNullOrEmpty(config.DefaultIV))
            {
                errors.Add("DefaultIV is missing");
            }
            else
            {
                try
                {
                    var ivBytes = Convert.FromBase64String(config.DefaultIV);
                    if (ivBytes.Length != config.IVLength)
                    {
                        errors.Add($"DefaultIV must be {config.IVLength} bytes, but is {ivBytes.Length} bytes");
                    }
                }
                catch (FormatException)
                {
                    errors.Add("DefaultIV is not valid Base64");
                }
            }

            // Production checks
            if (IsProduction)
            {
                if (config.AllowInsecureKeys)
                {
                    errors.Add("AllowInsecureKeys cannot be true in production");
                }

                // Add more production security checks here
                _logger.LogWarning("Production mode detected - ensure secure key management is implemented");
            }

            if (errors.Any())
            {
                var errorMessage = "Configuration validation failed: " + string.Join(", ", errors);
                _logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            _logger.LogInformation("Cryptography configuration validated successfully");
        }
    }

    public class CryptographyConfig
    {
        public string DefaultKey { get; set; } = string.Empty;
        public string DefaultIV { get; set; } = string.Empty;
        public string Algorithm { get; set; } = "AES-256-CBC";
        public int KeyLength { get; set; } = 32;
        public int IVLength { get; set; } = 16;
        public bool IsProduction { get; set; } = false;
        public bool AllowInsecureKeys { get; set; } = true;
    }

