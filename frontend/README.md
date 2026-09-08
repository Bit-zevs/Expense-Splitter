# Expense Splitter Frontend

Отдельный frontend-проект для Expense Splitter. Он использует существующий ASP.NET Core API, но собирается и запускается независимо от backend.

## Запуск

1. Запустите API из корня репозитория:

   ```bash
   dotnet run --project backend/src/ExpenseSplitter.Api
   ```

2. Установите зависимости и запустите frontend:

   ```bash
   cd frontend
   npm install
   npm run dev
   ```

3. Откройте `http://localhost:5173`.

По умолчанию frontend обращается к `http://localhost:5050`. Другой адрес можно задать в `.env.local`:

```env
VITE_API_BASE_URL=https://example.com
```

## Команды

- `npm run dev` — локальная разработка;
- `npm run build` — production-сборка в `dist/`;
- `npm run preview` — просмотр production-сборки.

Frontend подключает создание, открытие и удаление поездок, участников и расходов,
а также балансы и итоговый план переводов. При добавлении расхода можно выбрать дату
и время операции; по умолчанию подставляется текущая локальная минута. Кнопка
«Посмотреть пример» остаётся локальной демонстрацией и не создаёт данные в API.
