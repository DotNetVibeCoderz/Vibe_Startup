/**
 * FuelStation - SignalR Notification Client
 * Handles real-time notification subscription via JS interop.
 * Supports both the NotificationsPage and MainLayout badge updates.
 */
window.fuelStationNotifications = {
    connection: null,
    dotNetRef: null,
    layoutDotNetRef: null,

    /**
     * Initialize SignalR connection for the NotificationsPage.
     * @param {DotNetObjectReference} dotNetRef - Reference to the Blazor component
     */
    init: function (dotNetRef) {
        this.dotNetRef = dotNetRef;
        this.ensureConnection();
    },

    /**
     * Initialize SignalR connection for the MainLayout (badge count).
     * @param {DotNetObjectReference} dotNetRef - Reference to the layout component
     */
    initForLayout: function (dotNetRef) {
        this.layoutDotNetRef = dotNetRef;
        this.ensureConnection();
    },

    /**
     * Ensures a single shared SignalR connection is active.
     */
    ensureConnection: function () {
        if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
            return;
        }
        if (this.connection && this.connection.state === signalR.HubConnectionState.Connecting) {
            return;
        }
        if (this.connection && this.connection.state === signalR.HubConnectionState.Disconnected) {
            // Try to restart
            this.connection.start().catch(() => {});
            return;
        }
        this.startConnection();
    },

    /**
     * Start the SignalR connection to the NotificationHub.
     */
    startConnection: async function () {
        try {
            // Build the SignalR connection
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl("/notificationHub")
                .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
                .configureLogging(signalR.LogLevel.Warning)
                .build();

            // Listen for "ReceiveNotification" events from the server
            this.connection.on("ReceiveNotification", (notification) => {
                console.log("[NotificationHub] Received:", notification);

                // Notify the NotificationsPage if active
                if (this.dotNetRef) {
                    try {
                        this.dotNetRef.invokeMethodAsync("OnNotificationReceived", notification);
                    } catch (e) {
                        // Component may have been disposed
                        this.dotNetRef = null;
                    }
                }

                // Notify the MainLayout to update badge
                if (this.layoutDotNetRef) {
                    try {
                        this.layoutDotNetRef.invokeMethodAsync("OnNotificationReceivedForBadge");
                    } catch (e) {
                        // Layout ref may be stale
                    }
                }
            });

            // Handle reconnection events
            this.connection.onreconnecting((error) => {
                console.warn("[NotificationHub] Reconnecting...", error);
            });

            this.connection.onreconnected((connectionId) => {
                console.log("[NotificationHub] Reconnected:", connectionId);
            });

            this.connection.onclose((error) => {
                console.warn("[NotificationHub] Connection closed:", error);
            });

            // Start the connection
            await this.connection.start();
            console.log("[NotificationHub] Connected successfully");
        } catch (err) {
            console.error("[NotificationHub] Connection failed:", err);
            // Retry after 5 seconds
            setTimeout(() => this.startConnection(), 5000);
        }
    },

    /**
     * Stop the SignalR connection.
     */
    stop: async function () {
        if (this.connection) {
            await this.connection.stop();
            console.log("[NotificationHub] Disconnected");
        }
    },

    /**
     * Check if the connection is active.
     */
    isConnected: function () {
        return this.connection && this.connection.state === signalR.HubConnectionState.Connected;
    }
};
