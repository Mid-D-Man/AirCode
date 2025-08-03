using AirCode.Utilities.HelperScripts;

namespace AirCode.Services.VisualElements;
using Microsoft.AspNetCore.Components;

    public interface IBackdropService
    {
        bool IsVisible { get; }
        event Action OnStateChanged;
        void Show();
        void Hide();
    }

    public class BackdropService : IBackdropService
    {
        private bool _isVisible = false;
        
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

        public void Show()
        {
            IsVisible = true;
          
        }

        public void Hide()
        {
            IsVisible = false;
        }
    }
