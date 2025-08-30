namespace AirCode.Utilities.DataStructures;
/// <summary>
    /// Enum for QR code gradient direction
    /// </summary>
    public enum GradientDirection
    {
        LinearX,
        LinearY,
        Diagonal,
        Radial
    }

    /// <summary>
    /// Enum for QR code error correction level
    /// </summary>
    public enum ErrorCorrectionLevel
    {
        L, // Low - 7% of codewords can be restored
        M, // Medium - 15% of codewords can be restored
        Q, // Quartile - 25% of codewords can be restored
        H  // High - 30% of codewords can be restored
    }

    /// <summary>
    /// QR code theme options
    /// </summary>
    public enum QRCodeTheme
    {
        Standard,
        Gradient,
        Branded,
        GradientWithLogo // Combined option for both gradient and logo
    }

    /// <summary>
    /// Record for QR code base options
    /// </summary>
    public record QRCodeBaseOptions
    {
        /// <summary>
        /// QR code content
        /// </summary>
        public string Content { get; init; } = string.Empty;
        
        /// <summary>
        /// QR code size in pixels
        /// </summary>
        public int Size { get; init; } = 300;
        
        /// <summary>
        /// Dark color for QR code modules
        /// </summary>
        public string DarkColor { get; init; } = "#000000";
        
        /// <summary>
        /// Light color for QR code background
        /// </summary>
        public string LightColor { get; init; } = "#FFFFFF";
        
        /// <summary>
        /// Error correction level for QR code
        /// </summary>
        public ErrorCorrectionLevel ErrorLevel { get; init; } = ErrorCorrectionLevel.M;
        
        /// <summary>
        /// Margin around QR code in modules
        /// </summary>
        public int Margin { get; init; } = 4;
    }

    /// <summary>
    /// Record for QR code gradient options
    /// </summary>
    public record QRCodeGradientOptions : QRCodeBaseOptions
    {
        /// <summary>
        /// Gradient start color
        /// </summary>
        public string GradientColor1 { get; init; } = "#007bff";
        
        /// <summary>
        /// Gradient end color
        /// </summary>
        public string GradientColor2 { get; init; } = "#00bfff";
        
        /// <summary>
        /// Gradient direction
        /// </summary>
        public GradientDirection Direction { get; init; } = GradientDirection.Radial;
    }

    /// <summary>
    /// Record for QR code with logo options
    /// </summary>
    public record QRCodeBrandedOptions : QRCodeBaseOptions
    {
        /// <summary>
        /// Path to logo image
        /// </summary>
        public string LogoUrl { get; init; } = string.Empty;
        
        /// <summary>
        /// Logo size as percentage of QR code size (between 0 and 1)
        /// </summary>
        public float LogoSizeRatio { get; init; } = 0.25f;
        
        /// <summary>
        /// Add border around logo (default: true)
        /// </summary>
        public bool AddLogoBorder { get; init; } = true;
        
        /// <summary>
        /// Logo border color
        /// </summary>
        public string LogoBorderColor { get; init; } = "#FFFFFF";
        
        /// <summary>
        /// Logo border width in pixels
        /// </summary>
        public int LogoBorderWidth { get; init; } = 2;
        
        /// <summary>
        /// Logo border radius in pixels (for rounded corners)
        /// </summary>
        public int LogoBorderRadius { get; init; } = 5;
    }
    
    /// <summary>
    /// Record for QR code with both gradient and logo
    /// </summary>
    public record QRCodeGradientBrandedOptions : QRCodeGradientOptions
    {
        /// <summary>
        /// Path to logo image
        /// </summary>
        public string LogoUrl { get; init; } = string.Empty;
        
        /// <summary>
        /// Logo size as percentage of QR code size (between 0 and 1)
        /// </summary>
        public float LogoSizeRatio { get; init; } = 0.25f;
        
        /// <summary>
        /// Add border around logo (default: true)
        /// </summary>
        public bool AddLogoBorder { get; init; } = true;
        
        /// <summary>
        /// Logo border color
        /// </summary>
        public string LogoBorderColor { get; init; } = "#FFFFFF";
        
        /// <summary>
        /// Logo border width in pixels
        /// </summary>
        public int LogoBorderWidth { get; init; } = 2;
        
        /// <summary>
        /// Logo border radius in pixels (for rounded corners)
        /// </summary>
        public int LogoBorderRadius { get; init; } = 5;
    }

    /// <summary>
    /// Class for transferring QR code data across components
    /// </summary>
    public class QRCodeData
    {
        /// <summary>
        /// Unique identifier for the QR code
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        
        /// <summary>
        /// Content encoded in the QR code
        /// </summary>
        public string Content { get; set; } = string.Empty;
        
        /// <summary>
        /// SVG string representation of the QR code
        /// </summary>
        public string SvgContent { get; set; } = string.Empty;
        
        /// <summary>
        /// Creation timestamp
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Duration for which the QR code is valid (in minutes)
        /// </summary>
        public int ValidDuration { get; set; } = 30;
        
        /// <summary>
        /// Expiration timestamp
        /// </summary>
        public DateTime ExpiresAt => CreatedAt.AddMinutes(ValidDuration);
        
        /// <summary>
        /// Indicates if the QR code is still valid
        /// </summary>
        public bool IsValid => DateTime.UtcNow < ExpiresAt;
        
        /// <summary>
        /// Theme used for this QR code
        /// </summary>
        public QRCodeTheme Theme { get; set; } = QRCodeTheme.Standard;
    }
    