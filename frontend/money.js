import DecimalJs from 'decimal.js';

// 60 significant decimal digits cover .NET decimal amounts and totals of JS arrays.
export const Decimal = DecimalJs.clone({ precision: 60 });
export const MAX_CENTS = new Decimal('79228162514264337593543950335');

export function toCents(value) {
  if (!/^-?\d+(?:\.\d{1,2})?$/.test(String(value)))
    throw new Error('Введите сумму с точностью до двух знаков после запятой.');
  return new Decimal(value).times(100);
}

export function fromCents(cents) {
  return new Decimal(cents).dividedBy(100).toFixed(2);
}

export function parseAmount(value) {
  const cents = toCents(value.trim().replace(',', '.'));
  if (cents.lte(0) || cents.gt(MAX_CENTS))
    throw new Error('Сумма должна быть больше нуля и не превышать 792281625142643375935439503.35.');
  return fromCents(cents);
}

export function formatMoney(value, currency) {
  const amount = new Decimal(value);
  const [whole, fraction] = amount.abs().toFixed(2).split('.');
  const grouped = whole.replace(/\B(?=(\d{3})+(?!\d))/g, '\u00a0');
  const symbol = new Intl.NumberFormat('ru-RU', { style: 'currency', currency })
    .formatToParts(0).find(part => part.type === 'currency').value;
  return `${amount.lt(0) ? '−' : ''}${grouped}${fraction === '00' ? '' : ',' + fraction}\u00a0${symbol}`;
}

export function splitIntoShares(amount, participantIds) {
  const cents = toCents(amount);
  const ids = [...participantIds].sort();
  if (!ids.length) throw new Error('Выберите участников.');
  const base = cents.dividedToIntegerBy(ids.length);
  const remainder = cents.mod(ids.length);
  return ids.map((participantId, index) => ({
    participantId, amount: fromCents(base.plus(remainder.gt(index) ? 1 : 0)),
  }));
}
