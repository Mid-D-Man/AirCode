using Microsoft.JSInterop;

namespace Aircode.Services;
// Services/Scanner/IScannerService.cs
public interface IScannerService
{
    Task<bool> InitializeCameraAsync();
    Task<string> ScanQrCodeAsync();
    Task StopScanningAsync();
}

// Services/Scanner/ScannerService.cs
public class ScannerService : IScannerService
{
    private readonly IJSRuntime _jsRuntime;
    private bool _isInitialized;
    private DotNetObjectReference<ScannerService> _dotNetRef;

    public ScannerService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
        _dotNetRef = DotNetObjectReference.Create(this);
    }

    public async Task<bool> InitializeCameraAsync()
    {
        if (!_isInitialized)
        {
            _isInitialized = await _jsRuntime.InvokeAsync<bool>("initializeCamera", _dotNetRef);
        }
        return _isInitialized;
    }

    public async Task<string> ScanQrCodeAsync()
    {
        return await _jsRuntime.InvokeAsync<string>("scanQrCode");
    }

    public async Task StopScanningAsync()
    {
        await _jsRuntime.InvokeVoidAsync("stopScanning");
        _isInitialized = false;
    }
}