# База данных WorkPulse — ЛР7

PostgreSQL **18.6**, Flyway **Community 13.10.0**, Docker Compose. SaaS: общая схема `workpulse`, данные нескольких организаций, составные внешние ключи и PostgreSQL Row Level Security. Контейнерные образы закреплены по digest в `compose.yaml`. Локальные PostgreSQL, Java и .NET SDK не нужны.

Исходная модель ЛР7 зафиксирована миграциями V001–V007; её отчёт сформирован. Для ЛР9 добавлены V008–V009: пароли/токены устройств, сохранение снимков и защита завершённой аналитики. API, Windows-агент и UI подключены; общий запуск описан в [server/api-guide.md](../server/api-guide.md). Документы модели ЛР7 отражают исходную схему; расширения ЛР9 описаны контрактом и новыми миграциями.

## Быстрый запуск на Windows

1. Установить и запустить Docker Desktop с Linux containers / WSL2.
2. Открыть PowerShell в корне проекта.
3. Выполнить:

```powershell
.\database\setup.ps1
.\database\db.ps1 -Action up
.\database\db.ps1 -Action seed
.\database\db.ps1 -Action test
.\database\db.ps1 -Action info
```

`setup` создаёт игнорируемый `database/.env` с двумя разными случайными паролями. Существующий файл сохраняется. `up` поднимает PostgreSQL, дожидается готовности и применяет миграции; `seed` добавляет исключительно вымышленные данные двух организаций. `test` требует этого seed, выполняет проверки в откатываемых транзакциях. Seed необязателен для обычной разработки.

Если выполнение локальных `.ps1` заблокировано политикой компьютера, запустить каждый скрипт через `powershell -NoProfile -ExecutionPolicy Bypass -File .\database\db.ps1 -Action up` (аналогично другие действия). Это параметр данного процесса, постоянная политика не меняется.

Первый запуск скачивает образы и требует интернет. С уже загруженными образами запуск возможен без интернета. Ни UI, ни Figma для работы БД не нужны.

## Подключение DBeaver / DataGrip / psql

| Поле | Значение |
|---|---|
| Host | `127.0.0.1` |
| Port | `55432` (настраивается `DB_PORT` в `.env`) |
| Database | `workpulse` |
| Schema | `workpulse` |
| Пользователь для администрирования | `workpulse_owner` |
| Пароль администратора | `DB_OWNER_PASSWORD` из локального `.env` |
| Пользователь будущего сервера | `workpulse_app` |
| Пароль сервера | `DB_APP_PASSWORD` из локального `.env` |

Для просмотра всей модели в графическом клиенте использовать owner. Будущий сервер должен подключаться **только как `workpulse_app`**. Пример Npgsql-строки: `Host=127.0.0.1;Port=55432;Database=workpulse;Username=workpulse_app;Password=<локальный пароль>`; сохранять её в секретах конфигурации сервера, не в Git и не в браузере.

Owner — технический superuser Docker-инициализации, он обходит RLS и нужен для миграций/бэкапов. App — без superuser, BYPASSRLS и прав создания таблиц; не изменяет журнал аудита и справочник ролей.

Каждая операция сервера должна идти в транзакции с контекстом организации, полученным из проверенной авторизации:

```sql
BEGIN;
SELECT set_config('app.organization_id', '10000000-0000-0000-0000-000000000001', true);
SELECT set_config('app.actor_id', '10000000-0000-0000-0000-000000000001', true);
SELECT * FROM workpulse.employees;
COMMIT;
```

Третий аргумент `true` ограничивает настройки транзакцией и предотвращает утечку контекста при пуле соединений. Без контекста app видит пустой результат и не может записать tenant-данные. Нельзя передавать клиенту прямое подключение к БД: владелец app-пароля сам способен выставить контекст. RLS здесь защищает запросы сервера; проверка членства пользователя и области `self/department/organization` — задача авторизации API в следующих этапах. Таблица назначений ролей хранит эту информацию, но сама не реализует полную RBAC-матрицу.

## Повседневные команды

```powershell
.\database\db.ps1 -Action up        # запуск + недостающие миграции
.\database\db.ps1 -Action migrate   # применить новые миграции
.\database\db.ps1 -Action validate  # проверить историю и checksum файлов
.\database\db.ps1 -Action info      # версии и состояние
.\database\db.ps1 -Action seed      # повторяемый демонстрационный набор
.\database\db.ps1 -Action test      # целостность + изоляция через app по TCP
.\database\db.ps1 -Action backup    # custom-format pg_dump
.\database\db.ps1 -Action down      # убрать контейнеры/сеть, сохранить volume
```

После `down` данные остаются в `workpulse-database_pgdata`. Следующий `up` использует их. Команда не затрагивает другие Compose-проекты. Не добавлять `-v`, если нужно сохранить данные.

Логи и состояние:

```powershell
docker compose --project-directory database ps
docker compose --project-directory database logs --tail 100 postgres
docker compose --project-directory database exec postgres psql -U workpulse_owner -d workpulse
```

## Миграции и развитие схемы

Единственный редактируемый источник DDL — `migrations/VNNN__name.sql`. Семь миграций покрывают организационную модель, политики, рабочие данные, вспомогательные журналы, RLS/аудит и согласованность анализа. `schema-metadata.json`, словарь и ER-диаграмма — снимки фактической схемы для документации, не альтернативные миграции.

Добавлять `V008__...sql` и далее. Уже применённые файлы не редактировать: Flyway обнаружит изменение checksum. Не использовать `repair` для скрытия незапланированных расхождений. Для исправления — новая forward-миграция; для восстановления данных — бэкап. В бесплатной конфигурации здесь нет команды автоматического undo. PostgreSQL-миграции выполняются транзакционно; непрошедшую миграцию исправляют до успешного применения.

В дальнейшем EF Core/Npgsql отображает существующие таблицы и составные ключи. Не запускать `EnsureCreated` и не создавать второй параллельный набор EF-миграций. Если позже решим перейти на EF-миграции, это отдельное согласованное изменение владельца схемы с baseline и проверкой.

Демоданные отделены от DDL: `seed/demo.sql`. Производственный запуск не должен вызывать seed. Начальная организация и учётные записи создаются доверенной операцией provisioning; app может записывать только tenant текущего контекста, публичной регистрации и проверки личности здесь ещё нет.

## Резервное копирование и восстановление

`backup` сохраняет файл в игнорируемой папке `database/backups`. Файл создаётся внутри контейнера и копируется через `docker compose cp`, поэтому бинарный архив не повреждается перенаправлением PowerShell.

Проверка восстановления в **новую пустую БД**, без перезаписи рабочей:

```powershell
docker compose --project-directory database exec -T postgres createdb -U workpulse_owner workpulse_restore
docker compose --project-directory database cp .\database\backups\workpulse-YYYYMMDD-HHMMSS.dump postgres:/tmp/restore.dump
docker compose --project-directory database exec -T postgres pg_restore -U workpulse_owner -d workpulse_restore --exit-on-error /tmp/restore.dump
docker compose --project-directory database exec -T postgres psql -U workpulse_owner -d workpulse_restore -c "SELECT count(*) FROM workpulse.organizations;"
```

Подставить имя своего файла. На новом сервере сначала выполнить bootstrap роли `workpulse_app` (стандартный `up` его создаёт), затем восстановление. `pg_dump` сохраняет базу, но не серверные роли и не их пароли. В примере рабочая `workpulse` продолжает существовать независимо. Переключение приложения на восстановленную БД выполняется отдельно. Для полноценного production нужны внешние защищённые бэкапы и проверяемый регламент восстановления.

Изменение пароля в `.env` **не меняет пароль внутри уже созданной БД**: Docker bootstrap выполняется только на пустом volume. Сменить пароль через безопасную административную сессию (`\password workpulse_app` в psql), обновить локальный секрет и пересоздать контейнер. Для owner аналогично. Не удалять volume ради смены пароля.

## Если запуск не удался

| Симптом | Действие |
|---|---|
| Docker daemon / pipe недоступен | Запустить Docker Desktop, проверить Linux containers и доступ текущего пользователя |
| Порт 55432 занят | Изменить `DB_PORT`, повторить `up`, обновить порт клиента |
| Authentication failed после правки `.env` | Пароль существующего volume не поменялся; восстановить прежний секрет либо сменить пароль административно |
| app возвращает пустые таблицы | Начать транзакцию и установить корректный `app.organization_id` |
| Нарушение составного FK | Проверить, что связанные записи принадлежат одной организации и существуют |
| Checksum mismatch | Вернуть применённый файл из Git, оформить изменения новой миграцией |
| seed конфликтует с пользовательскими данными | Демонстрационные UUID/slug зарезервированы; запускать seed в учебной БД |

## Материалы к будущему отчёту

- [Модель и обоснование нормализации](model.md).
- [Словарь физических полей и ограничений](data-dictionary.md).
- [ER-диаграмма](er-diagram.md).
- [Результаты фактических проверок](verification.md).
- Исходные DDL: `migrations`; DML: `seed`; проверочные сценарии: `tests`.

Документация инструментов: [PostgreSQL Docker image](https://hub.docker.com/_/postgres), [Flyway Docker](https://documentation.red-gate.com/fd/flyway-docker-321585710.html), [проверка миграций](https://documentation.red-gate.com/flyway/reference/commands/validate), [PostgreSQL RLS](https://www.postgresql.org/docs/18/ddl-rowsecurity.html).
