import { readFileSync } from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import assert from 'node:assert/strict';

const source = readFileSync(
    new URL('../../WebRPG/Assets/Plugins/WebRPGAccountCredentials.jslib', import.meta.url),
    'utf8'
);
const endpoint = 'https://ws.oldshaw-webrpg.com/api/accounts/refresh';

function setup() {
    const calls = [];
    const library = {};
    const module = {
        fetchWithProgress(resource, init) {
            calls.push({ type: 'fetch', resource, init, receiver: this });
            return Promise.resolve('response');
        },
        cachedFetch(resource, init) {
            calls.push({ type: 'cache', resource, init, receiver: this });
            return Promise.resolve('response');
        }
    };
    const context = vm.createContext({
        URL,
        Set,
        Object,
        Module: module,
        LibraryManager: { library },
        UTF8ToString: value => value,
        mergeInto: (target, values) => Object.assign(target, values) });
    vm.runInContext(source, context);
    context.WebRPGAccountCredentials = library.$WebRPGAccountCredentials;
    return { calls, module, register: library.WebRPG_RegisterAccountRequest };
}

test('registered account POST requests include browser credentials on both Unity fetch paths', async () => {
    for (const method of ['fetchWithProgress', 'cachedFetch']) {
        const state = setup();
        state.register(endpoint);
        const signal = new AbortController().signal;
        const progress = () => {};
        const options = { method: 'POST', headers: { 'X-WebRPG-Request': '1' }, signal, onProgress: progress };
        assert.equal(await state.module[method](endpoint, options), 'response');
        assert.equal(state.calls[0].init.credentials, 'include');
        assert.equal(state.calls[0].init.signal, signal);
        assert.equal(state.calls[0].init.onProgress, progress);
        assert.equal(state.calls[0].init.headers, options.headers);
        assert.equal(options.credentials, undefined);
        assert.equal(state.calls[0].receiver, state.module);
    }
});

test('assets, other hosts, unregistered URLs, and GETs retain their original credential mode', async () => {
    const state = setup();
    state.register(endpoint);
    for (const [resource, method] of [
        ['https://ws.oldshaw-webrpg.com/Build/game.data', 'POST'],
        ['https://other.example/api/accounts/refresh', 'POST'],
        ['https://ws.oldshaw-webrpg.com/api/accounts/logout', 'POST'],
        [endpoint, 'GET']
    ]) {
        const options = { method, credentials: 'omit' };
        await state.module.fetchWithProgress(resource, options);
        assert.equal(state.calls.at(-1).init, options);
        assert.equal(state.calls.at(-1).init.credentials, 'omit');
    }
});

test('repeated registration does not stack wrappers and can register a second account endpoint', async () => {
    const state = setup();
    state.register(endpoint);
    const wrapper = state.module.fetchWithProgress;
    state.register(endpoint);
    const login = 'https://ws.oldshaw-webrpg.com/api/accounts/login';
    state.register(login);
    assert.equal(state.module.fetchWithProgress, wrapper);
    await state.module.fetchWithProgress(login, { method: 'POST' });
    assert.equal(state.calls.length, 1);
    assert.equal(state.calls[0].init.credentials, 'include');
});

test('non-account or non-HTTP URLs cannot be registered', () => {
    const state = setup();
    for (const address of ['https://ws.oldshaw-webrpg.com/other', 'file:///api/accounts/login'])
        assert.throws(() => state.register(address));
});
