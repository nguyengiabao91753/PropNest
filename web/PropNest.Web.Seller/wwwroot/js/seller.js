// Initialize Lucide icons
document.addEventListener("DOMContentLoaded", function () {
    if (window.lucide) {
        window.lucide.createIcons();
    }

    // Auto dismiss flash alerts
    setTimeout(function () {
        const alerts = document.querySelectorAll(".flash-alert");
        alerts.forEach(function (el) {
            el.style.opacity = "0";
            setTimeout(function () { el.remove(); }, 300);
        });
    }, 4500);
});

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
