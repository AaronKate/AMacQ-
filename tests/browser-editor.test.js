'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const {
  decodeLuaFile,
  encodeLuaFile,
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
} = require('../web/app.js');

const keyBindings = [
  'press = 1',
  'modeswitch = "scrolllock"',
  'AK_qq1156777787 = 4',
  'AK_qq1156777787_second = 0',
  'AK_Third = 5',
  'M4_qq1156777787 = 4',
  'M4_qq1156777787_second = 3',
  'M4_Third = 0',
].join('\n');

const sensitivity = [
  'AK_qq1156777787_X = 1.25',
  'AK_qq1156777787_Y = -0.5',
  'AK_qq1156777787_add_X = .25',
  'AK_qq1156777787_add_Y = 0',
  'M4_qq1156777787_X = 2',
  'M4_qq1156777787_Y = 2',
].join('\n');

test('round trips supported encodings and preserves BOM choice', () => {
  for (const encoding of ['utf-8', 'utf-8-bom', 'utf-16le', 'utf-16be']) {
    const bytes = encodeLuaFile('枪械 = 1', encoding);
    const decoded = decodeLuaFile(bytes);
    assert.equal(decoded.content, '枪械 = 1');
    assert.equal(decoded.encoding, encoding);
  }
});

test('discovers weapons from configured key binding suffixes', () => {
  assert.deepEqual(getPrimaryWeapons(keyBindings), ['AK', 'M4']);
});

test('updates numeric and quoted Lua assignments without changing surrounding text', () => {
  assert.match(setLuaValue(keyBindings, 'AK_Third', '0'), /AK_Third = 0/);
  assert.equal(getLuaStringValue(keyBindings, 'modeswitch'), 'scrolllock');
  assert.match(setLuaStringValue(keyBindings, 'modeswitch', 'capslock'), /modeswitch = "capslock"/);
  assert.throws(() => setLuaValue(keyBindings, 'Missing', '1'), /Variable not found/);
});

test('accepts only configured decimal values', () => {
  for (const value of ['0', '-1', '1.25', '-.5', '.25']) assert.equal(validateDecimalValue(value), value);
  for (const value of ['', '1.234', '1.', '--1', 'text']) assert.throws(() => validateDecimalValue(value));
});

test('applies global and selected weapon values then clears conflicting key fields', () => {
  const result = applyConfiguration({
    files: {
      KeyBindings: { content: keyBindings },
      Sensitivity: { content: sensitivity },
    },
  }, {
    weapon: 'AK', press: '3', modeSwitch: 'capslock',
    values: {
      'KeyBindings|qq1156777787': '4',
      'KeyBindings|qq1156777787_second': '3',
      'KeyBindings|Third': '5',
      'Sensitivity|qq1156777787_X': '1.5',
      'Sensitivity|qq1156777787_Y': '-.5',
      'Sensitivity|qq1156777787_add_X': '.25',
      'Sensitivity|qq1156777787_add_Y': '0',
    },
  });
  assert.match(result.files.KeyBindings.content, /press = 3/);
  assert.match(result.files.KeyBindings.content, /modeswitch = "capslock"/);
  assert.match(result.files.KeyBindings.content, /M4_qq1156777787 = 0/);
  assert.match(result.files.KeyBindings.content, /M4_qq1156777787_second = 0/);
  assert.match(result.files.Sensitivity.content, /AK_qq1156777787_X = 1.5/);
});

test('builds a model, rejects duplicate file names, and rejects missing weapons', () => {
  const model = buildConfigModel({
    KeyBindings: { name: 'KeyBindings.lua', content: keyBindings, encoding: 'utf-8' },
    Sensitivity: { name: 'Sensitivity.lua', content: sensitivity, encoding: 'utf-8' },
  });
  assert.deepEqual(model.weapons, ['AK', 'M4']);
  assert.throws(() => buildConfigModel({
    KeyBindings: { name: 'same.lua', content: keyBindings, encoding: 'utf-8' },
    Sensitivity: { name: 'same.lua', content: sensitivity, encoding: 'utf-8' },
  }), /不同的文件/);
  assert.throws(() => buildConfigModel({
    KeyBindings: { name: 'KeyBindings.lua', content: 'press = 1', encoding: 'utf-8' },
    Sensitivity: { name: 'Sensitivity.lua', content: sensitivity, encoding: 'utf-8' },
  }), /没有识别到枪械/);
});

test('uses direct saving only when both file handles exist', () => {
  assert.equal(shouldUseDirectSave({ KeyBindings: {}, Sensitivity: {} }), true);
  assert.equal(shouldUseDirectSave({ KeyBindings: {} }), false);
  assert.equal(shouldUseDirectSave({}), false);
});

test('accepts only loopback service URLs and maps selected service files', () => {
  assert.equal(isLocalServiceUrl('http://127.0.0.1:53120/'), true);
  assert.equal(isLocalServiceUrl('http://localhost:53120/'), false);
  assert.equal(isLocalServiceUrl('http://192.168.1.5:53120/'), false);
  assert.deepEqual(serviceResponseToFiles({
    keyBindings: { name: 'KeyBindings.lua', content: 'press = 1' },
    sensitivity: { name: 'Sensitivity.lua', content: 'AK_qq1156777787_X = 1' },
  }), {
    KeyBindings: { name: 'KeyBindings.lua', content: 'press = 1', encoding: 'utf-8' },
    Sensitivity: { name: 'Sensitivity.lua', content: 'AK_qq1156777787_X = 1', encoding: 'utf-8' },
  });
});

test('adds an empty body to service POST requests', () => {
  assert.deepEqual(normalizeServiceRequestOptions({ method: 'POST' }), { method: 'POST', body: '' });
  assert.deepEqual(normalizeServiceRequestOptions({ method: 'POST', body: '{}' }), { method: 'POST', body: '{}' });
  assert.deepEqual(normalizeServiceRequestOptions({ method: 'GET' }), { method: 'GET' });
});
