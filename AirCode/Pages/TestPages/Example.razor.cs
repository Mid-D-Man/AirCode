using Microsoft.AspNetCore.Components;

using Microsoft.AspNetCore.Components;
using AirCode.Components.SharedPrefabs.Cards;

namespace AirCode.Pages.TestPages{
    
    public partial class Example : ComponentBase
    {
        private NotificationComponent? notificationComponent;
        private string customMessage = string.Empty;
        private NotificationComponent.NotificationType selectedType = NotificationComponent.NotificationType.Info;
        private int customDuration = 5000;
        
        // Counter for demonstration purposes
        private int notificationCounter = 0;

        protected override void OnInitialized()
        {
            base.OnInitialized();
        }

        private void ShowSuccessNotification()
        {
            notificationCounter++;
            notificationComponent?.ShowSuccess(
                $"Operation completed successfully! #{notificationCounter}", 
                duration: 4000
            );
        }

        private void ShowErrorNotification()
        {
            notificationCounter++;
            notificationComponent?.ShowError(
                $"An error occurred during processing. Please try again. #{notificationCounter}", 
                duration: 7000
            );
        }

        private void ShowInfoNotification()
        {
            notificationCounter++;
            notificationComponent?.ShowInfo(
                $"Information: Your data has been saved to the system. #{notificationCounter}",
                duration: 5000
            );
        }

        private void ShowWarningNotification()
        {
            notificationCounter++;
            notificationComponent?.ShowWarning(
                $"Warning: This action cannot be undone. #{notificationCounter}",
                duration: 6000
            );
        }

        private void ShowHtmlNotification()
        {
            notificationCounter++;
            var htmlContent = $@"
                <div>
                    <strong>Rich Content Notification #{notificationCounter}</strong><br/>
                    <em>This notification contains HTML formatting</em><br/>
                    <small>Timestamp: {DateTime.Now:HH:mm:ss}</small>
                </div>";
            
            notificationComponent?.ShowInfo(
                htmlContent,
                duration: 8000,
                isHtml: true
            );
        }

        private void ShowCustomNotification()
        {
            if (string.IsNullOrWhiteSpace(customMessage))
            {
                notificationComponent?.ShowWarning("Please enter a message first!");
                return;
            }

            notificationCounter++;
            var message = $"{customMessage} #{notificationCounter}";
            var duration = customDuration > 0 ? customDuration : 5000;

            switch (selectedType)
            {
                case NotificationComponent.NotificationType.Success:
                    notificationComponent?.ShowSuccess(message, duration);
                    break;
                case NotificationComponent.NotificationType.Error:
                    notificationComponent?.ShowError(message, duration);
                    break;
                case NotificationComponent.NotificationType.Info:
                    notificationComponent?.ShowInfo(message, duration);
                    break;
                case NotificationComponent.NotificationType.Warning:
                    notificationComponent?.ShowWarning(message, duration);
                    break;
            }

            // Clear the form after showing notification
            customMessage = string.Empty;
            customDuration = 5000;
        }

        private void ClearAllNotifications()
        {
            notificationComponent?.ClearAll();
            notificationCounter = 0;
        }

        // Method to show notifications from other components or services
        public void ShowNotificationFromExternalSource(string message, NotificationComponent.NotificationType type)
        {
            notificationComponent?.ShowNotification(message, type);
        }

        // Advanced usage: Show notification based on API response
        private async Task HandleApiResponse<T>(Task<T> apiCall, string successMessage, string errorMessage)
        {
            try
            {
                var result = await apiCall;
                notificationComponent?.ShowSuccess(successMessage);
            }
            catch (Exception ex)
            {
                notificationComponent?.ShowError($"{errorMessage}: {ex.Message}");
            }
        }

        // Example of integration with other services
        private async Task SaveDataExample()
        {
            await HandleApiResponse(
                SimulateApiCall(),
                "Data saved successfully!",
                "Failed to save data"
            );
        }

        private async Task<bool> SimulateApiCall()
        {
            await Task.Delay(1000); // Simulate API delay
            return Random.Shared.Next(0, 2) == 1; // Random success/failure
        }
    }
}