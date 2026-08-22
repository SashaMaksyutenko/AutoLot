namespace AutoLot.Domain.Enums;

/// <summary>
/// Привід. Названий Drivetrain, а не Drive: у стандартній бібліотеці вже є
/// System.IO.DriveType для дисків, і два однойменні типи в одному файлі
/// вимагали б повного імені щоразу.
/// </summary>
public enum DrivetrainType
{
    FrontWheel = 0,
    RearWheel = 1,
    AllWheel = 2,
}
