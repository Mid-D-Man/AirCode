using Microsoft.AspNetCore.Components;
using AirCode.Services.Attendance;
using System.Text.RegularExpressions;
using AirCode.Services.Attendance;
namespace AirCode.Components.Admin.Shared
{
    public partial class ManualAttendancePopup : ComponentBase
    {
        [Inject]
        public IFirestoreAttendanceService FirestoreAttendanceService { get; set; } = default!;

        [Parameter]
        public bool IsVisible { get; set; } = false;

        [Parameter]
        public SessionData? SessionData { get; set; }

        [Parameter]
        public EventCallback OnClose { get; set; }

        [Parameter]
        public EventCallback<string> OnAttendanceSigned { get; set; }

        // Private fields
        private string matricNumber = string.Empty;
        private bool isProcessing = false;
        private bool hasError = false;
        private string errorMessage = string.Empty;
        private string successMessage = string.Empty;
        private Timer? successTimer;
public string SpinnerSubtitle => $"Signing attendance for {matricNumber}...";
        // Matric number validation pattern
        private static readonly Regex MatricNumberPattern = new(
            @"^[0-9]{2}[A-Z]{2}[0-9]{4}$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        protected override void OnParametersSet()
        {
            if (IsVisible)
            {
                // Reset form state when popup opens
                ResetForm();
            }
        }

        private void HandleMatricInput(ChangeEventArgs e)
        {
            var input = e.Value?.ToString()?.ToUpper() ?? string.Empty;
            matricNumber = input;
            
            // Clear previous error/success messages
            ClearMessages();
            
            // Validate format in real-time
            if (!string.IsNullOrEmpty(matricNumber) && !IsValidMatricFormat(matricNumber))
            {
                hasError = true;
                errorMessage = "Invalid format. Expected: 20CS1234";
            }
        }

        private bool IsValidMatricFormat(string matric)
        {
            return MatricNumberPattern.IsMatch(matric);
        }

        private async Task SignAttendanceAsync()
        {
            if (SessionData == null)
            {
                ShowError("Session data not available");
                return;
            }

            if (string.IsNullOrWhiteSpace(matricNumber))
            {
                ShowError("Please enter a matric number");
                return;
            }

            if (!IsValidMatricFormat(matricNumber))
            {
                ShowError("Invalid matric number format. Expected format: 20CS1234");
                return;
            }

            try
            {
                isProcessing = true;
                ClearMessages();
                StateHasChanged();

                var result = await FirestoreAttendanceService.ManualSignAttendanceAsync(
                    SessionData.SessionId,
                    SessionData.CourseCode,
                    matricNumber.ToUpper(),
                    isManualSign: true
                );

                if (result)
                {
                    ShowSuccess($"Successfully signed attendance for {matricNumber}");
                    
                    // Notify parent component
                    await OnAttendanceSigned.InvokeAsync(matricNumber);
                    
                    // Clear form after successful sign
                    matricNumber = string.Empty;
                    
                    // Auto-close popup after 2 seconds
                    successTimer = new Timer(async _ => 
                    {
                        await InvokeAsync(async () =>
                        {
                            await ClosePopup();
                        });
                    }, null, 2000, Timeout.Infinite);
                }
                else
                {
                    ShowError("Failed to sign attendance. Student may not be enrolled or already signed.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"An error occurred: {ex.Message}");
            }
            finally
            {
                isProcessing = false;
                StateHasChanged();
            }
        }

        private void ShowError(string message)
        {
            hasError = true;
            errorMessage = message;
            successMessage = string.Empty;
        }

        private void ShowSuccess(string message)
        {
            hasError = false;
            errorMessage = string.Empty;
            successMessage = message;
        }

        private void ClearMessages()
        {
            hasError = false;
            errorMessage = string.Empty;
            successMessage = string.Empty;
        }

        private void ResetForm()
        {
            matricNumber = string.Empty;
            ClearMessages();
            isProcessing = false;
            
            // Clear any existing timer
            successTimer?.Dispose();
            successTimer = null;
        }

        private async Task ClosePopup()
        {
            ResetForm();
            await OnClose.InvokeAsync();
        }

        private void HandleBackdropClick()
        {
            if (!isProcessing)
            {
                _ = ClosePopup();
            }
        }

        public void Dispose()
        {
            successTimer?.Dispose();
        }
    }

    // Session data model for the popup
    public class SessionData
    {
        public string SessionId { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public int Duration { get; set; }
    }
}
