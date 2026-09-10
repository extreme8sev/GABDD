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
        var penalties = BuildPenalties(profile.Games, neverPlayed, recentMinutes);
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

    private static PenaltySummary BuildPenalties(
        IReadOnlyList<OwnedGameInfo> games,
        int neverPlayed,
        int recentMinutes)
    {
        var violations = new List<Violation>();
        var fleetSize = games.Count;

        if (neverPlayed >= ViolationRules.NeverPlayedForStorageViolation)
        {
            violations.Add(new Violation(
                $"Хранение {neverPlayed} ед. техники без единого запуска",
                ViolationRules.PointsForUnlaunchedStorage));
        }

        var testDrives = games.Count(g =>
            g.PlaytimeForeverMinutes > 0
            && g.PlaytimeForeverMinutes < ViolationRules.TestDriveMaxMinutes);
        if (testDrives >= ViolationRules.MinTestDriveDrops)
        {
            violations.Add(new Violation(
                $"Бросил {testDrives} ед. после тест-драйва (пробег < 2 ч)",
                ViolationRules.PointsForTestDriveDrops));
        }

        if (recentMinutes <= 0 && fleetSize > 0)
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

        var top = games
            .OrderByDescending(g => g.PlaytimeForeverMinutes)
            .FirstOrDefault();
        if (top is not null)
        {
            var hours = top.PlaytimeForeverMinutes / 60.0;
            var name = ShortGame(top.Name);
            if (hours >= ViolationRules.OverResourceHours * 2)
            {
                violations.Add(new Violation(
                    $"Превышение ресурса «{name}» на {hours:0} ч — выезд на встречку без радара",
                    ViolationRules.PointsForOverResource));
            }
            else if (hours >= ViolationRules.OverResourceHours)
            {
                violations.Add(new Violation(
                    $"Эксплуатация «{name}» сверх ресурса: {hours:0} ч",
                    ViolationRules.PointsForOverResourceMild));
            }
        }

        var heavy = games
            .Where(g => g.PlaytimeForeverMinutes / 60.0 >= ViolationRules.DisciplineSwitchHours)
            .OrderByDescending(g => g.PlaytimeForeverMinutes)
            .Take(2)
            .ToList();
        if (heavy.Count == 2)
        {
            var a = GenreMapper.Map(heavy[0]);
            var b = GenreMapper.Map(heavy[1]);
            var overlap = a.Intersect(b).Any();
            if (!overlap)
            {
                var h0 = heavy[0].PlaytimeForeverMinutes / 60.0;
                var h1 = heavy[1].PlaytimeForeverMinutes / 60.0;
                violations.Add(new Violation(
                    $"Смена дисциплины без перерыва: «{ShortGame(heavy[0].Name)}» {h0:0} ч → «{ShortGame(heavy[1].Name)}» {h1:0} ч — рывки",
                    ViolationRules.PointsForDisciplineSwitch));
            }
        }

        var total = Math.Min(ViolationRules.MaxPoints, violations.Sum(v => v.Points));
        return new PenaltySummary(violations, total, ViolationRules.MaxPoints);
    }

    private static string ShortGame(string name)
    {
        name = name.Replace("™", "").Replace("®", "").Trim();
        return name.Length <= 28 ? name : name[..27] + "…";
    }
}
