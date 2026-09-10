namespace SteamFun.Domain;

/// <summary>Зафиксированные правила штрафных баллов (лимит 12).</summary>
public static class ViolationRules
{
    public const int MaxPoints = 12;

    /// <summary>Минимум игр без запуска для нарушения «хранение без запуска».</summary>
    public const int NeverPlayedForStorageViolation = 5;

    public const int PointsForUnlaunchedStorage = 2;

    /// <summary>0 минут за 14 дней → простой парка.</summary>
    public const int PointsForTwoWeekIdle = 1;

    /// <summary>Доля игр с нулевым пробегом для нарушения «управление с нулевым пробегом».</summary>
    public const double ZeroMileageRatioThreshold = 0.40;

    public const int PointsForZeroMileageFleet = 1;

    /// <summary>Игры с пробегом &gt; 0 и &lt; 2 ч — «бросил после тест-драйва».</summary>
    public const int MinTestDriveDrops = 8;
    public const int TestDriveMaxMinutes = 120;
    public const int PointsForTestDriveDrops = 2;

    /// <summary>Одна игра сверх «ресурса» категории.</summary>
    public const double OverResourceHours = 800;
    public const int PointsForOverResource = 2;
    public const int PointsForOverResourceMild = 1;

    /// <summary>Две тяжёлые дисциплины без общего жанра — «рывки».</summary>
    public const double DisciplineSwitchHours = 600;
    public const int PointsForDisciplineSwitch = 1;

    /// <summary>Категория открыта, если суммарно ≥ N часов в жанре.</summary>
    public const double HoursToOpenCategory = 5.0;

    /// <summary>ЛИШЁН: ≥ N игр жанра и суммарный пробег &lt; 1 ч.</summary>
    public const int MinGamesToRevoke = 3;

    public const double MaxHoursWhenRevoked = 1.0;
}

public sealed record Violation(string Description, int Points);

public sealed record PenaltySummary(
    IReadOnlyList<Violation> Violations,
    int TotalPoints,
    int MaxPoints)
{
    public int RemainingUntilRevocation => Math.Max(0, MaxPoints - TotalPoints);
}
