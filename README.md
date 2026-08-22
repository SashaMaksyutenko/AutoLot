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

### 2. Секрети

Секрети в репозиторії не зберігаються — лише в `user-secrets`:

```bash
cd backend/AutoLot.Api

dotnet user-secrets set "ConnectionStrings:AutoLot" \
  "Host=localhost;Port=5433;Database=autolot;Username=autolot;Password=<пароль>"

# Ключ підпису JWT, щонайменше 32 символи
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"

# Перший адміністратор. Без цих двох значень сід його не створює
dotnet user-secrets set "Seed:Admin:Email" "admin@autolot.local"
dotnet user-secrets set "Seed:Admin:Password" "<пароль>"
```

Вхід через Google вмикається наявністю ключів; без них схема не реєструється
взагалі, а `/api/auth/google/start` чесно відповідає `501`:

```bash
dotnet user-secrets set "Authentication:Google:ClientId" "<id>"
dotnet user-secrets set "Authentication:Google:ClientSecret" "<secret>"
```

Redirect URI у консолі Google — `http://localhost:5080/signin-google`.

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

### Автентифікація

| Метод і шлях | Що робить |
|---|---|
| `POST /api/auth/register` | реєстрація, одразу видає токени |
| `POST /api/auth/login` | вхід за email і паролем |
| `POST /api/auth/refresh` | обмінює refresh-cookie на нову пару токенів |
| `POST /api/auth/logout` | гасить сесію і чистить cookie |
| `GET /api/auth/me` | профіль поточного користувача |
| `GET /api/auth/google/start` | починає вхід через Google |
| `GET /api/auth/google/callback` | приймає користувача назад від Google |

Access-токен живе 15 хвилин і повертається в тілі відповіді. Refresh-токен
віддається **лише** в httpOnly cookie з областю `/api/auth`, у базі лежить
хешем і ротується при кожному оновленні: пред'явлення вже використаного токена
вважається крадіжкою і гасить усю сім'ю токенів цієї сесії.

Обмеження: 10 запитів за хвилину з однієї адреси на всі маршрути `/api/auth`,
блокування акаунта на 15 хвилин після 5 невдалих спроб входу.

### Довідник географії

Ієрархія має чотири рівні, два з них необов'язкові:
`область → район області → місто → район міста`. Київ і Севастополь районів
області не мають, райони міста є лише у великих містах.

| Метод і шлях | Що робить |
|---|---|
| `GET /api/geo/regions` | 27 областей, АР Крим і міста зі спеціальним статусом |
| `GET /api/geo/regions/{id}/districts` | райони області |
| `GET /api/geo/regions/{id}/cities?districtId=` | міста області, за потреби звужені до району |
| `GET /api/geo/cities/{id}/districts` | райони всередині міста |
| `PUT /api/profile/location` | зберігає місто й район міста поточного користувача |

Назви віддаються мовою із заголовка `Accept-Language`; якщо перекладу немає,
підставляється українська. У профілі зберігається лише місто й район міста —
область і район області однозначно випливають із міста.

Дані лежать у
[geography.json](backend/AutoLot.Infrastructure/Persistence/SeedData/geography.json)
і заливаються при старті застосунку. Сід ідемпотентний: він шукає записи за
сталим кодом, тож повторний запуск нічого не дублює. **Щоб розширити довідник,
достатньо дописати записи у файл — код міняти не треба.** Засіяно всі 27
регіонів, усі 136 районів за реформою 2020 року та 293 міста; кожне місто
прив'язане до свого району, крім Києва й Севастополя, які районів області не
мають. Села та селища до довідника поки не входять.

### Довідники автомобіля

| Метод і шлях | Що робить |
|---|---|
| `GET /api/cars/attributes` | кузов, паливо, КПП, привід і колір одним пакетом |
| `GET /api/cars/makes` | марки: спершу популярні, далі за абеткою |
| `GET /api/cars/makes/{id}/models` | моделі марки |
| `GET /api/cars/models/{id}/generations` | покоління моделі, найновіші першими |

Кузови, палива, коробки, приводи й кольори — це `enum` у
[AutoLot.Domain.Enums](backend/AutoLot.Domain/Enums/): переліки замкнені, тож
компілятор стежить, щоб ніхто не вписав неіснуючий тип. У базі лежать лише
їхні назви для показу, у таблиці `enum_translations`. Марки, моделі й покоління
навпаки живуть у таблицях, бо їх сотні й вони змінюються.

Дані — у
[car-attributes.json](backend/AutoLot.Infrastructure/Persistence/SeedData/car-attributes.json)
та
[car-makes.json](backend/AutoLot.Infrastructure/Persistence/SeedData/car-makes.json),
заливаються тим самим ідемпотентним механізмом, що й географія. Засіяно 49
марок, 446 моделей і 70 поколінь для найпопулярніших моделей. Кузовів 14,
типів пального 8 — включно з гібридом, плагін-гібридом, електро й воднем.

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
