import assert from 'node:assert/strict';
import test from 'node:test';
import { ApiError, createApi } from './api.js';

test('maps frontend operations to backend endpoints', async () => {
  const calls = [];
  const fetchImplementation = async (url, options) => {
    calls.push({ url, options });
    return { ok: true, status: 200, json: async () => ({}) };
  };
  const api = createApi('http://localhost:5050/', fetchImplementation);
  const tripId = 'trip-id';
  const participantId = 'participant-id';
  const expenseId = 'expense-id';

  await api.health();
  await api.createTrip('Trip', 'EUR');
  await api.getTrip(tripId);
  await api.deleteTrip(tripId);
  await api.getParticipants(tripId);
  await api.getParticipant(tripId, participantId);
  await api.addParticipant(tripId, 'Alice');
  await api.deleteParticipant(tripId, participantId);
  await api.getExpenses(tripId);
  await api.getExpense(tripId, expenseId);
  await api.addExpense(tripId, { amount: 10, occurredAt: '2026-09-08T09:37:00.000Z' });
  await api.deleteExpense(tripId, expenseId);
  await api.getBalances(tripId);
  await api.getSettlements(tripId);

  assert.deepEqual(calls.map(call => [call.url, call.options.method || 'GET']), [
    ['http://localhost:5050/health', 'GET'],
    ['http://localhost:5050/trips', 'POST'],
    ['http://localhost:5050/trips/trip-id', 'GET'],
    ['http://localhost:5050/trips/trip-id', 'DELETE'],
    ['http://localhost:5050/trips/trip-id/participants', 'GET'],
    ['http://localhost:5050/trips/trip-id/participants/participant-id', 'GET'],
    ['http://localhost:5050/trips/trip-id/participants', 'POST'],
    ['http://localhost:5050/trips/trip-id/participants/participant-id', 'DELETE'],
    ['http://localhost:5050/trips/trip-id/expenses', 'GET'],
    ['http://localhost:5050/trips/trip-id/expenses/expense-id', 'GET'],
    ['http://localhost:5050/trips/trip-id/expenses', 'POST'],
    ['http://localhost:5050/trips/trip-id/expenses/expense-id', 'DELETE'],
    ['http://localhost:5050/trips/trip-id/balances', 'GET'],
    ['http://localhost:5050/trips/trip-id/settlements', 'GET'],
  ]);
  assert.equal(calls[1].options.body, JSON.stringify({ name: 'Trip', currency: 'EUR' }));
  assert.equal(calls[1].options.headers['Content-Type'], 'application/json');
  assert.equal(calls[10].options.body, JSON.stringify({
    amount: 10,
    occurredAt: '2026-09-08T09:37:00.000Z',
  }));
});

test('surfaces API validation messages', async () => {
  const api = createApi('http://localhost:5050', async () => ({
    ok: false,
    status: 400,
    json: async () => ({ errors: { currency: ['Unsupported currency.'] } }),
  }));

  await assert.rejects(
    () => api.createTrip('Trip', 'GBP'),
    error => error instanceof ApiError
      && error.status === 400
      && error.message === 'Unsupported currency.',
  );
});
