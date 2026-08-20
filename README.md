# AutoLot

Майданчик продажу автомобілів з аукціоном для українського ринку. Портфоліо-проєкт;
повне технічне завдання — у [docs/SPEC.md](docs/SPEC.md).

## Стек

ASP.NET Core 10 (Web API, EF Core 10, SignalR, Quartz.NET) · PostgreSQL 18 · React 19 +
Vite + TypeScript + Tailwind CSS · TanStack Query.

## Структура

```
AutoLot/
├─ backend/
│  ├─ AutoLot.Domain           сутності та доменні правила; без зовнішніх залежностей
│  ├─ AutoLot.Application      сценарії, DTO, валідація, інтерфейси інфраструктури
│  ├─ AutoLot.Infrastructure   EF Core, доступ до даних, зовнішні сервіси
│  ├─ AutoLot.Api              контролери, DI, middleware, health-checks
│  └─ AutoLot.Tests            unit + integration
├─ frontend/                   React + Vite + TypeScript
├─ docs/
└─ docker-compose.yml
```

Залежності спрямовані всередину: `Api → Application → Domain`,
`Infrastructure → Application → Domain`. За напрямком стежать тести в
`AutoLot.Tests/Architecture`.

## Що потрібно

.NET SDK 10 · Node 24 · PostgreSQL 18 на порту **5433** · `dotnet-ef`
(`dotnet tool install --global dotnet-ef`).

## Запуск

### 1. База даних

Локальна служба PostgreSQL:

```bash
psql -h localhost -p 5433 -U postgres -f docs/db-setup.sql -v password='<пароль>'
```

Або через Docker (`cp .env.example .env`, задати `POSTGRES_PASSWORD`):

```bash
docker compose up -d
```

### 2. Рядок підключення

Секрети в репозиторії не зберігаються — лише в `user-secrets`:

```bash
cd backend/AutoLot.Api
dotnet user-secrets set "ConnectionStrings:AutoLot" \
  "Host=localhost;Port=5433;Database=autolot;Username=autolot;Password=<пароль>"
```

### 3. Міграції

```bash
dotnet ef database update \
  --project backend/AutoLot.Infrastructure \
  --startup-project backend/AutoLot.Api
```

Нова міграція:

```bash
dotnet ef migrations add <Назва> \
  --project backend/AutoLot.Infrastructure \
  --startup-project backend/AutoLot.Api \
  --output-dir Persistence/Migrations
```

### 4. Бекенд

```bash
dotnet run --project backend/AutoLot.Api
```

| Адреса | Що це |
|---|---|
| `http://localhost:5080` | API |
| `http://localhost:5080/scalar` | інтерактивна документація OpenAPI |
| `http://localhost:5080/openapi/v1.json` | сам документ OpenAPI |
| `http://localhost:5080/health` | усі перевірки |
| `http://localhost:5080/health/live` | процес живий |
| `http://localhost:5080/health/ready` | залежності на місці |

### 5. Фронтенд

```bash
cd frontend
npm install
npm run dev
```

`http://localhost:5173`. Запити `/api` та `/health` Vite проксює на бекенд, тож у
розробці все живе на одному походженні.

## Тести

```bash
dotnet test
```

## Правила роботи

Агент не робить git-комітів — наприкінці кожної сесії видає готовий текст меседжа,
фіксує зміни автор. Одна сесія — один закінчений пункт плану з `docs/SPEC.md §10`.
