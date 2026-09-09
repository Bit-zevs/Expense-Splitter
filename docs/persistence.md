# Хранение данных

Аккаунты, коды, заявки, права и транзакции авторизации описаны в [accounts.md](accounts.md).
Таблица ниже перечисляет финансовую часть; Identity и TripJoinRequests добавлены
миграцией AddAccountsAndJoinRequests, требующей пустую dev-базу.

Используется PostgreSQL 17 и Npgsql для EF Core 10. Конфигурации находятся в
`ExpenseSplitter.Infrastructure/Persistence/Configurations`, миграции — рядом
в `Persistence/Migrations`. Domain-сущности не содержат атрибутов EF. В калькуляторе балансов добавлена сортировка участников
по `Guid`, чтобы взаиморасчёты не зависели от порядка материализации коллекций.

## Таблицы

| Таблица | Ключ | Остальные столбцы |
| --- | --- | --- |
| `Trips` | `Id` | `Name`, `Currency`, `CreatedAt`, `OwnerAccountId`, `JoinCodeHash`, shadow `Revision` |
| `Participants` | `Id` | shadow `TripId`, `Name`, nullable `AccountId` |
| `Expenses` | `Id` | shadow `TripId`, `Amount`, `Description`, `PaidByParticipantId`, `SplitType`, `OccurredAt` |
| `ExpenseParticipants` | `(ExpenseId, ParticipantId)` | `Amount` |

`ExpenseParticipants` хранит owned-коллекцию `Expense.Shares` (`ExpenseShare`).
`ExpenseId` — shadow FK владельца; отдельный CLR-тип `ExpenseParticipant` и
суррогатный ID не нужны. Составной PK запрещает повторение участника внутри расхода.
`ParticipantIds` вычисляется из долей и исключён из EF-модели. `SettlementTransfer`
является результатом расчёта и не сохраняется.

GUID создаёт Domain, для ключей отключена генерация EF/БД. Время создания поездки
`Trip.CreatedAt` задаёт Domain с точностью БД. Время расхода `Expense.OccurredAt`
можно передать вручную; если оно не передано, используется текущее время. Domain
переводит его в UTC и отбрасывает секунды. PostgreSQL хранит оба значения в
`timestamp(6) with time zone`, поэтому POST-ответ и последующий GET возвращают
одно и то же значение времени.
Строки обязательны и хранятся как `text`: Domain не задаёт максимальную длину.
Имена участников могут совпадать. `SplitType` хранится как `integer`, CHECK допускает
только `0` (`Equal`); при добавлении способа деления нужно обновить CHECK миграцией.

## Relationships и удаление

Финансовые FK обязательны, ссылка Participant → Account необязательна. Каскады: `Trip → Participants`, `Trip → Expenses`,
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

Удаление владельца запрещено до передачи владения другому аккаунту в поездке.
FK аккаунта настроены Restrict. Удаление расхода через `Trip.RemoveExpense` удаляет только его доли; участники
остаются. `Trip.RemoveParticipant` сначала удаляет все расходы, где участник является
плательщиком или входит в доли, а затем самого участника. Это сохраняет инварианты
агрегата и позволяет выполнить удаление при `NO ACTION`-ссылках на участника.
Удаление корня `Trip` выполняет application-сценарий через хранилище, после чего
каскады БД удаляют весь агрегат. API предоставляет `DELETE` для всех трёх ресурсов;
успех возвращает `204`, отсутствующий ресурс — `404`.

## Деньги, индексы и границы проверки

Оба денежных поля имеют тип `numeric(29,2)`. CHECK для расхода требует
`0 < Amount <= 792281625142643375935439503.35`, для доли —
`0 <= Amount <= 792281625142643375935439503.35`. Это максимальный диапазон,
в котором каждое значение можно представить целым количеством копеек в `decimal`.
Более крупные значения `decimal` не поддерживаются: возле `decimal.MaxValue`
соседние копейки неразличимы, а Npgsql не гарантирует чтение `numeric(31,2)` в `decimal`.
Domain дополнительно допускает не более двух десятичных знаков.
Нулевая доля допустима, например
при делении одной копейки между тремя участниками. `numeric(18,2)` сузил бы диапазон
допустимых Domain-значений. Проверка лишних десятичных знаков выполняется в Domain:
SQL-тип с scale 2 сам по себе может округлять значения при записи в обход Domain.

Индексы: `Participants(TripId)`, `Expenses(TripId, OccurredAt)`,
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
Хранилище выбирает форму загрузки под use case: добавление участника загружает только
`Trip`, создание расхода — `Trip` с участниками, чтение коллекции — только нужную
коллекцию. Полный граф нужен лишь для balances/settlements; owned-доли загружаются
вместе с расходами. Частично загруженный `Trip` нельзя передавать калькулятору
взаиморасчётов.

```csharp
await using var transaction = await db.Database.BeginTransactionAsync(
    IsolationLevel.RepeatableRead,
    cancellationToken);

var trip = await db.Trips
    .AsNoTracking()
    .Include(trip => trip.Participants)
    .Include(trip => trip.Expenses)
    .AsSplitQuery()
    .SingleAsync(trip => trip.Id == tripId, cancellationToken);

await transaction.CommitAsync(cancellationToken);
```

`RepeatableRead` даёт всем частям split-query один PostgreSQL snapshot и не позволяет
собрать расходы и участников из разных состояний БД. Изменения делаются над
минимально необходимым tracked-графом в одном DbContext. Shadow FK хранятся в
change tracker; не следует загружать detached-граф и сохранять его слепым `Update`.

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
или секреты. При отсутствии строки подключения API завершает запуск с понятной
ошибкой до начала обработки запросов. API не применяет миграции автоматически.

```powershell
dotnet build ExpenseSplitter.sln
dotnet test ExpenseSplitter.sln
```

Интеграционные тесты требуют работающий Docker с Linux-контейнерами.
Testcontainers запускает отдельный `postgres:17.10`, а каждый тест получает новую
БД внутри него и применяет настоящие миграции. Пользовательские базы не используются;
тестовый контейнер удаляется после выполнения. Для model-тестов Docker не нужен:

```powershell
dotnet test backend/tests/ExpenseSplitter.Infrastructure.Tests --filter FullyQualifiedName~PersistenceModelTests
```

Проверяются round-trip графа, GUID, Unicode и дубли имён, максимальная сумма,
нулевые доли, формы загрузки и общий snapshot split-query при конкурентной записи,
добавление расхода после загрузки, стабильность взаиморасчётов,
FK и составной PK, каскады, запрет удаления используемого участника, атомарность
сохранения, CHECK-ограничения, индексы, совпадение модели с миграцией и её откат.

## Контракт записи и диагностики

Методы `Find*TrackedAsync` включают EF tracking, но не выполняют SQL `FOR UPDATE`.
`AddAsync` только добавляет сущность в tracker; каждый обработчик записи явно вызывает
`SaveChangesAsync`. Методы `Find*TrackedAsync` открывают `RepeatableRead`-транзакцию,
которая охватывает загрузку (включая все части split-query), изменение агрегата,
сохранение и commit. Неуспешная запись откатывается, а выход без сохранения
освобождает транзакцию при уничтожении scoped DbContext. Обработчики сначала вызывают
`ITripAccess`: он открывает общий snapshot проверки прав и обновляет `Trips.Revision`
для записи, блокируя строку поездки. Устаревший snapshot после отзыва доступа или другой
конкурентной записи завершается конфликтом сериализации. Сам ITripStore используется
внутри этой транзакции; его низкоуровневые методы отдельно права не проверяют.
Конкурентное удаление, конфликт сериализации и FK-конфликт записи возвращают HTTP 409;
клиенту следует обновить поездку перед повторением операции.

`/health` — только liveness процесса API, без проверки доступности PostgreSQL.
Не используйте его как readiness-проверку БД.

API возвращает денежные значения строками, сохраняющими точность decimal. Запросы
принимают строки и JSON numbers для совместимости; браузеры должны отправлять строки.
Денежные вычисления backend используют только decimal, включая промежуточные
значения. BigInteger запрещён; переход к нему не планируется и не допускается.
Для итоговых балансов и переводов проверяется точная представимость в decimal,
а не лимит одного расхода. Подробности — в `settlement-calculator.md`.

## Снимок для frontend

`GET /trips/{id}/snapshot` использует одну полную загрузку графа под RepeatableRead.
Из неё формируются `trip`, `participants`, `expenses`, `settlement` и `calculationError`.
Если расчёт не представим точно, endpoint всё равно возвращает HTTP 200 с базовыми
данными, `settlement: null` и сообщением в `calculationError`: расходы можно удалить.
Отсутствующая поездка возвращает 404.

Frontend загружает снимок при открытии, обновлении и после HTTP 409. Балансы,
расходы и участники заменяются совместно. Неуспешный reload после конфликта
убирает устаревшие данные из редактируемого интерфейса. Команда автоматически
не повторяется. Успешный POST остаётся успехом, даже если получение снимка не удалось.
OpenAPI описывает денежные поля как строки и документирует HTTP 409 для всех команд.
