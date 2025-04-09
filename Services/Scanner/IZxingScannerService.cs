using Microsoft.JSInterop;

namespace AirCode.Services.Scanner;
// Services/Scanner/IScannerService.cs
public interface IZxingScannerService
{
    Task<bool> InitializeCameraAsync();
    Task<string> ScanQrCodeAsync();
    Task StopScanningAsync();
}

// Services/Scanner/ScannerService.cs
public class ZxingScannerService : IZxingScannerService
{
    private readonly IJSRuntime _jsRuntime;
    private bool _isInitialized;
    private DotNetObjectReference<ZxingScannerService> _dotNetRef;

    public ZxingScannerService(IJSRuntime jsRuntime)
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