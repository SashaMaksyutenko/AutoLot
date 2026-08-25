namespace AutoLot.Domain.Enums;

/// <summary>Стан торгів. Змінюється лише в один бік: Active → Ended.</summary>
public enum AuctionStatus
{
    /// <summary>Торги йдуть, ставки приймаються.</summary>
    Active = 0,

    /// <summary>
    /// Час вийшов. Чи є переможець — окреме питання: лот міг не зібрати
    /// жодної ставки або не дотягнути до резервної ціни.
    /// </summary>
    Ended = 1,

    /// <summary>Торги припинені достроково — модератором або продавцем до першої ставки.</summary>
    Cancelled = 2,
}
