import { createApi } from './api.js';

const DEMO_TRIP_ID = 'f47ac10b-58cc-4372-a567-0e02b2c3d479';
const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL || 'http://localhost:5050').replace(/\/$/, '');
const api = createApi(API_BASE_URL);

function splitIntoShares(amount, participantIds) {
  const totalCents = Math.round(Number(amount) * 100);
  const base = Math.floor(totalCents / participantIds.length);
  let remainder = totalCents - base * participantIds.length;

  return participantIds.map(participantId => {
    const cents = base + (remainder-- > 0 ? 1 : 0);
    return { participantId, amount: cents / 100 };
  });
}

function createDemoState() {
  const p1 = '6f9619ff-8b86-d011-b42d-00cf4fc964ff';
  const p2 = '7c9e6679-7425-40de-944b-e07fc1f90ae7';
  const p3 = '9c858901-8a57-4791-81fe-4c455b099bc9';
  const p4 = '16fd2706-8baf-433b-82eb-8c7fada847da';
  const participants = [
    { id: p1, name: 'Алексей' },
    { id: p2, name: 'Мария' },
    { id: p3, name: 'Иван' },
    { id: p4, name: 'Ольга' },
  ];

  const expense = (id, amount, description, paidByParticipantId, participantIds, createdAt) => ({
    id,
    amount,
    description,
    paidByParticipantId,
    splitType: 'equal',
    createdAt,
    shares: splitIntoShares(amount, participantIds),
  });

  return {
    trip: {
      id: DEMO_TRIP_ID,
      name: 'Алтай 2026',
      createdAt: '2026-08-12T09:30:00Z',
      currency: 'RUB',
    },
    participants,
    expenses: [
      expense('0f8fad5b-d9cb-469f-a165-70867728950e', 12000, 'Проживание', p1, [p1, p2, p3, p4], '2026-08-12T11:15:00Z'),
      expense('1f8fad5b-d9cb-469f-a165-70867728950e', 4500, 'Продукты', p2, [p1, p2, p3, p4], '2026-08-12T17:40:00Z'),
      expense('2f8fad5b-d9cb-469f-a165-70867728950e', 3200, 'Топливо', p3, [p1, p2, p3], '2026-08-13T08:20:00Z'),
      expense('3f8fad5b-d9cb-469f-a165-70867728950e', 6000, 'Экскурсия', p4, [p1, p2, p3, p4], '2026-08-13T14:10:00Z'),
    ],
    activeTab: 'expenses',
    modal: null,
    selectedExpenseId: null,
    toast: null,
  };
}

let state = {
  trip: null,
  participants: [],
  expenses: [],
  settlement: { balances: [], transfers: [] },
  activeTab: 'expenses',
  modal: null,
  selectedExpenseId: null,
  toast: null,
  isDemo: false,
};
const app = document.getElementById('app');

function money(value) {
  return new Intl.NumberFormat('ru-RU', {
    style: 'currency',
    currency: state.trip.currency || 'RUB',
    minimumFractionDigits: 0,
    maximumFractionDigits: 2,
  }).format(value);
}

function dateLabel(value) {
  return new Intl.DateTimeFormat('ru-RU', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  }).format(new Date(value));
}

function icon(name, size = 18) {
  const paths = {
    plus: '<path d="M12 5v14M5 12h14"/>',
    arrow: '<path d="m9 18 6-6-6-6"/>',
    back: '<path d="m15 18-6-6 6-6"/>',
    receipt: '<path d="M6 2v20l3-2 3 2 3-2 3 2V2l-3 2-3-2-3 2-3-2Z"/><path d="M9 9h6M9 13h6"/>',
    close: '<path d="m18 6-12 12M6 6l12 12"/>',
    brand: '<path d="M4 7h16M7 4v6M17 4v6M6 12h12v8H6z"/><path d="M9 15h6"/>',
    link: '<path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"/><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"/>',
    check: '<path d="m20 6-11 11-5-5"/>',
    users: '<path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75"/>',
  };
  return `<svg width="${size}" height="${size}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${paths[name] || ''}</svg>`;
}

function header() {
  return `
    <header class="container topbar">
      <a class="brand" href="#/">
        <span class="brand-mark">${icon('brand', 16)}</span>
        <span>Expense Splitter</span>
      </a>
      <a class="btn btn-primary btn-sm" href="#/create">Создать поездку</a>
    </header>`;
}

function home() {
  return `
    <div class="shell">
      ${header()}
      <main class="container">
        <section class="hero-card">
          <div class="hero-copy">
            <div class="hero-eyebrow">Для поездок и небольших компаний</div>
            <h1>Кто за кого заплатил — посчитаем сами</h1>
            <p>Создайте поездку, добавьте участников и расходы. В конце получите баланс каждого и короткий список переводов.</p>
            <div class="hero-actions">
              <a class="btn btn-primary" href="#/create">Создать поездку ${icon('arrow', 16)}</a>
              <button class="btn btn-outline" data-demo>Посмотреть пример</button>
            </div>
          </div>
          <div class="hero-visual"><img src="./assets/mountains.svg" alt="Горная поездка"></div>
        </section>

        <section class="open-card">
          <div>
            <div class="section-kicker">Уже есть поездка?</div>
            <h2>Откройте её по ссылке или ID</h2>
            <p>Сохраните ссылку на поездку и поделитесь ей с остальными участниками.</p>
          </div>
          <form id="open-trip-form" class="open-form">
            <label class="sr-only" for="trip-id">Ссылка или ID поездки</label>
            <input class="input" id="trip-id" placeholder="UUID поездки или ссылка" autocomplete="off" required />
            <button class="btn btn-dark" type="submit">Открыть</button>
          </form>
        </section>
      </main>
    </div>`;
}

function createTrip() {
  return `
    <div class="shell">
      ${header()}
      <main class="container narrow-page">
        <a class="back-link" href="#/">${icon('back', 15)} На главную</a>
        <section class="form-card">
          <div class="form-intro">
            <div class="section-kicker">Новая поездка</div>
            <h1>С чего начнём?</h1>
            <p>Для самой поездки достаточно названия. Валюта фиксируется один раз и используется для всех расходов внутри неё.</p>
          </div>
          <form id="create-trip-form">
            <div class="field">
              <label for="trip-name">Название</label>
              <input class="input" id="trip-name" name="tripName" placeholder="Например, Амстердам на выходные" autocomplete="off" required />
            </div>
            <div class="field">
              <label for="trip-currency">Валюта поездки</label>
              <select class="input" id="trip-currency">
                <option value="RUB">RUB — ₽</option>
                <option value="EUR">EUR — €</option>
                <option value="USD">USD — $</option>
              </select>
              <span class="field-hint">Одна валюта используется для всех расходов внутри поездки.</span>
            </div>
            <div class="form-actions">
              <a class="btn btn-outline" href="#/">Отмена</a>
              <button class="btn btn-primary" type="submit">Создать поездку</button>
            </div>
          </form>
        </section>
      </main>
    </div>`;
}

function tripPage() {
  return `
    <div class="shell">
      ${header()}
      <main class="container trip-layout">
        <section class="trip-card">
          <div class="trip-head">
            <a class="back-link" href="#/">${icon('back', 15)} На главную</a>
            <div class="trip-title-row">
              <div>
                <div class="section-kicker">Поездка</div>
                <h1>${escapeHtml(state.trip.name)}</h1>
                <p>Создана ${dateLabel(state.trip.createdAt)} · ${state.participants.length} ${plural(state.participants.length, 'участник', 'участника', 'участников')}</p>
              </div>
              <div class="trip-actions">
                <button class="btn btn-outline" data-copy-link>${icon('link', 16)} Скопировать ссылку</button>
                ${primaryTripAction()}
              </div>
            </div>
            <nav class="tabs" aria-label="Разделы поездки">
              ${tabButton('expenses', 'Расходы', state.expenses.length)}
              ${tabButton('participants', 'Участники', state.participants.length)}
              ${tabButton('results', 'Расчёт')}
            </nav>
          </div>
          <div class="trip-body">${tabContent()}</div>
        </section>
      </main>
      ${state.modal ? modalMarkup() : ''}
      ${state.toast ? `<div class="toast">${icon('check', 16)} ${escapeHtml(state.toast)}</div>` : ''}
    </div>`;
}

function primaryTripAction() {
  if (state.activeTab === 'participants') {
    return `<button class="btn btn-primary" data-open="participant">${icon('plus', 16)} Добавить участника</button>`;
  }
  if (state.activeTab === 'expenses') {
    return `<button class="btn btn-primary" data-open="expense" ${state.participants.length ? '' : 'disabled'}>${icon('plus', 16)} Добавить расход</button>`;
  }
  return '';
}

function tabButton(id, label, count) {
  return `<button class="tab ${state.activeTab === id ? 'active' : ''}" data-tab="${id}"><span>${label}</span>${typeof count === 'number' ? `<span class="tab-count">${count}</span>` : ''}</button>`;
}

function tabContent() {
  if (state.activeTab === 'participants') return participantsTab();
  if (state.activeTab === 'results') return resultsTab();
  return expensesTab();
}

function expensesTab() {
  const total = state.expenses.reduce((sum, expense) => sum + Number(expense.amount), 0);

  if (!state.participants.length) {
    return emptyState('Сначала добавьте участников', 'Расход должен ссылаться на плательщика и хотя бы одного участника поездки.', 'participant', 'Добавить участника');
  }

  if (!state.expenses.length) {
    return emptyState('Расходов пока нет', 'Добавьте первый расход, выберите плательщика и тех, между кем он делится поровну.', 'expense', 'Добавить расход');
  }

  return `
    <div class="content-summary">
      <div><span>Всего расходов</span><strong>${money(total)}</strong></div>
      <div><span>Записей</span><strong>${state.expenses.length}</strong></div>
    </div>
    <div class="expense-list">
      ${[...state.expenses].reverse().map(expense => {
        const payer = participantById(expense.paidByParticipantId);
        return `
          <button class="expense-row" data-expense-id="${expense.id}">
            <span class="row-icon">${icon('receipt', 18)}</span>
            <span class="row-main">
              <strong>${escapeHtml(expense.description)}</strong>
              <small>${dateLabel(expense.createdAt)} · оплатил ${escapeHtml(payer?.name || '—')} · ${expense.shares.length} ${plural(expense.shares.length, 'участник', 'участника', 'участников')}</small>
            </span>
            <span class="row-amount">${money(expense.amount)}</span>
            <span class="row-chevron">${icon('arrow', 16)}</span>
          </button>`;
      }).join('')}
    </div>`;
}

function participantsTab() {
  if (!state.participants.length) {
    return emptyState('Участников пока нет', 'Участник — это только имя внутри конкретной поездки. Никаких отдельных профилей не требуется.', 'participant', 'Добавить участника');
  }

  return `
    <div class="tab-description">
      <div>
        <h2>Участники поездки</h2>
        <p>Здесь только люди из этой поездки — без аккаунтов, профилей и лишних настроек.</p>
      </div>
    </div>
    <div class="participants-list">
      ${state.participants.map((participant, index) => {
        const paid = state.expenses
          .filter(expense => expense.paidByParticipantId === participant.id)
          .reduce((sum, expense) => sum + Number(expense.amount), 0);
        return `
          <div class="person-row">
            <div class="avatar">${escapeHtml(participant.name.slice(0, 1).toUpperCase())}</div>
            <div class="person-main"><strong>${escapeHtml(participant.name)}</strong><span>Участник ${index + 1}</span></div>
            <div class="person-meta"><span>Оплатил</span><strong>${money(paid)}</strong></div>
          </div>`;
      }).join('')}
    </div>`;
}

function resultsTab() {
  if (!state.expenses.length) {
    return `
      <div class="results-empty">
        <div class="result-icon">${icon('receipt', 22)}</div>
        <h2>Расчёт появится после первого расхода</h2>
        <p>Баланс и переводы появятся автоматически на основе внесённых расходов.</p>
        <button class="btn btn-primary" data-open="expense" ${state.participants.length ? '' : 'disabled'}>${icon('plus', 16)} Добавить расход</button>
      </div>`;
  }

  const { balances, transfers } = state.isDemo
    ? calculateSettlement()
    : state.settlement;
  const total = state.expenses.reduce((sum, expense) => sum + Number(expense.amount), 0);

  return `
    <div class="results-hero">
      <div>
        <span>Общие расходы</span>
        <strong>${money(total)}</strong>
      </div>
      <p>Положительный баланс — участнику должны вернуть деньги. Отрицательный — участник должен доплатить.</p>
    </div>

    <div class="results-columns">
      <section>
        <div class="section-title-row"><h2>Балансы</h2><span>${balances.length}</span></div>
        <div class="balance-list">
          ${balances.map(item => {
            const participant = participantById(item.participantId);
            const positive = item.balance >= 0;
            return `
              <div class="balance-row">
                <div class="avatar small">${escapeHtml(participant?.name?.[0] || '?')}</div>
                <div class="balance-main"><strong>${escapeHtml(participant?.name || 'Участник')}</strong><span>${positive ? 'должен получить' : 'должен заплатить'}</span></div>
                <div class="balance-value ${positive ? 'positive' : 'negative'}">${positive && item.balance !== 0 ? '+' : ''}${money(item.balance)}</div>
              </div>`;
          }).join('')}
        </div>
      </section>

      <section>
        <div class="section-title-row"><h2>Кому перевести</h2><span>${transfers.length}</span></div>
        ${transfers.length ? `
          <div class="transfer-list">
            ${transfers.map(transfer => {
              const from = participantById(transfer.fromParticipantId);
              const to = participantById(transfer.toParticipantId);
              return `
                <div class="transfer-row">
                  <div class="transfer-route"><strong>${escapeHtml(from?.name || '—')}</strong>${icon('arrow', 15)}<strong>${escapeHtml(to?.name || '—')}</strong></div>
                  <div class="transfer-amount">${money(transfer.amount)}</div>
                </div>`;
            }).join('')}
          </div>` : `<div class="settled-note">Все уже в расчёте — дополнительных переводов не нужно.</div>`}
      </section>
    </div>`;
}

function emptyState(title, text, open, action) {
  return `
    <div class="empty-state">
      <div class="empty-icon">${open === 'participant' ? icon('users', 22) : icon('receipt', 22)}</div>
      <h2>${title}</h2>
      <p>${text}</p>
      <button class="btn btn-primary" data-open="${open}">${icon('plus', 16)} ${action}</button>
    </div>`;
}

function modalMarkup() {
  if (state.modal === 'participant') return participantModal();
  if (state.modal === 'expense-detail') return expenseDetailModal();
  return expenseModal();
}

function participantModal() {
  return `
    <div class="modal-backdrop" data-close="modal">
      <div class="modal" role="dialog" aria-modal="true" aria-label="Добавление участника" data-modal-card>
        <div class="modal-head">
          <div><div class="section-kicker">Участник поездки</div><h2>Добавить имя</h2><p>Никакой регистрации — участник существует только внутри этой поездки.</p></div>
          <button class="icon-btn" type="button" data-close="modal" aria-label="Закрыть">${icon('close', 17)}</button>
        </div>
        <form id="participant-form">
          <div class="modal-body">
            <div class="field">
              <label for="participant-name">Имя</label>
              <input class="input" id="participant-name" required placeholder="Например, Сергей" autocomplete="off" />
            </div>
          </div>
          <div class="modal-footer">
            <button class="btn btn-outline" type="button" data-close="modal">Отмена</button>
            <button class="btn btn-primary" type="submit">Добавить</button>
          </div>
        </form>
      </div>
    </div>`;
}

function expenseModal() {
  if (!state.participants.length) return participantModal();

  return `
    <div class="modal-backdrop" data-close="modal">
      <div class="modal modal-wide" role="dialog" aria-modal="true" aria-label="Добавление расхода" data-modal-card>
        <div class="modal-head">
          <div><div class="section-kicker">Новый расход</div><h2>Кто и за что заплатил?</h2><p>Сумма делится поровну между выбранными участниками.</p></div>
          <button class="icon-btn" type="button" data-close="modal" aria-label="Закрыть">${icon('close', 17)}</button>
        </div>
        <form id="expense-form">
          <div class="modal-body expense-form-grid">
            <div class="field span-2">
              <label for="expense-description">Описание</label>
              <input class="input" id="expense-description" required placeholder="Например, Ужин" autocomplete="off" />
            </div>
            <div class="field">
              <label for="expense-amount">Сумма</label>
              <div class="amount-input"><input class="input" id="expense-amount" required min="0.01" step="0.01" type="number" inputmode="decimal" placeholder="0.00" /><span>${escapeHtml(currencySymbol())}</span></div>
            </div>
            <div class="field">
              <label for="expense-payer">Кто оплатил</label>
              <select class="input" id="expense-payer">${state.participants.map(p => `<option value="${p.id}">${escapeHtml(p.name)}</option>`).join('')}</select>
            </div>
            <div class="field span-2">
              <div class="split-label"><label>Между кем разделить</label><button class="text-button" type="button" data-toggle-all>Выбрать всех</button></div>
              <div class="check-list">
                ${state.participants.map(p => `<label class="check-item"><input type="checkbox" name="split-participant" value="${p.id}" checked /><span>${escapeHtml(p.name)}</span></label>`).join('')}
              </div>
              <span class="field-hint">Если сумма не делится ровно, остаток распределяется по минимальным денежным единицам.</span>
            </div>
          </div>
          <div class="modal-footer">
            <button class="btn btn-outline" type="button" data-close="modal">Отмена</button>
            <button class="btn btn-primary" type="submit">Добавить расход</button>
          </div>
        </form>
      </div>
    </div>`;
}

function expenseDetailModal() {
  const expense = state.expenses.find(item => item.id === state.selectedExpenseId);
  if (!expense) return '';
  const payer = participantById(expense.paidByParticipantId);

  return `
    <div class="modal-backdrop" data-close="modal">
      <div class="modal" role="dialog" aria-modal="true" aria-label="Расход" data-modal-card>
        <div class="modal-head">
          <div><div class="section-kicker">Расход</div><h2>${escapeHtml(expense.description)}</h2><p>${dateLabel(expense.createdAt)}</p></div>
          <button class="icon-btn" type="button" data-close="modal" aria-label="Закрыть">${icon('close', 17)}</button>
        </div>
        <div class="modal-body detail-body">
          <div class="detail-amount">${money(expense.amount)}</div>
          <dl class="detail-list">
            <div><dt>Оплатил</dt><dd>${escapeHtml(payer?.name || '—')}</dd></div>
            <div><dt>Тип деления</dt><dd>Поровну</dd></div>
          </dl>
          <div class="section-title-row compact"><h3>Доли</h3><span>${expense.shares.length}</span></div>
          <div class="share-list">
            ${expense.shares.map(share => `<div><span>${escapeHtml(participantById(share.participantId)?.name || 'Участник')}</span><strong>${money(share.amount)}</strong></div>`).join('')}
          </div>
        </div>
      </div>
    </div>`;
}

function participantById(id) {
  return state.participants.find(participant => participant.id === id);
}

function calculateSettlement() {
  const balanceMap = new Map(state.participants.map(participant => [participant.id, 0]));

  for (const expense of state.expenses) {
    balanceMap.set(
      expense.paidByParticipantId,
      (balanceMap.get(expense.paidByParticipantId) || 0) + Math.round(Number(expense.amount) * 100),
    );
    for (const share of expense.shares) {
      balanceMap.set(
        share.participantId,
        (balanceMap.get(share.participantId) || 0) - Math.round(Number(share.amount) * 100),
      );
    }
  }

  const balances = state.participants.map(participant => ({
    participantId: participant.id,
    balance: (balanceMap.get(participant.id) || 0) / 100,
  }));

  const debtors = balances
    .filter(item => item.balance < 0)
    .map(item => ({ ...item, cents: Math.round(-item.balance * 100) }))
    .sort((a, b) => b.cents - a.cents);
  const creditors = balances
    .filter(item => item.balance > 0)
    .map(item => ({ ...item, cents: Math.round(item.balance * 100) }))
    .sort((a, b) => b.cents - a.cents);

  const transfers = [];
  let debtorIndex = 0;
  let creditorIndex = 0;

  while (debtorIndex < debtors.length && creditorIndex < creditors.length) {
    const debtor = debtors[debtorIndex];
    const creditor = creditors[creditorIndex];
    const cents = Math.min(debtor.cents, creditor.cents);

    if (cents > 0) {
      transfers.push({
        fromParticipantId: debtor.participantId,
        toParticipantId: creditor.participantId,
        amount: cents / 100,
      });
    }

    debtor.cents -= cents;
    creditor.cents -= cents;
    if (debtor.cents === 0) debtorIndex += 1;
    if (creditor.cents === 0) creditorIndex += 1;
  }

  return { balances, transfers };
}

function currencySymbol() {
  const parts = new Intl.NumberFormat('ru-RU', { style: 'currency', currency: state.trip.currency || 'RUB' }).formatToParts(0);
  return parts.find(part => part.type === 'currency')?.value || state.trip.currency;
}

function plural(value, one, few, many) {
  const mod10 = value % 10;
  const mod100 = value % 100;
  if (mod10 === 1 && mod100 !== 11) return one;
  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return few;
  return many;
}

function escapeHtml(value) {
  return String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
}

function parseTripId(value) {
  const trimmed = value.trim();
  const uuid = trimmed.match(/[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}/i);
  return uuid?.[0] || null;
}

function currentRoute() {
  return location.hash.replace('#', '') || '/';
}

function render() {
  const route = currentRoute();
  if (route === '/create') app.innerHTML = createTrip();
  else if (route.startsWith('/trip/') && state.trip) app.innerHTML = tripPage();
  else app.innerHTML = home();
  bind();
}

async function loadTrip(tripId, activeTab = 'expenses') {
  const [trip, participants, expenses, balances, settlements] = await Promise.all([
    api.getTrip(tripId),
    api.getParticipants(tripId),
    api.getExpenses(tripId),
    api.getBalances(tripId),
    api.getSettlements(tripId),
  ]);

  state = {
    trip,
    participants,
    expenses,
    settlement: {
      balances: balances.map(balance => ({
        participantId: balance.participantId,
        balance: balance.amount,
      })),
      transfers: settlements.transfers,
    },
    activeTab,
    modal: null,
    selectedExpenseId: null,
    toast: null,
    isDemo: false,
  };
}

async function refreshTrip(toast = null) {
  const { id } = state.trip;
  const { activeTab } = state;
  await loadTrip(id, activeTab);
  state.toast = toast;
  render();
  if (toast) clearToastLater();
}

async function handleRouteChange() {
  const route = currentRoute();
  const tripId = route.startsWith('/trip/') ? parseTripId(route) : null;

  if (!tripId || state.trip?.id === tripId) {
    render();
    return;
  }

  try {
    await loadTrip(tripId);
    render();
  } catch {
    location.hash = '#/';
  }
}

function bind() {
  document.querySelectorAll('[data-tab]').forEach(button => button.addEventListener('click', () => {
    state.activeTab = button.dataset.tab;
    state.modal = null;
    render();
  }));

  document.querySelectorAll('[data-open]').forEach(button => button.addEventListener('click', () => {
    if (button.disabled) return;
    state.modal = button.dataset.open;
    render();
  }));

  document.querySelectorAll('[data-expense-id]').forEach(button => button.addEventListener('click', async () => {
    try {
      const expense = state.isDemo
        ? state.expenses.find(item => item.id === button.dataset.expenseId)
        : await api.getExpense(state.trip.id, button.dataset.expenseId);
      const index = state.expenses.findIndex(item => item.id === expense.id);
      if (index >= 0) state.expenses[index] = expense;
      state.selectedExpenseId = expense.id;
      state.modal = 'expense-detail';
      render();
    } catch (error) {
      state.toast = error.message;
      render();
      clearToastLater();
    }
  }));

  document.querySelectorAll('[data-close="modal"]').forEach(element => element.addEventListener('click', event => {
    if (event.currentTarget.classList.contains('modal-backdrop') && event.target.closest('[data-modal-card]')) return;
    state.modal = null;
    state.selectedExpenseId = null;
    render();
  }));

  document.querySelector('[data-modal-card]')?.addEventListener('click', event => event.stopPropagation());

  document.querySelector('[data-demo]')?.addEventListener('click', () => {
    state = createDemoState();
    state.settlement = calculateSettlement();
    state.isDemo = true;
    location.hash = `#/trip/${state.trip.id}`;
  });

  document.getElementById('open-trip-form')?.addEventListener('submit', async event => {
    event.preventDefault();
    const tripId = parseTripId(document.getElementById('trip-id').value);
    if (!tripId) {
      showInlineError(document.getElementById('trip-id'), 'Введите корректный UUID или ссылку с UUID поездки.');
      return;
    }

    try {
      await loadTrip(tripId);
      location.hash = `#/trip/${tripId}`;
    } catch (error) {
      showInlineError(
        document.getElementById('trip-id'),
        error.status === 404 ? 'Поездка с таким ID не найдена.' : error.message,
      );
    }
  });

  document.getElementById('create-trip-form')?.addEventListener('submit', async event => {
    event.preventDefault();
    const name = document.getElementById('trip-name').value.trim();
    const currency = document.getElementById('trip-currency').value;
    if (!name) return;

    try {
      const trip = await api.createTrip(name, currency);
      await loadTrip(trip.id, 'participants');
      location.hash = `#/trip/${trip.id}`;
    } catch (error) {
      showFormError(event.currentTarget, error.message);
    }
  });

  document.getElementById('participant-form')?.addEventListener('submit', async event => {
    event.preventDefault();
    const input = document.getElementById('participant-name');
    const name = input.value.trim();
    if (!name) return;

    try {
      const participant = await api.addParticipant(state.trip.id, name);
      await api.getParticipant(state.trip.id, participant.id);
      await refreshTrip(`${name} добавлен в поездку`);
    } catch (error) {
      showInlineError(input, error.message);
    }
  });

  document.getElementById('expense-form')?.addEventListener('submit', async event => {
    event.preventDefault();
    const description = document.getElementById('expense-description').value.trim();
    const amount = Number(document.getElementById('expense-amount').value);
    const paidByParticipantId = document.getElementById('expense-payer').value;
    const participantIds = [...document.querySelectorAll('input[name="split-participant"]:checked')].map(input => input.value);

    if (!description || !Number.isFinite(amount) || amount <= 0) return;
    if (!participantIds.length) {
      const list = document.querySelector('.check-list');
      list?.classList.add('invalid');
      return;
    }

    try {
      const expense = await api.addExpense(state.trip.id, {
        amount,
        description,
        paidByParticipantId,
        participantIds,
      });
      await api.getExpense(state.trip.id, expense.id);
      await refreshTrip('Расход добавлен');
    } catch (error) {
      showFormError(event.currentTarget, error.message);
    }
  });

  document.querySelector('[data-toggle-all]')?.addEventListener('click', () => {
    document.querySelectorAll('input[name="split-participant"]').forEach(input => { input.checked = true; });
  });

  document.querySelector('[data-copy-link]')?.addEventListener('click', async () => {
    const url = `${location.href.split('#')[0]}#/trip/${state.trip.id}`;
    try {
      await navigator.clipboard.writeText(url);
      state.toast = 'Ссылка скопирована';
    } catch {
      state.toast = `Ссылка: ${url}`;
    }
    render();
    clearToastLater();
  });
}

function showInlineError(input, message) {
  input.classList.add('input-error');
  let error = input.parentElement.querySelector('.inline-error');
  if (!error) {
    error = document.createElement('span');
    error.className = 'inline-error';
    input.insertAdjacentElement('afterend', error);
  }
  error.textContent = message;
}

function showFormError(form, message) {
  let error = form.querySelector('.inline-error');
  if (!error) {
    error = document.createElement('span');
    error.className = 'inline-error';
    form.querySelector('.modal-footer, .form-actions')?.insertAdjacentElement('beforebegin', error);
  }
  error.textContent = message;
}

function clearToastLater() {
  window.setTimeout(() => {
    if (!state.toast) return;
    state.toast = null;
    render();
  }, 2400);
}

window.addEventListener('hashchange', handleRouteChange);
handleRouteChange();
