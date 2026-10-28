(() => {
    "use strict";

    const endpoint = "/api/traffic/events";
    const excluded = path => /^\/(admin|staff)(\/|$)/i.test(path || "");
    const pathOnly = value => {
        try {
            const url = new URL(value, window.location.origin);
            if (url.origin !== window.location.origin || excluded(url.pathname)) return null;
            return url.pathname.slice(0, 512);
        } catch { return null; }
    };
    const newEventId = () => {
        if (globalThis.crypto?.randomUUID) return crypto.randomUUID();
        if (!globalThis.crypto?.getRandomValues) return null;
        const bytes = new Uint8Array(16);
        globalThis.crypto.getRandomValues(bytes);
        bytes[6] = (bytes[6] & 0x0f) | 0x40;
        bytes[8] = (bytes[8] & 0x3f) | 0x80;
        const hex = [...bytes].map(value => value.toString(16).padStart(2, "0")).join("");
        return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
    };
    const emit = (kind, pagePath, targetPath) => {
        if (!pagePath || excluded(pagePath)) return;
        const eventId = newEventId();
        if (!eventId) return;
        const payload = JSON.stringify({ eventId, kind, pagePath, ...(targetPath ? { targetPath } : {}) });
        const body = new Blob([payload], { type: "application/json" });
        try {
            if (navigator.sendBeacon && navigator.sendBeacon(endpoint, body)) return;
        } catch { /* Fetch fallback below. */ }
        fetch(endpoint, { method: "POST", body, credentials: "same-origin", keepalive: true, headers: { "Content-Type": "application/json" } }).catch(() => {});
    };

    const current = pathOnly(window.location.pathname);
    if (!current) return;
    emit("view", current);
    document.addEventListener("click", event => {
        if (!event.isTrusted || event.defaultPrevented || !(event.target instanceof Element)) return;
        const anchor = event.target.closest("a[href]");
        if (!anchor || anchor.hasAttribute("download")) return;
        const target = pathOnly(anchor.href);
        if (!target) return;
        emit("click", current, target);
    }, { capture: true, passive: true });
})();
