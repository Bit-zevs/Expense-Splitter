import test from 'node:test';
import assert from 'node:assert/strict';
import { parseAmount, toCents, fromCents, splitIntoShares, formatMoney, MAX_CENTS } from './money.js';

test('preserves cents throughout the supported range', () => {
  for (const value of ['90071992547409.91', '792281625142643375935439503.35', '0.01']) {
    assert.equal(parseAmount(value), value);
    assert.equal(fromCents(toCents(value)), value);
    const shares = splitIntoShares(value, ['a', 'b', 'c']);
    assert.equal(shares.reduce((sum, share) => sum + toCents(share.amount), 0n), toCents(value));
  }
  assert.equal(fromCents(MAX_CENTS + MAX_CENTS), '1584563250285286751870879006.70');
  assert.equal(toCents('1584563250285286751870879006.7'), MAX_CENTS * 2n);
  assert.equal(formatMoney('1584563250285286751870879006.7', 'RUB').replaceAll('\u00a0', ''), '1584563250285286751870879006,70₽');
  assert.equal(formatMoney('90071992547409.91', 'RUB').replaceAll('\u00a0', ''), '90071992547409,91₽');
});

test('rejects invalid money without rounding', () => {
  for (const value of ['0', '-1', '1.001', '1e10', '', 'NaN', '792281625142643375935439503.36']) {
    assert.throws(() => parseAmount(value));
  }
  assert.equal(parseAmount('12,3'), '12.30');
  assert.deepEqual(splitIntoShares('0.01', ['a', 'b']).map(s => s.amount), ['0.01', '0.00']);
});
