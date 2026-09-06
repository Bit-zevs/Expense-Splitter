# Хранение данных

Используется PostgreSQL 17 и Npgsql для EF Core 10. Конфигурации находятся в
`ExpenseSplitter.Infrastructure/Persistence/Configurations`, миграции — рядом
в `Persistence/Migrations`. Domain-сущности не содержат атрибутов EF и не изменены
ради хранения данных. В калькуляторе балансов добавлена сортировка участников
по `Guid`, чтобы взаиморасчёты не зависели от порядка материализации коллекций.

## Таблицы

| Таблица | Ключ | Остальные столбцы |
| --- | --- | --- |
| `Trips` | `Id` | `Name`, `CreatedAt` |
| `Participants` | `Id` | shadow `TripId`, `Name` |
| `Expenses` | `Id` | shadow `TripId`, `Amount`, `Description`, `PaidByParticipantId`, `SplitType`, `CreatedAt` |
| `ExpenseParticipants` | `(ExpenseId, ParticipantId)` | `Amount` |

`ExpenseParticipants` хранит owned-коллекцию `Expense.Shares` (`ExpenseShare`).
`ExpenseId` — shadow FK владельца; отдельный CLR-тип `ExpenseParticipant` и
суррогатный ID не нужны. Составной PK запрещает повторение участника внутри расхода.
`ParticipantIds` вычисляется из долей и исключён из EF-модели. `SettlementTransfer`
является результатом расчёта и не сохраняется.

GUID создаёт Domain, для ключей отключена генерация EF/БД. `CreatedAt` также задаёт
Domain; PostgreSQL хранит UTC в `timestamp(6) with time zone`. Точность времени —
микросекунды, поэтому при сохранении отбрасываются последние 0–9 тиков .NET.
Строки обязательны и хранятся как `text`: Domain не задаёт максимальную длину.
Имена участников могут совпадать. `SplitType` хранится как `integer`, CHECK допускает
только `0` (`Equal`); при добавлении способа деления нужно обновить CHECK миграцией.

## Relationships и удаление

Все FK обязательны. Каскады: `Trip → Participants`, `Trip → Expenses`,
`Expense → ExpenseParticipants`. Ссылки `Expense.PaidByParticipantId → Participant`
и `ExpenseShare.ParticipantId → Participant` используют `NO ACTION`.

В начальной миграции эти две ссылки дополнительно настроены как
`DEFERRABLE INITIALLY DEFERRED`: PostgreSQL проверяет их в конце транзакции.
Это позволяет завершить многоуровневый каскад удаления поездки, прежде чем проверять
ссылки на её участников. Удаление отдельного используемого участника по-прежнему
не может быть закоммичено. С собственной транзакцией ошибка может возникнуть
на `CommitAsync`, а не на `SaveChangesAsync`; обработка ошибок должна охватывать
всю транзакцию. Каскады и запреты проверяются на реальном PostgreSQL с загруженными
и незагруженными зависимостями.

Deferred-настройка задаётся SQL миграции, поскольку используемый провайдер не
предоставляет Fluent API для неё. Базу нужно создавать через `MigrateAsync` или
`dotnet ef database update`, а не `EnsureCreated`. При пересоздании этих FK будущей
миграцией необходимо сохранить `DEFERRABLE INITIALLY DEFERRED`.
Синтаксис описан в [PostgreSQL ALTER TABLE](https://www.postgresql.org/docs/17/sql-altertable.html).

Удаление расхода удаляет только его доли; участники остаются. Правила удаления
в EF не добавляют бизнес-операции удаления в Domain/API.

## Деньги, индексы и границы проверки

Оба денежных поля имеют тип `numeric(29,2)`. CHECK для расхода требует
`0 < Amount <= 792281625142643375935439503.35`, для доли —
`0 <= Amount <= 792281625142643375935439503.35`. Нулевая доля допустима, например
при делении одной копейки между тремя участниками. `numeric(18,2)` сузил бы диапазон
допустимых Domain-значений. Проверка лишних десятичных знаков выполняется в Domain:
SQL-тип с scale 2 сам по себе может округлять значения при записи в обход Domain.

Индексы: `Participants(TripId)`, `Expenses(TripId, CreatedAt)`,
`Expenses(PaidByParticipantId)`, `ExpenseParticipants(ParticipantId)`.
Составной PK долей покрывает поиск по `ExpenseId`. Дополнительных индексов на
`Expenses(TripId)` и `ExpenseParticipants(ExpenseId)` нет. Имена не уникальны.

FK проверяют существование связанной строки. Принадлежность плательщика и всех
получателей долей той же поездке, непустой состав долей и равенство суммы долей
сумме расхода остаются инвариантами агрегата `Trip`. Эти правила проверяются до
добавления расхода. Прямые записи в БД или перенос сущностей между поездками могут
обойти эти проверки и не входят в поддерживаемый workflow.

## Загрузка и сохранение

Коллекции настроены на доступ через `_participants`, `_expenses`, `_shares`.
Для операций над агрегатом нужно загрузить участников и все расходы; owned-доли
загружаются вместе с расходами. Частично загруженный `Trip` нельзя передавать
калькулятору взаиморасчётов.

```csharp
var trip = await db.Trips
    .Include(trip => trip.Participants)
    .Include(trip => trip.Expenses)
    .AsSplitQuery()
    .SingleAsync(trip => trip.Id == tripId, cancellationToken);

trip.AddEqualExpense(amount, description, payerId, participantIds);
await db.SaveChangesAsync(cancellationToken);
```

Изменения делаются над tracked-агрегатом в одном DbContext. Shadow FK хранятся
в change tracker; не следует загружать detached-граф и сохранять его слепым `Update`.
При требованиях к согласованному снимку нескольких SQL-запросов загрузку можно
выполнять в транзакции с подходящим уровнем изоляции.

## Локальный запуск и тесты

Команды выполняются из корня репозитория:

```powershell
docker compose -f backend/compose.yaml up -d --wait
dotnet tool restore
dotnet ef database update --project backend/src/ExpenseSplitter.Infrastructure --startup-project backend/src/ExpenseSplitter.Api -- --environment Development
dotnet run --project backend/src/ExpenseSplitter.Api
```

Development connection string хранится в .NET User Secrets вне репозитория.
Перед запуском Compose задайте `POSTGRES_PASSWORD` в текущей PowerShell-сессии
и сохраните такую же строку подключения в User Secrets:

```powershell
$env:POSTGRES_PASSWORD = [guid]::NewGuid().ToString("N")
dotnet user-secrets set "ConnectionStrings:ExpenseSplitter" "Host=localhost;Port=55432;Database=expense_splitter;Username=expense_splitter;Password=$env:POSTGRES_PASSWORD" --project backend/src/ExpenseSplitter.Api
```

БД и пользователь Compose — `expense_splitter`, порт опубликован только на loopback.
Для другой среды задайте `ConnectionStrings__ExpenseSplitter` через окружение
или секреты. При отсутствии строки подключения регистрация Infrastructure
выдаёт понятную ошибку. API не применяет миграции автоматически при запуске.

```powershell
dotnet build backend/ExpenseSplitter.Backend.sln
dotnet test backend/ExpenseSplitter.Backend.sln
```

Интеграционные тесты требуют работающий Docker с Linux-контейнерами.
Testcontainers запускает отдельный `postgres:17.10`, а каждый тест получает новую
БД внутри него и применяет настоящие миграции. Пользовательские базы не используются;
тестовый контейнер удаляется после выполнения. Для model-тестов Docker не нужен:

```powershell
dotnet test backend/tests/ExpenseSplitter.Infrastructure.Tests --filter FullyQualifiedName~PersistenceModelTests
```

Проверяются round-trip графа, GUID, Unicode и дубли имён, максимальная сумма,
нулевые доли, добавление расхода после загрузки, стабильность взаиморасчётов,
FK и составной PK, каскады, запрет удаления используемого участника, атомарность
сохранения, CHECK-ограничения, индексы, совпадение модели с миграцией и её откат.
