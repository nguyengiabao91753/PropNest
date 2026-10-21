// Initialize Lucide icons
document.addEventListener("DOMContentLoaded", function () {
    if (window.lucide) {
        window.lucide.createIcons();
    }

    // Auto dismiss toast popups
    setTimeout(function () {
        const toasts = document.querySelectorAll(".toast-item");
        toasts.forEach(function (el) {
            el.style.opacity = "0";
            el.style.transform = "translateX(20px)";
            setTimeout(function () { el.remove(); }, 300);
        });
    }, 4500);

    // Global Back to Top Button
    const backToTopBtn = document.getElementById("backToTopBtn");
    if (backToTopBtn) {
        window.addEventListener("scroll", () => {
            if (window.scrollY > 300) {
                backToTopBtn.classList.remove("hidden");
                backToTopBtn.classList.add("grid");
            } else {
                backToTopBtn.classList.add("hidden");
                backToTopBtn.classList.remove("grid");
            }
        });

        backToTopBtn.addEventListener("click", () => {
            window.scrollTo({
                top: 0,
                behavior: "smooth"
            });
        });
    }
});

function dismissToast(btn) {
    const toast = btn.closest(".toast-item");
    if (toast) {
        toast.style.opacity = "0";
        toast.style.transform = "translateX(20px)";
        setTimeout(function () { toast.remove(); }, 300);
    }
}

// Top-up modal helpers
function openTopUpModal() {
    const modal = document.getElementById("topUpModal");
    if (modal) {
        modal.classList.remove("hidden");
        modal.classList.add("flex");
    }
}

function closeTopUpModal() {
    const modal = document.getElementById("topUpModal");
    if (modal) {
        modal.classList.add("hidden");
        modal.classList.remove("flex");
    }
}

function setTopUpAmount(val) {
    const input = document.getElementById("topUpAmountInput");
    if (input) {
        input.value = val;
    }
}

// User dropdown menu toggle
function toggleUserDropdown() {
    const menu = document.getElementById("userDropdownMenu");
    if (menu) {
        menu.classList.toggle("hidden");
    }
}

// Close dropdown if clicked outside
document.addEventListener("click", function (e) {
    const btn = document.getElementById("userDropdownBtn");
    const menu = document.getElementById("userDropdownMenu");
    if (btn && menu && !btn.contains(e.target) && !menu.contains(e.target)) {
        menu.classList.add("hidden");
    }
});
