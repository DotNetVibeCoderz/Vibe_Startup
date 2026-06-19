window.comblangAuth = {
    postJson: async (url, payload) => {
        const response = await fetch(url, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        let data = null;
        try {
            data = await response.json();
        } catch {
            data = null;
        }

        return {
            ok: response.ok,
            status: response.status,
            data
        };
    }
};
