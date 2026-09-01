using AutoLot.Application.Listings.Dtos;
using AutoLot.Domain.Enums;
using FluentValidation;

namespace AutoLot.Application.Listings.Validation;

/// <summary>
/// Перевірки скарги.
///
/// Головне правило тут — умовне: пояснення потрібне лише тоді, коли причина
/// сама по собі нічого не пояснює. Для «дублікат» досить самого пункту, а
/// «інше» без тексту — це порожній сигнал, на який модератор витратить час
/// і нічого не зрозуміє.
/// </summary>
public sealed class SubmitReportRequestValidator : AbstractValidator<SubmitReportRequest>
{
    public SubmitReportRequestValidator()
    {
        // IsInEnum ловить значення, якого в переліку немає: клієнт може
        // надіслати будь-яке число, і без перевірки воно мовчки лягло б
        // у базу причиною «17».
        RuleFor(request => request.Reason)
            .IsInEnum().WithMessage("Оберіть причину зі списку.");

        RuleFor(request => request.Comment)
            .MaximumLength(1000).WithMessage("Пояснення задовге — до 1000 символів.");

        RuleFor(request => request.Comment)
            .NotEmpty().WithMessage("Опишіть, у чому річ.")
            .When(request => request.Reason == ListingReportReason.Other);
    }
}

public sealed class ResolveReportRequestValidator : AbstractValidator<ResolveReportRequest>
{
    public ResolveReportRequestValidator()
    {
        RuleFor(request => request.Note)
            .MaximumLength(1000).WithMessage("Нотатка задовга — до 1000 символів.");
    }
}
