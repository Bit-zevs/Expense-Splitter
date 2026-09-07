export class ApiError extends Error {
  constructor(message, status) {
    super(message);
    this.status = status;
  }
}

export function createApi(baseUrl, fetchImplementation = globalThis.fetch) {
  const normalizedBaseUrl = baseUrl.replace(/\/$/, '');

  async function request(path, options = {}) {
    const response = await fetchImplementation(`${normalizedBaseUrl}${path}`, {
      ...options,
      headers: {
        ...(options.body ? { 'Content-Type': 'application/json' } : {}),
        ...options.headers,
      },
    });

    if (!response.ok) {
      let problem = null;
      try {
        problem = await response.json();
      } catch {
        // The API may return an empty 404 response.
      }

      const validationMessage = problem?.errors
        ? Object.values(problem.errors).flat().join(' ')
        : null;
      throw new ApiError(
        validationMessage
          || problem?.title
          || (response.status === 404 ? 'Поездка не найдена.' : 'Не удалось выполнить запрос.'),
        response.status,
      );
    }

    return response.status === 204 ? null : response.json();
  }

  return {
    health: () => request('/health'),
    createTrip: (name, currency) => request('/trips', {
      method: 'POST',
      body: JSON.stringify({ name, currency }),
    }),
    getTrip: tripId => request(`/trips/${tripId}`),
    getParticipants: tripId => request(`/trips/${tripId}/participants`),
    getParticipant: (tripId, participantId) => request(`/trips/${tripId}/participants/${participantId}`),
    addParticipant: (tripId, name) => request(`/trips/${tripId}/participants`, {
      method: 'POST',
      body: JSON.stringify({ name }),
    }),
    getExpenses: tripId => request(`/trips/${tripId}/expenses`),
    getExpense: (tripId, expenseId) => request(`/trips/${tripId}/expenses/${expenseId}`),
    addExpense: (tripId, expense) => request(`/trips/${tripId}/expenses`, {
      method: 'POST',
      body: JSON.stringify(expense),
    }),
    getBalances: tripId => request(`/trips/${tripId}/balances`),
    getSettlements: tripId => request(`/trips/${tripId}/settlements`),
  };
}
