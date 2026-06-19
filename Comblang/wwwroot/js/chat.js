/**
 * Comblang Chat — SignalR JS Interop Module
 * 
 * Provides Blazor-interop friendly wrappers around SignalR hub connections
 * for real-time chat and notifications. Designed to be invoked from Blazor
 * components via IJSRuntime.
 */
window.comblangChat = (function () {
    // ── Private state ──────────────────────────────────────────
    let chatConnection = null;
    let notificationConnection = null;
    let dotNetChatRef = null;      // Blazor component reference for chat callbacks
    let dotNetNotificationRef = null;

    // ── Helpers ───────────────────────────────────────────────

    /** Builds a SignalR hub URL including the access token from cookies or localStorage. */
    function buildHubUrl(hubPath) {
        const baseUrl = window.location.origin;
        return baseUrl + hubPath;
    }

    /** Attempts to retrieve a bearer token from localStorage. */
    function getAccessToken() {
        // Try localStorage first (JWT), then cookie fallback
        const token = localStorage.getItem('authToken') || localStorage.getItem('token');
        return token || null;
    }

    // ── Chat Hub ──────────────────────────────────────────────

    /**
     * Connects to /hubs/chat and wires up ReceiveMessage, UserTyping, and
     * MessagesRead events. Incoming events are forwarded to the Blazor
     * component via DotNet.invokeMethodAsync.
     * 
     * @param {object} dotNetReference - A DotNetObjectReference to the Blazor component.
     */
    async function connectChatHub(dotNetReference) {
        if (chatConnection && chatConnection.state === signalR.HubConnectionState.Connected) {
            console.log('[Chat] Already connected.');
            return;
        }

        dotNetChatRef = dotNetReference;

        const url = buildHubUrl('/hubs/chat');

        chatConnection = new signalR.HubConnectionBuilder()
            .withUrl(url, {
                accessTokenFactory: getAccessToken
            })
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        // ── Inbound event handlers ────────────────────────────

        chatConnection.on('ReceiveMessage', (message) => {
            if (dotNetChatRef) {
                dotNetChatRef.invokeMethodAsync('OnReceiveMessage', message)
                    .catch(err => console.error('[Chat] OnReceiveMessage error:', err));
            }
        });

        chatConnection.on('UserTyping', (data) => {
            if (dotNetChatRef) {
                dotNetChatRef.invokeMethodAsync('OnUserTyping', data)
                    .catch(err => console.error('[Chat] OnUserTyping error:', err));
            }
        });

        chatConnection.on('MessagesRead', (data) => {
            if (dotNetChatRef) {
                dotNetChatRef.invokeMethodAsync('OnMessagesRead', data)
                    .catch(err => console.error('[Chat] MessagesRead error:', err));
            }
        });

        chatConnection.on('MessageSent', (data) => {
            if (dotNetChatRef) {
                dotNetChatRef.invokeMethodAsync('OnMessageSent', data)
                    .catch(err => console.error('[Chat] MessageSent error:', err));
            }
        });

        chatConnection.onreconnecting(() => {
            console.log('[Chat] Reconnecting...');
        });

        chatConnection.onreconnected(async () => {
            console.log('[Chat] Reconnected.');
        });

        chatConnection.onclose(async () => {
            console.log('[Chat] Connection closed.');
        });

        try {
            await chatConnection.start();
            console.log('[Chat] Connected.');
        } catch (err) {
            console.error('[Chat] Connection failed:', err);
            // Retry after 5 seconds
            setTimeout(() => connectChatHub(dotNetReference), 5000);
        }
    }

    /**
     * Disconnects the chat hub.
     */
    async function disconnectChatHub() {
        if (chatConnection) {
            await chatConnection.stop();
            chatConnection = null;
            console.log('[Chat] Disconnected.');
        }
    }

    // ── Notification Hub ──────────────────────────────────────

    /**
     * Connects to /hubs/notification and wires up NewMessage, NewMatch,
     * NewLike, and ReceiveNotification events.
     * 
     * @param {object} dotNetReference - A DotNetObjectReference to the Blazor component.
     */
    async function connectNotificationHub(dotNetReference) {
        if (notificationConnection && notificationConnection.state === signalR.HubConnectionState.Connected) {
            console.log('[Notification] Already connected.');
            return;
        }

        dotNetNotificationRef = dotNetReference;

        const url = buildHubUrl('/hubs/notification');

        notificationConnection = new signalR.HubConnectionBuilder()
            .withUrl(url, {
                accessTokenFactory: getAccessToken
            })
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        // ── Inbound event handlers ────────────────────────────

        notificationConnection.on('NewMessage', (data) => {
            if (dotNetNotificationRef) {
                dotNetNotificationRef.invokeMethodAsync('OnNewMessageNotification', data)
                    .catch(err => console.error('[Notification] NewMessage error:', err));
            }
        });

        notificationConnection.on('NewMatch', (data) => {
            if (dotNetNotificationRef) {
                dotNetNotificationRef.invokeMethodAsync('OnNewMatchNotification', data)
                    .catch(err => console.error('[Notification] NewMatch error:', err));
            }
        });

        notificationConnection.on('NewLike', (data) => {
            if (dotNetNotificationRef) {
                dotNetNotificationRef.invokeMethodAsync('OnNewLikeNotification', data)
                    .catch(err => console.error('[Notification] NewLike error:', err));
            }
        });

        notificationConnection.on('ReceiveNotification', (data) => {
            if (dotNetNotificationRef) {
                dotNetNotificationRef.invokeMethodAsync('OnReceiveNotification', data)
                    .catch(err => console.error('[Notification] ReceiveNotification error:', err));
            }
        });

        notificationConnection.onreconnecting(() => {
            console.log('[Notification] Reconnecting...');
        });

        notificationConnection.onreconnected(async () => {
            console.log('[Notification] Reconnected.');
        });

        notificationConnection.onclose(async () => {
            console.log('[Notification] Connection closed.');
        });

        try {
            await notificationConnection.start();
            console.log('[Notification] Connected.');
        } catch (err) {
            console.error('[Notification] Connection failed:', err);
            setTimeout(() => connectNotificationHub(dotNetReference), 5000);
        }
    }

    /**
     * Disconnects the notification hub.
     */
    async function disconnectNotificationHub() {
        if (notificationConnection) {
            await notificationConnection.stop();
            notificationConnection = null;
            console.log('[Notification] Disconnected.');
        }
    }

    // ── Utility ───────────────────────────────────────────────

    /**
     * Scrolls a chat container element to the bottom.
     * @param {string} elementId - The DOM ID of the scroll container.
     */
    function scrollToBottom(elementId) {
        const el = document.getElementById(elementId);
        if (el) {
            el.scrollTop = el.scrollHeight;
        }
    }

    /**
     * Returns whether the chat connection is currently connected.
     */
    function isChatConnected() {
        return chatConnection && chatConnection.state === signalR.HubConnectionState.Connected;
    }

    /**
     * Returns whether the notification connection is currently connected.
     */
    function isNotificationConnected() {
        return notificationConnection && notificationConnection.state === signalR.HubConnectionState.Connected;
    }

    // ── Public API ────────────────────────────────────────────
    return {
        connectChatHub,
        disconnectChatHub,
        connectNotificationHub,
        disconnectNotificationHub,
        scrollToBottom,
        isChatConnected,
        isNotificationConnected
    };
})();
