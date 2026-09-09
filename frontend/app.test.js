import test, { beforeEach } from 'node:test';
import assert from 'node:assert/strict';

const appElement = { innerHTML: '' };
const elements = new Map(), selectors = new Map();
let checked = [];
globalThis.document = {
  getElementById: id => id === 'app' ? appElement : elements.get(id),
  querySelector: selector => selectors.get(selector)?.[0] || null,
  querySelectorAll: selector => selector === 'input[name="split-participant"]:checked' ? checked : selectors.get(selector) || [],
};
globalThis.location = { hash: '#/' };
globalThis.window = { addEventListener() {}, setTimeout() {} };
let respond;
const calls = [];
globalThis.fetch = async (url, options) => { calls.push([url, options]); return respond(url, options); };
const { loadTrip, handleRouteChange, getState, refreshCalculation } = await import('./app.js');
const a = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const b = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';
const ok = value => ({ ok: true, status: 200, json: async () => structuredClone(value) });
const fail = (status, title = 'Failure') => ({ ok: false, status, json: async () => ({ title }) });
const deferred = () => { let release; const promise = new Promise(resolve => { release = resolve; }); return { promise, release }; };
const expense = (id = 'e', payer = 'p', participant = 'q') => ({
  id, description: id, amount: '90071992547409.91', occurredAt: '2026-09-08', paidByParticipantId: payer,
  shares: [{ participantId: participant, amount: '90071992547409.91' }],
});
function snapshot(id = a, calculationError = null) {
  return {
    trip: { id, name: id, createdAt: '2026-09-08', currency: 'RUB' },
    participants: [{ id: 'p', name: 'P' }, { id: 'q', name: 'Q' }], expenses: [expense()],
    settlement: calculationError ? null : { balances: [{ participantId: 'p', balance: '90071992547409.91' }], transfers: [] },
    calculationError,
  };
}
function data(url) {
  assert.ok(url.endsWith('/snapshot'), url);
  return ok(snapshot(url.split('/trips/')[1].split('/')[0]));
}
async function open(id = a) { location.hash = `#/trip/${id}`; await handleRouteChange(); }
function form(id) {
  let submit;
  const error = { textContent: '' };
  const element = { addEventListener: (_, handler) => { submit = handler; }, setAttribute() {}, querySelector: selector => selector === '.inline-error' ? error : null };
  elements.set(id, element);
  return { submit: () => submit({ preventDefault() {}, currentTarget: element }), error };
}
function button(selector, dataset = {}) {
  let click;
  selectors.set(selector, [{ dataset, addEventListener: (_, handler) => { click = handler; } }]);
  return () => click();
}
beforeEach(async () => {
  elements.clear(); selectors.clear(); checked = []; calls.length = 0;
  respond = data; location.hash = '#/'; await handleRouteChange();
});

test('one snapshot per route, late load cannot replace newer route; invalid route hides stale data', async () => {
  const gate = deferred();
  respond = async url => { if (url.includes(a)) await gate.promise; return data(url); };
  const old = open(a);
  await open(b);
  gate.release(); await old;
  assert.equal(getState().trip.id, b);
  assert.equal(getState().settlement.balances[0].balance, '90071992547409.91');
  location.hash = '#/trip/invalid'; await handleRouteChange();
  assert.ok(!appElement.innerHTML.includes(b));
  calls.length = 0; await open(b);
  assert.equal(calls.length, 1);
  assert.ok(calls[0][0].endsWith('/snapshot'));
});

test('failed calculation in a successful snapshot does not hide base data', async () => {
  respond = () => ok(snapshot(a, 'Overflow'));
  await open();
  assert.equal(getState().expenses.length, 1);
  assert.equal(getState().participants.length, 2);
  assert.equal(getState().calculationError, 'Overflow');
});

test('successful participant POST remains saved when snapshot refresh fails', async () => {
  const participantForm = form('participant-form'); elements.set('participant-name', { value: 'New' });
  await open(); calls.length = 0;
  respond = (_, options) => options.method === 'POST' ? ok({ id: 'new', name: 'New' }) : fail(503);
  await participantForm.submit();
  assert.equal(getState().participants.filter(p => p.id === 'new').length, 1);
  assert.equal(getState().modal, null);
  assert.match(getState().toast, /добавлен.*Не удалось обновить/);
  assert.equal(calls.length, 2);
  assert.ok(calls[1][0].endsWith('/snapshot'));
});

test('expense POST preserves decimal and serializes local time to UTC; refresh failure cannot undo success', async t => {
  const oldTimezone = process.env.TZ; process.env.TZ = 'Asia/Yekaterinburg';
  t.after(() => { if (oldTimezone === undefined) delete process.env.TZ; else process.env.TZ = oldTimezone; });
  const expenseForm = form('expense-form');
  for (const [id, value] of Object.entries({ 'expense-description': 'New', 'expense-amount': '792281625142643375935439503.35', 'expense-payer': 'p', 'expense-occurred-at': '2026-09-08T12:00' })) elements.set(id, { value });
  checked = [{ value: 'q' }];
  await open(); calls.length = 0;
  respond = (_, options) => {
    if (options.method !== 'POST') return fail(503);
    const request = JSON.parse(options.body);
    assert.equal(request.amount, '792281625142643375935439503.35');
    assert.equal(request.occurredAt, '2026-09-08T07:00:00.000Z');
    return ok({ ...expense('new'), ...request });
  };
  await expenseForm.submit();
  assert.equal(getState().expenses.filter(e => e.id === 'new').length, 1);
  assert.match(getState().toast, /Расход добавлен.*Не удалось обновить/);
  assert.equal(calls.length, 2);
});

test('late mutation response cannot modify another trip', async () => {
  const participantForm = form('participant-form'); elements.set('participant-name', { value: 'Late' });
  await open();
  const gate = deferred();
  respond = async (url, options) => { if (options.method === 'POST') { await gate.promise; return ok({ id: 'late', name: 'Late' }); } return data(url); };
  const pending = participantForm.submit(); await open(b); gate.release(); await pending;
  assert.equal(getState().trip.id, b);
  assert.ok(getState().participants.every(p => p.id !== 'late'));
});

test('newer refresh replaces the whole snapshot; older refresh cannot overwrite it', async () => {
  await open(); const gate = deferred(); let count = 0;
  respond = async () => {
    const index = ++count; if (index === 1) await gate.promise;
    const value = snapshot(); value.participants.push({ id: `new${index}`, name: 'New' });
    value.expenses = [expense('new', `new${index}`, 'p')];
    value.settlement.balances = [{ participantId: `new${index}`, balance: '1.00' }];
    return ok(value);
  };
  const old = refreshCalculation(); await refreshCalculation(); gate.release(); await old;
  assert.equal(getState().expenses[0].paidByParticipantId, 'new2');
  assert.equal(getState().settlement.balances[0].participantId, 'new2');
  assert.ok(getState().participants.some(p => p.id === 'new2'));
});

test('delete participant removes related expenses even if refresh fails', async () => {
  const deletionForm = form('delete-form'); const remove = button('[data-delete-participant]', { deleteParticipant: 'q' });
  respond = () => { const value = snapshot(); value.expenses = [expense('share'), expense('payer', 'q', 'p'), expense('keep', 'p', 'p')]; return ok(value); };
  await open(); remove(); calls.length = 0;
  respond = (_, options) => options.method === 'DELETE' ? { ok: true, status: 204 } : fail(503);
  await deletionForm.submit();
  assert.deepEqual(getState().participants.map(p => p.id), ['p']);
  assert.deepEqual(getState().expenses.map(e => e.id), ['keep']);
  assert.equal(getState().pendingDeletion, null);
  assert.match(getState().toast, /удалён.*Не удалось обновить/);
  assert.equal(calls.length, 2);
});

for (const action of ['participant', 'expense', 'delete']) test(`409 on ${action} reloads full state without replaying mutation`, async () => {
  const mutationForm = form(`${action === 'delete' ? 'delete' : action}-form`);
  elements.set('participant-name', { value: 'New' });
  for (const [id, value] of Object.entries({ 'expense-description': 'New', 'expense-amount': '1.00', 'expense-payer': 'p', 'expense-occurred-at': '2026-09-08T12:00' })) elements.set(id, { value });
  checked = [{ value: 'q' }];
  const remove = button('[data-delete-participant]', { deleteParticipant: 'q' });
  await open(); if (action === 'delete') remove(); calls.length = 0;
  respond = (_, options) => {
    if (options.method === 'POST' || options.method === 'DELETE') return fail(409, 'Conflict');
    const value = snapshot(); value.participants = [{ id: 'fresh', name: 'Fresh' }]; value.expenses = []; value.settlement = { balances: [], transfers: [] }; return ok(value);
  };
  await mutationForm.submit();
  assert.deepEqual(getState().participants.map(p => p.id), ['fresh']);
  assert.deepEqual(getState().expenses, []);
  assert.equal(getState().modal, null);
  assert.match(getState().toast, /были обновлены.*Повторите действие/);
  assert.equal(calls.length, 2);
});

for (const status of [404, 503]) test(`failed 409 recovery (${status}) removes stale editable state`, async () => {
  const participantForm = form('participant-form'); elements.set('participant-name', { value: 'New' });
  await open();
  respond = (_, options) => fail(options.method === 'POST' ? 409 : status);
  await participantForm.submit();
  assert.equal(getState().trip, null);
  assert.match(appElement.innerHTML, /Повторить загрузку/);
});

test('late 409 reload does not replace a newer route', async () => {
  const participantForm = form('participant-form'); elements.set('participant-name', { value: 'New' });
  await open(); const gate = deferred(); const started = deferred();
  respond = async (url, options) => {
    if (options.method === 'POST') return fail(409);
    if (url.includes(a)) { started.release(); await gate.promise; }
    return data(url);
  };
  const pending = participantForm.submit(); await started.promise;
  assert.equal(getState().trip, null);
  await open(b); gate.release(); await pending;
  assert.equal(getState().trip.id, b);
});
