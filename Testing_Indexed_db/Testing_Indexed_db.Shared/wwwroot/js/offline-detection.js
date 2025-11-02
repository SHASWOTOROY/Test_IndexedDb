// Offline Detection Service
window.offlineDetection = {
    dotNetRef: null,
    
    init(dotNetRef) {
        this.dotNetRef = dotNetRef;
        
        // Set up event listeners
        window.addEventListener('online', () => {
            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync('OnOnlineStatusChanged', true);
            }
        });
        
        window.addEventListener('offline', () => {
            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync('OnOnlineStatusChanged', false);
            }
        });
    },
    
    isOnline() {
        return navigator.onLine;
    }
};


