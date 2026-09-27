// Client Favorites management via LocalStorage
const FAVORITES_KEY = 'propnest_favorites';

function getFavorites() {
    try {
        const data = localStorage.getItem(FAVORITES_KEY);
        return data ? JSON.parse(data) : [];
    } catch {
        return [];
    }
}

function toggleFavorite(id) {
    const list = getFavorites();
    const idx = list.indexOf(id);
    let isSaved = false;

    if (idx > -1) {
        list.splice(idx, 1);
        isSaved = false;
        showToast('Đã bỏ lưu bất động sản', 'Tin đăng đã được xóa khỏi danh sách yêu thích.');
    } else {
        list.push(id);
        isSaved = true;
        showToast('Đã lưu bất động sản!', 'Bạn có thể xem lại trong mục BĐS đã lưu.');
    }

    try {
        localStorage.setItem(FAVORITES_KEY, JSON.stringify(list));
    } catch (e) {
        console.error(e);
    }

    updateFavoritesUI();
    return isSaved;
}

function updateFavoritesUI() {
    const list = getFavorites();
    const countBadge = document.getElementById('favCountBadge');
    if (countBadge) {
        countBadge.innerText = list.length;
        if (list.length > 0) countBadge.classList.remove('hidden');
        else countBadge.classList.add('hidden');
    }

    // Update heart icons across the page
    document.querySelectorAll('[data-fav-id]').forEach(btn => {
        const id = btn.getAttribute('data-fav-id');
        const heartIcon = btn.querySelector('svg, i');
        if (list.includes(id)) {
            btn.classList.add('text-primary');
            if (heartIcon) heartIcon.setAttribute('fill', 'currentColor');
        } else {
            btn.classList.remove('text-primary');
            if (heartIcon) heartIcon.setAttribute('fill', 'none');
        }
    });
}

function toggleCategoryDropdown() {
    const menu = document.getElementById('categoryDropdownMenu');
    if (menu) menu.classList.toggle('hidden');
}

function toggleMobileMenu() {
    const sheet = document.getElementById('mobileMenuSheet');
    if (sheet) sheet.classList.toggle('hidden');
}

function showToast(title, desc) {
    const toast = document.getElementById('liveToast');
    if (!toast) return;

    document.getElementById('toastTitle').innerText = title;
    document.getElementById('toastDesc').innerText = desc || '';

    toast.classList.remove('hidden', 'translate-y-10', 'opacity-0');
    toast.classList.add('translate-y-0', 'opacity-100');

    setTimeout(() => {
        toast.classList.add('translate-y-10', 'opacity-0');
        setTimeout(() => toast.classList.add('hidden'), 300);
    }, 3500);
}

// Close dropdown on outside click
document.addEventListener('click', (e) => {
    const btn = document.getElementById('categoryDropdownBtn');
    const menu = document.getElementById('categoryDropdownMenu');
    if (btn && menu && !btn.contains(e.target) && !menu.contains(e.target)) {
        menu.classList.add('hidden');
    }
});

document.addEventListener('DOMContentLoaded', () => {
    if (window.lucide) {
        window.lucide.createIcons();
    }
    updateFavoritesUI();

    // Auto dismiss flash alerts
    setTimeout(() => {
        document.querySelectorAll('.flash-alert').forEach(el => {
            el.style.opacity = '0';
            setTimeout(() => el.remove(), 400);
        });
    }, 4000);
});
