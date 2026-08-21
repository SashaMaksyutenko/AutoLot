namespace AutoLot.Domain.Common;

/// <summary>
/// Сутність, для якої важливо знати час створення та останньої зміни.
/// Значення проставляє інфраструктура, а не код домену — див. AuditableEntityInterceptor.
/// </summary>
public abstract class AuditableEntity : Entity, IAuditable
{
    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
