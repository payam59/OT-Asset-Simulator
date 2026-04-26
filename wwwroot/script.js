let socket;
let bbmdList = [];
let shouldReconnectSocket = true;
let reconnectTimer = null;
let userRole = 'read_only';
let latestTags = [];
let returnToAssetTagsAfterEdit = false;
let selectedAssetForTagModal = '';
let selectedAssetForInjectModal = '';

function renderAlarms(alarms) {
    const alarmList = document.getElementById('alarmList');
    if (!alarmList) return;
    if (alarms.length === 0) {
        alarmList.innerHTML = '<div class="list-group-item text-muted">No active alarms.</div>';
        return;
    }
    alarmList.innerHTML = alarms.map(a => `
        <div class="list-group-item list-group-item-danger">
            <div class="d-flex justify-content-between">
                <strong>${a.asset_name}</strong>
                <small>${new Date(a.created_at * 1000).toLocaleString()}</small>
            </div>
            <div>${a.message}</div>
        </div>
    `).join('');
}

function connectWebSocket() {
    const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    socket = new WebSocket(`${protocol}//${window.location.host}/ws`);
    socket.onmessage = () => {
        window.fetchAssets();
        refreshAlarms();
    };
    socket.onclose = function() {
        if (!shouldReconnectSocket) return;
        if (reconnectTimer) clearTimeout(reconnectTimer);
        reconnectTimer = setTimeout(connectWebSocket, 2000);
    };
}

function refreshAlarms() {
    if (typeof window.fetchAlarms === 'function') window.fetchAlarms();
}

function renderBBMDs(bbmds) {
    const grid = document.getElementById('bbmdGrid');
    if (!grid) return;
    if (bbmds.length === 0) {
        grid.innerHTML = '<div class="col-12"><p class="text-muted">No BBMD devices configured. Click "Manage BBMD" to add one.</p></div>';
        return;
    }
    grid.innerHTML = bbmds.map(b => `
        <div class="col-md-6 col-lg-4 mb-3">
            <div class="card border-success shadow-sm">
                <div class="card-body">
                    <div class="d-flex justify-content-between align-items-start mb-2">
                        <div>
                            <h6 class="mb-0"><i class="fas fa-server text-success me-1"></i>${b.name}</h6>
                            <small class="text-muted">${b.description || 'No description'}</small>
                        </div>
                        <span class="badge ${b.enabled ? 'bg-success' : 'bg-secondary'}">${b.enabled ? 'Active' : 'Disabled'}</span>
                    </div>
                    <div class="small mt-2 mb-2">
                        <div><strong>Port:</strong> ${b.port}</div>
                        <div><strong>Device ID:</strong> ${b.device_id}</div>
                        <div><strong>IP:</strong> ${b.ip_address}</div>
                    </div>
                    ${userRole === 'admin' || userRole === 'read_write' ? `<div class="d-flex gap-2">` : `<div class="d-flex gap-2 d-none">`}
                        <button class="btn btn-sm btn-outline-primary" onclick="window.editBBMD(${b.id})" type="button"><i class="fas fa-edit"></i> Edit</button>
                        <button class="btn btn-sm btn-outline-danger" onclick="window.deleteBBMD(${b.id})" type="button"><i class="fas fa-trash"></i> Delete</button>
                    </div>
                </div>
            </div>
        </div>
    `).join('');
}

function groupByAsset(tags) {
    const map = new Map();
    for (const t of tags) {
        const assetName = (t.asset_name || t.name || 'Unknown').trim();
        if (!map.has(assetName)) map.set(assetName, { name: assetName, tags: [] });
        map.get(assetName).tags.push(t);
    }
    return Array.from(map.values()).sort((a, b) => a.name.localeCompare(b.name));
}

function tagValueLabel(t) {
    const isDigital = t.sub_type === 'Digital';
    const isActive = t.current_value >= 0.5;
    return isDigital ? (isActive ? 'ON' : 'OFF') : Number(t.current_value || 0).toFixed(2);
}

function jsStringEscape(value) {
    return String(value ?? '')
        .replace(/\\/g, '\\\\')
        .replace(/'/g, "\\'");
}

function renderAssets(tags) {
    const grid = document.getElementById('assetGrid');
    if (!grid) return;

    const assets = groupByAsset(tags);
    if (assets.length === 0) {
        grid.innerHTML = '<div class="col-12"><p class="text-muted">No assets created yet.</p></div>';
        return;
    }

    grid.innerHTML = assets.map(asset => {
        const alarms = asset.tags.filter(t => t.alarm_state === 1);
        const inAlarm = alarms.length > 0;
        const cardBorderClass = inAlarm ? 'border-danger border-3 bg-danger-subtle' : '';
        const protocolSummary = [...new Set(asset.tags.map(t => (t.protocol || '').toUpperCase()))].join(', ');

        const tagPreview = asset.tags.slice(0, 4).map(t => {
            const inTagAlarm = t.alarm_state === 1;
            return `<span class="badge ${inTagAlarm ? 'bg-danger' : 'bg-secondary'} me-1 mb-1">${t.tag_name || t.name}: ${tagValueLabel(t)}</span>`;
        }).join('');

        const alarmTags = alarms.length
            ? `<div class="alert alert-danger py-1 px-2 mb-2 small"><i class="fas fa-exclamation-triangle me-1"></i>Alarm Tags: ${alarms.map(a => a.tag_name || a.name).join(', ')}</div>`
            : '';

        return `
        <div class="col-md-6 col-lg-4 mb-4">
            <div class="card asset-card p-3 shadow-sm ${cardBorderClass}">
                <div class="d-flex justify-content-between align-items-center mb-2">
                    <span class="badge bg-dark">${protocolSummary || 'N/A'}</span>
                    <div class="d-flex gap-1">
                        <button class="btn btn-sm btn-outline-primary" onclick="window.openAssetTagsModal('${jsStringEscape(encodeURIComponent(asset.name))}')"><i class="fas fa-tags me-1"></i>Edit Tags</button>
                        ${(userRole === 'admin' || userRole === 'read_write') ? `
                            <button class="btn btn-sm btn-outline-dark" onclick="window.openInjectModal('${jsStringEscape(encodeURIComponent(asset.name))}')" title="Inject values into this asset's tags" aria-label="Inject values">
                                <i class="fas fa-syringe"></i>
                            </button>
                            <button class="btn btn-sm btn-outline-danger" onclick="window.deleteAssetGroup('${jsStringEscape(encodeURIComponent(asset.name))}')" title="Delete asset and all tags" aria-label="Delete asset">
                                <i class="fas fa-trash me-1"></i>Delete
                            </button>` : ''}
                    </div>
                </div>
                ${alarmTags}
                <div class="text-center">
                    <h5 class="mb-1 fw-bold">${asset.name}</h5>
                    <small class="text-muted">${asset.tags.length} tag(s)</small>
                    <div class="mt-2">${tagPreview || '<span class="text-muted">No tags</span>'}</div>
                    ${asset.tags.length > 4 ? `<small class="text-muted">+${asset.tags.length - 4} more tag(s)</small>` : ''}
                </div>
            </div>
        </div>`;
    }).join('');
}

window.openAssetTagsModal = function(assetNameEncoded) {
    const assetName = decodeURIComponent(assetNameEncoded);
    selectedAssetForTagModal = assetName;
    const modal = bootstrap.Modal.getOrCreateInstance(document.getElementById('assetTagsModal'));
    document.getElementById('assetTagsTitle').textContent = `Asset Tags: ${assetName}`;
    const addTagButton = document.getElementById('assetTagsAddTagBtn');
    if (addTagButton) {
        addTagButton.style.display = (userRole === 'admin' || userRole === 'read_write') ? 'inline-block' : 'none';
    }

    const tags = latestTags.filter(t => (t.asset_name || '').trim() === assetName);
    const list = document.getElementById('assetTagsList');
    if (!tags.length) {
        list.innerHTML = '<div class="list-group-item text-muted">No tags found.</div>';
    } else {
        list.innerHTML = tags.map(t => {
            const alarm = t.alarm_state === 1;
            return `<div class="list-group-item d-flex justify-content-between align-items-center ${alarm ? 'list-group-item-danger' : ''}">
                <div>
                    <div><strong>${t.tag_name || t.name}</strong> <span class="badge bg-secondary">${(t.protocol || '').toUpperCase()}</span></div>
                    <small class="text-muted">Runtime: ${t.name} | Addr: ${t.address} | Value: ${tagValueLabel(t)}</small>
                    ${(t.protocol || '').toLowerCase() === 'dnp3' ? `<div><small class="text-muted">Kepware: ${t.dnp3_kepware_address || 'n/a'}</small></div>` : ''}
                    ${alarm ? `<div class="small text-danger">${t.alarm_message || 'Alarm active'}</div>` : ''}
                </div>
                <div class="d-flex gap-2">
                    <button class="btn btn-sm btn-outline-primary" onclick="window.openEditModal('${jsStringEscape(encodeURIComponent(t.name))}')"><i class="fas fa-edit"></i></button>
                    <button class="btn btn-sm btn-outline-danger" onclick="window.deleteAsset('${jsStringEscape(encodeURIComponent(t.name))}')"><i class="fas fa-trash"></i></button>
                </div>
            </div>`;
        }).join('');
    }

    modal.show();
};

function renderInjectModalList(assetName) {
    const list = document.getElementById('injectTagList');
    if (!list) return;

    const tags = latestTags
        .filter(t => (t.asset_name || '').trim() === assetName)
        .sort((a, b) => (a.tag_name || a.name || '').localeCompare(b.tag_name || b.name || ''));

    if (!tags.length) {
        list.innerHTML = '<div class="list-group-item text-muted">No tags found for this asset.</div>';
        return;
    }

    list.innerHTML = tags.map(t => {
        const displayName = t.tag_name || t.name;
        const isDigital = (t.sub_type || '').toLowerCase() === 'digital';
        const valueText = isDigital
            ? `${Number(t.current_value || 0) >= 0.5 ? 'ON (1)' : 'OFF (0)'}`
            : Number(t.current_value || 0).toFixed(2);

        if (isDigital) {
            return `<div class="list-group-item">
                <div class="d-flex justify-content-between align-items-center flex-wrap gap-2">
                    <div>
                        <div><strong>${displayName}</strong> <span class="badge bg-secondary">Digital</span></div>
                        <small class="text-muted">Current: ${valueText}</small>
                    </div>
                    <div class="btn-group">
                        <button class="btn btn-sm btn-outline-success" onclick="window.injectDigitalValue('${jsStringEscape(encodeURIComponent(t.name))}', 1)">ON</button>
                        <button class="btn btn-sm btn-outline-danger" onclick="window.injectDigitalValue('${jsStringEscape(encodeURIComponent(t.name))}', 0)">OFF</button>
                        <button class="btn btn-sm btn-outline-secondary" onclick="window.releaseTagToAuto('${jsStringEscape(encodeURIComponent(t.name))}')">Auto</button>
                    </div>
                </div>
            </div>`;
        }

        return `<div class="list-group-item">
            <div class="d-flex justify-content-between align-items-center flex-wrap gap-2">
                <div>
                    <div><strong>${displayName}</strong> <span class="badge bg-info text-dark">Analog</span></div>
                    <small class="text-muted">Current: ${valueText}</small>
                </div>
                <div class="d-flex gap-2 align-items-center">
                    <input type="number" step="any" class="form-control form-control-sm" id="inject_input_${encodeURIComponent(t.name)}" placeholder="Value" style="max-width: 140px;">
                    <button class="btn btn-sm btn-primary" onclick="window.injectAnalogValue('${jsStringEscape(encodeURIComponent(t.name))}')">
                        <i class="fas fa-bolt me-1"></i>Inject
                    </button>
                    <button class="btn btn-sm btn-outline-secondary" onclick="window.releaseTagToAuto('${jsStringEscape(encodeURIComponent(t.name))}')">Auto</button>
                </div>
            </div>
        </div>`;
    }).join('');
}

window.openInjectModal = function(assetNameEncoded) {
    const assetName = decodeURIComponent(assetNameEncoded);
    selectedAssetForInjectModal = assetName;
    const modal = bootstrap.Modal.getOrCreateInstance(document.getElementById('injectModal'));
    document.getElementById('injectModalTitle').textContent = `Inject Values: ${assetName}`;
    renderInjectModalList(assetName);
    modal.show();
};

window.injectDigitalValue = async function(tagNameEncoded, value) {
    const tagName = decodeURIComponent(tagNameEncoded);
    const response = await fetch(`/api/override/${encodeURIComponent(tagName)}?value=${value}`, { method: 'PUT' });
    if (!response.ok) {
        alert(`Failed to inject ${tagName}: ${await response.text()}`);
        return;
    }

    await window.fetchAssets();
    renderInjectModalList(selectedAssetForInjectModal);
};

window.injectAnalogValue = async function(tagNameEncoded) {
    const tagName = decodeURIComponent(tagNameEncoded);
    const inputId = `inject_input_${encodeURIComponent(tagName)}`;
    const valueRaw = document.getElementById(inputId)?.value;
    const numericValue = Number(valueRaw);
    if (!Number.isFinite(numericValue)) {
        alert('Please enter a valid numeric value.');
        return;
    }

    const response = await fetch(`/api/override/${encodeURIComponent(tagName)}?value=${numericValue}`, { method: 'PUT' });
    if (!response.ok) {
        alert(`Failed to inject ${tagName}: ${await response.text()}`);
        return;
    }

    await window.fetchAssets();
    renderInjectModalList(selectedAssetForInjectModal);
};

window.releaseTagToAuto = async function(tagNameEncoded) {
    const tagName = decodeURIComponent(tagNameEncoded);
    const response = await fetch(`/api/release/${encodeURIComponent(tagName)}`, { method: 'PUT' });
    if (!response.ok) {
        alert(`Failed to enable auto mode for ${tagName}: ${await response.text()}`);
        return;
    }

    await window.fetchAssets();
    renderInjectModalList(selectedAssetForInjectModal);
};

window.fetchBBMDs = async function() {
    try {
        const response = await fetch('/api/bbmd');
        bbmdList = await response.json();
        renderBBMDs(bbmdList);
        updateBBMDSelects();
        updateBBMDList();
    } catch (error) {
        console.error('Error fetching BBMDs:', error);
    }
};

window.fetchAssets = async function() {
    const response = await fetch('/api/assets');
    const assets = await response.json();
    latestTags = assets;
    renderAssets(assets);
    window.fetchAlarms();
};

window.fetchAlarms = async function() {
    try {
        const response = await fetch('/api/alarms?active_only=1');
        if (!response.ok) return;
        const alarms = await response.json();
        renderAlarms(alarms);
    } catch (error) {
        console.error('Failed to fetch alarms:', error);
    }
};

function updateBBMDSelects() {
    const selects = ['bbmd_select', 'edit_bbmd_select'];
    selects.forEach(selectId => {
        const select = document.getElementById(selectId);
        if (!select) return;
        select.innerHTML = '<option value="">No BBMD (Local device)</option>' +
            bbmdList.map(b => `<option value="${b.id}">${b.name} - Port:${b.port} DevID:${b.device_id}</option>`).join('');
    });
}

function updateBBMDList() {
    const list = document.getElementById('bbmdList');
    if (!list) return;
    if (bbmdList.length === 0) {
        list.innerHTML = '<p class="text-muted">No BBMD devices configured.</p>';
        return;
    }
    list.innerHTML = bbmdList.map(b => `
        <div class="border rounded p-2 mb-2 ${b.enabled ? 'border-success' : 'border-secondary'}">
            <div class="d-flex justify-content-between align-items-center">
                <div><strong>${b.name}</strong> <small class="text-muted">${b.description || ''}</small><br>
                <small>Port: ${b.port} | Device ID: ${b.device_id} | IP: ${b.ip_address}</small></div>
                <button class="btn btn-sm btn-danger" onclick="window.deleteBBMD(${b.id})" type="button"><i class="fas fa-trash"></i></button>
            </div>
        </div>`).join('');
}

function inferModbusRegisterTypeFromAddress(rawAddress) {
    const cleaned = String(rawAddress ?? '').trim();
    if (!cleaned) return null;
    const digitsOnly = cleaned.replace(/\D/g, '');
    if (!digitsOnly || digitsOnly.length < 5) return null;
    const table = digitsOnly.charAt(0);
    if (table === '0') return 'coil';
    if (table === '1') return 'discrete';
    if (table === '3') return 'input';
    if (table === '4') return 'holding';
    return null;
}

function enforceModbusRegisterType(prefix = '') {
    const addrEl = document.getElementById(prefix + 'modbus_addr');
    const regEl = document.getElementById(prefix + 'modbus_register_type');
    if (!addrEl || !regEl) return;
    const inferred = inferModbusRegisterTypeFromAddress(addrEl.value);
    Array.from(regEl.options).forEach(o => o.disabled = false);
    if (!inferred) return;
    Array.from(regEl.options).forEach(o => { o.disabled = o.value !== inferred; });
    regEl.value = inferred;
}

window.showAddBBMDForm = function() {
    document.getElementById('bbmdFormTitle').textContent = 'New BBMD Configuration';
    document.getElementById('bbmd_edit_id').value = '';
    document.getElementById('addBBMDForm').style.display = 'block';
};

window.editBBMD = function(id) {
    const bbmd = bbmdList.find(b => b.id === id);
    if (!bbmd) return;
    bootstrap.Modal.getOrCreateInstance(document.getElementById('bbmdModal')).show();
    document.getElementById('bbmdFormTitle').textContent = 'Edit BBMD Configuration';
    document.getElementById('bbmd_edit_id').value = id;
    document.getElementById('bbmd_name').value = bbmd.name;
    document.getElementById('bbmd_desc').value = bbmd.description || '';
    document.getElementById('bbmd_port').value = bbmd.port;
    document.getElementById('bbmd_device_id').value = bbmd.device_id;
    document.getElementById('bbmd_ip').value = bbmd.ip_address;
    document.getElementById('addBBMDForm').style.display = 'block';
};

window.cancelBBMDForm = function() {
    document.getElementById('addBBMDForm').style.display = 'none';
    document.getElementById('bbmd_edit_id').value = '';
    document.getElementById('bbmd_name').value = '';
    document.getElementById('bbmd_desc').value = '';
    document.getElementById('bbmd_port').value = '47808';
    document.getElementById('bbmd_device_id').value = '1234';
    document.getElementById('bbmd_ip').value = '0.0.0.0';
};

window.saveBBMD = async function() {
    const editId = document.getElementById('bbmd_edit_id').value;
    const data = {
        name: document.getElementById('bbmd_name').value,
        description: document.getElementById('bbmd_desc').value,
        port: parseInt(document.getElementById('bbmd_port').value),
        device_id: parseInt(document.getElementById('bbmd_device_id').value),
        ip_address: document.getElementById('bbmd_ip').value,
        enabled: 1
    };
    const isEdit = editId !== '';
    const res = await fetch(isEdit ? `/api/bbmd/${editId}` : '/api/bbmd', {
        method: isEdit ? 'PUT' : 'POST',
        headers: {'Content-Type': 'application/json'},
        body: JSON.stringify(data)
    });
    if (res.ok) {
        window.cancelBBMDForm();
        window.fetchBBMDs();
    } else {
        alert('Failed to save BBMD: ' + (await res.text()));
    }
};

window.deleteBBMD = async function(id) {
    if (!confirm('Delete this BBMD? Associated assets will be unlinked.')) return;
    await fetch(`/api/bbmd/${id}`, { method: 'DELETE' });
    window.fetchBBMDs();
};

window.saveNewAsset = async function() {
    const protocol = document.getElementById('protocol').value;
    const isBacnet = protocol === 'bacnet';
    const isModbus = protocol === 'modbus';
    const assetName = (document.getElementById('asset_name').value || '').trim();
    const tagName = (document.getElementById('tag_name').value || '').trim();
    if (!assetName || !tagName) {
        alert('Asset and Tag names are required.');
        return;
    }

    const runtimeName = `${assetName.replace(/\s+/g, '_')}_${tagName.replace(/\s+/g, '_')}`;
    const address = isBacnet ? parseInt(document.getElementById('addr').value) :
        (isModbus ? parseInt(document.getElementById('modbus_addr').value) : 1);
    const icon = isBacnet ? document.getElementById('icon').value :
        (isModbus ? document.getElementById('modbus_icon').value : document.getElementById('dnp3_icon').value);

    const data = {
        name: runtimeName,
        asset_name: assetName,
        tag_name: tagName,
        type: 'General',
        sub_type: document.getElementById('sub_type').value,
        protocol,
        address,
        min_range: parseFloat(document.getElementById('min').value) || 0,
        max_range: parseFloat(document.getElementById('max').value) || 100,
        drift_rate: parseFloat(document.getElementById('drift').value) || 0,
        icon,
        bacnet_port: parseInt(document.getElementById('bac_port').value) || 47808,
        bacnet_device_id: parseInt(document.getElementById('bac_id').value) || 1234,
        is_normally_open: parseInt(document.getElementById('logic_state').value),
        change_probability: parseFloat(document.getElementById('prob').value) || 0,
        change_interval: parseInt(document.getElementById('interval').value) || 15,
        bbmd_id: isBacnet && document.getElementById('bbmd_select').value ? parseInt(document.getElementById('bbmd_select').value) : null,
        object_type: isBacnet ? document.getElementById('object_type').value : 'value',
        bacnet_properties: isBacnet ? (document.getElementById('bacnet_properties').value || '{}') : '{}',
        modbus_unit_id: parseInt(document.getElementById('modbus_unit_id').value) || 1,
        modbus_register_type: document.getElementById('modbus_register_type').value || 'holding',
        modbus_ip: document.getElementById('modbus_ip').value || '0.0.0.0',
        modbus_port: parseInt(document.getElementById('modbus_port').value) || 5020,
        modbus_zero_based: document.getElementById('modbus_zero_based').checked ? 1 : 0,
        modbus_word_order: document.getElementById('modbus_word_order').value || 'low_high',
        modbus_alarm_address: document.getElementById('modbus_alarm_address').value === '' ? null : parseInt(document.getElementById('modbus_alarm_address').value),
        modbus_alarm_bit: parseInt(document.getElementById('modbus_alarm_bit').value) || 0,
        dnp3_ip: document.getElementById('dnp3_ip').value || '0.0.0.0',
        dnp3_port: parseInt(document.getElementById('dnp3_port').value) || 20000,
        dnp3_outstation_address: parseInt(document.getElementById('dnp3_outstation_address').value) || 10,
        dnp3_master_address: parseInt(document.getElementById('dnp3_master_address').value) || 1,
        dnp3_address: document.getElementById('dnp3_address').value || '10.0.1.Value'
    };

    const res = await fetch('/api/assets', { method: 'POST', headers: {'Content-Type': 'application/json'}, body: JSON.stringify(data) });
    if (res.ok) {
        bootstrap.Modal.getInstance(document.getElementById('addModal')).hide();
        window.fetchAssets();
    } else {
        alert('Failed to create tag: ' + (await res.text()));
    }
};

window.openAddTagFromAssetModal = function() {
    if (!selectedAssetForTagModal) return;
    const tagsModalEl = document.getElementById('assetTagsModal');
    const tagsModal = bootstrap.Modal.getOrCreateInstance(tagsModalEl);
    const addModal = bootstrap.Modal.getOrCreateInstance(document.getElementById('addModal'));

    tagsModalEl.addEventListener('hidden.bs.modal', () => {
        document.getElementById('asset_name').value = selectedAssetForTagModal;
        document.getElementById('tag_name').value = '';
        addModal.show();
    }, { once: true });

    tagsModal.hide();
};

window.openEditModal = async function(nameEncoded) {
    const name = decodeURIComponent(nameEncoded);
    const res = await fetch(`/api/assets/${encodeURIComponent(name)}`);
    if (!res.ok) return alert('Failed to load tag.');
    const a = await res.json();

    document.getElementById('edit_name').value = a.name;
    document.getElementById('edit_asset_name').value = a.asset_name || '';
    document.getElementById('edit_tag_name').value = a.tag_name || '';
    document.getElementById('edit_sub_type').value = a.sub_type;
    document.getElementById('edit_protocol').value = a.protocol;
    document.getElementById('edit_min').value = a.min_range;
    document.getElementById('edit_max').value = a.max_range;
    document.getElementById('edit_drift').value = a.drift_rate;
    document.getElementById('edit_logic_state').value = a.is_normally_open;
    document.getElementById('edit_prob').value = a.change_probability;
    document.getElementById('edit_interval').value = a.change_interval;
    document.getElementById('edit_bac_port').value = a.bacnet_port;
    document.getElementById('edit_bac_id').value = a.bacnet_device_id;
    document.getElementById('edit_object_type').value = a.object_type || 'value';
    document.getElementById('edit_bbmd_select').value = a.bbmd_id || '';
    document.getElementById('edit_bacnet_properties').value = a.bacnet_properties || '{}';

    if (a.protocol === 'bacnet') {
        document.getElementById('edit_addr').value = a.address;
        document.getElementById('edit_icon').value = a.icon;
    } else if (a.protocol === 'modbus') {
        document.getElementById('edit_modbus_addr').value = a.address;
        document.getElementById('edit_modbus_icon').value = a.icon;
        document.getElementById('edit_modbus_unit_id').value = a.modbus_unit_id || 1;
        document.getElementById('edit_modbus_register_type').value = a.modbus_register_type || 'holding';
        document.getElementById('edit_modbus_ip').value = a.modbus_ip || '0.0.0.0';
        document.getElementById('edit_modbus_port').value = a.modbus_port || 5020;
        document.getElementById('edit_modbus_zero_based').checked = (a.modbus_zero_based ?? 1) === 1;
        document.getElementById('edit_modbus_word_order').value = a.modbus_word_order || 'low_high';
        document.getElementById('edit_modbus_alarm_address').value = a.modbus_alarm_address ?? '';
        document.getElementById('edit_modbus_alarm_bit').value = a.modbus_alarm_bit ?? 0;
    } else {
        document.getElementById('edit_dnp3_address').value = a.dnp3_address || a.dnp3_kepware_address || '10.0.1.Value';
        document.getElementById('edit_dnp3_icon').value = a.icon;
        document.getElementById('edit_dnp3_ip').value = a.dnp3_ip || '0.0.0.0';
        document.getElementById('edit_dnp3_port').value = a.dnp3_port || 20000;
        document.getElementById('edit_dnp3_outstation_address').value = a.dnp3_outstation_address ?? 10;
        document.getElementById('edit_dnp3_master_address').value = a.dnp3_master_address ?? 1;
    }

    window.toggleFields('edit_');
    window.toggleProtocolFields('edit_');

    const editModalEl = document.getElementById('editModal');
    const editModal = bootstrap.Modal.getOrCreateInstance(editModalEl);
    const assetTagsModalEl = document.getElementById('assetTagsModal');
    const tagsAreOpen = assetTagsModalEl && assetTagsModalEl.classList.contains('show');

    if (tagsAreOpen) {
        const tagsModal = bootstrap.Modal.getOrCreateInstance(assetTagsModalEl);
        assetTagsModalEl.addEventListener('hidden.bs.modal', () => {
            editModal.show();
        }, { once: true });
        tagsModal.hide();
    } else {
        editModal.show();
    }};

window.saveAssetEdit = async function() {
    const name = document.getElementById('edit_name').value;
    const protocol = document.getElementById('edit_protocol').value;
    const isBacnet = protocol === 'bacnet';
    const isModbus = protocol === 'modbus';

    const assetName = (document.getElementById('edit_asset_name').value || '').trim();
    const tagName = (document.getElementById('edit_tag_name').value || '').trim();
    const runtimeName = `${assetName.replace(/\s+/g, '_')}_${tagName.replace(/\s+/g, '_')}`;

    const data = {
        name: runtimeName,
        asset_name: assetName,
        tag_name: tagName,
        type: 'General',
        sub_type: document.getElementById('edit_sub_type').value,
        protocol,
        address: isBacnet ? parseInt(document.getElementById('edit_addr').value) :
            (isModbus ? parseInt(document.getElementById('edit_modbus_addr').value) : 1),
        min_range: parseFloat(document.getElementById('edit_min').value) || 0,
        max_range: parseFloat(document.getElementById('edit_max').value) || 100,
        drift_rate: parseFloat(document.getElementById('edit_drift').value) || 0,
        icon: isBacnet ? document.getElementById('edit_icon').value :
            (isModbus ? document.getElementById('edit_modbus_icon').value : document.getElementById('edit_dnp3_icon').value),
        bacnet_port: parseInt(document.getElementById('edit_bac_port').value) || 47808,
        bacnet_device_id: parseInt(document.getElementById('edit_bac_id').value) || 1234,
        is_normally_open: parseInt(document.getElementById('edit_logic_state').value),
        change_probability: parseFloat(document.getElementById('edit_prob').value) || 0,
        change_interval: parseInt(document.getElementById('edit_interval').value) || 15,
        bbmd_id: isBacnet && document.getElementById('edit_bbmd_select').value ? parseInt(document.getElementById('edit_bbmd_select').value) : null,
        object_type: isBacnet ? document.getElementById('edit_object_type').value : 'value',
        bacnet_properties: isBacnet ? (document.getElementById('edit_bacnet_properties').value || '{}') : '{}',
        modbus_unit_id: parseInt(document.getElementById('edit_modbus_unit_id').value) || 1,
        modbus_register_type: document.getElementById('edit_modbus_register_type').value || 'holding',
        modbus_ip: document.getElementById('edit_modbus_ip').value || '0.0.0.0',
        modbus_port: parseInt(document.getElementById('edit_modbus_port').value) || 5020,
        modbus_zero_based: document.getElementById('edit_modbus_zero_based').checked ? 1 : 0,
        modbus_word_order: document.getElementById('edit_modbus_word_order').value || 'low_high',
        modbus_alarm_address: document.getElementById('edit_modbus_alarm_address').value === '' ? null : parseInt(document.getElementById('edit_modbus_alarm_address').value),
        modbus_alarm_bit: parseInt(document.getElementById('edit_modbus_alarm_bit').value) || 0,
        dnp3_ip: document.getElementById('edit_dnp3_ip').value || '0.0.0.0',
        dnp3_port: parseInt(document.getElementById('edit_dnp3_port').value) || 20000,
        dnp3_outstation_address: parseInt(document.getElementById('edit_dnp3_outstation_address').value) || 10,
        dnp3_master_address: parseInt(document.getElementById('edit_dnp3_master_address').value) || 1,
        dnp3_address: document.getElementById('edit_dnp3_address').value || '10.0.1.Value'
    };

    const returnAssetName = (document.getElementById('edit_asset_name').value || '').trim();
    
    const res = await fetch(`/api/assets/${encodeURIComponent(name)}`, {
        method: 'PUT', headers: {'Content-Type': 'application/json'}, body: JSON.stringify(data)
    });
    if (res.ok) {
        await window.fetchAssets();
        const editModalEl = document.getElementById('editModal');
        const editModal = bootstrap.Modal.getInstance(editModalEl);

        if (returnToAssetTagsAfterEdit && returnAssetName) {
            editModalEl.addEventListener('hidden.bs.modal', () => {
                window.openAssetTagsModal(encodeURIComponent(returnAssetName));
            }, { once: true });
        }

        editModal?.hide();
        returnToAssetTagsAfterEdit = false;
    } else {
        alert('Failed to update tag: ' + (await res.text()));
    }
};

window.deleteAsset = async function(nameEncoded) {
    const name = decodeURIComponent(nameEncoded);
    if (!confirm(`Remove tag ${name}?`)) return;
    await fetch(`/api/assets/${encodeURIComponent(name)}`, { method: 'DELETE' });
    window.fetchAssets();
};

window.deleteAssetGroup = async function(assetNameEncoded) {
    const assetName = decodeURIComponent(assetNameEncoded);
    const matchingTags = latestTags.filter(t => (t.asset_name || '').trim() === assetName);
    const tagCount = matchingTags.length;
    const warning = tagCount > 0
        ? `Delete asset ${assetName} and all ${tagCount} tag(s)? This cannot be undone.`
        : `Delete asset ${assetName} and all of its tags?`;
    if (!confirm(warning)) return;

    const response = await fetch(`/api/assets/by-asset/${encodeURIComponent(assetName)}`, { method: 'DELETE' });
    if (!response.ok) {
        alert(`Failed to delete ${assetName}: ${await response.text()}`);
        return;
    }

    await window.fetchAssets();
};

window.toggleDigital = async function(name, currentVal) {
    const newVal = currentVal >= 0.5 ? 0.0 : 1.0;
    await fetch(`/api/override/${name}?value=${newVal}`, { method: 'PUT' });
    window.fetchAssets();
};

window.sendRelease = async function(name) {
    await fetch(`/api/release/${name}`, { method: 'PUT' });
    window.fetchAssets();
};

window.sendOverride = async function(name) {
    const val = prompt('Manual Analog Value:');
    if (val !== null) {
        await fetch(`/api/override/${name}?value=${val}`, { method: 'PUT' });
        window.fetchAssets();
    }
};

window.toggleFields = function(prefix = '') {
    const subType = document.getElementById(prefix + 'sub_type').value;
    const isDigital = subType === 'Digital';
    document.getElementById(prefix + 'analog_fields').style.display = isDigital ? 'none' : 'block';
    document.getElementById(prefix + 'digital_fields').style.display = isDigital ? 'block' : 'none';
};

window.toggleProtocolFields = function(prefix = '') {
    const protocol = ((document.getElementById(prefix + 'protocol')?.value) || '').toLowerCase().trim();
    const isBacnet = protocol === 'bacnet';
    const isModbus = protocol === 'modbus';
    const isDnp3 = protocol === 'dnp3' || protocol === 'dnp';
    document.getElementById(prefix + 'bacnet_config_section').style.display = isBacnet ? 'block' : 'none';
    document.getElementById(prefix + 'modbus_config_section').style.display = isModbus ? 'block' : 'none';
    document.getElementById(prefix + 'dnp3_config_section').style.display = isDnp3 ? 'block' : 'none';
    document.getElementById(prefix + 'object_type_container').style.display = isBacnet ? 'block' : 'none';
};

window.importCsv = async function() {
    const fileInput = document.getElementById('import_file');
    const protocol = document.getElementById('import_protocol').value;
    const assetName = (document.getElementById('import_asset_name').value || '').trim();
    if (!fileInput.files.length) {
        alert('Please select a CSV file.');
        return;
    }
    if (!assetName) {
        alert('Please enter an asset/device name.');
        return;
    }

    const form = new FormData();
    form.append('file', fileInput.files[0]);
    form.append('protocol', protocol);
    form.append('asset_name', assetName);

    const res = await fetch('/api/assets/import', { method: 'POST', body: form });
    const payload = await res.json();
    if (!res.ok) {
        alert(payload.detail || 'Import failed');
        return;
    }

    let msg = payload.message || 'Import complete';
    if (payload.errors && payload.errors.length) {
        msg += `\nRows with errors: ${payload.errors.length}`;
    }
    alert(msg);
    bootstrap.Modal.getInstance(document.getElementById('importModal')).hide();
    fileInput.value = '';
    window.fetchAssets();
};

async function fetchMe() {
    try {
        const res = await fetch('/api/auth/me');
        if (res.ok) {
            const data = await res.json();
            userRole = data.role;
            if (userRole === 'admin') {
                document.getElementById('manageUsersBtn')?.classList.remove('d-none');
                document.getElementById('viewLogsBtn')?.classList.remove('d-none');
            }
            if (userRole === 'read_only') {
                document.querySelectorAll('.btn-primary[data-bs-target="#addModal"]').forEach(el => el.classList.add('d-none'));
                document.querySelectorAll('.btn-info[data-bs-target="#importModal"]').forEach(el => el.classList.add('d-none'));
                document.querySelectorAll('.btn-success[data-bs-target="#bbmdModal"]').forEach(el => el.classList.add('d-none'));
            }
        }
    } catch {}
}

async function logout() {
    await fetch('/api/auth/logout', { method: 'POST' });
    window.location.href = '/login';
}
window.logout = logout;

const originalFetch = window.fetch;
window.fetch = async function() {
    const res = await originalFetch.apply(this, arguments);
    if (res.status === 401 || res.status === 403) window.location.href = '/login';
    return res;
};

document.addEventListener('DOMContentLoaded', async () => {
    await fetchMe();

    document.getElementById('protocol')?.addEventListener('change', () => window.toggleProtocolFields(''));
    document.getElementById('edit_protocol')?.addEventListener('change', () => window.toggleProtocolFields('edit_'));
    document.getElementById('modbus_addr')?.addEventListener('input', () => enforceModbusRegisterType(''));
    document.getElementById('edit_modbus_addr')?.addEventListener('input', () => enforceModbusRegisterType('edit_'));

    window.toggleProtocolFields('');
    enforceModbusRegisterType('');
    enforceModbusRegisterType('edit_');

    window.fetchBBMDs();
    window.fetchAssets();
    refreshAlarms();
    setInterval(window.fetchAssets, 5000);
    setInterval(refreshAlarms, 5000);
    connectWebSocket();
});

window.addEventListener('beforeunload', () => {
    shouldReconnectSocket = false;
    if (reconnectTimer) clearTimeout(reconnectTimer);
    if (socket && socket.readyState === WebSocket.OPEN) socket.close();
});
