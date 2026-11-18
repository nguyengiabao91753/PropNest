// Shared Vietnam Pre/Post-Merger Address Searchable Combobox & AI Content Suggestion for Listing Wizard
(function () {
    let currentAdminMode = 'PostMerger';
    let cachedProvincesPost = null;
    let cachedProvincesPre = null;
    let pendingConversionCandidate = null;
    const comboboxRegistry = new Map();

    window.initLocationAndAiHelpers = async function () {
        const modeInput = document.getElementById('inputAdminMode');
        currentAdminMode = (modeInput && modeInput.value === 'PreMerger') ? 'PreMerger' : 'PostMerger';

        initSearchableCombobox('selectProvince', 'Tìm Tỉnh / Thành phố...');
        initSearchableCombobox('selectDistrict', 'Tìm Quận / Huyện...');
        initSearchableCombobox('selectWard', 'Tìm Phường / Xã...');

        document.addEventListener('click', function (e) {
            comboboxRegistry.forEach(entry => {
                if (!entry.wrapper.contains(e.target)) {
                    closeCombobox(entry);
                }
            });
        });

        const selectProvince = document.getElementById('selectProvince');
        const selectDistrict = document.getElementById('selectDistrict');
        const selectWard = document.getElementById('selectWard');

        if (selectProvince) selectProvince.addEventListener('change', onProvinceChanged);
        if (selectDistrict) selectDistrict.addEventListener('change', onDistrictChanged);
        if (selectWard) selectWard.addEventListener('change', onWardChanged);

        await renderAdminModeUI(currentAdminMode, true);
        updateSpecsHint();
    };

    // ==========================================
    // Searchable Combobox Component
    // ==========================================
    function initSearchableCombobox(selectId, searchPlaceholder) {
        const selectEl = document.getElementById(selectId);
        if (!selectEl || comboboxRegistry.has(selectId)) return;

        selectEl.classList.add('sr-only');
        selectEl.tabIndex = -1;

        const wrapper = document.createElement('div');
        wrapper.className = 'relative';
        selectEl.parentNode.insertBefore(wrapper, selectEl);
        wrapper.appendChild(selectEl);

        const triggerBtn = document.createElement('button');
        triggerBtn.type = 'button';
        triggerBtn.id = `${selectId}_trigger`;
        triggerBtn.className = 'w-full flex items-center justify-between gap-2 rounded-xl border border-border bg-surface px-3 py-2.5 text-xs text-left outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 transition-all text-neutral-900 font-medium';

        const triggerLabel = document.createElement('span');
        triggerLabel.className = 'truncate';
        triggerLabel.textContent = '-- Chọn --';

        const chevron = document.createElement('span');
        chevron.className = 'text-neutral-400 shrink-0 text-[10px]';
        chevron.textContent = '▼';

        triggerBtn.appendChild(triggerLabel);
        triggerBtn.appendChild(chevron);
        wrapper.appendChild(triggerBtn);

        const dropdown = document.createElement('div');
        dropdown.className = 'hidden absolute left-0 right-0 z-50 mt-1.5 rounded-xl border border-border bg-white shadow-xl overflow-hidden';

        const searchWrap = document.createElement('div');
        searchWrap.className = 'p-2 border-b border-border bg-neutral-50/80';

        const searchInput = document.createElement('input');
        searchInput.type = 'search';
        searchInput.placeholder = searchPlaceholder || 'Nhập để tìm nhanh...';
        searchInput.autocomplete = 'off';
        searchInput.className = 'w-full rounded-lg border border-border bg-white px-2.5 py-1.5 text-xs outline-none focus:border-primary focus:ring-1 focus:ring-primary/30 text-neutral-900';
        searchWrap.appendChild(searchInput);
        dropdown.appendChild(searchWrap);

        const listEl = document.createElement('div');
        listEl.className = 'max-h-56 overflow-y-auto py-1';
        dropdown.appendChild(listEl);

        wrapper.appendChild(dropdown);

        const entry = { selectEl, wrapper, triggerBtn, triggerLabel, dropdown, searchInput, listEl };
        comboboxRegistry.set(selectId, entry);

        triggerBtn.addEventListener('click', function () {
            const isOpen = !dropdown.classList.contains('hidden');
            comboboxRegistry.forEach(other => {
                if (other !== entry) closeCombobox(other);
            });
            if (isOpen) {
                closeCombobox(entry);
            } else {
                openCombobox(entry);
            }
        });

        searchInput.addEventListener('input', function () {
            renderComboboxOptions(entry, searchInput.value);
        });

        searchInput.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeCombobox(entry);
                triggerBtn.focus();
            } else if (e.key === 'Enter') {
                e.preventDefault();
                const firstBtn = listEl.querySelector('button[data-value]:not([data-empty])');
                if (firstBtn) firstBtn.click();
            }
        });

        refreshSearchableSelect(selectId);
    }

    function openCombobox(entry) {
        entry.searchInput.value = '';
        renderComboboxOptions(entry, '');
        entry.dropdown.classList.remove('hidden');
        setTimeout(() => entry.searchInput.focus(), 10);
    }

    function closeCombobox(entry) {
        entry.dropdown.classList.add('hidden');
    }

    function renderComboboxOptions(entry, filterText) {
        const normFilter = normalizeSimple(filterText);
        const options = Array.from(entry.selectEl.options);
        entry.listEl.innerHTML = '';

        let matchCount = 0;
        options.forEach(opt => {
            const text = opt.textContent || '';
            if (normFilter && opt.value !== '' && !normalizeSimple(text).includes(normFilter)) {
                return;
            }
            if (normFilter && opt.value === '') {
                return;
            }

            matchCount++;
            const itemBtn = document.createElement('button');
            itemBtn.type = 'button';
            itemBtn.dataset.value = opt.value;
            if (opt.value === '') itemBtn.dataset.empty = 'true';

            const isSelected = opt.value === entry.selectEl.value && opt.value !== '';
            itemBtn.className = isSelected
                ? 'w-full text-left px-3 py-2 text-xs bg-primary/10 text-primary font-bold hover:bg-primary/15 transition-colors flex items-center justify-between'
                : 'w-full text-left px-3 py-2 text-xs text-neutral-800 hover:bg-neutral-100 transition-colors flex items-center justify-between';

            const lbl = document.createElement('span');
            lbl.textContent = text;
            itemBtn.appendChild(lbl);

            if (isSelected) {
                const tick = document.createElement('span');
                tick.textContent = '✓';
                itemBtn.appendChild(tick);
            }

            itemBtn.addEventListener('click', function () {
                const changed = entry.selectEl.value !== opt.value;
                entry.selectEl.value = opt.value;
                syncTriggerLabel(entry);
                closeCombobox(entry);
                if (changed) {
                    entry.selectEl.dispatchEvent(new Event('change', { bubbles: true }));
                }
            });

            entry.listEl.appendChild(itemBtn);
        });

        if (matchCount === 0) {
            const emptyMsg = document.createElement('div');
            emptyMsg.className = 'px-3 py-3 text-xs text-muted-foreground text-center';
            emptyMsg.textContent = 'Không tìm thấy kết quả phù hợp.';
            entry.listEl.appendChild(emptyMsg);
        }
    }

    function refreshSearchableSelect(selectId) {
        const entry = comboboxRegistry.get(selectId);
        if (!entry) return;
        syncTriggerLabel(entry);
        if (!entry.dropdown.classList.contains('hidden')) {
            renderComboboxOptions(entry, entry.searchInput.value);
        }
    }

    function syncTriggerLabel(entry) {
        const selectedOpt = entry.selectEl.options[entry.selectEl.selectedIndex];
        const text = selectedOpt ? selectedOpt.textContent : '-- Chọn --';
        entry.triggerLabel.textContent = text;
        if (!entry.selectEl.value) {
            entry.triggerLabel.className = 'truncate text-muted-foreground font-normal';
        } else {
            entry.triggerLabel.className = 'truncate text-neutral-900 font-medium';
        }
    }

    // ==========================================
    // Specs Hint & Admin Mode Logic
    // ==========================================
    window.updateSpecsHint = function () {
        const propType = document.getElementById('inputPropertyType')?.value;
        const hint = document.getElementById('specsHintText');
        if (!hint) return;
        if (propType === 'Land') {
            hint.textContent = 'Đất nền thường không có tầng/phòng ngủ/ban công (có thể để trống).';
            hint.className = 'text-[11px] text-amber-700 font-medium';
        } else {
            hint.textContent = 'Giúp khách hàng lọc chính xác và tạo mô tả chi tiết hơn.';
            hint.className = 'text-[11px] text-muted-foreground';
        }
    };

    window.switchAdminMode = async function (mode) {
        if (mode === currentAdminMode) return;
        currentAdminMode = mode;
        const modeInput = document.getElementById('inputAdminMode');
        if (modeInput) modeInput.value = mode;

        setHiddenField('inputCity', '');
        setHiddenField('inputDistrict', '');
        setHiddenField('inputWard', '');
        setHiddenField('inputProvinceCode', '');
        setHiddenField('inputDistrictCode', '');
        setHiddenField('inputWardCode', '');
        hideLegacyConversionBanner();

        await renderAdminModeUI(mode, false);
        if (typeof window.updateLivePreview === 'function') {
            window.updateLivePreview();
        }
    };

    async function renderAdminModeUI(mode, restoreSavedValues) {
        const btnPost = document.getElementById('btnModePost');
        const btnPre = document.getElementById('btnModePre');
        const districtWrapper = document.getElementById('districtFieldWrapper');
        const grid = document.getElementById('adminSelectorsGrid');

        if (mode === 'PreMerger') {
            if (btnPre) btnPre.className = 'rounded-lg px-3 py-1.5 text-[11px] font-bold transition-all bg-primary text-white shadow-2xs';
            if (btnPost) btnPost.className = 'rounded-lg px-3 py-1.5 text-[11px] font-semibold transition-all text-neutral-600 hover:text-neutral-900';
            if (districtWrapper) districtWrapper.classList.remove('hidden');
            if (grid) {
                grid.classList.remove('sm:grid-cols-2');
                grid.classList.add('sm:grid-cols-3');
            }
        } else {
            if (btnPost) btnPost.className = 'rounded-lg px-3 py-1.5 text-[11px] font-bold transition-all bg-primary text-white shadow-2xs';
            if (btnPre) btnPre.className = 'rounded-lg px-3 py-1.5 text-[11px] font-semibold transition-all text-neutral-600 hover:text-neutral-900';
            if (districtWrapper) districtWrapper.classList.add('hidden');
            if (grid) {
                grid.classList.remove('sm:grid-cols-3');
                grid.classList.add('sm:grid-cols-2');
            }
        }

        await loadProvinces(mode, restoreSavedValues);
    }

    async function loadProvinces(mode, restoreSavedValues) {
        const selectProvince = document.getElementById('selectProvince');
        const selectDistrict = document.getElementById('selectDistrict');
        const selectWard = document.getElementById('selectWard');
        if (!selectProvince) return;

        selectProvince.innerHTML = '<option value="">-- Đang tải Tỉnh / Thành phố... --</option>';
        if (selectDistrict) selectDistrict.innerHTML = '<option value="">-- Chọn Quận / Huyện --</option>';
        if (selectWard) selectWard.innerHTML = '<option value="">-- Chọn Phường / Xã --</option>';
        refreshSearchableSelect('selectProvince');
        refreshSearchableSelect('selectDistrict');
        refreshSearchableSelect('selectWard');

        let provinces = (mode === 'PreMerger') ? cachedProvincesPre : cachedProvincesPost;
        if (!provinces) {
            const queryMode = (mode === 'PreMerger') ? 'pre' : 'post';
            const resp = await fetch(`/Properties/Locations/Provinces?mode=${queryMode}`);
            provinces = resp.ok ? await resp.json() : [];
            if (mode === 'PreMerger') cachedProvincesPre = provinces;
            else cachedProvincesPost = provinces;
        }

        selectProvince.innerHTML = '<option value="">-- Chọn Tỉnh / Thành phố --</option>';
        provinces.forEach(p => {
            const opt = document.createElement('option');
            opt.value = p.code;
            opt.textContent = p.name;
            opt.dataset.name = p.name;
            selectProvince.appendChild(opt);
        });

        if (restoreSavedValues) {
            const savedProvCode = document.getElementById('inputProvinceCode')?.value;
            const savedCityName = document.getElementById('inputCity')?.value;
            let matchedOption = null;

            if (savedProvCode) {
                matchedOption = Array.from(selectProvince.options).find(o => o.value === savedProvCode);
            }
            if (!matchedOption && savedCityName) {
                matchedOption = Array.from(selectProvince.options).find(o =>
                    normalizeSimple(o.dataset.name) === normalizeSimple(savedCityName) ||
                    normalizeSimple(o.dataset.name).includes(normalizeSimple(savedCityName)));
            }

            if (matchedOption) {
                selectProvince.value = matchedOption.value;
                setHiddenField('inputProvinceCode', matchedOption.value);
                setHiddenField('inputCity', matchedOption.dataset.name || matchedOption.textContent);
                refreshSearchableSelect('selectProvince');
                await loadChildrenForProvince(matchedOption.value, true);
                return;
            }
        }

        refreshSearchableSelect('selectProvince');
    }

    async function onProvinceChanged() {
        const selectProvince = document.getElementById('selectProvince');
        const selectedOpt = selectProvince.options[selectProvince.selectedIndex];
        const provCode = selectProvince.value;
        const provName = selectedOpt?.dataset?.name || '';

        setHiddenField('inputProvinceCode', provCode);
        setHiddenField('inputCity', provName);
        setHiddenField('inputDistrictCode', '');
        setHiddenField('inputDistrict', '');
        setHiddenField('inputWardCode', '');
        setHiddenField('inputWard', '');
        hideLegacyConversionBanner();
        clearFieldValidation('City');
        if (typeof window.unmarkError === 'function') {
            window.unmarkError(document.getElementById('inputCity'));
        }

        if (provCode) {
            await loadChildrenForProvince(provCode, false);
        } else {
            const selectDistrict = document.getElementById('selectDistrict');
            const selectWard = document.getElementById('selectWard');
            if (selectDistrict) selectDistrict.innerHTML = '<option value="">-- Chọn Quận / Huyện --</option>';
            if (selectWard) selectWard.innerHTML = '<option value="">-- Chọn Phường / Xã --</option>';
            refreshSearchableSelect('selectDistrict');
            refreshSearchableSelect('selectWard');
        }

        if (typeof window.updateLivePreview === 'function') window.updateLivePreview();
    }

    async function loadChildrenForProvince(provinceCode, restoreSavedValues) {
        const selectDistrict = document.getElementById('selectDistrict');
        const selectWard = document.getElementById('selectWard');

        if (currentAdminMode === 'PreMerger') {
            if (!selectDistrict) return;
            selectDistrict.innerHTML = '<option value="">-- Đang tải Quận / Huyện... --</option>';
            if (selectWard) selectWard.innerHTML = '<option value="">-- Chọn Phường / Xã --</option>';
            refreshSearchableSelect('selectDistrict');
            refreshSearchableSelect('selectWard');

            const resp = await fetch(`/Properties/Locations/Districts/${provinceCode}`);
            const districts = resp.ok ? await resp.json() : [];

            selectDistrict.innerHTML = '<option value="">-- Chọn Quận / Huyện --</option>';
            districts.forEach(d => {
                const opt = document.createElement('option');
                opt.value = d.code;
                opt.textContent = d.name;
                opt.dataset.name = d.name;
                selectDistrict.appendChild(opt);
            });

            if (restoreSavedValues) {
                const savedDistCode = document.getElementById('inputDistrictCode')?.value;
                const savedDistName = document.getElementById('inputDistrict')?.value;
                let matched = null;
                if (savedDistCode) {
                    matched = Array.from(selectDistrict.options).find(o => o.value === savedDistCode);
                }
                if (!matched && savedDistName) {
                    matched = Array.from(selectDistrict.options).find(o =>
                        normalizeSimple(o.dataset.name) === normalizeSimple(savedDistName));
                }
                if (matched) {
                    selectDistrict.value = matched.value;
                    setHiddenField('inputDistrictCode', matched.value);
                    setHiddenField('inputDistrict', matched.dataset.name || matched.textContent);
                    refreshSearchableSelect('selectDistrict');
                    await loadWardsForDistrict(matched.value, true);
                    return;
                }
            }
            refreshSearchableSelect('selectDistrict');
        } else {
            if (!selectWard) return;
            selectWard.innerHTML = '<option value="">-- Đang tải Phường / Xã... --</option>';
            refreshSearchableSelect('selectWard');
            const resp = await fetch(`/Properties/Locations/ProvinceWards/${provinceCode}`);
            const wards = resp.ok ? await resp.json() : [];

            selectWard.innerHTML = '<option value="">-- Chọn Phường / Xã --</option>';
            wards.forEach(w => {
                const opt = document.createElement('option');
                opt.value = w.code;
                opt.textContent = w.name;
                opt.dataset.name = w.name;
                selectWard.appendChild(opt);
            });

            if (restoreSavedValues) {
                const savedWardCode = document.getElementById('inputWardCode')?.value;
                const savedWardName = document.getElementById('inputWard')?.value;
                let matched = null;
                if (savedWardCode) {
                    matched = Array.from(selectWard.options).find(o => o.value === savedWardCode);
                }
                if (!matched && savedWardName) {
                    matched = Array.from(selectWard.options).find(o =>
                        normalizeSimple(o.dataset.name) === normalizeSimple(savedWardName));
                }
                if (matched) {
                    selectWard.value = matched.value;
                    setHiddenField('inputWardCode', matched.value);
                    setHiddenField('inputWard', matched.dataset.name || matched.textContent);
                }
            }
            refreshSearchableSelect('selectWard');
        }
    }

    async function onDistrictChanged() {
        const selectDistrict = document.getElementById('selectDistrict');
        const selectedOpt = selectDistrict.options[selectDistrict.selectedIndex];
        const distCode = selectDistrict.value;
        const distName = selectedOpt?.dataset?.name || '';

        setHiddenField('inputDistrictCode', distCode);
        setHiddenField('inputDistrict', distName);
        setHiddenField('inputWardCode', '');
        setHiddenField('inputWard', '');
        hideLegacyConversionBanner();
        clearFieldValidation('District');
        if (typeof window.unmarkError === 'function') {
            window.unmarkError(document.getElementById('inputDistrict'));
        }

        if (distCode) {
            await loadWardsForDistrict(distCode, false);
        } else {
            const selectWard = document.getElementById('selectWard');
            if (selectWard) selectWard.innerHTML = '<option value="">-- Chọn Phường / Xã --</option>';
            refreshSearchableSelect('selectWard');
        }

        if (typeof window.updateLivePreview === 'function') window.updateLivePreview();
    }

    async function loadWardsForDistrict(districtCode, restoreSavedValues) {
        const selectWard = document.getElementById('selectWard');
        if (!selectWard) return;

        selectWard.innerHTML = '<option value="">-- Đang tải Phường / Xã... --</option>';
        refreshSearchableSelect('selectWard');

        const resp = await fetch(`/Properties/Locations/DistrictWards/${districtCode}`);
        const wards = resp.ok ? await resp.json() : [];

        selectWard.innerHTML = '<option value="">-- Chọn Phường / Xã --</option>';
        wards.forEach(w => {
            const opt = document.createElement('option');
            opt.value = w.code;
            opt.textContent = w.name;
            opt.dataset.name = w.name;
            selectWard.appendChild(opt);
        });

        if (restoreSavedValues) {
            const savedWardCode = document.getElementById('inputWardCode')?.value;
            const savedWardName = document.getElementById('inputWard')?.value;
            let matched = null;
            if (savedWardCode) {
                matched = Array.from(selectWard.options).find(o => o.value === savedWardCode);
            }
            if (!matched && savedWardName) {
                matched = Array.from(selectWard.options).find(o =>
                    normalizeSimple(o.dataset.name) === normalizeSimple(savedWardName));
            }
            if (matched) {
                selectWard.value = matched.value;
                setHiddenField('inputWardCode', matched.value);
                setHiddenField('inputWard', matched.dataset.name || matched.textContent);
            }
        }
        refreshSearchableSelect('selectWard');
    }

    async function onWardChanged() {
        const selectWard = document.getElementById('selectWard');
        const selectedOpt = selectWard.options[selectWard.selectedIndex];
        const wardCode = selectWard.value;
        const wardName = selectedOpt?.dataset?.name || '';

        setHiddenField('inputWardCode', wardCode);
        setHiddenField('inputWard', wardName);
        clearFieldValidation('Ward');

        if (currentAdminMode === 'PreMerger' && wardCode) {
            await fetchLegacyToPostConversion(wardCode, wardName);
        } else {
            hideLegacyConversionBanner();
        }

        if (typeof window.updateLivePreview === 'function') window.updateLivePreview();
    }

    async function fetchLegacyToPostConversion(wardCode, wardName) {
        const provCode = document.getElementById('inputProvinceCode')?.value || '';
        const resp = await fetch(`/Properties/Locations/ConvertLegacy?legacyWardCode=${encodeURIComponent(wardCode)}&legacyWardName=${encodeURIComponent(wardName)}&legacyProvinceCode=${encodeURIComponent(provCode)}`);
        if (!resp.ok) {
            hideLegacyConversionBanner();
            return;
        }

        const mapping = await resp.json();
        if (!mapping || !mapping.newWard || !mapping.newProvince) {
            hideLegacyConversionBanner();
            return;
        }

        const oldDistrict = document.getElementById('inputDistrict')?.value || '';
        const oldProvince = document.getElementById('inputCity')?.value || '';
        const legacyText = [wardName, oldDistrict, oldProvince].filter(Boolean).join(', ');

        pendingConversionCandidate = {
            newProvinceCode: mapping.newProvince.code,
            newProvinceName: mapping.newProvince.name,
            newWardCode: mapping.newWard.code,
            newWardName: mapping.newWard.name,
            legacyText
        };

        const banner = document.getElementById('legacyConversionBanner');
        const textEl = document.getElementById('legacyConversionText');
        if (banner && textEl) {
            const mergedCount = (mapping.mergedFromLegacyNames || []).length;
            const mergedHint = mergedCount > 1 ? ` (sáp nhập từ ${mergedCount} phường/xã cũ)` : '';
            textEl.textContent = `${mapping.newWard.name}, ${mapping.newProvince.name}${mergedHint}`;
            banner.classList.remove('hidden');
        }
    }

    window.applyConvertedPostMerger = async function () {
        if (!pendingConversionCandidate) return;
        const c = pendingConversionCandidate;

        currentAdminMode = 'PostMerger';
        setHiddenField('inputAdminMode', 'PostMerger');
        setHiddenField('inputProvinceCode', c.newProvinceCode);
        setHiddenField('inputCity', c.newProvinceName);
        setHiddenField('inputDistrictCode', '');
        setHiddenField('inputDistrict', '');
        setHiddenField('inputWardCode', c.newWardCode);
        setHiddenField('inputWard', c.newWardName);
        setLegacyAddressDisplay(c.legacyText);

        hideLegacyConversionBanner();
        await renderAdminModeUI('PostMerger', true);
        if (typeof window.updateLivePreview === 'function') window.updateLivePreview();
    };

    window.parseAndFillAddress = async function () {
        const input = document.getElementById('pasteAddressInput');
        const btn = document.getElementById('btnParseAddress');
        const raw = input?.value?.trim();

        if (!raw) {
            showParseFeedback('Vui lòng dán hoặc nhập chuỗi địa chỉ cần nhận dạng.', false);
            return;
        }

        if (btn) {
            btn.disabled = true;
            btn.innerHTML = '<span class="animate-spin inline-block mr-1">⏳</span> Đang nhận dạng...';
        }

        try {
            const resp = await fetch('/Properties/ParseAddress', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ rawAddress: raw })
            });

            const data = await resp.json();
            if (!resp.ok || !data.matched) {
                showParseFeedback(data.explanation || 'Không thể tự động khớp địa chỉ. Vui lòng chọn thủ công bên dưới.', false);
                return;
            }

            currentAdminMode = 'PostMerger';
            setHiddenField('inputAdminMode', 'PostMerger');
            setHiddenField('inputProvinceCode', data.suggestedProvinceCode || '');
            setHiddenField('inputCity', data.suggestedProvinceName || '');
            setHiddenField('inputDistrictCode', '');
            setHiddenField('inputDistrict', '');
            setHiddenField('inputWardCode', data.suggestedWardCode || '');
            setHiddenField('inputWard', data.suggestedWardName || '');
            setLegacyAddressDisplay(data.legacyAddress || '');

            if (data.streetAddress) {
                const addressInput = document.getElementById('inputAddress');
                if (addressInput) {
                    addressInput.value = data.streetAddress;
                    clearFieldValidation('Address');
                    if (typeof window.unmarkError === 'function') window.unmarkError(addressInput);
                }
            }

            hideLegacyConversionBanner();
            await renderAdminModeUI('PostMerger', true);
            showParseFeedback(data.explanation || 'Đã nhận dạng và tự động chọn địa chỉ thành công!', true);

            if (typeof window.updateLivePreview === 'function') window.updateLivePreview();
        } catch (err) {
            showParseFeedback('Có lỗi kết nối khi nhận dạng địa chỉ. Vui lòng thử lại.', false);
        } finally {
            if (btn) {
                btn.disabled = false;
                btn.innerHTML = '<i data-lucide="search-check" class="size-4"></i> <span>Nhận dạng địa chỉ</span>';
                if (window.lucide) window.lucide.createIcons();
            }
        }
    };

    window.clearLegacyAddress = function () {
        setLegacyAddressDisplay('');
    };

    // ==========================================
    // AI Suggest SEO Title & Detailed Description
    // ==========================================
    window.suggestAiDescription = async function () {
        const btn = document.getElementById('btnAiSuggestDesc');
        const statusEl = document.getElementById('aiDescStatus');

        const payload = {
            title: document.getElementById('inputTitle')?.value || '',
            propertyType: document.getElementById('inputPropertyType')?.value || 'Apartment',
            listingType: document.querySelector('input[name="ListingType"]:checked')?.value || 'sale',
            price: parseNullableFloat(document.getElementById('inputPrice')?.value),
            area: parseNullableFloat(document.getElementById('inputArea')?.value),
            city: document.getElementById('inputCity')?.value || '',
            district: document.getElementById('inputDistrict')?.value || '',
            ward: document.getElementById('inputWard')?.value || '',
            address: document.getElementById('inputAddress')?.value || '',
            legacyAddress: document.getElementById('inputLegacyAddress')?.value || '',
            administrativeMode: document.getElementById('inputAdminMode')?.value || 'PostMerger',
            floors: parseNullableInt(document.getElementById('inputFloors')?.value),
            bedrooms: parseNullableInt(document.getElementById('inputBedrooms')?.value),
            bathrooms: parseNullableInt(document.getElementById('inputBathrooms')?.value),
            balconyDirection: document.getElementById('inputBalconyDirection')?.value || ''
        };

        if (btn) {
            btn.disabled = true;
            btn.innerHTML = '<span class="animate-spin inline-block mr-1">⏳</span> AI đang tạo nội dung...';
        }
        if (statusEl) {
            statusEl.textContent = 'Hệ thống AI đang phân tích thông tin để gợi ý tiêu đề chuẩn SEO & mô tả...';
            statusEl.className = 'text-[11px] text-primary font-medium mt-1.5 block';
        }

        try {
            const resp = await fetch('/Properties/SuggestDescription', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });

            if (!resp.ok) {
                throw new Error('Request failed');
            }

            const result = await resp.json();
            if (result) {
                if (result.suggestedTitle) {
                    const titleInput = document.getElementById('inputTitle');
                    if (titleInput) {
                        titleInput.value = result.suggestedTitle;
                        if (typeof window.unmarkError === 'function') {
                            window.unmarkError(titleInput);
                        }
                    }
                }

                if (result.suggestedDescription && window.quill) {
                    window.quill.setContents([]);
                    window.quill.clipboard.dangerouslyPasteHTML(0, result.suggestedDescription);
                    const hiddenDesc = document.getElementById('hiddenDescription');
                    if (hiddenDesc) {
                        hiddenDesc.value = window.quill.root.innerHTML;
                        if (typeof window.unmarkError === 'function') {
                            window.unmarkError(hiddenDesc);
                        }
                    }
                }

                if (typeof window.updateLivePreview === 'function') {
                    window.updateLivePreview();
                }

                if (statusEl) {
                    statusEl.textContent = '';
                    statusEl.className = 'hidden';
                }
            }
        } catch (err) {
            if (statusEl) {
                statusEl.textContent = 'Chưa thể tạo gợi ý lúc này. Vui lòng thử lại sau giây lát.';
                statusEl.className = 'text-[11px] text-rose-600 font-medium mt-1.5 block';
            }
        } finally {
            if (btn) {
                btn.disabled = false;
                btn.innerHTML = '<i data-lucide="sparkles" class="size-3.5"></i> <span>AI Gợi ý Tiêu đề & Mô tả</span>';
                if (window.lucide) window.lucide.createIcons();
            }
        }
    };

    function showParseFeedback(message, isSuccess) {
        const el = document.getElementById('addressParseFeedback');
        if (!el) return;
        el.textContent = message;
        el.className = isSuccess
            ? 'rounded-lg px-3 py-2 text-[11px] leading-relaxed bg-emerald-50 text-emerald-900 border border-emerald-200 font-medium'
            : 'rounded-lg px-3 py-2 text-[11px] leading-relaxed bg-rose-50 text-rose-800 border border-rose-200 font-medium';
    }

    function hideLegacyConversionBanner() {
        pendingConversionCandidate = null;
        document.getElementById('legacyConversionBanner')?.classList.add('hidden');
    }

    function setLegacyAddressDisplay(value) {
        setHiddenField('inputLegacyAddress', value);
        const box = document.getElementById('savedLegacyAddressBox');
        const text = document.getElementById('savedLegacyAddressText');
        if (!box || !text) return;
        if (value && value.trim()) {
            text.textContent = value;
            box.classList.remove('hidden');
        } else {
            text.textContent = '';
            box.classList.add('hidden');
        }
    }

    function setHiddenField(id, val) {
        const el = document.getElementById(id);
        if (el) el.value = val ?? '';
    }

    function clearFieldValidation(fieldName) {
        const span = document.querySelector(`[data-valmsg-for="${fieldName}"]`);
        if (span) span.textContent = '';
    }

    function normalizeSimple(s) {
        return (s || '').trim().toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd');
    }

    function parseNullableFloat(val) {
        const n = parseFloat(val);
        return isNaN(n) ? null : n;
    }

    function parseNullableInt(val) {
        const n = parseInt(val, 10);
        return isNaN(n) ? null : n;
    }
})();
