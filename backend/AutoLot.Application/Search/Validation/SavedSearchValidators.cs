using AutoLot.Application.Search.Dtos;
using AutoLot.Domain.Search;
using FluentValidation;
using AutoLot.Application.Common.Localization;

namespace AutoLot.Application.Search.Validation;

/// <summary>
/// Межі назви збереженого пошуку.
///
/// Ті самі числа, що й у домені, взяті з нього ж константами. Дублювати їх
/// цифрами тут означало б завести два джерела правди: одне змінили б, друге
/// лишили, і валідатор пропускав би те, що сутність відкидає.
/// </summary>
public sealed class SaveSearchRequestValidator : AbstractValidator<SaveSearchRequest>
{
    public SaveSearchRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage(MessageCodes.SavedSearchNameRequired)
            .MaximumLength(SavedSearch.MaxNameLength)
            .WithMessage($"Назва задовга — до {SavedSearch.MaxNameLength} символів.");
    }
}

public sealed class RenameSearchRequestValidator : AbstractValidator<RenameSearchRequest>
{
    public RenameSearchRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage(MessageCodes.SavedSearchNameRequired)
            .MaximumLength(SavedSearch.MaxNameLength)
            .WithMessage($"Назва задовга — до {SavedSearch.MaxNameLength} символів.");
    }
}
