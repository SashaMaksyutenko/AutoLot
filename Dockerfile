# Образ усього застосунку: API разом із зібраним фронтендом.
#
# Чому ОДИН образ, а не два. Спокусливо винести фронтенд в окремий контейнер
# з nginx — так роблять часто. Тут це зламало б SEO: пошуковий робот не
# виконує JavaScript, тому заголовок і фото конкретного авто для нього
# підставляє БЕКЕНД, коли віддає index.html (див. SpaHosting.cs). Якби
# сторінку роздавав nginx, роботи бачили б порожню заготовку.
#
# Збірка багатоетапна. Кожен FROM починає новий етап зі своїм набором
# інструментів, а в підсумковий образ ми копіюємо лише результат. Тому в
# кінцевому образі немає ні .NET SDK, ні Node, ні вихідного коду — лише
# зібраний застосунок.

# ─────────────────────────── 1. Фронтенд ───────────────────────────
FROM node:24-alpine AS frontend
WORKDIR /src

# Спершу тільки файли залежностей. Docker кешує кожен крок окремо, і поки ці
# два файли не змінилися, довге встановлення пакетів береться з кешу — навіть
# якщо в коді сторінок змінилося все.
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

COPY frontend/ ./
RUN npm run build

# ─────────────────────────── 2. Бекенд ───────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src

# Той самий прийом, що й вище: спочатку файли проєктів, потім відновлення
# пакетів, і лише тоді решта коду.
COPY Directory.Build.props ./
COPY backend/AutoLot.Domain/AutoLot.Domain.csproj backend/AutoLot.Domain/
COPY backend/AutoLot.Application/AutoLot.Application.csproj backend/AutoLot.Application/
COPY backend/AutoLot.Infrastructure/AutoLot.Infrastructure.csproj backend/AutoLot.Infrastructure/
COPY backend/AutoLot.Api/AutoLot.Api.csproj backend/AutoLot.Api/
RUN dotnet restore backend/AutoLot.Api/AutoLot.Api.csproj

COPY backend/ backend/
RUN dotnet publish backend/AutoLot.Api/AutoLot.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# ─────────────────────────── 3. Що поїде на сервер ───────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Два пакети, обидва потрібні.
#
# libfontconfig1 — системна служба пошуку шрифтів. Її вимагає рідна бібліотека
# SkiaSharp, якою застосунок обробляє фото; без неї падає перше ж завантаження
# зображення.
#
# curl — щоб було чим робити перевірку життя внизу. У базовому образі немає
# жодного інструмента для HTTP-запиту: він розрахований на те, щоб містити
# рівно застосунок і нічого більше.
#
# Списки пакетів після встановлення прибираємо: у фінальному образі вони
# займають десятки мегабайтів і більше ніколи не знадобляться.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=backend /app/publish ./
COPY --from=frontend /src/dist ./spa

# Тека для завантажених фото. Оголошуємо томом, щоб знімки пережили
# перестворення контейнера: без цього оновлення образу стирало б усі фото.
RUN mkdir -p /app/uploads
VOLUME /app/uploads

# Подвійне підкреслення — те, як .NET читає вкладені налаштування зі змінних
# оточення: Frontend__DistPath це "Frontend": { "DistPath": … }.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    Frontend__DistPath=/app/spa \
    PhotoStorage__RootPath=/app/uploads

EXPOSE 8080

# Перевірка життя, яку бачить docker compose. Питаємо /health/ready, а не
# /health/live: «процес запустився» замало, важливо, щоб застосунок дістався
# бази. start-period дає час на міграції — доки він не мине, невдалі спроби
# не рахуються як збій.
HEALTHCHECK --interval=15s --timeout=5s --start-period=60s --retries=5 \
    CMD curl --fail --silent http://localhost:8080/health/ready || exit 1

ENTRYPOINT ["dotnet", "AutoLot.Api.dll"]
