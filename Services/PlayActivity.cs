using SteamFun.Domain;

namespace SteamFun.Services;

/// <summary>
/// Оценка «во что играют сейчас», а не за всю жизнь.
/// Lifetime-часы (CS2 на 2500 ч) не должны забивать актуальный профиль.
/// </summary>
public static class PlayActivity
{
    /// <summary>Игра считается «живой», если трогали за этот срок.</summary>
    public static readonly TimeSpan RecentWindow = TimeSpan.FromDays(180);

    public static bool IsRecentlyActive(OwnedGameInfo game, TimeSpan? window = null)
    {
        if (game.Playtime2WeeksMinutes > 0)
            return true;

        var maxAge = window ?? RecentWindow;
        if (game.LastPlayedUtc is null)
            return false;

        return DateTimeOffset.UtcNow - game.LastPlayedUtc.Value <= maxAge;
    }

    /// <summary>
    /// Чем выше — тем актуальнее игра для «кто ты сейчас».
    /// 14 дней >> недавний last_played >> старый forever.
    /// </summary>
    public static double Score(OwnedGameInfo game)
    {
        double score = game.Playtime2WeeksMinutes * 200.0;

        if (game.LastPlayedUtc is DateTimeOffset last)
        {
            var days = (DateTimeOffset.UtcNow - last).TotalDays;
            if (days < 0) days = 0;

            // Свежесть last_played усиливает «вес» накопленных часов.
            var freshness = days switch
            {
                <= 14 => 1.0,
                <= 30 => 0.6,
                <= 90 => 0.25,
                <= 180 => 0.1,
                <= 365 => 0.03,
                _ => 0.005,
            };

            score += game.PlaytimeForeverMinutes * freshness;
            score += Math.Max(0, 400.0 - days); // бонус «трогали недавно»
        }
        else
        {
            // Нет даты — почти не учитываем вечный пробег.
            score += game.PlaytimeForeverMinutes * 0.002;
        }

        return score;
    }

    public static OwnedGameInfo? TopActiveGame(IEnumerable<OwnedGameInfo> games) =>
        games
            .OrderByDescending(Score)
            .ThenByDescending(g => g.Playtime2WeeksMinutes)
            .ThenByDescending(g => g.LastPlayedUtc ?? DateTimeOffset.MinValue)
            .FirstOrDefault(g => Score(g) > 1);

    public static IReadOnlyList<OwnedGameInfo> RankByActivity(IEnumerable<OwnedGameInfo> games, int take) =>
        games
            .OrderByDescending(Score)
            .ThenByDescending(g => g.Playtime2WeeksMinutes)
            .ThenByDescending(g => g.LastPlayedUtc ?? DateTimeOffset.MinValue)
            .Take(take)
            .ToList();

    /// <summary>
    /// Часы по категории для характера: недавние игры полностью +
    /// заметный lifetime (Дота на 3к ч год назад всё ещё «ты каточник»).
    /// </summary>
    public static double CategoryHoursForIdentity(
        IEnumerable<OwnedGameInfo> games,
        CategoryCode code,
        TimeSpan? window = null)
    {
        double sumHours = 0;
        foreach (var game in games)
        {
            if (!GenreMapper.Map(game).Contains(code))
                continue;

            var hours = game.PlaytimeForeverMinutes / 60.0;
            if (hours <= 0)
                continue;

            if (IsRecentlyActive(game, window))
            {
                sumHours += hours;
                continue;
            }

            // Старый, но весомый пробег — не обнуляем (иначе 5к часов Доты = «0 МОБА»).
            var days = game.LastPlayedUtc is DateTimeOffset last
                ? Math.Max(0, (DateTimeOffset.UtcNow - last).TotalDays)
                : 9999;

            var weight = days switch
            {
                <= 180 => 0.9,
                <= 365 => 0.75,
                <= 730 => 0.55,
                _ => hours >= 200 ? 0.4 : hours >= 50 ? 0.2 : 0.05,
            };

            sumHours += hours * weight;
        }

        return sumHours;
    }

    public static string? FormatLastPlayed(OwnedGameInfo game)
    {
        if (game.Playtime2WeeksMinutes > 0)
            return "играет сейчас (14 дн.)";

        if (game.LastPlayedUtc is null)
            return null;

        var days = (DateTimeOffset.UtcNow - game.LastPlayedUtc.Value).TotalDays;
        if (days < 2) return "заходил вчера";
        if (days < 14) return $"заходил {days:0} дн. назад";
        if (days < 60) return $"заходил ~{(int)(days / 7)} нед. назад";
        if (days < 400) return $"заходил ~{(int)(days / 30)} мес. назад";
        return $"не заходил ~{(int)(days / 365)} г.";
    }
}
