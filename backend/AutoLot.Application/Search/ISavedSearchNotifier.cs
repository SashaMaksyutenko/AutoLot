namespace AutoLot.Application.Search;

/// <summary>
/// Розсилка листів про нові збіги в збережених пошуках.
///
/// Окремо від <see cref="ISavedSearchService"/>, бо це інша робота й інший
/// хто. Той обслуговує людину, яка натиснула кнопку; цей — фонову задачу,
/// що прокидається за розкладом і нікого не питає.
/// </summary>
public interface ISavedSearchNotifier
{
    /// <summary>
    /// Проходить усі пошуки з увімкненими сповіщеннями й надсилає листи про
    /// те, що з'явилося після минулого разу. Повертає, скільки листів пішло.
    /// </summary>
    Task<int> NotifyAsync(CancellationToken cancellationToken = default);
}
