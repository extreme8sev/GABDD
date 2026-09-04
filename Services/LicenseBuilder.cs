using SteamFun.Domain;

namespace SteamFun.Services;

public static class LicenseBuilder
{
    public static GamerLicense Build(SteamProfileSnapshot profile, GenderAddress gender = GenderAddress.He)
    {
        var neverPlayed = profile.Games.Count(g => g.PlaytimeForeverMinutes <= 0);
        var totalMinutes = profile.Games.Sum(g => g.PlaytimeForeverMinutes);
        var recentMinutes = profile.Games.Sum(g => g.Playtime2WeeksMinutes);
        var pronouns = Pronouns.For(gender);

        var categories = BuildCategories(profile.Games);
        var penalties = BuildPenalties(profile.Games.Count, neverPlayed, recentMinutes);
        var jokes = JokeFieldsBuilder.Build(
            profile,
            categories,
            profile.Games.Count,
            neverPlayed,
            totalMinutes / 60.0,
            recentMinutes / 60.0,
            penalties,
            pronouns);

        var personality = PersonalityBuilder.Build(
            profile,
            categories,
            profile.Games.Count,
            neverPlayed,
            totalMinutes / 60.0,
            recentMinutes / 60.0,
            pronouns);

        return new GamerLicense(
            Profile: profile,
            FleetSize: profile.Games.Count,
            NeverPlayedCount: neverPlayed,
            TotalHours: totalMinutes / 60.0,
            HoursLast14Days: recentMinutes / 60.0,
            Categories: categories,
            Penalties: penalties,
            Citizenship: jokes.Citizenship,
            ValidUntil: jokes.ValidUntil,
            Residence: jokes.Residence,
            Transmission: jokes.Transmission,
            BloodType: jokes.BloodType,
            SpecialMarks: jokes.SpecialMarks,
            SpecialNotes: jokes.SpecialNotes,
            MedicalNotes: jokes.MedicalNotes,
            Personality: personality);
    }

    private static IReadOnlyList<GameCategory> BuildCategories(IReadOnlyList<OwnedGameInfo> games)
    {
        var buckets = GameCategoryCatalog.All.ToDictionary(
            c => c.Code,
            c => (Title: c.TitleRu, Games: 0, Minutes: 0));

        foreach (var game in games)
        {
            foreach (var code in GenreMapper.Map(game))
            {
                var cur = buckets[code];
                buckets[code] = (cur.Title, cur.Games + 1, cur.Minutes + game.PlaytimeForeverMinutes);
            }
        }

        var list = new List<GameCategory>();
        foreach (var (code, title) in GameCategoryCatalog.All)
        {
            var b = buckets[code];
            var hours = b.Minutes / 60.0;
            var (status, reason) = ResolveStatus(b.Games, hours, code, title);
            list.Add(new GameCategory(code, title, status, b.Games, hours, reason));
        }

        return list;
    }

    private static (CategoryStatus Status, string? Reason) ResolveStatus(
        int owned,
        double hours,
        CategoryCode code,
        string titleRu)
    {
        if (owned >= ViolationRules.MinGamesToRevoke && hours < ViolationRules.MaxHoursWhenRevoked)
        {
            var reason =
                $"Кат. {code} ({titleRu}): куплено {owned} ед., пробег {hours:0.#} ч. " +
                "Транспорт обнаружен заброшенным на парковке.";
            return (CategoryStatus.Revoked, reason);
        }

        if (hours >= ViolationRules.HoursToOpenCategory)
            return (CategoryStatus.Opened, null);

        return (CategoryStatus.NotOpened, null);
    }

    private static PenaltySummary BuildPenalties(int fleetSize, int neverPlayed, int recentMinutes)
    {
        var violations = new List<Violation>();

        if (neverPlayed >= ViolationRules.NeverPlayedForStorageViolation)
        {
            violations.Add(new Violation(
                $"Хранение {neverPlayed} ед. техники без единого запуска",
                ViolationRules.PointsForUnlaunchedStorage));
        }

        if (recentMinutes <= 0)
        {
            violations.Add(new Violation(
                "Простой всего парка: 0 ч за 14 дней",
                ViolationRules.PointsForTwoWeekIdle));
        }

        var zeroRatio = fleetSize == 0 ? 0 : (double)neverPlayed / fleetSize;
        if (fleetSize > 0 && zeroRatio >= ViolationRules.ZeroMileageRatioThreshold)
        {
            violations.Add(new Violation(
                $"Управление {neverPlayed} единицами транспорта с нулевым пробегом",
                ViolationRules.PointsForZeroMileageFleet));
        }

        var total = Math.Min(ViolationRules.MaxPoints, violations.Sum(v => v.Points));
        return new PenaltySummary(violations, total, ViolationRules.MaxPoints);
    }
}
