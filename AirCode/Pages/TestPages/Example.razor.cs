using Microsoft.AspNetCore.Components;

using Microsoft.AspNetCore.Components;
using AirCode.Components.SharedPrefabs.Cards;

namespace AirCode.Pages.TestPages{
    
    public partial class Example : ComponentBase
    {
        private NotificationComponent notificationComponent;

        private void ShowSuccess()
        {
            notificationComponent.ShowSuccess("Operation completed successfully!", 3000);
        }

        private void ShowInfo()
        {
            notificationComponent.ShowInfo("Here's some helpful information.");
        }

        private void ShowWarning()
        {
            notificationComponent.ShowWarning("Please be aware of this warning.", 7000);
        }

        private void ShowError()
        {
            notificationComponent.ShowError("An error occurred during processing.");
        }

        private void ClearAll()
        {
            notificationComponent.ClearAll();
        }

        // Alternative usage with service injection
        // @inject IToastService ToastService
    
        // private void ShowDirectToast()
        // {
        //     ToastService.ShowSuccess("Direct toast message", settings =>
        //     {
        //         settings.Timeout = 3;
        //         settings.AdditionalClasses = "custom-toast success-toast";
        //         settings.ShowProgressBar = true;
        //     });
        // }
    }
}