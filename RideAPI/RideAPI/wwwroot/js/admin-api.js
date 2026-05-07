(function () {
    const toastRootId = 'adminToastRoot';

    function ensureToastRoot() {
        let root = document.getElementById(toastRootId);
        if (!root) {
            root = document.createElement('div');
            root.id = toastRootId;
            root.className = 'toast-root';
            document.body.appendChild(root);
        }
        return root;
    }

    function showToast(message, type = 'info') {
        const root = ensureToastRoot();
        const item = document.createElement('div');
        item.className = `toast-item ${type}`;
        item.textContent = message;
        root.appendChild(item);
        window.setTimeout(() => item.remove(), 4500);
    }

    async function readPayload(response) {
        const text = await response.text();
        if (!text) return null;

        try {
            return JSON.parse(text);
        } catch {
            return text;
        }
    }

    function messageFromPayload(payload, fallback) {
        if (!payload) return fallback;
        if (typeof payload === 'string') return payload;
        return payload.message || payload.error || fallback;
    }

    async function fetchJson(url, options = {}) {
        const response = await fetch(url, {
            credentials: 'same-origin',
            ...options
        });
        const payload = await readPayload(response);

        if (response.status === 401) {
            showToast('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.', 'error');
            const returnUrl = encodeURIComponent(window.location.pathname + window.location.search);
            window.location.href = `/admin/login?returnUrl=${returnUrl}`;
            throw new Error('Unauthorized');
        }

        if (response.status === 403) {
            showToast('Bạn không có quyền truy cập chức năng này.', 'error');
            window.location.href = '/admin/unauthorized';
            throw new Error('Forbidden');
        }

        if (!response.ok) {
            const message = messageFromPayload(payload, `API lỗi ${response.status}.`);
            showToast(message, 'error');
            throw new Error(message);
        }

        return payload;
    }

    function setBusy(button, isBusy, busyText) {
        if (!button) return;
        if (isBusy) {
            button.dataset.originalText = button.textContent;
            button.disabled = true;
            button.textContent = busyText || 'Đang xử lý...';
            button.classList.add('is-loading');
            return;
        }

        button.disabled = false;
        button.textContent = button.dataset.originalText || button.textContent;
        button.classList.remove('is-loading');
    }

    window.AdminApi = {
        fetchJson,
        showToast,
        setBusy
    };
})();
