window.auth = {
    // Melakukan login via HTTP POST agar cookie auth bisa dibuat di response browser
    login: async (model) => {
        const response = await fetch('/auth/login', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            credentials: 'include',
            body: JSON.stringify(model)
        });

        let data = {};
        try {
            data = await response.json();
        } catch {
            // Abaikan jika response kosong / bukan JSON
        }

        return {
            ok: response.ok,
            status: response.status,
            data
        };
    },

    // Logout via HTTP POST
    logout: async () => {
        const response = await fetch('/auth/logout', {
            method: 'POST',
            credentials: 'include'
        });

        return {
            ok: response.ok,
            status: response.status
        };
    }
};
