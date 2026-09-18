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

Создан каркас из проектов Api, Application, Domain и Infrastructure. Реализован GET /health, подключены OpenAPI и Swagger UI. SDK задан в global.json, версии пакетов — в файле проекта Api. PostgreSQL, миграции, авторизация, бизнес-сценарии, контейнеры и CI пока не реализованы.

Планируемая структура для совместного запуска:

```text
JobMatch/
  jobmatch-frontend/
  jobmatch-backend/
  jobmatch-docs/
```

Compose будет находиться в этом репозитории и собирать frontend из соседней папки. Ко второй лабораторной: API с `/health`, подключение к PostgreSQL, начальная миграция и воспроизводимый запуск.

## Требования для каркаса

- .NET SDK 10.0.400 или совместимый более новый SDK 10.0 согласно [global.json](global.json). Одного Runtime недостаточно.
- Git и доступ к репозиторию.
- Интернет для первого восстановления NuGet-пакетов.

Docker и PostgreSQL для текущего каркаса не требуются.

```bash
dotnet --version
dotnet --list-sdks
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
| `http://localhost:5140/openapi/v1.json` | OpenAPI-документ с GET `/health` |
| `http://localhost:5140/swagger` | Swagger UI; GET `/health` → Try it out → Execute возвращает 200 |

Во втором терминале:

```bash
curl -i http://localhost:5140/health
curl -i http://localhost:5140/openapi/v1.json
```

Примеры запросов для редакторов с поддержкой HTTP-файлов: [JobMatch.Api.http](src/JobMatch.Api/JobMatch.Api.http).

`/health` проверяет, что API отвечает; подключение к БД он пока не проверяет. OpenAPI и Swagger UI доступны только в `Development`. Для `/` обработчик не задан: 404 по этому адресу ожидаем. Шаблонный `/weatherforecast` удалён.

## Приёмка каркаса

Альберт получает ветку PR на своём компьютере, собирает и запускает API по README, проверяет `/health`, OpenAPI и запрос через Swagger UI, затем сообщает результат в PR. До этой проверки каркас не считается полностью принятым.

Следующая задача — PostgreSQL и начальная миграция. Контейнеры, CI и бизнес-сценарии выполняются отдельными задачами.

## Связанные репозитории

- [Frontend](https://github.com/CODEXSQUAD/jobmatch-frontend).
- [Docs](https://github.com/CODEXSQUAD/jobmatch-docs) — MVP, ERD и согласованные API-контракты.

## Работа с задачами

Задача содержит ответственного, срок и критерий готовности. Изменения выполняются в отдельной ветке и проходят PR с ревью другого участника. Изменение схемы сопровождается миграцией; изменение API согласуется с frontend и документацией.

Не коммитить пароли, токены и реальные персональные данные. Для демонстрации использовать вымышленные данные.
