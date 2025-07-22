using Microsoft.AspNetCore.Components;
using AirCode.Services.Attendance;
using AirCode.Components.SharedPrefabs.Fields;
using System.Text.RegularExpressions;
using SessionData = AirCode.Services.Attendance.SessionData;

namespace AirCode.Components.Admin.Shared
{
    public partial class ManualAttendancePopup : ComponentBase
    {
        [Inject]
        public IFirestoreAttendanceService FirestoreAttendanceService { get; set; } = default!;

        [Parameter]
        public bool IsVisible { get; set; } = false;

        [Parameter]
        public AirCode.Services.Attendance.SessionData? SessionData { get; set; }

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

        // FIXED: Corrected regex pattern - removed extra digit from year
        // Format: U21CYS1083, U22CS1083, U24CYS1094
        private static readonly Regex MatricNumberPattern = new(
            @"^U[0-9]{2}[A-Z]{2,3}[0-9]{4}$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        protected override void OnParametersSet()
        {
            if (IsVisible)
            {
                ResetForm();
            }
        }

        // FIXED: Removed async to prevent clearing behavior
        private void HandleMatricInput(string value)
        {
            matricNumber = value.ToUpper();
            ClearMessages();
            StateHasChanged();
        }

        private async Task ValidateMatricNumber(FormField.ValidationEventArgs args)
        {
            if (string.IsNullOrEmpty(args.Value))
                return;

            if (!IsValidMatricFormat(args.Value))
            {
                ShowError("Invalid format. Expected: U21CYS1083");
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
                ShowError("Invalid matric number format. Expected format: U21CYS1083");
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
                    
                    await OnAttendanceSigned.InvokeAsync(matricNumber);
                    matricNumber = string.Empty;
                    
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

    public class SessionData
    {
        public string SessionId { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public int Duration { get; set; }
    }
}