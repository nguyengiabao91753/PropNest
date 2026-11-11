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

// Reject modal helpers
function openRejectModal(id, title) {
    const modal = document.getElementById("rejectModal");
    const listingIdInput = document.getElementById("rejectListingId");
    const targetTitleSpan = document.getElementById("rejectTargetTitle");

    if (modal && listingIdInput) {
        listingIdInput.value = id;
        if (targetTitleSpan) {
            targetTitleSpan.innerText = id + (title ? " - " + title : "");
        }
        modal.classList.remove("hidden");
        modal.classList.add("flex");
    }
}

function closeRejectModal() {
    const modal = document.getElementById("rejectModal");
    if (modal) {
        modal.classList.add("hidden");
        modal.classList.remove("flex");
    }
}
