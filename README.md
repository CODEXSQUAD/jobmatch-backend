# JobMatch — Backend

API, бизнес-логика и база данных JobMatch — сервиса поиска работы с ограниченным количеством откликов.

## Ответственность

- Авторизация, роли и проверка доступа к данным.
- Резюме, компании, вакансии, поиск и отклики.
- Статусы заявок и вакансий.
- Дневной лимит, начисления, списания и журнал операций.
- Снимок резюме, транзакции и защита от повторных списаний.
- PostgreSQL: модели, ограничения, миграции и тестовые данные.
- Проверки API и бизнес-логики.
- Docker Compose для совместного запуска frontend, backend и БД.

## Команда

- **Клим** — тимлид, backend, DevOps; архитектура, авторизация, резюме, отклики, лимиты и интеграция.
- **Альберт** — системный аналитик, backend; компании, вакансии, поиск и статусы.
- **Матвей** — database engineer; схема, ограничения и тестовые данные вместе с backend-разработчиками.
- **Игорь** — DevOps; Compose и окружение вместе с Климом.

## Планируемый стек

C#, ASP.NET Core (.NET 10 LTS), Entity Framework Core, Npgsql, PostgreSQL, ASP.NET Core Identity, Docker Compose.
Стек предлагается для утверждения на первом созвоне.

## Текущий статус

Создан каркас из проектов Api, Application, Domain и Infrastructure. Реализованы `GET /health` и каталог `GET /api/vacancies`, подключены OpenAPI и Swagger UI. Добавлены доменные модели, ASP.NET Core Identity, EF Core, Npgsql, `JobMatchDbContext`, начальная миграция PostgreSQL и повторяемое заполнение вымышленными демонстрационными данными. Совместный запуск описан в `compose.yaml`. Авторизация, остальные бизнес-сценарии и CI пока не реализованы.

Структура для совместного запуска:

```text
JobMatch/
  jobmatch-frontend/
  jobmatch-backend/
  jobmatch-docs/
```

Compose находится в этом репозитории и собирает frontend из соседней папки. Оба репозитория должны содержать изменения задачи [backend #4](https://github.com/CODEXSQUAD/jobmatch-backend/issues/4); пока они не объединены, используйте ветку `feature/docker-compose` в обоих репозиториях.

## Совместный запуск через Docker Compose

Нужны Git и Docker Engine с Compose v2 (проверка конфигурации выполнена на Compose 2.18.1). На Windows используйте Docker Desktop с Linux-контейнерами и работающим WSL 2. Устанавливать Node.js, .NET SDK или PostgreSQL на хост для этого способа не нужно. Первый запуск требует интернета для загрузки образов и зависимостей.

Проверьте Docker: `docker version` должен показывать **Client и Server**, а `docker info --format '{{.OSType}}'` — `linux`.

Из корня `jobmatch-backend`, при соседнем расположении репозиториев:

```bash
cp .env.example .env
docker compose config --quiet
docker compose up --build
```

В PowerShell вместо `cp` можно использовать `Copy-Item .env.example .env`. Создайте `.env` только один раз; не перезаписывайте существующие настройки. Пример пароля и демонстрационные аккаунты предназначены для локальной учебной среды. `.env` исключён из Git и Docker build context.

Сайт доступен на [http://localhost:8080](http://localhost:8080). Если порт занят, измените `FRONTEND_PORT` в `.env`, например на `8081`, и повторите запуск. Единственный порт хоста привязан к `127.0.0.1`: PostgreSQL и backend доступны только внутри сети Compose. Это локальное демонстрационное окружение, без публичного размещения и HTTPS.

### Последовательность старта

1. `postgres` запускает PostgreSQL 17 и проходит `pg_isready`.
2. `backend` запускает `deploy/start-demo.sh`. Существующая команда `--seed-demo-data` сначала применяет EF Core-миграции, затем добавляет отсутствующие демонстрационные записи и завершается. При ошибке HTTP-сервер не запускается.
3. Тот же контейнер запускает API на порту 8080 в `Development`. Его healthcheck читает `/api/vacancies?pageSize=1`, проверяя также доступ к БД и схеме. Обычный `/health` по-прежнему проверяет только ответ API.
4. После готовности backend стартует `frontend`: Nginx раздаёт сборку React и перенаправляет `/health` и `/api/...` к `backend:8080`, сохраняя пути и query-параметры. Прямое открытие маршрутов React поддерживается через `index.html`.

Инициализация выполняется при каждом старте backend. Существующие миграции повторно не применяются, seed добавляет только недостающие записи. Compose рассчитан на один экземпляр backend. Демоданные включены намеренно; этот запуск не предназначен для production.

### Проверка

Во втором терминале из `jobmatch-backend`:

```bash
docker compose ps
curl http://localhost:8080/health
curl 'http://localhost:8080/api/vacancies?pageSize=3&page=1'
```

Ожидаются три сервиса `healthy`, `{"status":"ok"}` и JSON каталога. В новой демонстрационной БД — 6 опубликованных вакансий, 1 черновик и 1 закрытая; API выдаёт только опубликованные. Откройте `/vacancies` для просмотра данных и `/system` для проверки API. Повторное заполнение не должно увеличивать число вакансий.

Посмотреть состояние данных непосредственно в PostgreSQL:

```bash
docker compose exec postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "SELECT count(*) FROM vacancy;"'
```

### Остановка, повторный запуск и сохранение данных

```bash
docker compose stop
docker compose start
```

Либо остановите и удалите только контейнеры и сеть:

```bash
docker compose down
docker compose up --build -d
```

Именованный volume `postgres_data` сохраняет данные; по умолчанию его имя — `jobmatch_postgres_data`. Для проверки сохранности зафиксируйте количество и измените описание одной демонстрационной компании в своей тестовой БД, выполните `down` и повторный `up`, затем убедитесь, что изменение сохранилось. Одного совпадения числа вакансий недостаточно: seed умеет восстанавливать отсутствующие записи.

**Не добавляйте `-v` к `docker compose down` при проверке сохранения: этот флаг удаляет volume с БД.** Не меняйте имя проекта Compose или путь хранения для обычного повторного запуска. Изменение `POSTGRES_PASSWORD`, `POSTGRES_USER` или `POSTGRES_DB` в `.env` не меняет уже инициализированную БД; для существующего volume используйте прежние значения или отдельно согласуйте изменение учётных данных.

Повторно запустить миграции и seed при работающей БД (без отдельного .NET SDK):

```bash
docker compose stop frontend backend
docker compose run --rm --no-deps --entrypoint dotnet backend JobMatch.Api.dll --seed-demo-data
docker compose up -d
```

### Диагностика и приёмка

Фактические результаты запуска, проверки API и сохранения данных: [протокол от 30.09.2026](docs/compose-verification.md).

```bash
docker compose logs --tail=100 postgres backend frontend
docker compose exec frontend nginx -t
```

- Нет секции Server в `docker version`: сначала запустите/исправьте Docker Engine; изменение Compose не устраняет сбой Docker Desktop.
- Не найден соседний `jobmatch-frontend`: проверьте структуру каталогов и наличие его Dockerfile.
- Backend перезапускается: проверьте журнал миграции/seed и совпадение параметров `.env` с существующей БД.
- Ошибка 502: проверьте состояние backend. Nginx повторно разрешает имя сервиса через Docker DNS, поэтому смена IP при пересоздании backend не требует пересборки frontend.
- `/health` работает, а каталог нет: проверьте PostgreSQL, миграции и логи backend; `/health` сам по себе не проверяет БД.

Клим воспроизводит запуск, проверяет каталог, повторную инициализацию и сохранение изменения после `down`/`up`, затем проверяет оба связанных PR. Факт подготовки файлов не заменяет эту приёмку. На чистом Linux-окружении с 8 ГБ RAM запуск проверяется отдельно.

## Требования для отдельного запуска backend

- .NET SDK 10.0.400 или совместимый более новый SDK 10.0 согласно [global.json](global.json). Одного Runtime недостаточно.
- Git и доступ к репозиторию.
- Интернет для первого восстановления NuGet-пакетов.
- PostgreSQL для применения миграции и проверки подключения. Он может работать локально или в контейнере.

Docker не нужен для локальной сборки и создания SQL-скрипта миграции. Для запуска всех компонентов используйте Compose выше.

```bash
dotnet --version
dotnet --list-sdks
```

Локальный `dotnet-ef` закреплён в `dotnet-tools.json`. После клонирования восстановите его:

```bash
dotnet tool restore
dotnet tool run dotnet-ef --version
```

## Проекты и зависимости

| Проект | Назначение | Прямые зависимости |
|---|---|---|
| Domain | Сущности и правила предметной области | Нет |
| Application | Сценарии приложения и интерфейсы | Domain |
| Infrastructure | Работа с БД и интеграциями | Application, Domain |
| Api | HTTP endpoints, конфигурация и запуск | Application, Infrastructure |

Solution находится в `JobMatch.slnx`, проекты — в `src/`. Библиотеки пока содержат шаблонные классы; бизнес-логика будет добавлена в следующих задачах.

## Сборка и запуск

Все команды выполняются из корня `jobmatch-backend` в Git Bash или другом терминале:

```bash
dotnet restore
dotnet build --no-restore
dotnet run --project src/JobMatch.Api --launch-profile http
```

Профиль `http` в [launchSettings.json](src/JobMatch.Api/Properties/launchSettings.json) задаёт адрес `http://localhost:5140` и окружение `Development`. Сервер работает до остановки через `Ctrl+C`. После изменений сохраните файлы и перезапустите сервер.

Если порт занят, временно выберите другой:

```bash
dotnet run --project src/JobMatch.Api --launch-profile http --urls http://localhost:5141
```

В этом случае замените порт в адресах проверки. HTTPS для этой локальной проверки необязателен.

## Проверка API

| Адрес | Ожидаемый результат |
|---|---|
| `http://localhost:5140/health` | HTTP 200 и `{"status":"ok"}` |
| `http://localhost:5140/api/vacancies` | HTTP 200 и страница опубликованных вакансий из PostgreSQL |
| `http://localhost:5140/openapi/v1.json` | OpenAPI-документ с GET `/health` и GET `/api/vacancies` |
| `http://localhost:5140/swagger` | Swagger UI; GET `/health` → Try it out → Execute возвращает 200 |

Во втором терминале:

```bash
curl -i http://localhost:5140/health
curl -i http://localhost:5140/api/vacancies
curl -i http://localhost:5140/openapi/v1.json
```

Примеры запросов для редакторов с поддержкой HTTP-файлов: [JobMatch.Api.http](src/JobMatch.Api/JobMatch.Api.http).

`/health` проверяет, что API отвечает; подключение к БД он пока не проверяет. OpenAPI и Swagger UI доступны только в `Development`. Для `/` обработчик не задан: 404 по этому адресу ожидаем. Шаблонный `/weatherforecast` удалён.

## PostgreSQL и миграции

Приложение читает строку подключения из `ConnectionStrings:DefaultConnection`. В `appsettings.json` находится только безопасный пример для локальной учебной БД:

```text
Host=localhost;Port=5432;Database=jobmatch;Username=jobmatch;Password=jobmatch_dev
```

Не используйте этот пароль для общего или публичного окружения. Настоящее значение передавайте через секрет окружения. Для Git Bash:

```bash
export ConnectionStrings__DefaultConnection='Host=localhost;Port=5432;Database=jobmatch;Username=jobmatch;Password=ваш_локальный_пароль'
```

Переменная окружения имеет приоритет над `appsettings.json`. Не добавляйте локальные `.env` и настоящие пароли в Git.

При запуске backend вне Compose пустую локальную PostgreSQL можно создать одной командой:

```bash
docker run --name jobmatch-postgres \
  --env POSTGRES_DB=jobmatch \
  --env POSTGRES_USER=jobmatch \
  --env POSTGRES_PASSWORD=jobmatch_dev \
  --publish 5432:5432 \
  --volume jobmatch-postgres-data:/var/lib/postgresql/data \
  --health-cmd 'pg_isready -U jobmatch -d jobmatch' \
  --health-interval 5s \
  --health-timeout 5s \
  --health-retries 12 \
  --detach postgres:17-alpine
```

При последующих запусках используйте `docker start jobmatch-postgres`, для остановки — `docker stop jobmatch-postgres`. Именованный volume сохраняет данные между перезапусками контейнера.

Применить все миграции к пустой или существующей локальной БД:

```bash
dotnet tool restore
dotnet tool run dotnet-ef database update \
  --project src/JobMatch.Infrastructure \
  --startup-project src/JobMatch.Api
```

Создать SQL-скрипт без подключения к PostgreSQL:

```bash
dotnet tool run dotnet-ef migrations script --idempotent \
  --project src/JobMatch.Infrastructure \
  --startup-project src/JobMatch.Api
```

После изменения моделей создать следующую миграцию:

```bash
dotnet tool run dotnet-ef migrations add НазваниеМиграции \
  --project src/JobMatch.Infrastructure \
  --startup-project src/JobMatch.Api \
  --output-dir Persistence/Migrations
```

Начальная схема находится в `src/JobMatch.Infrastructure/Persistence/Migrations`. Таблицы `user_claim`, `user_login` и `user_token` относятся к техническому хранилищу ASP.NET Core Identity; основные предметные таблицы соответствуют ERD и словарю данных.

## Демонстрационные данные

Команда ниже в окружении `Development` сначала применяет миграции, затем добавляет только отсутствующие записи с фиксированными идентификаторами и завершает работу без запуска HTTP-сервера:

```bash
dotnet run --project src/JobMatch.Api -- --seed-demo-data
```

Повторный запуск безопасен и не создаёт дубликаты. Заполняются три вымышленных работодателя, три компании, навыки и восемь вакансий: шесть опубликованных, один черновик и одна закрытая. Данные предназначены только для локальной разработки и демонстрации, поэтому API отклоняет команду seed в любом окружении, кроме `Development`. Для демонстрационных работодателей используется пароль `EmployerDemo123!`; не используйте эти учётные записи или пароль в общем и публичном окружении.

После заполнения запустите API обычной командой:

```bash
dotnet run --project src/JobMatch.Api --launch-profile http
```

## Каталог вакансий

`GET /api/vacancies` читает данные через EF Core из PostgreSQL и возвращает только вакансии со статусом `Published` и заполненной датой публикации. Черновики и закрытые вакансии в активный каталог не входят. Сортировка стабильна: сначала `publishedAt` от новых к старым, затем `id`.

Поддерживаемые на этой неделе параметры:

| Параметр | Описание |
|---|---|
| `q` | Поиск без учёта регистра по части названия, от 1 до 200 символов. |
| `city` | Точное совпадение города без учёта регистра, от 1 до 120 символов. |
| `workFormat` | `Office`, `Remote` или `Hybrid`, без учёта регистра. |
| `page` | Номер страницы от 1; по умолчанию 1. |
| `pageSize` | Размер страницы от 1 до 100; по умолчанию 20. |

Примеры:

```bash
curl 'http://localhost:5140/api/vacancies'
curl 'http://localhost:5140/api/vacancies?q=разработчик&city=Москва'
curl 'http://localhost:5140/api/vacancies?workFormat=Remote&page=1&pageSize=2'
curl 'http://localhost:5140/api/vacancies?city=НесуществующийГород'
```

Последний запрос возвращает успешную пустую страницу с `items: []`, `totalCount: 0` и `totalPages: 0`. Некорректная пагинация или фильтр возвращают HTTP 400 с полями `code` и `message`.

Фильтры по зарплате, компании, навыкам, полнотекстовый поиск и произвольная сортировка пока не поддерживаются и оставлены для следующих задач.

## Приёмка каркаса

Альберт получает ветку PR на своём компьютере, собирает и запускает API по README, проверяет `/health`, OpenAPI и запрос через Swagger UI, затем сообщает результат в PR. До этой проверки каркас не считается полностью принятым.

Следующие задачи — карточка вакансии, авторизация, остальные бизнес-сценарии и CI.

## Связанные репозитории

- [Frontend](https://github.com/CODEXSQUAD/jobmatch-frontend).
- [Docs](https://github.com/CODEXSQUAD/jobmatch-docs) — MVP, ERD и согласованные API-контракты.

## Работа с задачами

Задача содержит ответственного, срок и критерий готовности. Изменения выполняются в отдельной ветке и проходят PR с ревью другого участника. Изменение схемы сопровождается миграцией; изменение API согласуется с frontend и документацией.

Не коммитить пароли, токены и реальные персональные данные. Для демонстрации использовать вымышленные данные.
