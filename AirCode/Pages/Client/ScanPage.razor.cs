using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using AirCode.Components.SharedPrefabs.Scanner;
using AirCode.Models.Supabase;
using AirCode.Services.Auth;
using AirCode.Services.Courses;
using AirCode.Services.SupaBase;
using AirCode.Utilities.HelperScripts;

namespace AirCode.Pages.Client;

public partial class ScanPage : ComponentBase
{
    [Inject] public ISupabaseEdgeFunctionService EdgeService { get; set; } = null!;
    [Inject] public QRCodeDecoder QRDecoder { get; set; } = null!;
    [Inject] public IAuthService AuthService { get; set; } = null!;
    [Inject] public ICourseService CourseService { get; set; } = null!;

    private MID_Nmiq_QrCode_Scanner? qrScanner;
    private bool isScanning = false;
    private bool isProcessing = false;
    private string statusMessage = string.Empty;
    private bool isSuccess = false;
    private string currentUserMatricNumber = "SukaBlak";
    private string deviceGUID = "N";
    
    protected override async Task OnInitializedAsync()
    {
        try
        {
            currentUserMatricNumber = await AuthService.GetMatricNumberAsync();
            deviceGUID = await AuthService.GetDeviceIdAsync();
            
            MID_HelperFunctions.DebugMessage("QR Scanner initialized successfully", DebugClass.Info);
        }
        catch (Exception ex)
        {
            statusMessage = "Failed to initialize scanner";
            isSuccess = false;
            MID_HelperFunctions.DebugMessage($"Failed to initialize scanner: {ex.Message}", DebugClass.Exception);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Scanner OFF by default - wait for user action
        }
    }

    private async Task OnBeforeInternalNavigation(LocationChangingContext context)
    {
        await StopScanning();
    }

    public async Task OnQrCodeScanned(string qrCodeData)
    {
        if (!string.IsNullOrEmpty(qrCodeData) && CanProcessAttendance())
        {
            MID_HelperFunctions.DebugMessage($"QR Code detected: {qrCodeData}", DebugClass.Info);
        
            isProcessing = true;
            statusMessage = string.Empty;
            await InvokeAsync(StateHasChanged);
        
            try
            {
                var decodedSessionData = await QRDecoder.DecodeSessionDataAsync(qrCodeData);

                if (decodedSessionData != null)
                {
                    MID_HelperFunctions.DebugMessage($"Valid AirCode QR detected for course: {decodedSessionData.CourseCode}", DebugClass.Info);
                    var myCourses = await CourseService.GetStudentCoursesByMatricAsync(currentUserMatricNumber);
                    var courseRefs = myCourses.GetEnrolledCourses();

                    bool isTakingCourse = courseRefs.FirstOrDefault(course => course.CourseCode == decodedSessionData.CourseCode) != null;

                    if (isTakingCourse)
                    {
                        await ProcessAttendanceWithNewPayload(qrCodeData);
                    }
                    else
                    {
                        statusMessage = "You are not enrolled in this course";
                        isSuccess = false;
                        MID_HelperFunctions.DebugMessage("You are not taking this course", DebugClass.Warning);
                    }
                }
                else
                {
                    statusMessage = "Invalid QR code format";
                    isSuccess = false;
                    MID_HelperFunctions.DebugMessage("QR code is not a valid AirCode attendance QR", DebugClass.Warning);
                }
            }
            catch (Exception ex)
            {
                statusMessage = "Error processing QR code";
                isSuccess = false;
                MID_HelperFunctions.DebugMessage($"Error processing QR code: {ex.Message}", DebugClass.Exception);
            }
            finally
            {
                isProcessing = false;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private async Task ProcessAttendanceWithNewPayload(string qrCode)
    {
        try
        {
            MID_HelperFunctions.DebugMessage($"Processing attendance with payload for QR: {qrCode}", DebugClass.Info);
        
            var attendanceRecord = new AttendanceRecord
            {
                MatricNumber = currentUserMatricNumber,
                HasScannedAttendance = true,
                IsOnlineScan = true,
                DeviceGUID = deviceGUID
            };
        
            var edgeFunctionRequest = await QRDecoder.CreateEdgeFunctionRequestAsync(qrCode, attendanceRecord);
            
            if (edgeFunctionRequest == null)
            {
                statusMessage = "Failed to create valid request";
                isSuccess = false;
                return;
            }
        
            var result = await EdgeService.ProcessAttendanceWithPayloadAsync(edgeFunctionRequest);
        
            if (result.Success)
            {
                statusMessage = "Attendance recorded successfully!";
                isSuccess = true;
                MID_HelperFunctions.DebugMessage("Attendance recorded successfully!", DebugClass.Info);
                
                await Task.Delay(2000);
            }
            else
            {
                var userFriendlyMessage = GetUserFriendlyErrorMessage(result.ErrorCode, result.Message);
                statusMessage = userFriendlyMessage;
                isSuccess = false;
                MID_HelperFunctions.DebugMessage($"Attendance processing failed: {result.Message}", DebugClass.Warning);
            }
        }
        catch (Exception ex)
        {
            statusMessage = "Failed to process attendance";
            isSuccess = false;
            MID_HelperFunctions.DebugMessage($"Exception in ProcessAttendanceWithNewPayload: {ex}", DebugClass.Exception);
        }
    }

    private string GetUserFriendlyErrorMessage(string errorCode, string originalMessage)
    {
        return errorCode switch
        {
            "DUPLICATE_ATTENDANCE" => "You have already marked attendance for this session",
            "SESSION_NOT_FOUND" => "This attendance session does not exist",
            "TEMPORAL_KEY_EXPIRED" => "QR code has expired. Please scan the latest code",
            "TEMPORAL_KEY_MISSING" => "Please scan a fresh QR code",
            "DEVICE_SECURITY_VIOLATION" => "This device has already been used by another student",
            "DEVICE_GUID_MISSING" => "Device identification is required",
            "MATRIC_NUMBER_MISSING" => "Student identification number is required",
            "SESSION_QUERY_ERROR" => "Database connection failed. Check your connection",
            "ATTENDANCE_UPDATE_ERROR" => "Failed to save attendance. Please try again",
            "NETWORK_ERROR" => "Network connection failed. Check your internet",
            "INVALID_JSON" => "Invalid request format. Please try scanning again",
            "MISSING_PARAMETERS" => "Required information missing. Please try again",
            _ => originalMessage
        };
    }

    private bool CanProcessAttendance()
    {
        if (isProcessing) return false;
        
        if (string.IsNullOrWhiteSpace(currentUserMatricNumber))
        {
            statusMessage = "User not logged in";
            isSuccess = false;
            return false;
        }

        return true;
    }

    private async Task StartScanning()
    {
        if (qrScanner != null)
        {
            isScanning = true;
            statusMessage = string.Empty;
            await qrScanner.StartScanningAsync();
            StateHasChanged();
        }
    }

    private async Task StopScanning()
    {
        if (qrScanner != null)
        {
            isScanning = false;
            await qrScanner.StopScanningAsync();
            StateHasChanged();
        }
    }

    private async Task SwitchCamera()
    {
        if (qrScanner != null)
        {
            await qrScanner.SwitchCameraAsync();
        }
    }

    private void ClearStatus()
    {
        statusMessage = string.Empty;
        StateHasChanged();
    }
}