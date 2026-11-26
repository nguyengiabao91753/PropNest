/**
 * PropNest Unified Listing Image Manager
 * Hỗ trợ:
 *  - Hiển thị chung cả ảnh đã lưu (existing) và ảnh mới chọn (new) trong cùng một lưới
 *  - Kéo thả (Drag & Drop) để đổi thứ tự ảnh trực tiếp
 *  - Nút di chuyển nhanh Trái / Phải và Đặt làm ảnh bìa (#1)
 *  - Xóa từng ảnh (cả ảnh đã lưu và ảnh mới chọn)
 *  - Chọn ảnh nhiều đợt (append, không làm mất ảnh đã chọn trước đó) + kéo thả tệp từ máy tính
 *  - Tự động đồng bộ DataTransfer(fileInput.files) + hidden JSON manifest khi submit form
 */
(function () {
    'use strict';

    const ALLOWED_EXTS = ['.jpg', '.jpeg', '.png', '.webp'];
    const MAX_FILE_BYTES = 10 * 1024 * 1024; // 10 MB

    function escapeHtml(str) {
        return String(str || '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function hasAllowedExtension(fileName) {
        const lower = (fileName || '').toLowerCase();
        return ALLOWED_EXTS.some(ext => lower.endsWith(ext));
    }

    window.createListingImageManager = function createListingImageManager(options) {
        const dropZone = document.getElementById(options.dropZoneId);
        const fileInput = document.getElementById(options.fileInputId);
        const gridEl = document.getElementById(options.gridId);
        const gallerySectionEl = options.gallerySectionId ? document.getElementById(options.gallerySectionId) : null;
        const countEl = options.countId ? document.getElementById(options.countId) : null;
        const manifestInput = document.getElementById(options.manifestInputId);
        const modifiedInput = options.modifiedInputId ? document.getElementById(options.modifiedInputId) : null;
        const errorBox = options.errorBoxId ? document.getElementById(options.errorBoxId) : null;
        const clearNewBtn = options.clearNewBtnId ? document.getElementById(options.clearNewBtnId) : null;
        const formEl = options.formId ? document.getElementById(options.formId) : (fileInput ? fileInput.closest('form') : null);
        const maxImages = options.maxImages || 15;
        const onChange = typeof options.onChange === 'function' ? options.onChange : null;

        let uidSeq = 1;
        let dragSourceIndex = null;
        let isModified = false;

        const initialExisting = Array.isArray(options.initialImages) ? options.initialImages : [];
        const items = initialExisting.map(img => ({
            uid: uidSeq++,
            kind: 'existing',
            imageId: Number(img.imageId ?? img.ImageId),
            url: img.url ?? img.Url ?? '',
            name: 'Đã lưu'
        }));

        const initialSignature = computeSignature(items);

        function computeSignature(list) {
            return JSON.stringify(list.map(it => it.kind === 'existing' ? `e:${it.imageId}` : `n:${it.uid}`));
        }

        function showError(msg) {
            if (!errorBox) return;
            if (!msg) {
                errorBox.textContent = '';
                errorBox.classList.add('hidden');
                return;
            }
            errorBox.textContent = msg;
            errorBox.classList.remove('hidden');
        }

        function markModifiedIfNeeded() {
            const changed = computeSignature(items) !== initialSignature;
            isModified = changed;
            if (modifiedInput) {
                modifiedInput.value = changed ? 'true' : 'false';
            }
        }

        function syncToForm() {
            const manifestTokens = [];
            const dt = new DataTransfer();
            let newIdx = 0;

            items.forEach(item => {
                if (item.kind === 'existing') {
                    manifestTokens.push(`existing:${item.imageId}`);
                } else if (item.kind === 'new' && item.file) {
                    dt.items.add(item.file);
                    manifestTokens.push(`new:${newIdx}`);
                    newIdx++;
                }
            });

            if (manifestInput) {
                manifestInput.value = JSON.stringify(manifestTokens);
            }

            if (fileInput) {
                try {
                    fileInput.files = dt.files;
                } catch (e) {
                    // Trình duyệt cũ không hỗ trợ gán trực tiếp FileList
                }
            }

            markModifiedIfNeeded();
            return {
                total: items.length,
                existingCount: items.filter(x => x.kind === 'existing').length,
                newCount: newIdx,
                removedCount: Math.max(0, initialExisting.length - items.filter(x => x.kind === 'existing').length),
                coverUrl: items.length > 0 ? items[0].url : ''
            };
        }

        function moveItem(fromIdx, toIdx) {
            if (fromIdx < 0 || fromIdx >= items.length || toIdx < 0 || toIdx >= items.length || fromIdx === toIdx) {
                return;
            }
            const [moved] = items.splice(fromIdx, 1);
            items.splice(toIdx, 0, moved);
            render();
        }

        function removeItem(idx) {
            if (idx < 0 || idx >= items.length) return;
            const [removed] = items.splice(idx, 1);
            if (removed && removed.kind === 'new' && removed.url && removed.url.startsWith('blob:')) {
                URL.revokeObjectURL(removed.url);
            }
            showError('');
            render();
        }

        function setAsCover(idx) {
            if (idx <= 0 || idx >= items.length) return;
            moveItem(idx, 0);
        }

        function clearNewFiles() {
            for (let i = items.length - 1; i >= 0; i--) {
                if (items[i].kind === 'new') {
                    if (items[i].url && items[i].url.startsWith('blob:')) {
                        URL.revokeObjectURL(items[i].url);
                    }
                    items.splice(i, 1);
                }
            }
            showError('');
            render();
        }

        function addFiles(fileList) {
            if (!fileList || fileList.length === 0) return;
            const errors = [];
            const incoming = Array.from(fileList);

            for (const file of incoming) {
                if (items.length >= maxImages) {
                    errors.push(`Mỗi tin đăng chỉ được chứa tối đa ${maxImages} hình ảnh.`);
                    break;
                }
                if (!hasAllowedExtension(file.name)) {
                    errors.push(`Tệp "${file.name}" không đúng định dạng (chỉ hỗ trợ JPG, PNG, WEBP).`);
                    continue;
                }
                if (file.size <= 0) {
                    errors.push(`Tệp "${file.name}" bị rỗng.`);
                    continue;
                }
                if (file.size > MAX_FILE_BYTES) {
                    errors.push(`Tệp "${file.name}" vượt quá dung lượng tối đa 10MB.`);
                    continue;
                }

                const objectUrl = URL.createObjectURL(file);
                items.push({
                    uid: uidSeq++,
                    kind: 'new',
                    file: file,
                    url: objectUrl,
                    name: file.name
                });
            }

            showError(errors.length > 0 ? errors.join(' ') : '');
            render();
        }

        function render() {
            const summary = syncToForm();

            if (countEl) {
                countEl.textContent = `${summary.total} / ${maxImages}`;
            }

            if (clearNewBtn) {
                if (summary.newCount > 0) clearNewBtn.classList.remove('hidden');
                else clearNewBtn.classList.add('hidden');
            }

            if (gallerySectionEl) {
                if (items.length > 0) gallerySectionEl.classList.remove('hidden');
                else gallerySectionEl.classList.add('hidden');
            }

            if (gridEl) {
                gridEl.innerHTML = '';
                items.forEach((item, idx) => {
                    const isCover = idx === 0;
                    const card = document.createElement('div');
                    card.className = [
                        'propnest-img-tile relative rounded-xl overflow-hidden border bg-neutral-100 shadow-xs transition-all select-none cursor-grab active:cursor-grabbing group',
                        isCover ? 'border-primary ring-2 ring-primary/35' : 'border-border hover:border-primary/60'
                    ].join(' ');
                    card.setAttribute('draggable', 'true');
                    card.dataset.index = String(idx);

                    const coverBadge = isCover
                        ? `<span class="inline-flex items-center gap-0.5 rounded-md bg-primary px-1.5 py-0.5 text-[9px] font-bold text-white shadow-xs">★ Ảnh bìa</span>`
                        : `<span class="inline-flex items-center rounded-md bg-black/70 px-1.5 py-0.5 text-[9px] font-bold text-white">#${idx + 1}</span>`;

                    const kindBadge = item.kind === 'new'
                        ? `<span class="rounded bg-blue-600/90 px-1.5 py-0.5 text-[8px] font-semibold text-white">Mới</span>`
                        : `<span class="rounded bg-emerald-700/85 px-1.5 py-0.5 text-[8px] font-semibold text-white">Đã lưu</span>`;

                    card.innerHTML = `
                        <div class="relative aspect-square w-full overflow-hidden bg-neutral-100">
                            <img src="${escapeHtml(item.url)}" alt="${escapeHtml(item.name)}"
                                 class="size-full object-cover pointer-events-none" />
                            <div class="absolute top-1.5 left-1.5 flex items-center gap-1">
                                ${coverBadge}
                            </div>
                            <button type="button" data-action="remove" data-idx="${idx}"
                                    title="Xóa ảnh này"
                                    class="absolute top-1.5 right-1.5 grid size-6 place-items-center rounded-full bg-rose-600/90 text-white hover:bg-rose-700 shadow-xs transition-colors">
                                <span class="text-xs font-bold leading-none">×</span>
                            </button>
                            <div class="absolute bottom-1.5 left-1.5">
                                ${kindBadge}
                            </div>
                        </div>
                        <div class="flex items-center justify-between gap-1 bg-white px-1.5 py-1 border-t border-border/70">
                            <button type="button" data-action="left" data-idx="${idx}" ${idx === 0 ? 'disabled' : ''}
                                    title="Chuyển sang trái"
                                    class="grid size-6 place-items-center rounded-md border border-border text-neutral-700 hover:border-primary hover:text-primary disabled:opacity-30 disabled:cursor-not-allowed text-[11px] font-bold">
                                ←
                            </button>
                            <button type="button" data-action="cover" data-idx="${idx}" ${isCover ? 'disabled' : ''}
                                    title="${isCover ? 'Đang là ảnh bìa' : 'Đặt làm ảnh bìa (#1)'}"
                                    class="flex-1 rounded-md px-1.5 py-0.5 text-[10px] font-semibold transition-colors ${isCover ? 'bg-amber-50 text-amber-700 border border-amber-200 cursor-default' : 'border border-border text-neutral-700 hover:border-amber-400 hover:bg-amber-50 hover:text-amber-700'}">
                                ${isCover ? 'Ảnh bìa' : '★ Bìa'}
                            </button>
                            <button type="button" data-action="right" data-idx="${idx}" ${idx === items.length - 1 ? 'disabled' : ''}
                                    title="Chuyển sang phải"
                                    class="grid size-6 place-items-center rounded-md border border-border text-neutral-700 hover:border-primary hover:text-primary disabled:opacity-30 disabled:cursor-not-allowed text-[11px] font-bold">
                                →
                            </button>
                        </div>
                    `;

                    // Drag & Drop handlers
                    card.addEventListener('dragstart', function (e) {
                        dragSourceIndex = idx;
                        card.classList.add('opacity-50');
                        if (e.dataTransfer) {
                            e.dataTransfer.effectAllowed = 'move';
                            e.dataTransfer.setData('text/plain', String(idx));
                        }
                    });

                    card.addEventListener('dragover', function (e) {
                        e.preventDefault();
                        if (dragSourceIndex === null || dragSourceIndex === idx) return;
                        card.classList.add('ring-2', 'ring-primary', 'scale-[1.02]');
                        if (e.dataTransfer) e.dataTransfer.dropEffect = 'move';
                    });

                    card.addEventListener('dragleave', function () {
                        card.classList.remove('ring-2', 'ring-primary', 'scale-[1.02]');
                        if (idx === 0) card.classList.add('ring-2', 'ring-primary/35');
                    });

                    card.addEventListener('drop', function (e) {
                        e.preventDefault();
                        card.classList.remove('scale-[1.02]');
                        const from = dragSourceIndex;
                        dragSourceIndex = null;
                        if (from !== null && from !== idx) {
                            moveItem(from, idx);
                        }
                    });

                    card.addEventListener('dragend', function () {
                        dragSourceIndex = null;
                        card.classList.remove('opacity-50', 'scale-[1.02]');
                    });

                    gridEl.appendChild(card);
                });
            }

            if (onChange) {
                onChange(summary, items.slice());
            }
        }

        // Delegate button clicks inside gallery grid
        if (gridEl) {
            gridEl.addEventListener('click', function (e) {
                const btn = e.target.closest('button[data-action]');
                if (!btn || btn.disabled) return;
                e.preventDefault();
                e.stopPropagation();
                const action = btn.dataset.action;
                const idx = parseInt(btn.dataset.idx, 10);
                if (isNaN(idx)) return;

                if (action === 'remove') removeItem(idx);
                else if (action === 'left') moveItem(idx, idx - 1);
                else if (action === 'right') moveItem(idx, idx + 1);
                else if (action === 'cover') setAsCover(idx);
            });
        }

        // File input picker (append newly picked files instead of overwriting!)
        if (fileInput) {
            fileInput.addEventListener('change', function () {
                if (this.files && this.files.length > 0) {
                    const picked = Array.from(this.files);
                    addFiles(picked);
                }
            });
        }

        // OS File Drag & Drop onto DropZone
        if (dropZone) {
            ['dragenter', 'dragover'].forEach(evtName => {
                dropZone.addEventListener(evtName, function (e) {
                    if (dragSourceIndex !== null) return;
                    const hasFiles = e.dataTransfer && Array.from(e.dataTransfer.types || []).includes('Files');
                    if (!hasFiles) return;
                    e.preventDefault();
                    e.stopPropagation();
                    dropZone.classList.add('border-primary', 'bg-primary/5');
                });
            });

            ['dragleave', 'drop'].forEach(evtName => {
                dropZone.addEventListener(evtName, function (e) {
                    dropZone.classList.remove('border-primary', 'bg-primary/5');
                    if (evtName === 'drop' && dragSourceIndex === null) {
                        const hasFiles = e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files.length > 0;
                        if (!hasFiles) return;
                        e.preventDefault();
                        e.stopPropagation();
                        addFiles(e.dataTransfer.files);
                    }
                });
            });
        }

        if (clearNewBtn) {
            clearNewBtn.addEventListener('click', function () {
                clearNewFiles();
            });
        }

        if (formEl) {
            formEl.addEventListener('submit', function () {
                syncToForm();
            });
        }

        render();

        return {
            addFiles,
            moveItem,
            removeItem,
            setAsCover,
            clearNewFiles,
            syncToForm,
            getSummary: () => syncToForm(),
            isModified: () => isModified
        };
    };
})();
