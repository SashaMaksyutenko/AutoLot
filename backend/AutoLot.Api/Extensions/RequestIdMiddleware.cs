namespace AutoLot.Api.Extensions;

/// <summary>
/// Наскрізний ідентифікатор запиту (SPEC §8).
///
/// Одне звернення до сайту породжує десяток записів у логах: контролер,
/// сервіс, запити до бази, фонова розсилка. Без спільного ідентифікатора
/// зібрати їх докупи можна хіба що за часом — а на завантаженому сервері
/// сусідні мілісекунди належать різним людям.
///
/// Ідентифікатор ще й повертається у відповіді заголовком. Це не дрібниця:
/// людина, у якої щось зламалося, може назвати цей рядок, і в логах одразу
/// знайдеться саме її запит — без розпитувань «а котра була година».
/// </summary>
internal sealed class RequestIdMiddleware(RequestDelegate next, ILogger<RequestIdMiddleware> logger)
{
    /// <summary>
    /// Заголовок, у якому ідентифікатор і приходить, і повертається.
    /// «X-Request-Id» — усталена назва, її розуміють проксі й засоби
    /// збирання логів.
    /// </summary>
    public const string HeaderName = "X-Request-Id";

    /// <summary>
    /// Стеля довжини для чужого ідентифікатора. Захист не від зловмисника,
    /// а від сміття: рядок на кілобайт у кожному рядку логів роздув би їх
    /// у стократ.
    /// </summary>
    private const int MaxLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var requestId = ReadIncoming(context) ?? context.TraceIdentifier;

        // Заголовок ставимо ДО виклику наступного обробника: після того, як
        // відповідь почала йти в мережу, змінювати заголовки вже пізно, а
        // ідентифікатор потрібен саме на помилкових відповідях.
        context.Response.Headers[HeaderName] = requestId;

        // BeginScope додає поле до КОЖНОГО запису логів усередині using —
        // хоч би як глибоко він стався. Саме це й зшиває слід одного
        // звернення докупи.
        //
        // Область задається ШАБЛОНОМ, а не словником, і це не смак.
        // Текстовий формувальник виводить область через ToString(), і
        // словник друкується як «Dictionary`2[String,Object]» — назвою типу
        // замість значення. Шаблон же дає і читабельний рядок у консолі,
        // і окреме поле в JSON.
        //
        // Поле зветься CorrelationId, а не RequestId: під другим іменем
        // ASP.NET уже пише власний ідентифікатор з'єднання, і два різні
        // поняття під одним іменем у логах — вірний спосіб згаяти годину
        // на розслідуванні.
        using var scope = logger.BeginScope("CorrelationId:{CorrelationId}", requestId);

        await next(context);
    }

    /// <summary>
    /// Читає ідентифікатор, який надіслав клієнт.
    /// </summary>
    /// <remarks>
    /// Чуже значення в логах — це вхідні дані, і поводитися з ними треба як
    /// із вхідними. Найнебезпечніший тут не обсяг, а переведення рядка:
    /// вставивши його, зловмисник домалював би в лог власний «запис» і міг
    /// би приховати свої дії серед підроблених. Тому лишаємо лише літери,
    /// цифри й три безпечні знаки.
    /// </remarks>
    private static string? ReadIncoming(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out var values))
        {
            return null;
        }

        var candidate = values.ToString();

        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > MaxLength)
        {
            return null;
        }

        return candidate.All(IsSafe) ? candidate : null;
    }

    private static bool IsSafe(char symbol) =>
        char.IsAsciiLetterOrDigit(symbol) || symbol is '-' or '_' or ':';
}

/// <summary>Підключення проміжного обробника одним рядком у Program.</summary>
internal static class RequestIdMiddlewareExtensions
{
    /// <summary>
    /// Ставити його треба ПЕРШИМ. Усе, що станеться далі — зокрема
    /// перехоплення помилок, — має потрапити в лог уже з ідентифікатором.
    /// </summary>
    public static IApplicationBuilder UseRequestId(this IApplicationBuilder app) =>
        app.UseMiddleware<RequestIdMiddleware>();
}
