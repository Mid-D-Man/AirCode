using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace AirCode.Components.SharedPrefabs.Scanner;

public class MID_Nmiq_QrCode_ScannerBase : ComponentBase, IAsyncDisposable
{
    [Parameter] public string Width { get; set; } = "100%";
    [Parameter] public string Height { get; set; } = "400px";
    [Parameter] public EventCallback<string> OnQrCodeDetected { get; set; }
    [Parameter] public string VideoId { get; set; } = Guid.NewGuid().ToString("N");
    
    [Inject] public IJSRuntime JSRuntime { get; set; } = null!;
    
    private DotNetObjectReference<MID_Nmiq_QrCode_ScannerBase>? objRef;
    private bool isScanning = false;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            objRef = DotNetObjectReference.Create(this);
        }
    }

    [JSInvokable]
    public async Task OnQrCodeScanned(string qrCodeData)
    {
        if (!string.IsNullOrEmpty(qrCodeData) && OnQrCodeDetected.HasDelegate)
        {
            await OnQrCodeDetected.InvokeAsync(qrCodeData);
        }
    }

    public async Task StartScanningAsync()
    {
        if (objRef != null && !isScanning)
        {
            isScanning = true;
            await JSRuntime.InvokeVoidAsync("qrScanHelper.startScanWithId", VideoId, objRef);
        }
    }

    public async Task StopScanningAsync()
    {
        if (isScanning)
        {
            isScanning = false;
            await JSRuntime.InvokeVoidAsync("qrScanHelper.stopScanWithId", VideoId);
        }
    }

    public async Task SwitchCameraAsync()
    {
        if (isScanning)
        {
            await JSRuntime.InvokeVoidAsync("qrScanHelper.switchCameraWithId", VideoId);
        }
    }

    public bool IsScanning => isScanning;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopScanningAsync();
            objRef?.Dispose();
        }
        catch
        {
            // Suppress disposal exceptions
        }
    }
}