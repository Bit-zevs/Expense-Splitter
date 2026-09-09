import { Decimal, toCents, fromCents } from './money.js';

function exactResult(cents) {
  let coefficient = cents.abs();
  let scale = 2;
  const maximumCoefficient = new Decimal('79228162514264337593543950335');
  while (coefficient.gt(maximumCoefficient) && scale > 0 && coefficient.mod(10).isZero()) {
    coefficient = coefficient.dividedBy(10);
    scale--;
  }
  if (coefficient.gt(maximumCoefficient))
    throw new Error('Money cannot be represented exactly as a decimal monetary value.');
  return fromCents(cents);
}

// Same stable participant-ID order and greedy matching as the backend.
export function calculateSettlement(participants, expenses) {
  const ids = participants.map(p => p.id).sort();
  const amounts = new Map(ids.map(id => [id, new Decimal(0)]));
  for (const expense of expenses) {
    amounts.set(expense.paidByParticipantId, amounts.get(expense.paidByParticipantId).plus(toCents(expense.amount)));
    for (const share of expense.shares)
      amounts.set(share.participantId, amounts.get(share.participantId).minus(toCents(share.amount)));
  }
  const balances = ids.map(participantId => ({ participantId, balance: exactResult(amounts.get(participantId)) }));
  const debtors = balances.filter(b => toCents(b.balance).lt(0)).map(b => ({ ...b, cents: toCents(b.balance).negated() }));
  const creditors = balances.filter(b => toCents(b.balance).gt(0)).map(b => ({ ...b, cents: toCents(b.balance) }));
  const transfers = [];
  let d = 0, c = 0;
  while (d < debtors.length && c < creditors.length) {
    const debtor = debtors[d], creditor = creditors[c];
    const amount = Decimal.min(debtor.cents, creditor.cents);
    transfers.push({ fromParticipantId: debtor.participantId, toParticipantId: creditor.participantId, amount: exactResult(amount) });
    debtor.cents = debtor.cents.minus(amount);
    creditor.cents = creditor.cents.minus(amount);
    if (debtor.cents.isZero()) d++;
    if (creditor.cents.isZero()) c++;
  }
  return { balances, transfers };
}
