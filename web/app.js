'use strict';

const TARGET_FILES = ['KeyBindings', 'Sensitivity'];
const VALUE_PATTERN = '-?(?:\\d+(?:\\.\\d{1,2})?|\\.\\d{1,2})';
const DECIMAL_PATTERN = new RegExp(`^${VALUE_PATTERN}$`);
const FIELD_DEFS = [
  { file: 'KeyBindings', suffix: 'qq1156777787', type: 'combo' },
  { file: 'KeyBindings', suffix: 'qq1156777787_second', type: 'combo' },
  { file: 'KeyBindings', suffix: 'Third', type: 'combo' },
  { file: 'Sensitivity', suffix: 'qq1156777787_X', type: 'decimal' },
  { file: 'Sensitivity', suffix: 'qq1156777787_Y', type: 'decimal' },
  { file: 'Sensitivity', suffix: 'qq1156777787_add_X', type: 'decimal' },
  { file: 'Sensitivity', suffix: 'qq1156777787_add_Y', type: 'decimal' },
];

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function decodeLuaFile(bytes) {
  const data = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes);
  if (data.length >= 3 && data[0] === 0xef && data[1] === 0xbb && data[2] === 0xbf) {
    return { content: new TextDecoder('utf-8').decode(data.subarray(3)), encoding: 'utf-8-bom' };
  }
  if (data.length >= 2 && data[0] === 0xff && data[1] === 0xfe) {
    return { content: new TextDecoder('utf-16le').decode(data.subarray(2)), encoding: 'utf-16le' };
  }
  if (data.length >= 2 && data[0] === 0xfe && data[1] === 0xff) {
    const swapped = new Uint8Array(data.length - 2);
    for (let index = 2; index < data.length; index += 2) {
      swapped[index - 2] = data[index + 1];
      swapped[index - 1] = data[index];
    }
    return { content: new TextDecoder('utf-16le').decode(swapped), encoding: 'utf-16be' };
  }
  return { content: new TextDecoder('utf-8').decode(data), encoding: 'utf-8' };
}

function encodeLuaFile(content, encoding) {
  const body = new TextEncoder().encode(content);
  if (encoding === 'utf-8') return body;
  if (encoding === 'utf-8-bom') return Uint8Array.from([0xef, 0xbb, 0xbf, ...body]);

  const utf16 = new Uint8Array(content.length * 2);
  for (let index = 0; index < content.length; index += 1) {
    const code = content.charCodeAt(index);
    utf16[index * 2] = code & 0xff;
    utf16[index * 2 + 1] = code >> 8;
  }
  if (encoding === 'utf-16le') return Uint8Array.from([0xff, 0xfe, ...utf16]);
  if (encoding === 'utf-16be') {
    const result = new Uint8Array(utf16.length + 2);
    result[0] = 0xfe;
    result[1] = 0xff;
    for (let index = 0; index < utf16.length; index += 2) {
      result[index + 2] = utf16[index + 1];
      result[index + 3] = utf16[index];
    }
    return result;
  }
  throw new Error(`Unsupported encoding: ${encoding}`);
}

function getLuaAssignments(content) {
  return [...content.matchAll(new RegExp(`^\\s*(?<name>[A-Za-z0-9_]+)\\s*=\\s*(?<value>${VALUE_PATTERN})`, 'gm'))]
    .map((match) => ({ name: match.groups.name, value: match.groups.value }));
}

function getPrimaryWeapons(content) {
  const suffixes = FIELD_DEFS.filter((field) => field.file === 'KeyBindings')
    .map((field) => escapeRegExp(field.suffix)).join('|');
  const pattern = new RegExp(`^(?<weapon>[A-Za-z0-9]+)_(?:${suffixes})$`);
  const seen = new Set();
  return getLuaAssignments(content).flatMap(({ name }) => {
    const match = name.match(pattern);
    if (!match || seen.has(match.groups.weapon)) return [];
    seen.add(match.groups.weapon);
    return [match.groups.weapon];
  });
}

function setLuaValue(content, variableName, newValue) {
  const pattern = new RegExp(`^(\\s*${escapeRegExp(variableName)}\\s*=\\s*)${VALUE_PATTERN}`, 'm');
  if (!pattern.test(content)) throw new Error(`Variable not found in content: ${variableName}`);
  return content.replace(pattern, `$1${newValue}`);
}

function getLuaStringValue(content, variableName) {
  const match = content.match(new RegExp(`^\\s*${escapeRegExp(variableName)}\\s*=\\s*"(?<value>[^"]*)"`, 'm'));
  return match ? match.groups.value : null;
}

function setLuaStringValue(content, variableName, newValue) {
  const pattern = new RegExp(`^(\\s*${escapeRegExp(variableName)}\\s*=\\s*)"[^"]*"`, 'm');
  if (!pattern.test(content)) throw new Error(`Variable not found in content: ${variableName}`);
  return content.replace(pattern, `$1"${newValue}"`);
}

function validateDecimalValue(value) {
  if (!DECIMAL_PATTERN.test(value)) throw new Error('请输入数值（支持负数，最多两位小数）。');
  return value;
}

function hasLuaAssignment(content, variableName) {
  return new RegExp(`^\\s*${escapeRegExp(variableName)}\\s*=`, 'm').test(content);
}

function applyConfiguration(model, selection) {
  const files = Object.fromEntries(TARGET_FILES.map((file) => [file, { ...model.files[file] }]));
  files.KeyBindings.content = setLuaValue(files.KeyBindings.content, 'press', selection.press);
  files.KeyBindings.content = setLuaStringValue(files.KeyBindings.content, 'modeswitch', selection.modeSwitch);

  for (const field of FIELD_DEFS) {
    const key = `${field.file}|${field.suffix}`;
    const value = selection.values[key];
    const variableName = `${selection.weapon}_${field.suffix}`;
    if (!hasLuaAssignment(files[field.file].content, variableName)) continue;
    if (field.type === 'decimal') validateDecimalValue(value);
    if (field.type === 'combo' && !/^[0-9]$/.test(value)) throw new Error('请选择一个按键。');
    files[field.file].content = setLuaValue(files[field.file].content, variableName, value);
  }

  const keyValues = new Map(FIELD_DEFS.filter((field) => field.file === 'KeyBindings')
    .map((field) => [field.suffix, selection.values[`KeyBindings|${field.suffix}`]])
    .filter(([, value]) => value && value !== '0'));
  for (const { name, value } of getLuaAssignments(files.KeyBindings.content)) {
    const match = name.match(/^(?<weapon>[A-Za-z0-9]+)_(?<suffix>.+)$/);
    if (match && match.groups.weapon !== selection.weapon && value !== '0' && keyValues.get(match.groups.suffix) === value) {
      files.KeyBindings.content = setLuaValue(files.KeyBindings.content, name, '0');
    }
  }
  return { ...model, files };
}

const PRESS_OPTIONS = [
  { text: '鼠标左键', value: '1' },
  { text: '按住右键 + 鼠标左键', value: '3' },
];
const MODE_SWITCH_OPTIONS = [
  { text: 'Scroll Lock', value: 'scrolllock' },
  { text: 'Caps Lock', value: 'capslock' },
  { text: 'Num Lock', value: 'numlock' },
];
const MOUSE_PROFILES = {
  '通用双侧键鼠标': [['左侧后退键(4)', '4'], ['左侧前进键(5)', '5']],
  G102: [['左侧后退键(4)', '4'], ['左侧前进键(5)', '5']],
  'G304 / G305': [['左侧后退键(4)', '4'], ['左侧前进键(5)', '5']],
  'G Pro Wireless（GPW）': [['左侧后退键(4)', '4'], ['左侧前进键(5)', '5'], ['右侧后退键(7)', '7'], ['右侧前进键(8)', '8']],
  'G Pro X Superlight（GPX）': [['左侧后退键(4)', '4'], ['左侧前进键(5)', '5']],
  G402: [['左侧后退键(4)', '4'], ['左侧前进键(5)', '5']],
  'G502 Hero': [['左侧后退键(4)', '4'], ['左侧前进键(5)', '5']],
  'G502 X': [['左侧后退键(4)', '4'], ['左侧前进键(5)', '5']],
};

const FIELD_LABELS = {
  qq1156777787: '无修饰键',
  qq1156777787_second: '按住 Alt',
  Third: '按住 Ctrl',
  qq1156777787_X: '灵敏度 X',
  qq1156777787_Y: '灵敏度 Y',
  qq1156777787_add_X: '灵敏度 增幅 X',
  qq1156777787_add_Y: '灵敏度 增幅 Y',
};

function buildConfigModel(files) {
  if (files.KeyBindings.name === files.Sensitivity.name) throw new Error('请为两个配置角色选择不同的文件。');
  const weapons = getPrimaryWeapons(files.KeyBindings.content);
  if (!weapons.length) throw new Error('按键配置文件中没有识别到枪械。');
  if (!getLuaAssignments(files.KeyBindings.content).some(({ name }) => name === 'press')) throw new Error('按键配置文件缺少 press。');
  if (getLuaStringValue(files.KeyBindings.content, 'modeswitch') === null) throw new Error('按键配置文件缺少 modeswitch。');
  return { files, weapons };
}

function populateSelect(select, options, selectedValue) {
  select.replaceChildren(...options.map(({ text, value }) => new Option(text, value, false, value === selectedValue)));
}

function shouldUseDirectSave(handles) {
  return TARGET_FILES.every((file) => handles[file]);
}

async function canWriteDirectly(handles) {
  if (!shouldUseDirectSave(handles)) return false;
  for (const file of TARGET_FILES) {
    const permission = await handles[file].queryPermission({ mode: 'readwrite' });
    if (permission === 'granted') continue;
    if (await handles[file].requestPermission({ mode: 'readwrite' }) !== 'granted') return false;
  }
  return true;
}

function downloadFile(file) {
  const blob = new Blob([encodeLuaFile(file.content, file.encoding)], { type: 'application/octet-stream' });
  const link = document.createElement('a');
  link.href = URL.createObjectURL(blob);
  link.download = file.name;
  link.click();
  window.setTimeout(() => URL.revokeObjectURL(link.href), 0);
}

async function saveModel(model, handles) {
  if (await canWriteDirectly(handles)) {
    for (const file of TARGET_FILES) {
      const writable = await handles[file].createWritable();
      await writable.write(encodeLuaFile(model.files[file].content, model.files[file].encoding));
      await writable.close();
    }
    return 'direct';
  }
  for (const file of TARGET_FILES) downloadFile(model.files[file]);
  return 'download';
}

function isLocalServiceUrl(url) {
  try { return new URL(url).hostname === '127.0.0.1'; } catch { return false; }
}

function serviceResponseToFiles(response) {
  return {
    KeyBindings: { name: response.keyBindings.name, content: response.keyBindings.content, encoding: 'utf-8' },
    Sensitivity: { name: response.sensitivity.name, content: response.sensitivity.content, encoding: 'utf-8' },
  };
}

function normalizeServiceRequestOptions(options) {
  if (options.method === 'POST' && options.body === undefined) return { ...options, body: '' };
  return options;
}

function initializeBrowserEditor() {
  const elements = Object.fromEntries(['choose-files', 'refresh-files', 'key-file-input', 'sensitivity-file-input', 'mouse-model', 'weapon-list', 'press', 'mode-switch', 'field-cards', 'selected-weapon', 'save-mode', 'service-status', 'status', 'shutdown-service', 'apply'].map((id) => [id, document.getElementById(id)]));
  const state = { model: null, handles: {}, selectedWeapon: null };
  const serviceMode = isLocalServiceUrl(window.location.href);
  const requestService = async (path, options = {}) => {
    const response = await fetch(path, normalizeServiceRequestOptions(options));
    const result = await response.json();
    if (!response.ok) throw new Error(result.error || '本机服务请求失败。');
    return result;
  };
  const setStatus = (message, isError = false) => {
    elements.status.textContent = message;
    elements.status.style.color = isError ? '#ff9ca8' : '#bdb3dd';
  };
  const getKeyOptions = () => [['无按键(0)', '0'], ...(MOUSE_PROFILES[elements['mouse-model'].value] || MOUSE_PROFILES['通用双侧键鼠标'])];
  const updateSaveMode = () => {
    const direct = state.handles.service || TARGET_FILES.every((file) => state.handles[file]);
    elements['save-mode'].textContent = `保存模式：${direct ? '可直接写回' : '下载后替换'}`;
  };
  const fieldValue = (field) => {
    const variableName = `${state.selectedWeapon}_${field.suffix}`;
    return getLuaAssignments(state.model.files[field.file].content).find(({ name }) => name === variableName)?.value ?? '';
  };
  const renderFields = () => {
    elements['field-cards'].replaceChildren();
    if (!state.model || !state.selectedWeapon) return;
    for (const [file, title] of [['KeyBindings', '按键'], ['Sensitivity', '灵敏度']]) {
      const group = document.createElement('section');
      group.className = 'field-group';
      group.append(Object.assign(document.createElement('h4'), { textContent: title }));
      for (const field of FIELD_DEFS.filter((item) => item.file === file)) {
        const value = fieldValue(field);
        const row = document.createElement('div');
        row.className = 'field-row';
        row.append(Object.assign(document.createElement('label'), { textContent: FIELD_LABELS[field.suffix] }));
        const control = document.createElement(field.type === 'combo' ? 'select' : 'input');
        control.dataset.fieldKey = `${field.file}|${field.suffix}`;
        control.disabled = value === '';
        if (field.type === 'combo') {
          populateSelect(control, getKeyOptions().map(([text, optionValue]) => ({ text, value: optionValue })), value);
        } else {
          control.type = 'text';
          control.value = value;
          control.inputMode = 'decimal';
        }
        row.append(control);
        group.append(row);
      }
      elements['field-cards'].append(group);
    }
  };
  const renderWeapons = () => {
    elements['weapon-list'].replaceChildren();
    for (const weapon of state.model.weapons) {
      const button = document.createElement('button');
      button.type = 'button'; button.textContent = weapon;
      button.setAttribute('aria-current', String(weapon === state.selectedWeapon));
      button.addEventListener('click', () => {
        state.selectedWeapon = weapon;
        elements['selected-weapon'].textContent = `枪械：${weapon}`;
        renderWeapons(); renderFields();
      });
      const item = document.createElement('li'); item.append(button);
      elements['weapon-list'].append(item);
    }
  };
  const loadEntries = (entries, handles = {}, source = '配置') => {
    try {
      state.model = buildConfigModel(entries);
      state.handles = handles;
      state.selectedWeapon = state.model.weapons[0];
      const keyContent = state.model.files.KeyBindings.content;
      populateSelect(elements.press, PRESS_OPTIONS, getLuaAssignments(keyContent).find(({ name }) => name === 'press').value);
      populateSelect(elements['mode-switch'], MODE_SWITCH_OPTIONS, getLuaStringValue(keyContent, 'modeswitch'));
      elements['selected-weapon'].textContent = `枪械：${state.selectedWeapon}`;
      renderWeapons(); renderFields(); updateSaveMode();
      elements.apply.disabled = false;
      elements['refresh-files'].disabled = false;
      setStatus(`${source}已加载。`);
    } catch (error) {
      state.model = null;
      elements.apply.disabled = true;
      elements['refresh-files'].disabled = true;
      setStatus(`加载配置失败：${error.message}`, true);
    }
  };
  const loadFiles = async (files, handles = {}) => {
    try {
      const entries = {};
      for (const file of TARGET_FILES) {
        const decoded = decodeLuaFile(new Uint8Array(await files[file].arrayBuffer()));
        entries[file] = { name: files[file].name, content: decoded.content, encoding: decoded.encoding };
      }
      loadEntries(entries, handles);
    } catch (error) {
      setStatus(`加载配置失败：${error.message}`, true);
    }
  };
  const chooseWithInputs = () => {
    elements['key-file-input'].value = '';
    elements['sensitivity-file-input'].value = '';
    elements['key-file-input'].click();
  };
  elements['key-file-input'].addEventListener('change', () => elements['sensitivity-file-input'].click());
  elements['sensitivity-file-input'].addEventListener('change', () => {
    const keyFile = elements['key-file-input'].files[0];
    const sensitivityFile = elements['sensitivity-file-input'].files[0];
    if (keyFile && sensitivityFile) loadFiles({ KeyBindings: keyFile, Sensitivity: sensitivityFile });
  });
  elements['choose-files'].addEventListener('click', async () => {
    if (serviceMode) {
      try {
        const result = await requestService('/api/select-files', { method: 'POST' });
        if (result.cancelled) { setStatus('已取消选择文件。'); return; }
        loadEntries(serviceResponseToFiles(result.files), { service: true }, '本机服务配置');
      } catch (error) {
        setStatus(`选择文件失败：${error.message}`, true);
      }
      return;
    }
    if (!window.showOpenFilePicker) return chooseWithInputs();
    try {
      const options = { multiple: false, types: [{ description: 'Lua 文件', accept: { 'text/plain': ['.lua'] } }] };
      const [keyHandle] = await window.showOpenFilePicker(options);
      const [sensitivityHandle] = await window.showOpenFilePicker(options);
      await loadFiles({ KeyBindings: await keyHandle.getFile(), Sensitivity: await sensitivityHandle.getFile() }, { KeyBindings: keyHandle, Sensitivity: sensitivityHandle });
    } catch (error) {
      if (error.name !== 'AbortError') setStatus(`选择文件失败：${error.message}`, true);
    }
  });
  elements['refresh-files'].addEventListener('click', async () => {
    if (state.handles.service) {
      elements['choose-files'].click();
      return;
    }
    if (TARGET_FILES.every((file) => state.handles[file])) {
      await loadFiles({ KeyBindings: await state.handles.KeyBindings.getFile(), Sensitivity: await state.handles.Sensitivity.getFile() }, state.handles);
      return;
    }
    chooseWithInputs();
  });
  elements.apply.addEventListener('click', async () => {
    try {
      if (!state.model || !state.selectedWeapon) return;
      const values = Object.fromEntries([...elements['field-cards'].querySelectorAll('[data-field-key]')]
        .filter((control) => !control.disabled)
        .map((control) => [control.dataset.fieldKey, control.value]));
      state.model = applyConfiguration(state.model, {
        weapon: state.selectedWeapon,
        press: elements.press.value,
        modeSwitch: elements['mode-switch'].value,
        values,
      });
      let mode;
      if (state.handles.service) {
        await requestService('/api/apply', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            keyBindingsContent: state.model.files.KeyBindings.content,
            sensitivityContent: state.model.files.Sensitivity.content,
          }),
        });
        mode = 'direct';
      } else {
        mode = await saveModel(state.model, state.handles);
      }
      setStatus(mode === 'direct' ? '应用成功：已写回原文件。' : '已下载修改文件，请替换原文件。');
      updateSaveMode();
      renderFields();
    } catch (error) {
      try {
        for (const file of TARGET_FILES) downloadFile(state.model.files[file]);
        setStatus(`无法写回原文件：${error.message}。已下载修改文件，请替换原文件。`, true);
      } catch (downloadError) {
        setStatus(`保存失败：${error.message}；下载回退失败：${downloadError.message}`, true);
      }
    }
  });
  const refreshServiceStatus = async () => {
    if (!serviceMode) {
      elements['service-status'].textContent = '纯网页模式：保存时下载同名文件。';
      return;
    }
    try {
      await requestService('/api/status');
      elements['service-status'].textContent = '本机服务已连接，可直接保存。';
      elements['shutdown-service'].hidden = false;
    } catch {
      elements['service-status'].textContent = '本机服务不可用：将使用下载保存。';
      elements['shutdown-service'].hidden = true;
    }
  };
  elements['shutdown-service'].addEventListener('click', async () => {
    try {
      await requestService('/api/shutdown', { method: 'POST' });
      elements['service-status'].textContent = '本机服务已停止。';
      elements['shutdown-service'].hidden = true;
    } catch (error) {
      setStatus(`停止服务失败：${error.message}`, true);
    }
  });
  elements['mouse-model'].addEventListener('change', renderFields);
  populateSelect(elements['mouse-model'], Object.keys(MOUSE_PROFILES).map((value) => ({ text: value, value })), '通用双侧键鼠标');
  setStatus('请选择两个 Lua 配置文件开始编辑。');
  refreshServiceStatus();
  return { state, elements, loadFiles, setStatus, updateSaveMode, renderFields };
}

const exported = {
  TARGET_FILES,
  FIELD_DEFS,
  decodeLuaFile,
  encodeLuaFile,
  getLuaAssignments,
  getPrimaryWeapons,
  getLuaStringValue,
  setLuaValue,
  setLuaStringValue,
  validateDecimalValue,
  applyConfiguration,
  buildConfigModel,
  shouldUseDirectSave,
  isLocalServiceUrl,
  serviceResponseToFiles,
  normalizeServiceRequestOptions,
};

if (typeof module !== 'undefined') module.exports = exported;

if (typeof document !== 'undefined') document.addEventListener('DOMContentLoaded', initializeBrowserEditor);
