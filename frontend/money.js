export const MAX_CENTS = 79228162514264337593543950335n;

export function toCents(value) {
  const match = /^(-?)(\d+)(?:\.(\d{1,2}))?$/.exec(String(value));
  if (!match) throw new Error('Введите сумму с точностью до двух знаков после запятой.');
  return (match[1] ? -1n : 1n) * (BigInt(match[2]) * 100n + BigInt((match[3] || '').padEnd(2, '0')));
}

export function fromCents(cents) {
  const absolute = cents < 0n ? -cents : cents;
  return `${cents < 0n ? '-' : ''}${absolute / 100n}.${String(absolute % 100n).padStart(2, '0')}`;
}

export function parseAmount(value) {
  const cents = toCents(value.trim().replace(',', '.'));
  if (cents <= 0n || cents > MAX_CENTS) throw new Error('Сумма должна быть больше нуля и не превышать 792281625142643375935439503.35.');
  return fromCents(cents);
}

export function formatMoney(value, currency) {
  const cents = toCents(value);
  const absolute = cents < 0n ? -cents : cents;
  const whole = new Intl.NumberFormat('ru-RU').format(absolute / 100n);
  const fraction = absolute % 100n;
  const symbol = new Intl.NumberFormat('ru-RU', { style: 'currency', currency })
    .formatToParts(0).find(part => part.type === 'currency').value;
  return `${cents < 0n ? '−' : ''}${whole}${fraction ? ',' + String(fraction).padStart(2, '0') : ''}\u00a0${symbol}`;
}

export function splitIntoShares(amount, participantIds) {
  const cents = toCents(amount);
  const count = BigInt(participantIds.length);
  if (!count) throw new Error('Выберите участников.');
  return participantIds.map((participantId, index) => ({
    participantId,
    amount: fromCents(cents / count + (BigInt(index) < cents % count ? 1n : 0n)),
  }));
}
