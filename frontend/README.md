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

### Production и денежный контракт

Для отдельного frontend origin задайте не только `VITE_API_BASE_URL` при сборке,
но и `Cors:AllowedOrigins` в конфигурации backend (например,
`Cors__AllowedOrigins__0=https://expenses.example.com`).

Денежные поля API передаются строками; frontend использует только десятичную арифметику `decimal.js` (60 значащих цифр).
`BigInt` и JavaScript `Number` для денег запрещены; backend использует только `decimal`.
Обновляйте frontend и backend вместе. `/health` проверяет только liveness API,
а не готовность PostgreSQL. Ошибка расчёта не блокирует участников и расходы.

Экран поездки загружается одним `GET /trips/{id}/snapshot`, включая расчёт.
После 409 выполняется полный reload без автоматического повторения команды.
Demo использует порядок участников по ID, как backend.
Изображение гор подключено через `new URL(..., import.meta.url)` и попадает в production bundle.

Перед следующей крупной функциональностью следует разделить `app.js` на
views/state/actions/router. Масштабный рефакторинг перед MVP не требуется.
