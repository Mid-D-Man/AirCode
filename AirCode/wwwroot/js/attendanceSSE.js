// wwwroot/js/attendanceSSE.js
window.attendanceSSE = {
    eventSource: null,
    dotNetRef: null,

    connect: function(endpoint, dotNetReference) {
        this.disconnect();
        
        this.dotNetRef = dotNetReference;
        this.eventSource = new EventSource(endpoint);
        
        this.eventSource.onopen = function(event) {
            console.log('SSE connection opened for attendance notifications');
        };
        
        this.eventSource.onmessage = function(event) {
            if (attendanceSSE.dotNetRef) {
                attendanceSSE.dotNetRef.invokeMethodAsync('OnNotificationReceived', event.data);
            }
        };
        
        this.eventSource.addEventListener('attendance-update', function(event) {
            if (attendanceSSE.dotNetRef) {
                attendanceSSE.dotNetRef.invokeMethodAsync('OnNotificationReceived', event.data);
            }
        });
        
        this.eventSource.onerror = function(event) {
            console.error('SSE connection error:', event);
            // Implement retry logic if needed
            setTimeout(() => {
                if (attendanceSSE.eventSource && 
                    attendanceSSE.eventSource.readyState === EventSource.CLOSED) {
                    // Auto-reconnect logic
                    attendanceSSE.connect(endpoint, attendanceSSE.dotNetRef);
                }
            }, 5000);
        };
    },
    
    disconnect: function() {
        if (this.eventSource) {
            this.eventSource.close();
            this.eventSource = null;
        }
        this.dotNetRef = null;
    }
};
