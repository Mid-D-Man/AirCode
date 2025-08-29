using Microsoft.AspNetCore.Components;
using AirCode.Services.Attendance;
using AirCode.Components.SharedPrefabs.Fields;
using AirCode.Services.VisualElements;
using System.Text.RegularExpressions;
using AirCode.Domain.Entities;
using AirCode.Domain.ValueObjects;

namespace AirCode.Components.Admin.Shared
{
    public partial class ManualAttendancePopup : ComponentBase, IDisposable
    {
        [Inject]
        public IFirestoreAttendanceService FirestoreAttendanceService { get; set; } = default!;

        [Inject]
        public IBackdropService BackdropService { get; set; } = default!;

        [Parameter]
        public bool IsVisible { get; set; } = false;

        [Parameter]
        public PartialSessionData? SessionData { get; set; }

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
        private bool _previousVisibleState = false;
        
        // Portal management
        private ElementReference modalOverlayRef;
        private string InstanceId = Guid.NewGuid().ToString("N")[..8];
        private string ModalSelector => $"#manual-attendance-modal-{InstanceId}";
        private bool _isInPortal = false;

        public string SpinnerSubtitle => $"Signing attendance for {matricNumber}...";

        // Regex pattern for matric number validation
        private static readonly Regex MatricNumberPattern = new(
            @"^U[0-9]{2}[A-Z]{2,3}[0-9]{4}$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        protected override async Task OnParametersSetAsync()
        {
            if (IsVisible && !_previousVisibleState)
            {
                await ShowModalAsync();
                ResetForm();
            }
            else if (!IsVisible && _previousVisibleState)
            {
                await HideModalAsync();
            }
            
            _previousVisibleState = IsVisible;
        }

        private async Task ShowModalAsync()
        {
            try
            {
                await Task.Delay(50);
                await BackdropService.MoveElementToPortalAsync(ModalSelector);
                _isInPortal = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error showing modal: {ex.Message}");
            }
        }

        private async Task HideModalAsync()
        {
            try
            {
                if (_isInPortal)
                {
                    await BackdropService.ReturnElementFromPortalAsync(ModalSelector);
                    _isInPortal = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error hiding modal: {ex.Message}");
            }
        }

        private void HandleMatricInput(string value)
        {
            matricNumber = value.ToUpper();
            ClearMessages();
            StateHasChanged();
        }

        private async Task ValidateMatricNumber(ValidationEventArgs args)
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
            await HideModalAsync();
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
            
            // Ensure we return from portal on disposal
            if (_isInPortal)
            {
                try
                {
                    _ = BackdropService.ReturnElementFromPortalAsync(ModalSelector);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during disposal: {ex.Message}");
                }
            }
        }
    }
}
