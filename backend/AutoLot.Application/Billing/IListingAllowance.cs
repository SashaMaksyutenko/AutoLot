namespace AutoLot.Application.Billing;

/// <summary>
/// Скільки активних оголошень дозволено конкретній людині.
///
/// Окремий вузький інтерфейс, хоч реалізує його той самий клас, що й
/// <see cref="IBillingService"/>. Причина проста: сервісу оголошень потрібне
/// рівно одне число, а не поповнення, історія й оформлення підписки. Взявши
/// весь білінг, він отримав би доступ до операцій із грошима, які до
/// публікації оголошення не мають жодного стосунку.
/// </summary>
public interface IListingAllowance
{
    /// <summary>
    /// Ліміт активних оголошень. <c>null</c> означає «без межі» — так відповідає
    /// і найдорожчий тариф, і дилерський акаунт.
    /// </summary>
    Task<int?> GetListingLimitAsync(long userId, CancellationToken cancellationToken = default);
}
