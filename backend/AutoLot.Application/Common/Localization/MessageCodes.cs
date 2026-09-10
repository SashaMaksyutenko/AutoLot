namespace AutoLot.Application.Common.Localization;

/// <summary>
/// Коди повідомлень про помилки.
///
/// Валідатори називають ПРАВИЛО, яке порушено, а не готовий текст: перевести
/// код у слова — робота крайнього шару, який єдиний знає мову запиту. Через
/// це прикладний рівень лишається без жодного рядка, призначеного людині.
///
/// Кожна константа тут зобов'язана мати переклад обома мовами — це перевіряє
/// окремий тест, тож забути половину не вийде.
/// </summary>
public static class MessageCodes
{
    public const string AuthEmailRequired = "auth.email.required";
    public const string AuthEmailTooLong = "auth.email.tooLong";
    public const string AuthEmailMalformed = "auth.email.malformed";
    public const string AuthPasswordRequired = "auth.password.required";
    public const string AuthPasswordTooShort = "auth.password.tooShort";
    public const string AuthPasswordTooLong = "auth.password.tooLong";
    public const string AuthPasswordNeedsLower = "auth.password.needsLower";
    public const string AuthPasswordNeedsUpper = "auth.password.needsUpper";
    public const string AuthPasswordNeedsDigit = "auth.password.needsDigit";
    public const string AuthDisplayNameRequired = "auth.displayName.required";
    public const string AuthDisplayNameTooShort = "auth.displayName.tooShort";
    public const string AuthDisplayNameTooLong = "auth.displayName.tooLong";
    public const string AuthAccountTypeUnknown = "auth.accountType.unknown";
    public const string AuthPhoneFormat = "auth.phone.format";
    public const string RecoveryEmailRequired = "recovery.email.required";
    public const string RecoveryEmailMalformed = "recovery.email.malformed";
    public const string RecoveryEmailTooLong = "recovery.email.tooLong";
    public const string RecoveryTokenIncomplete = "recovery.token.incomplete";
    public const string RecoveryPasswordRequired = "recovery.password.required";
    public const string PlaceCityInvalid = "place.city.invalid";
    public const string PlaceDistrictInvalid = "place.district.invalid";
    public const string PlaceCityFirst = "place.city.first";
    public const string ProfileDisplayNameRequired = "profile.displayName.required";
    public const string CarVinFormat = "car.vin.format";
    public const string CarMakeRequired = "car.make.required";
    public const string CarModelRequired = "car.model.required";
    public const string CarGenerationInvalid = "car.generation.invalid";
    public const string CarFeaturesDuplicate = "car.features.duplicate";
    public const string CarFeaturesInvalid = "car.features.invalid";
    public const string CarConditionUnknown = "car.condition.unknown";
    public const string CarMileageRequired = "car.mileage.required";
    public const string CarMileageUnrealistic = "car.mileage.unrealistic";
    public const string CarMileageNewTooHigh = "car.mileage.newTooHigh";
    public const string CarOwnersNewHasNone = "car.owners.newHasNone";
    public const string CarOwnersUnrealistic = "car.owners.unrealistic";
    public const string CarFuelUnknown = "car.fuel.unknown";
    public const string CarTransmissionUnknown = "car.transmission.unknown";
    public const string CarDrivetrainUnknown = "car.drivetrain.unknown";
    public const string CarEngineRequired = "car.engine.required";
    public const string CarEngineNotForElectric = "car.engine.notForElectric";
    public const string CarEngineRange = "car.engine.range";
    public const string CarPowerRange = "car.power.range";
    public const string CarConsumptionCityUnrealistic = "car.consumption.cityUnrealistic";
    public const string CarConsumptionHighwayUnrealistic = "car.consumption.highwayUnrealistic";
    public const string CarConsumptionCombinedUnrealistic = "car.consumption.combinedUnrealistic";
    public const string CarConsumptionNotForElectric = "car.consumption.notForElectric";
    public const string CarBatteryRequired = "car.battery.required";
    public const string CarBatteryOnlyElectric = "car.battery.onlyElectric";
    public const string CarBatteryRange = "car.battery.range";
    public const string CarRangeUnrealistic = "car.range.unrealistic";
    public const string CarRangeNeedsBattery = "car.range.needsBattery";
    public const string CarChargingPortNeedsBattery = "car.chargingPort.needsBattery";
    public const string CarChargingPortUnknown = "car.chargingPort.unknown";
    public const string CarBodyUnknown = "car.body.unknown";
    public const string CarColourUnknown = "car.colour.unknown";
    public const string CarDamageUnknown = "car.damage.unknown";
    public const string CarSeatsRange = "car.seats.range";
    public const string CarDoorsRange = "car.doors.range";
    public const string CarEcologyUnknown = "car.ecology.unknown";
    public const string CarPaintUnknown = "car.paint.unknown";
    public const string CarImportedFromInvalid = "car.importedFrom.invalid";
    public const string CarManufacturerCountryInvalid = "car.manufacturerCountry.invalid";
    public const string ListingTitleRequired = "listing.title.required";
    public const string ListingTitleTooShort = "listing.title.tooShort";
    public const string ListingTitleTooLong = "listing.title.tooLong";
    public const string ListingDescriptionRequired = "listing.description.required";
    public const string ListingDescriptionTooShort = "listing.description.tooShort";
    public const string ListingDescriptionTooLong = "listing.description.tooLong";
    public const string ListingCityRequired = "listing.city.required";
    public const string ListingDistrictInvalid = "listing.district.invalid";
    public const string ListingPricePositive = "listing.price.positive";
    public const string ListingPriceUnrealistic = "listing.price.unrealistic";
    public const string ListingCurrencyUnknown = "listing.currency.unknown";
    public const string ListingTypeUnknown = "listing.type.unknown";
    public const string ListingReserveBelowStart = "listing.reserve.belowStart";
    public const string ListingReserveAuctionOnly = "listing.reserve.auctionOnly";
    public const string ModerationReasonRequired = "moderation.reason.required";
    public const string ModerationReasonTooShort = "moderation.reason.tooShort";
    public const string ModerationReasonTooLong = "moderation.reason.tooLong";
    public const string QuestionTextRequired = "question.text.required";
    public const string QuestionTextTooShort = "question.text.tooShort";
    public const string QuestionTextTooLong = "question.text.tooLong";
    public const string QuestionAnswerRequired = "question.answer.required";
    public const string QuestionAnswerTooLong = "question.answer.tooLong";
    public const string ReportReasonRequired = "report.reason.required";
    public const string ReportCommentTooLong = "report.comment.tooLong";
    public const string ReportCommentRequired = "report.comment.required";
    public const string ReportNoteTooLong = "report.note.tooLong";
    public const string CatalogPageMin = "catalog.page.min";
    public const string CatalogTextTooLong = "catalog.text.tooLong";
    public const string CatalogSortUnknown = "catalog.sort.unknown";
    public const string CatalogPriceRange = "catalog.price.range";
    public const string CatalogYearRange = "catalog.year.range";
    public const string CatalogMileageRange = "catalog.mileage.range";
    public const string CatalogEngineRange = "catalog.engine.range";
    public const string CatalogPowerRange = "catalog.power.range";
    public const string ChatMessageRequired = "chat.message.required";
    public const string ChatMessageTooLong = "chat.message.tooLong";
    public const string SavedSearchNameRequired = "savedSearch.name.required";
}
