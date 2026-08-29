using System.ComponentModel.DataAnnotations;

namespace AutoLot.Infrastructure.Email;

/// <summary>Налаштування пошти. Секрети — лише в user-secrets (SPEC §8).</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>
    /// Куди складати листи замість надсилання. Порожньо — надсилаємо
    /// по-справжньому через SMTP.
    ///
    /// Це не «фальшива відправка»: лист формується повністю й лягає файлом,
    /// який відкривається поштовою програмою. У розробці так видно точний
    /// вміст листа, і не треба ані справжнього SMTP, ані чужої скриньки.
    /// </summary>
    public string? DropFolder { get; set; }

    [Required]
    public string FromAddress { get; set; } = "no-reply@autolot.local";

    public string FromName { get; set; } = "AutoLot";

    /// <summary>Адреса сайту — з неї збираються посилання в листах.</summary>
    [Required]
    public string SiteUrl { get; set; } = "http://localhost:5173";

    // ── SMTP, потрібен лише коли DropFolder порожній ──────────────────

    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    public bool UseStartTls { get; set; } = true;

    public string? UserName { get; set; }

    public string? Password { get; set; }
}
