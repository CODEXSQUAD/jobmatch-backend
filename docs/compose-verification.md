# Проверка совместного запуска — 30 сентября 2026 года

Задача: [backend#4](https://github.com/CODEXSQUAD/jobmatch-backend/issues/4). Проверены локальные изменения веток `feature/docker-compose` в соседних backend и frontend. CI разрабатывается отдельно в `feature/ci-build`.

## Окружение и результат

Windows, Docker Desktop 4.93.0, Linux Engine 29.8.1, Compose 5.5.1. Перед первым запуском контейнеров и volumes не было. `.env` создан из `.env.example` и исключён из Git. В build context не входят локальные зависимости, результаты сборки и `.env`.

Команда из backend:

```bash
docker compose up --build -d --wait --wait-timeout 180
```

Оба образа успешно собраны из исходников. Frontend, backend и PostgreSQL получили статус `healthy`. Backend выполняется от пользователя `app`, UID 1654. На хост опубликован только `127.0.0.1:8080` frontend; БД и API доступны внутри сети Compose.

| Проверка | Результат |
|---|---|
| `docker compose config --quiet` | Конфигурация корректна |
| `docker compose exec frontend nginx -t` | Конфигурация Nginx корректна |
| Автоматическая миграция | В `__EFMigrationsHistory` присутствует `20260921115804_InitialCreate` |
| Демонстрационные данные | В таблице `vacancy` 8 записей, API возвращает 6 опубликованных |
| `GET /health` через порт 8080 | `{"status":"ok"}` |
| `GET /api/vacancies?pageSize=3&page=1` и `page=2` | По 3 разных записи, параметры и префикс `/api` сохранены |
| Прямое открытие `/vacancies` | Nginx возвращает SPA, браузер показывает данные PostgreSQL |
| Пагинация в браузере | Вторая страница показывает Data Engineer, QA-инженера и DevOps-инженера |
| Остановленный backend | Вместо карточек показана понятная ошибка с кнопкой повторной загрузки |
| Восстановление backend | Кнопка повторной загрузки возвращает каталог |
| Остановка всех контейнеров и повторный `up` | Изменение записи компании сохранено |
| `down` без `-v`, затем `up --build` | Изменение сохранено после пересоздания контейнеров и сети |
| Отдельный повторный seed по README | Завершается успешно; миграция не повторяется, вакансий остаётся 8, изменение компании сохранено |

## Как проверялось сохранение данных

В описание демонстрационной компании с ID `20000000-0000-4000-8000-000000000001` добавлена временная отметка `[compose-persistence-check]`. После каждого перезапуска запрос к PostgreSQL подтверждал её наличие. Это проверяет сохранение существующей записи, а не только восстановление стандартных данных seed-командой.

Проверены обе последовательности:

```bash
docker compose stop
docker compose up -d --wait --wait-timeout 120
```

```bash
docker compose down
docker compose up --build -d --wait --wait-timeout 120
```

Volume `jobmatch_postgres_data` не удалялся. После проверок убрана только временная отметка; исходное описание компании восстановлено.

Для повторной миграции и заполнения использована команда из README:

```bash
docker compose stop frontend backend
docker compose run --rm --no-deps --entrypoint dotnet backend JobMatch.Api.dll --seed-demo-data
docker compose up -d --wait --wait-timeout 120
```

## Границы проверки

Подтверждён локальный запуск Linux-контейнеров через Docker Desktop. Независимый запуск Климом, приёмка двух PR и проверка на отдельной Linux-машине с 8 ГБ RAM ещё не подтверждены. Контейнеры оставлены работающими для просмотра. Это демонстрационное окружение с вымышленными данными, а не production-развёртывание.
