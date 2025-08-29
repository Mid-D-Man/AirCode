using Microsoft.JSInterop;

namespace AirCode.Services.VisualElements
{
    public interface IBackdropService
    {
        bool IsVisible { get; }
        event Action OnStateChanged;
        Task ShowAsync();
        Task HideAsync();
        Task MoveElementToPortalAsync(string elementSelector);
        Task ReturnElementFromPortalAsync(string elementSelector);
    }

    public class BackdropService : IBackdropService
    {
        private readonly IJSRuntime _jsRuntime;
        private bool _isVisible = false;
        
        public BackdropService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }
        
        public bool IsVisible
        {
            get => _isVisible;
            private set
            {
                _isVisible = value;
                OnStateChanged?.Invoke();
            }
        }

        public event Action? OnStateChanged;

        public async Task ShowAsync()
        {
            IsVisible = true;
        }

        public async Task HideAsync()
        {
            IsVisible = false;
        }

        // Portal functionality
        public async Task MoveElementToPortalAsync(string elementSelector)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("moveElementToPortal", elementSelector);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error moving element to portal: {ex.Message}");
            }
        }

        public async Task ReturnElementFromPortalAsync(string elementSelector)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("returnElementFromPortal", elementSelector);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error returning element from portal: {ex.Message}");
            }
        }
    }
}