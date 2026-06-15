// SoccerWizard - Client-side JavaScript
// SignalR connection and real-time updates

let connection = null;

// Initialize SignalR connection
function initSignalR() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/matchhub")
        .withAutomaticReconnect()
        .build();
    
    // Handle live match notifications
    connection.on("LiveNotification", (data) => {
        console.log("Live notification:", data);
        showToast(data.message);
    });
    
    // Handle user count changes
    connection.on("UserCountChanged", (count) => {
        console.log(`Online users: ${count}`);
    });
    
    // Start connection
    connection.start()
        .then(() => console.log("SignalR connected"))
        .catch(err => console.error("SignalR error:", err));
}

// Join a match group for real-time updates
function joinMatchGroup(matchId) {
    if (connection && connection.state === signalR.HubConnectionState.Connected) {
        connection.invoke("JoinMatchGroup", matchId).catch(console.error);
    }
}

// Update match score in real-time
function updateScore(matchId, homeScore, awayScore, status) {
    if (connection && connection.state === signalR.HubConnectionState.Connected) {
        connection.invoke("UpdateScore", matchId, homeScore, awayScore, status).catch(console.error);
    }
}

// Toast notification
function showToast(message) {
    const toast = document.createElement('div');
    toast.className = 'sw-toast';
    toast.innerHTML = `
        <div class="sw-toast-content">
            <span>⚽</span>
            <span>${message}</span>
        </div>
    `;
    document.body.appendChild(toast);
    
    setTimeout(() => {
        toast.classList.add('fade-out');
        setTimeout(() => toast.remove(), 300);
    }, 5000);
}

// Theme helpers
function swGetTheme() {
    return localStorage.getItem("sw-theme") || "dark";
}

function swSetTheme(theme) {
    document.documentElement.setAttribute("data-theme", theme);
    localStorage.setItem("sw-theme", theme);
}

// Toggle sidebar on mobile
document.addEventListener('DOMContentLoaded', () => {
    const toggleBtn = document.querySelector('.sw-mobile-toggle');
    const sidebar = document.querySelector('.sw-sidebar');
    
    if (toggleBtn && sidebar) {
        toggleBtn.addEventListener('click', () => {
            sidebar.classList.toggle('open');
        });
    }
});

// Apply stored theme on load
document.addEventListener('DOMContentLoaded', () => {
    swSetTheme(swGetTheme());
});

// Auto-init SignalR
document.addEventListener('DOMContentLoaded', () => {
    if (typeof signalR !== 'undefined') {
        initSignalR();
    }
});
