using AutoLot.Application.Common.Abstractions;

namespace AutoLot.Tests.TestDoubles;

/// <summary>
/// Годинник, який завжди показує один і той самий час. Потрібен там, де
/// перевіряється поведінка, залежна від часу: з реальним UtcNow очікуваний
/// результат мінявся б із кожним запуском.
/// </summary>
internal sealed class FixedClock(DateTimeOffset now) : IDateTimeProvider
{
    public DateTimeOffset UtcNow => now;
}
