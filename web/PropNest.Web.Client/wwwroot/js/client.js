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

function dismissToast(el) {
    const toast = el.closest('.toast-item') || el;
    if (!toast) return;
    toast.style.opacity = '0';
    toast.style.transform = 'translateY(-12px) scale(0.96)';
    setTimeout(() => {
        if (toast.parentElement) toast.remove();
    }, 300);
}

function showToast(title, desc, type = 'success') {
    let container = document.getElementById('toastContainer');
    if (!container) {
        container = document.createElement('div');
        container.id = 'toastContainer';
        container.className = 'fixed top-6 right-6 z-[9999] flex flex-col gap-3 pointer-events-none max-w-sm sm:max-w-md w-full px-4 sm:px-0';
        document.body.appendChild(container);
    }

    const isSuccess = type === 'success';
    const toast = document.createElement('div');
    toast.className = `toast-item pointer-events-auto flex items-start gap-3 rounded-2xl border ${isSuccess ? 'border-emerald-400/40' : 'border-rose-400/40'} bg-white/95 p-4 shadow-2xl backdrop-blur-md transition-all duration-300 ring-1 ${isSuccess ? 'ring-emerald-500/15' : 'ring-rose-500/15'} text-neutral-900`;
    toast.innerHTML = `
        <div class="grid size-8 shrink-0 place-items-center rounded-xl ${isSuccess ? 'bg-emerald-100 text-emerald-700' : 'bg-rose-100 text-rose-700'}">
            <i data-lucide="${isSuccess ? 'check-circle-2' : 'alert-triangle'}" class="size-5"></i>
        </div>
        <div class="flex-1 min-w-0 pt-0.5">
            <p class="text-xs font-bold ${isSuccess ? 'text-emerald-900' : 'text-rose-900'}">${title}</p>
            ${desc ? `<p class="text-xs text-neutral-700 mt-0.5 leading-relaxed">${desc}</p>` : ''}
        </div>
        <button type="button" onclick="dismissToast(this)" class="shrink-0 text-neutral-400 hover:text-neutral-700 p-1 rounded-lg">
            <i data-lucide="x" class="size-4"></i>
        </button>
    `;

    container.appendChild(toast);
    if (window.lucide) window.lucide.createIcons();

    setTimeout(() => dismissToast(toast), 4500);
}

// User Profile Modal Handlers
function openProfileModal() {
    const modal = document.getElementById('userProfileModal');
    if (modal) {
        modal.classList.remove('hidden');
        if (window.lucide) window.lucide.createIcons();
    }
}

function closeProfileModal() {
    const modal = document.getElementById('userProfileModal');
    if (modal) modal.classList.add('hidden');
}

// Close modal on outside click
document.addEventListener('click', (e) => {
    const modal = document.getElementById('userProfileModal');
    if (modal && !modal.classList.contains('hidden') && e.target === modal) {
        closeProfileModal();
    }
});

// Close modal on Escape
document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') closeProfileModal();
});

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

    // Global Back to Top Button
    const backToTopBtn = document.getElementById('backToTopBtn');
    if (backToTopBtn) {
        window.addEventListener('scroll', () => {
            if (window.scrollY > 300) {
                backToTopBtn.classList.remove('hidden');
                backToTopBtn.classList.add('grid');
            } else {
                backToTopBtn.classList.add('hidden');
                backToTopBtn.classList.remove('grid');
            }
        });

        backToTopBtn.addEventListener('click', () => {
            window.scrollTo({
                top: 0,
                behavior: 'smooth'
            });
        });
    }

    // Auto dismiss any server-rendered toast popups after 4.5s
    document.querySelectorAll('.toast-item').forEach(toast => {
        setTimeout(() => dismissToast(toast), 4500);
    });
});
