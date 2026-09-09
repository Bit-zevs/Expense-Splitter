# Аккаунты и доступ к поездкам

## Модель и ограничения

Одна PostgreSQL-база, один `ExpenseSplitterDbContext` на запрос, наследующий
`IdentityUserContext<ApplicationUser, Guid>`. Глобальные роли Identity не используются.
Domain знает только GUID аккаунта и не зависит от Identity.

| Таблица | Содержание |
| --- | --- |
| AspNetUsers | Аккаунт Identity, уникальный нормализованный email, DisplayName до 100 символов |
| AspNetUserClaims / Logins / Tokens | Стандартные служебные таблицы Identity |
| Trips | Прежние поля, OwnerAccountId, nullable JoinCodeHash, служебная Revision |
| Participants | Прежние поля, nullable AccountId; null означает фантома |
| TripJoinRequests | Только pending: Id, TripId, AccountId, RequestedAt |
| Expenses / ExpenseParticipants | Прежние расходы и точные decimal-доли |

Один аккаунт может иметь одного Participant и одну pending-заявку на поездку:
уникальные индексы `(TripId, AccountId)`, для Participant — частичный, исключающий null.
Одинаковые имена разрешены. FK на AccountId автоматически создаёт индекс для будущего
списка поездок; дополнительный `(AccountId, TripId)` сейчас не нужен.

При создании поездки атомарно создаётся Participant владельца. Передача владения
допустима только уже зарегистрированному участнику. Эти инварианты принадлежат
application/domain; FK владельца гарантирует существование аккаунта, но не его
членство. Прямое изменение агрегатов SQL в обход приложения не поддерживается.

Имя Participant локально для поездки. Новый участник получает DisplayName аккаунта;
при привязке к фантому сохраняются его ID, имя, расходы и доли. Email не выдаётся
в ответах поездки или списке заявок. Отдельного профиля и списка поездок пока нет.

## Identity, cookies и CSRF

Регистрация и вход используют `UserManager` и `SignInManager`. Регистрация возвращает
200 без входа, как Identity API. DisplayName обязателен. Подтверждение email пока не
нужно. Действуют стандартные правила пароля Identity (минимум 6 символов, цифра,
строчная/прописная буквы и небуквенный символ), максимум API — 128 символов.
Пять неудачных попыток блокируют вход на 15 минут; неправильный пароль, блокировка
и неизвестный email возвращают одинаковый 401.

Cookie `__Host-ExpenseSplitter.Auth`: HttpOnly, Secure, SameSite=Lax, Path=/, 8 часов
со sliding expiration. RememberMe сохраняет cookie между сеансами браузера.
Logout очищает cookie текущего браузера. Смена пароля обновляет текущую cookie;
прочие сессии проверяет стандартный SecurityStampValidator (интервал 30 минут).
Logout не отзывает немедленно ранее скопированные cookie. Членство поездки проверяется
по БД на каждом запросе, поэтому удаление участника сразу закрывает ему доступ.
Почтовый sender, восстановление забытого пароля, внешние провайдеры и 2FA пока не
экспонируются; есть смена известного текущего пароля.

Порядок вызовов из браузера/API-клиента:

1. GET `/auth/csrf` устанавливает antiforgery-cookie и возвращает `{ token }`.
2. Для POST/PUT/PATCH/DELETE передавать cookies и заголовок `X-CSRF-TOKEN: token`.
   Это относится также к регистрации и входу.
3. После входа или смены пользователя заново получить `/auth/csrf`.

Для JSON явно вызывается `IAntiforgery.ValidateRequestAsync`; одного UseAntiforgery
для таких endpoints недостаточно. Невалидный CSRF возвращает 400. Проверка входа
выполняется раньше CSRF: анонимный вызов защищённого endpoint возвращает 401.
CORS разрешает только заданные `Cors:AllowedOrigins`, с credentials, без wildcard.
Конфигурация рассчитана на same-site HTTPS размещение UI и API. Cross-site deployment
требует отдельного выбора SameSite и проверки ограничений браузеров.

Dev API доступен на `https://localhost:7050`; при необходимости выполнить
`dotnet dev-certs https --trust`. Secure-cookie не работают через обычный HTTP.
Для production нужны HTTPS и постоянное защищённое хранилище ключей Data Protection;
при нескольких экземплярах ключи и имя приложения должны быть общими.
В test host используются эфемерные ключи.

## Права

| Операция | Owner | Member | Посторонний / pending |
| --- | --- | --- | --- |
| Чтение поездки, snapshot, расходов, участников, расчётов | Да | Да | 404 |
| Создание и удаление любого расхода | Да | Да | 404 |
| Создание/удаление фантомов и реальных участников | Да | 403 | 404 |
| Код, список заявок, одобрение и отклонение | Да | 403 | 404 |
| Передача владения, удаление поездки | Да | 403 | 404 |

Fallback policy требует вход везде, кроме health, auth register/login/csrf и
development OpenAPI. `ITripAccess` вызывается всеми обработчиками существующих поездок.
ICurrentAccount берёт ID из проверенной identity. Поля запроса не определяют актёра.

## Коды и заявки

Код: 12 случайных символов Crockford Base32, 60 бит энтропии. Алфавит:
`0123456789ABCDEFGHJKMNPQRSTVWXYZ`. I/L/O/U исключены; 0 и 1 допустимы.
Регистр и пробелы по краям несущественны; дефисы и alias-замены не принимаются.
В БД только SHA-256 bytea (32 байта), с уникальным частичным индексом.
Исходный код возвращается при создании поездки или ротации и недоступен через GET.
Код бессрочный; новый сразу отменяет старый, но сохраняет существующие заявки.
Передача владения сама по себе код не меняет. Ответы API имеют Cache-Control: no-store.
Тела ответов с кодом нельзя добавлять в HTTP body logging.

Вошедший пользователь вводит код и получает заявку со статусом HTTP 201 и Location.
Доступ к поездке открывается только после одобрения. Владелец выбирает фантома или
создаёт нового участника. Чужой Participant возвращает 404, уже связанный — 409.
Подтверждение атомарно привязывает/создаёт Participant и удаляет заявку.
Отклонение/отмена удаляют заявку. Повторная заявка разрешена сразу.
Дубликат pending-заявки или заявка уже состоящего в поездке аккаунта возвращают 409.
Истории решений нет: удалённая заявка возвращает 404 независимо от причины.
Собственная pending-заявка раскрывает только её ID, TripId и время; не содержимое поездки.

Rate limiting: auth 30 запросов/минуту на IP, ввод кода 10/минуту на аккаунт; превышение —
429. Лимиты локальны экземпляру. За proxy следует настроить доверенные forwarded headers,
для нескольких экземпляров общий лимит можно применять на gateway.

## Контракт API

| Метод и путь | Тело | Успех |
| --- | --- | --- |
| GET `/auth/csrf` | — | 200, `{ token }` |
| POST `/auth/register` | `{ email, password, displayName }` | 200 |
| POST `/auth/login` | `{ email, password, rememberMe? }` | 200, cookie |
| POST `/auth/logout` | `{}` | 204 |
| POST `/auth/change-password` | `{ currentPassword, newPassword }` | 204 |
| POST `/trips` | `{ name, currency? }` | 201, поездка + ownerAccountId + joinCode |
| POST `/trips/{id}/join-code` | `{}` | 200, `{ code }` |
| POST `/trip-join-requests` | `{ code }` | 201, заявка + Location |
| GET `/trip-join-requests/{id}` | — | 200, своя pending-заявка |
| DELETE `/trip-join-requests/{id}` | — | 204, отмена своей заявки |
| GET `/trips/{id}/join-requests` | — | 200, pending-заявки с DisplayName, без email |
| POST `/trips/{id}/join-requests/{requestId}/approve` | `{ participantId? }` | 200, Participant |
| DELETE `/trips/{id}/join-requests/{requestId}` | — | 204, отклонение |
| PUT `/trips/{id}/owner` | `{ accountId }` | 204 |

Прежние endpoints сохранены и защищены. ParticipantResult дополнен nullable accountId;
GetTripResult — ownerAccountId. Identity-сущности и хеш кода не сериализуются.
Ошибки — ProblemDetails/ValidationProblemDetails. 409 также обозначает запрет удаления
владельца или конфликт записи. Старый frontend требует отдельной адаптации к авторизации.

## Удаление, транзакции и миграция

Удаление участника удаляет все расходы, где он платил или имел долю (включая нулевую).
Аккаунт и другие поездки остаются. Владелец должен сначала передать владение.
Самостоятельный выход и удаление аккаунта пока не представлены endpoints.
FK аккаунта настроены Restrict, чтобы не уничтожать финансовые данные неявно.

Права и данные читаются в одной RepeatableRead-транзакции. После проверки прав,
перед изменением данных обновляется Revision поездки: блокируется её строка и устаревшая RR-транзакция
получает serialization conflict (409), если доступ уже был отозван. Все изменения
расходов/участников/заявок и передача владения используют этот порядок. Отказанные
операции откатываются при освобождении scoped DbContext. Повтора команд нет.
ITripStore сохранён. Infrastructure реализует application-контракт ITripMembershipService
и координирует relational-запросы Identity и поездки; доменные правила остаются в Trip.

Миграция `AddAccountsAndJoinRequests` требует пустую таблицу Trips. Владельцев старых
анонимных поездок достоверно установить невозможно. Миграция не удаляет данные сама;
она явно отказывает при их наличии. Для dev согласован сброс с предварительным pg_dump.
Миграции применяются вручную, не на старте API.

`dotnet test backend/ExpenseSplitter.Backend.sln` запускает все backend-тесты. PostgreSQL
тесты требуют Docker и создают отдельные временные базы. Security API tests используют
настоящие Identity/cookies/CSRF/EF. Прежние контрактные тесты изолируют доступ test-only
заглушками. Проверяются pending-приватность, права, привязка, отмена, ротация, дубликаты,
владение, удаление, индексы и конкурентный отзыв доступа.

## Официальные источники

- [Identity для Web API/SPA](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0)
- [Cookie API в .NET 10](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/api-endpoint-auth?view=aspnetcore-10.0)
- [Resource-based authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resource-based?view=aspnetcore-10.0)
- [Antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0)
- [HTTP RFC 9110](https://www.rfc-editor.org/rfc/rfc9110.html)
