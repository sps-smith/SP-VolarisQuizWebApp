// Shared modal helpers used by layout and views
function openModal(html) {
    document.getElementById('modal-content').innerHTML = html;
    const overlay = document.getElementById('modal-overlay');
    const box = document.getElementById('modal-box');
    box.classList.toggle('access-modal', document.getElementById('modal-content')?.firstElementChild?.classList.contains('access-edit-modal'));
    overlay.style.display = 'flex';
}

function closeModal() {
    const overlay = document.getElementById('modal-overlay');
    overlay.style.display = 'none';
    document.getElementById('modal-content').innerHTML = '';
    document.getElementById('modal-box')?.classList.remove('access-modal');
}

document.addEventListener('DOMContentLoaded', function () {
    // Close language menu when clicking outside
    document.addEventListener('click', function (e) {
        const picker = document.getElementById('lang-picker');
        if (picker && !picker.contains(e.target)) {
            const menu = document.getElementById('lang-menu');
            if (menu) menu.style.display = 'none';
        }
    });
});

// Language picker toggle
function toggleLangMenu(e) {
    e.stopPropagation();
    const menu = document.getElementById('lang-menu');
    if (menu) menu.style.display = menu.style.display === 'none' ? 'block' : 'none';
}

async function stopImpersonation() {
    const token = document.querySelector('[name="__RequestVerificationToken"]')?.value ?? '';
    if (!token) {
        alert('Your session token is missing. Refresh the page and try again.');
        return;
    }

    const resp = await fetch('/impersonation/stop', {
        method: 'POST',
        headers: { 'RequestVerificationToken': token }
    });

    if (!resp.ok) {
        const fallback = 'Failed to stop impersonation.';
        try {
            const payload = await resp.json();
            alert(payload.error || fallback);
        } catch {
            alert(await resp.text() || fallback);
        }
        return;
    }

    const payload = await resp.json();
    window.location.href = payload.redirectUrl || '/';
}
