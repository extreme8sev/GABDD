namespace SteamFun.Domain;

/// <summary>Категории допуска — как на макете ГАБДД.</summary>
public enum CategoryCode
{
    A, // МОБА
    B, // Шутеры
    C, // РПГ
    V, // Экшены
    D, // Стратегии
    E, // Соулс-лайки
    H, // Хорроры
    P, // Платформеры
    G, // Рогалики
    X, // Песочницы
    M, // Казуалки
    F, // Файтинги
    I, // Симуляторы
    R, // Гонки
    S, // Выживание
    O, // MMO
}

public enum CategoryStatus
{
    NotOpened, // не открыта
    Opened,    // открыта
    Revoked,   // ЛИШЁН
}

public sealed record GameCategory(
    CategoryCode Code,
    string TitleRu,
    CategoryStatus Status,
    int OwnedGames,
    double TotalHours,
    string? RevokeReason);

public static class GameCategoryCatalog
{
    public static IReadOnlyList<(CategoryCode Code, string TitleRu)> All { get; } =
    [
        (CategoryCode.A, "МОБА"),
        (CategoryCode.B, "Шутеры"),
        (CategoryCode.C, "РПГ"),
        (CategoryCode.V, "Экшены"),
        (CategoryCode.D, "Стратегии"),
        (CategoryCode.E, "Соулс-лайки"),
        (CategoryCode.H, "Хорроры"),
        (CategoryCode.P, "Платформеры"),
        (CategoryCode.G, "Рогалики"),
        (CategoryCode.X, "Песочницы"),
        (CategoryCode.M, "Казуалки"),
        (CategoryCode.F, "Файтинги"),
        (CategoryCode.I, "Симуляторы"),
        (CategoryCode.R, "Гонки"),
        (CategoryCode.S, "Выживание"),
        (CategoryCode.O, "MMO"),
    ];
}
