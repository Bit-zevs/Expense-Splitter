import test from 'node:test';
import assert from 'node:assert/strict';
import { calculateSettlement } from './settlement.js';

test('demo uses backend participant-ID matching order, not largest-balance matching', () => {
  const ids = [1, 2, 3, 4].map(n => `00000000-0000-4000-8000-00000000000${n}`);
  const participants = ids.toReversed().map(id => ({ id }));
  const expenses = [[0, 2, '4.00'], [0, 3, '2.00'], [1, 3, '4.00']].map(([payer, debtor, amount]) => ({
    paidByParticipantId: ids[payer], amount, shares: [{ participantId: ids[debtor], amount }],
  }));
  assert.deepEqual(calculateSettlement(participants, expenses).transfers, [
    { fromParticipantId: ids[2], toParticipantId: ids[0], amount: '4.00' },
    { fromParticipantId: ids[3], toParticipantId: ids[0], amount: '2.00' },
    { fromParticipantId: ids[3], toParticipantId: ids[1], amount: '4.00' },
  ]);
});

test('demo uses the same exact decimal result limits as the backend', () => {
  const participants = [{ id: 'a' }, { id: 'b' }];
  const expense = { amount: '792281625142643375935439503.35', paidByParticipantId: 'a', shares: [{ participantId: 'b', amount: '792281625142643375935439503.35' }] };
  assert.equal(calculateSettlement(participants, [expense, expense]).transfers[0].amount, '1584563250285286751870879006.70');
  assert.throws(() => calculateSettlement(participants, [expense, expense, expense]), /exactly/);
});
