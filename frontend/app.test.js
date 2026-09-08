import test from 'node:test';
import assert from 'node:assert/strict';

const appElement = { innerHTML: '' };
const elements = new Map();
let checkedParticipants = [];
globalThis.document = {
  getElementById: id => id === 'app' ? appElement : elements.get(id),
  querySelector: () => null,
  querySelectorAll: selector => selector === 'input[name="split-participant"]:checked' ? checkedParticipants : [],
};
globalThis.location = { hash: '#/' };
globalThis.window = { addEventListener() {}, setTimeout() {} };
let respond;
const calls = [];
globalThis.fetch = async (url, options) => {
  calls.push([url, options]);
  return respond(url, options);
};
const { loadTrip, handleRouteChange, getState, refreshCalculation, calculateSettlement } = await import('./app.js');
const a = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const b = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';
const ok = value => ({ ok: true, status: 200, json: async () => value });
function data(url) {
  const id = url.split('/trips/')[1].split('/')[0];
  if (url.endsWith('/participants')) return ok([{ id: 'p', name: 'P' }, { id: 'q', name: 'Q' }]);
  if (url.endsWith('/expenses')) return ok([{ id: 'e', description: 'Expense', occurredAt: '2026-09-08', amount: '90071992547409.91', paidByParticipantId: 'p', shares: [{ participantId: 'q', amount: '90071992547409.91' }] }]);
  if (url.endsWith('/settlements')) return ok({ balances: [{ participantId: 'p', balance: '90071992547409.91' }], transfers: [] });
  return ok({ id, name: id, createdAt: '2026-09-08', currency: 'RUB' });
}

test('late loads cannot replace a newer route, invalid route hides previous trip, return reloads', async () => {
  let release;
  const gate = new Promise(resolve => { release = resolve; });
  respond = async url => { if (url.includes(a)) await gate; return data(url); };
  location.hash = `#/trip/${a}`;
  const first = handleRouteChange();
  location.hash = `#/trip/${b}`;
  await handleRouteChange();
  release();
  await first;
  assert.equal(getState().trip.id, b);
  assert.equal(getState().settlement.balances[0].balance, '90071992547409.91');
  location.hash = '#/trip/invalid';
  await handleRouteChange();
  assert.ok(!appElement.innerHTML.includes(b));
  calls.length = 0;
  location.hash = `#/trip/${b}`;
  await handleRouteChange();
  assert.equal(calls.length, 4);
  assert.ok(calls.every(([url]) => !url.endsWith('/balances')));
});

test('calculation failure preserves expenses and successful mutation feedback', async () => {
  respond = url => url.endsWith('/settlements')
    ? { ok: false, status: 422, json: async () => ({ title: 'Overflow' }) } : data(url);
  location.hash = `#/trip/${a}`;
  await loadTrip(a);
  assert.equal(getState().expenses.length, 1);
  assert.equal(getState().calculationError, 'Overflow');
  await refreshCalculation('Расход добавлен');
  assert.equal(getState().expenses.length, 1);
  assert.equal(getState().modal, null);
  assert.match(getState().toast, /Расход добавлен.*Не удалось обновить/);
  assert.equal(calculateSettlement().transfers[0].amount, '90071992547409.91');
});

test('successful participant POST remains saved when calculation refresh fails', async () => {
  let submit;
  const form = { addEventListener: (_, handler) => { submit = handler; }, setAttribute() {}, querySelector: () => null };
  elements.set('participant-form', form);
  elements.set('participant-name', { value: 'New' });
  respond = (url, options) => options.method === 'POST' ? ok({ id: 'new', name: 'New' })
    : url.endsWith('/settlements') ? { ok: false, status: 422, json: async () => ({ title: 'Overflow' }) } : data(url);
  location.hash = `#/trip/${a}`;
  await handleRouteChange();
  calls.length = 0;
  await submit({ preventDefault() {}, currentTarget: form });
  assert.equal(getState().participants.filter(p => p.id === 'new').length, 1);
  assert.match(getState().toast, /добавлен.*Не удалось обновить/);
  assert.equal(calls.length, 2);
  assert.equal(calls[0][1].method, 'POST');
  assert.ok(calls[1][0].endsWith('/settlements'));
  elements.clear();
});

test('expense submit sends exact money and retains 201 result after refresh failure', async () => {
  let submit;
  const form = { addEventListener: (_, handler) => { submit = handler; }, setAttribute() {}, querySelector: () => null };
  elements.set('expense-form', form);
  for (const [id, value] of Object.entries({
    'expense-description': 'New expense', 'expense-amount': '792281625142643375935439503.35',
    'expense-payer': 'p', 'expense-occurred-at': '2026-09-08T12:00',
  })) elements.set(id, { value });
  checkedParticipants = [{ value: 'q' }];
  respond = (url, options) => {
    if (options.method === 'POST') {
      const request = JSON.parse(options.body);
      assert.equal(request.amount, '792281625142643375935439503.35');
      return ok({ ...request, id: 'new-expense', shares: [{ participantId: 'q', amount: request.amount }] });
    }
    return url.endsWith('/settlements') ? { ok: false, status: 422, json: async () => ({ title: 'Overflow' }) } : data(url);
  };
  location.hash = `#/trip/${a}`;
  await handleRouteChange();
  calls.length = 0;
  await submit({ preventDefault() {}, currentTarget: form });
  assert.equal(getState().expenses.filter(e => e.id === 'new-expense').length, 1);
  assert.equal(getState().modal, null);
  assert.match(getState().toast, /Расход добавлен.*Не удалось обновить/);
  assert.equal(calls.length, 2);
  elements.clear();
  checkedParticipants = [];
});

test('late mutation response cannot modify another trip', async () => {
  let submit;
  let release;
  const gate = new Promise(resolve => { release = resolve; });
  const form = { addEventListener: (_, handler) => { submit = handler; }, setAttribute() {}, querySelector: () => null };
  elements.set('participant-form', form);
  elements.set('participant-name', { value: 'Late' });
  respond = async (url, options) => {
    if (options.method === 'POST') { await gate; return ok({ id: 'late', name: 'Late' }); }
    return data(url);
  };
  location.hash = `#/trip/${a}`;
  await handleRouteChange();
  const pending = submit({ preventDefault() {}, currentTarget: form });
  location.hash = `#/trip/${b}`;
  await handleRouteChange();
  release();
  await pending;
  assert.equal(getState().trip.id, b);
  assert.ok(getState().participants.every(p => p.id !== 'late'));
  elements.clear();
});

test('older calculation refresh cannot overwrite a newer result', async () => {
  respond = data;
  location.hash = `#/trip/${a}`;
  await handleRouteChange();
  let release;
  const gate = new Promise(resolve => { release = resolve; });
  let count = 0;
  respond = async () => {
    const request = ++count;
    if (request === 1) await gate;
    return ok({ balances: [{ participantId: 'p', balance: `${request}.00` }], transfers: [] });
  };
  const old = refreshCalculation();
  await refreshCalculation();
  release();
  await old;
  assert.equal(getState().settlement.balances[0].balance, '2.00');
});
