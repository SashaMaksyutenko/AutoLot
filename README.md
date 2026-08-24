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
| `GET /api/cars/features` | опції комплектації, згруповані за розділами |
| `GET /api/geo/countries` | країни для полів «виробник» і «країна пригону» |

Кузови, палива, коробки, приводи й кольори — це `enum` у
[AutoLot.Domain.Enums](backend/AutoLot.Domain/Enums/): переліки замкнені, тож
компілятор стежить, щоб ніхто не вписав неіснуючий тип. У базі лежать лише
їхні назви для показу, у таблиці `enum_translations`. Марки, моделі й покоління
навпаки живуть у таблицях, бо їх сотні й вони змінюються.

Дані — у
[car-attributes.json](backend/AutoLot.Infrastructure/Persistence/SeedData/car-attributes.json)
та
[car-makes.json](backend/AutoLot.Infrastructure/Persistence/SeedData/car-makes.json),
заливаються тим самим ідемпотентним механізмом, що й географія. Засіяно 48
марок, 426 моделей і 70 поколінь для найпопулярніших моделей. Кузовів 14,
типів пального 8 — включно з гібридом, плагін-гібридом, електро й воднем.

### Каталог

`GET /api/catalog` — пошук із фільтрами, сортуванням і пагінацією. Показує
**лише активні** оголошення: це межа видимості, а не фільтр, який можна зняти
параметром.

Фільтри: марка, модель, покоління · ціна з валютою · рік, пробіг, об'єм,
потужність · кузов, паливо, КПП, привід, колір · новий чи вживаний · ДТП,
розмитнення, наявність в Україні, країна пригону · область і місто · тип
продавця · опції комплектації · наявність фото.

Набори значень працюють як **«або»** (седан + універсал = обидва типи), опції
комплектації — як **«і»** (підігрів + парктроніки = авто, де є обидва). Ціна
порівнюється в гривні, тож «до 5000 USD» знаходить і гривневі оголошення.
Розмір сторінки обмежений 60 — інакше один запит витягнув би всю базу.

Сортування: найновіші, дешевші, дорожчі, менший пробіг, свіжіший рік. У кожен
порядок доданий ключ за `Id`, інакше оголошення з однаковою ціною могли б
з'їхати між сторінками й показатися двічі.

**Демо-дані.** У режимі розробки при першому старті створюється 200 оголошень
від 12 продавців із фото-заглушками, на яких намальовані марка, модель і рік
(SPEC §11). Вимикається `DemoData:Enabled`; за замовчуванням вимкнено.

### Оголошення та модерація

| Метод і шлях | Що робить |
|---|---|
| `POST /api/listings` | створює чернетку |
| `PUT /api/listings/{id}` | редагує чернетку або відхилене оголошення |
| `GET /api/listings/{id}` | картка; без входу видно лише опубліковані |
| `GET /api/listings/mine?status=` | власні оголошення |
| `POST /api/listings/{id}/submit` | подає на модерацію |
| `POST /api/listings/{id}/sold` | позначає проданим |
| `POST /api/listings/{id}/archive` | прибирає з видачі |
| `DELETE /api/listings/{id}` | видаляє чернетку |
| `GET /api/listings/{id}/photos` | фото оголошення |
| `POST /api/listings/{id}/photos` | завантажує одне фото (multipart) |
| `PUT /api/listings/{id}/photos/order` | задає порядок повним переліком |
| `POST /api/listings/{id}/photos/{photoId}/primary` | робить фото головним |
| `DELETE /api/listings/{id}/photos/{photoId}` | видаляє фото |
| `GET /api/moderation/listings` | черга модерації |
| `POST /api/moderation/listings/{id}/approve` | схвалює й публікує на 60 днів |
| `POST /api/moderation/listings/{id}/reject` | відхиляє з причиною |

Життєвий цикл: `Draft → PendingModeration → Active → (Sold | Archived)`,
з поверненням у `Rejected` і повторним поданням. Переходи описані методами
самої сутності [Listing](backend/AutoLot.Domain/Listings/Listing.cs), а не
розкидані по сервісах.

Чуже неопубліковане оголошення віддає `404`, а не `403` — інакше за кодом
відповіді можна було б перебирати чужі чернетки. Ліміт приватної особи —
5 оголошень у видачі; чернеток може бути скільки завгодно, бо місця вони не
займають. Дилер обмежень не має.

**Фото.** Завантажене зображення ніколи не зберігається таким, як прийшло.
Тип файла визначається за вмістом, а не за розширенням; зображення
перекодовується в JPEG, що зрізає EXIF із координатами зйомки та будь-який
вкладений вміст; зберігаються дві копії — до 1920 пікселів по довшій стороні
та мініатюра 400 для списків. Ліміти: 10 МБ на файл, 20 фото на оголошення.
Файли лежать поза `wwwroot` і роздаються за `/media`; тека `uploads/` у
репозиторій не потрапляє. Перше фото стає головним само, а якщо головне
видалити — головним стане наступне.

`Listing` тримає ціну, місто, статус і умови угоди; технічні характеристики
винесені в `Car`, опції комплектації — у зв'язок «багато до багатьох» із
довідником `Feature` (52 опції в п'яти розділах), країни — у `Country`
(46 записів). Ціна додатково зберігається перерахованою в гривню — щоб
оголошення в різних валютах сортувалися разом. Курс поки береться з
конфігурації; за планом його замінить щоденна задача з API НБУ.

Поля двигуна й батареї лежать в одній таблиці, тож база сама по собі
дозволила б електромобіль з об'ємом двигуна 1.6. Не дозволяє
[CarSpecificationValidator](backend/AutoLot.Application/Listings/Validation/CarSpecificationValidator.cs):
для електро батарея обов'язкова, об'єм двигуна й витрата пального заборонені;
гібрид має право на обидва набори; у нового авто не буває попередніх власників.

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
