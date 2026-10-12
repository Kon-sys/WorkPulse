# WorkPulse REST API v1

Базовый адрес локальной версии: `http://127.0.0.1:5080/api/v1`. JSON UTF-8, GUID – строки UUID, моменты – ISO 8601 с часовым поясом, дата найма – `YYYY-MM-DD`. Запросы к организации выполняются по идентичности пользователя/устройства, а не по переданному клиентом tenant-заголовку. OpenAPI доступен в Development по `/openapi/v1.json`; эта таблица дополняет автоматически генерируемые схемы ответов.

## Доступ и заголовки

| Роль | Область | Изменения |
|---|---|---|
| admin | Организация | Сотрудники, подразделения, пользователи, устройства, опубликованные политики и назначения; анализ, отсутствие, рассмотрение нарушений |
| hr | Организация | Анализ, разрешённое отсутствие, рассмотрение нарушений |
| manager | Назначенное подразделение | Анализ, разрешённое отсутствие, рассмотрение нарушений своих сотрудников |
| employee | Свои данные | Начало/завершение своей сессии и обеда; чтение своих результатов |
| device | Только закреплённые организация, устройство и сотрудник | Приём событий; без доступа к веб-маршрутам |

Все роли читают политики/ресурсы своей организации. Веб-запросы используют `Cookie: WorkPulse.Session=...`. Каждый изменяющий веб-запрос, включая login, требует `X-CSRF-TOKEN`, полученный через `/auth/csrf` вместе с CSRF-cookie. После login токен обновляется. Агент использует только `Authorization: Bearer <token>`; cookie не даёт доступ к приёму событий, токен устройства не даёт доступ к веб-маршрутам. `Content-Type: application/json` требуется при наличии тела.

## Маршруты

Все перечисленные ответы успешного выполнения имеют код 200, кроме logout (204). Несуществующие и недоступные индивидуальные объекты возвращают 404. В списках сначала применяется область пользователя, затем ограничение выборки.

| Метод и путь | Тело / параметры | Ответ / права |
|---|---|---|
| GET `/auth/csrf` | Нет | `{token:string}`; анонимно |
| POST `/auth/login` | Login | Access, устанавливает cookie; анонимно, ограничение частоты |
| GET `/auth/me` | Нет | Access текущего пользователя |
| POST `/auth/logout` | Пустое тело | 204, удаляет веб-cookie |
| GET `/departments` | Нет | Массив Department |
| POST `/departments` | DepartmentInput | Department; admin |
| GET `/employees` | Нет | Массив Employee с `department_name` |
| POST `/employees` | EmployeeInput | Employee; admin |
| GET `/users` | Нет | id, employee_id, email, is_active, grants; admin, без паролей/хешей |
| POST `/users` | UserInput | `{id,email,role}`; admin |
| DELETE `/users/{id}` | id UUID | `true`; admin, деактивация и отзыв существующих cookie; себя отключить нельзя |
| GET `/devices` | Нет | Массив Device доступных сотрудников |
| POST `/devices` | DeviceInput | `{id,credentialId,token}`; admin, токен показывается один раз |
| POST `/devices/{id}/credentials` | id UUID | Новый токен, прежние отозваны; admin |
| DELETE `/devices/{id}` | id UUID | `true`; admin, retired_at и отзыв токенов, история сохраняется |
| GET `/policies` | Нет | Массив PolicyVersion с названием политики |
| POST `/policies` | PolicyInput | `{id,policyId}`; admin, создаёт и публикует новую версию |
| POST `/policies/{id}/assignments` | Assignment | Назначение; admin, id – версия, заменяет текущую открытую область |
| GET `/resources` | Нет | Массив Resource |
| GET `/employees/{id}/sessions` | id UUID | До 200 сессий, поле `breaks` – JSON-строка массива перерывов |
| POST `/employees/{id}/sessions` | id UUID | Сессия; только сотрудник для себя, открытая сессия повторно не создаётся |
| POST `/sessions/{id}/end` | id UUID | `{id,operation,at}`; только сотрудник для себя |
| POST `/sessions/{id}/breaks` | `{kind:"lunch"}` | `{id,operation,at}`; только сотрудник для себя |
| POST `/sessions/{id}/breaks/end` | id UUID | `{id,operation,at}`; сотрудник – свой обед, HR/manager/admin – разрешённое отсутствие |
| POST `/sessions/{id}/absence` | id UUID | `{id,operation,at}`; HR/manager/admin в своей области |
| POST `/sessions/{id}/analysis` | AnalysisInput | AnalysisRun, новый неизменяемый результат; HR/manager/admin |
| GET `/employees/{id}/analysis-runs` | id UUID | До 200 завершённых запусков |
| GET `/analysis-runs/{id}` | id UUID | Сохранённый AnalysisRun; разрешённое чтение сотрудника |
| GET `/violations` | Нет | До 1000 нарушений актуальных запусков; фильтры имени/типа выполняет UI |
| GET `/violations/{id}` | id UUID | `{violation,comments}`; разрешённое чтение сотрудника |
| PATCH `/violations/{id}` | ReviewInput | `{id,status}`; HR/manager/admin |
| POST `/agent/events` | `{events:EventInput[]}` | `{accepted:UUID[],duplicates:UUID[]}`; токен устройства |

## Входные модели

Все поля обязательны, кроме помеченных `?`. Не передавайте дополнительные поля организации в CRUD-запросах: область устанавливает сервер.

| Модель | Поля и ограничения |
|---|---|
| Login | organizationId: UUID; email: string; password: string (12–128) |
| DepartmentInput | code: string; name: string; непустые, до 200 |
| EmployeeInput | departmentId: UUID; employeeNumber: string; fullName: string (до 200); positionName?: string/null; hiredOn: date |
| UserInput | employeeId?: UUID/null; email: string; password: string (12–128); role: admin/hr/manager/employee; departmentId?: UUID/null. employee требует employeeId, manager – departmentId |
| DeviceInput | employeeId: UUID; name: string (до 200); platform: windows/linux/macos. Реальный сборщик текущей версии – Windows |
| PolicyInput | name: string; idleLimitSeconds/nonTargetLimitSeconds/lunchLimitSeconds: positive int; timeZone: IANA string; rules: массив до 1000 `{resourceId:UUID,category:productive/non_target/neutral}` без повторов ресурса |
| Assignment | scope: organization/department/employee; targetId: UUID/null. Для organization – null, иначе ID выбранной области |
| AnalysisInput | until: datetime, не позднее времени API; signalLifetimeSeconds: int 1–86400. Период от начала сессии, максимум 7 дней/100000 событий |
| ReviewInput | status: reviewed/dismissed; comment?: string/null (до 4000) |
| EventInput | clientEventId: UUID; occurredAt: datetime; kind: Login/Focus/Input/Idle/Heartbeat/Logout; application?: string/null – имя процесса до 128, буквы/цифры/точка/дефис/подчёркивание, без пути/заголовка |

События: 1–500 в пакете, время не более чем на 5 минут впереди часов API. Перед сохранением моменты нормализуются до UTC и микросекунд. Идентичный повтор `(organization,device,clientEventId)` подтверждается как duplicate; другое содержимое того же ключа даёт 409 и откатывает весь пакет. Порядок доставки не обязан совпадать со временем события. Получатель и время приёма определяются сервером. Только Login/Focus/Input могут автоматически начать новую сессию. Logout устройства не завершает общую сессию сотрудника.

## Примеры

Вход (CSRF-cookie и токен предварительно получены):

```http
POST /api/v1/auth/login
Content-Type: application/json
X-CSRF-TOKEN: <csrf>

{"organizationId":"10000000-0000-0000-0000-000000000001","email":"admin@example.test","password":"<локальный-пароль>"}
```

Успешный Access содержит organizationId, userId, employeeId (может быть null), email, grants (role/scope/departmentId), isAdmin. Конкретные UUID создаются при развёртывании. Возвращаемый securityStamp – маркер версии учётной записи; cookie защищена серверными ключами и не формируется из Access на клиенте.

Приём события:

```http
POST /api/v1/agent/events
Content-Type: application/json
Authorization: Bearer <токен-устройства>

{"events":[{"clientEventId":"7a50d2e2-1a6c-4e53-8dbd-34237dd1ad80","occurredAt":"2026-10-13T08:00:00Z","kind":"Focus","application":"code.exe"}]}
```

```json
{"accepted":["7a50d2e2-1a6c-4e53-8dbd-34237dd1ad80"],"duplicates":[]}
```

AnalysisRun: id, organizationId, sessionId, policyVersionId, algorithmVersion, signalLifetime (TimeSpan-строка), periodStart, periodEnd, intervals, violations. Каждый интервал содержит start/end/category/duration и sources (deviceId/clientEventId). Категории: Productive, NonTarget, Neutral, Idle, Lunch, AuthorizedAbsence, Unknown. Нарушения: Idle, NonTarget, LunchOverrun с start/end/limit/duration. Неизвестный ресурс – Neutral, пропуск сигналов – Unknown.

CRUD-ответы из PostgreSQL используют имена `snake_case`; доменные снимки и DTO входа – `camelCase`. Поля `breaks` и `grants` в списках являются JSON-строками и требуют разбора. Данные не смешиваются с демонстрационными значениями интерфейса.

## Ошибки

| Код | Причина |
|---|---|
| 400 | Неверная модель/параметр, CSRF, нарушение CHECK/FK, неприемлемый пакет |
| 401 | Нет действующей cookie/токена, неверный пароль, временная блокировка входа |
| 403 | Роль не допускает операцию |
| 404 | Объект не существует или недоступен по области данных |
| 409 | Конфликт уникальности/интервалов, бизнес-правило сессии/обеда, конфликт повторного события |
| 429 | Более 20 попыток входа с одного адреса за минуту |
| 500 | Внутренняя ошибка; SQL-детали не возвращаются клиенту |

Пять неуспешных попыток для существующей записи включают 15-минутную блокировку. Cookie проверяется по активной записи, securityStamp и актуальным ролям при каждом запросе. Ошибки имеют формат ProblemDetails (`type/title/status`, иногда `traceId`). Пример: `{"title":"Сотрудник не найден.","status":404,"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5"}`.

## Принципы реализации

- SRP: WindowsObservation читает датчики, DiskOutbox доставляет/сохраняет очередь, IdentityStore проверяет идентичность, ActivityReconstructor строит шкалу, ViolationDetector ищет нарушения.
- OCP/DIP: классификатор подключается через IResourceClassifier; HTTP-приём через IEventInbox, чтение анализа через IAnalysisResults. DI настраивается в Program, ядро не зависит от ASP.NET/Npgsql.
- ISP: приём событий и чтение результатов имеют отдельные небольшие интерфейсы.
- LSP: проверочный HttpMessageHandler агента заменяет настоящий транспорт, сохраняя контракт HttpClient; offline/ack/restart проверяются тем же DiskOutbox.
- DRY: единый TenantDatabase задаёт транзакционный контекст; общие проверки Employee/Scope и загрузка сессии/политики используются в сценариях.
- KISS: один сервер и PostgreSQL, без брокера и дополнительных сетевых аналитических служб. SQL-миграции Flyway – единственный источник схемы.

Использованные источники библиотек: [Npgsql basic usage](https://www.npgsql.org/doc/basic-usage.html), [ASP.NET Core CSRF](https://learn.microsoft.com/aspnet/core/security/anti-request-forgery), [ASP.NET Core OpenAPI](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0). Npgsql 10.0.0 и Microsoft.AspNetCore.OpenApi 10.0.12 закреплены в csproj.
