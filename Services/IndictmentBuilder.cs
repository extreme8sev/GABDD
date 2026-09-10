using SteamFun.Domain;

namespace SteamFun.Services;

/// <summary>
/// «Статьи обвинения» — конкретные игры, часы, %, контрасты (как в макете-досье).
/// </summary>
public static class IndictmentBuilder
{
    public sealed record Result(string Headline, IReadOnlyList<string> Charges);

    public static Result Build(
        SteamProfileSnapshot profile,
        int fleetSize,
        int neverPlayed,
        double totalHours,
        Random rng)
    {
        var years = profile.AccountCreated is null
            ? 0
            : Math.Max(1, (int)((DateTimeOffset.Now - profile.AccountCreated.Value).TotalDays / 365.25));

        var headline = BuildHeadline(years, neverPlayed, fleetSize, totalHours, rng);
        var charges = new List<string>();

        var byHours = profile.Games
            .Where(g => g.PlaytimeForeverMinutes > 0)
            .OrderByDescending(g => g.PlaytimeForeverMinutes)
            .ToList();

        var achByApp = profile.Top10Achievements
            .Where(a => !a.StatsPrivate && a.Total > 0)
            .ToDictionary(a => a.AppId);

        // 1) Топ-игра как % жизни в Steam
        if (byHours.Count > 0 && totalHours >= 200)
        {
            var top = byHours[0];
            var h = top.PlaytimeForeverMinutes / 60.0;
            var pct = 100.0 * h / totalHours;
            var days = h / 24.0;
            if (pct >= 8 || h >= 400)
            {
                charges.Add(Pick(rng,
                    $"«{Short(top.Name)}» — {h:0} ч, это {pct:0}% всей жизни в Steam. " +
                    $"Карту нюхали {days:0} суток подряд и всё ещё называют это хобби.",
                    $"«{Short(top.Name)}» сожрала {h:0} ч ({pct:0}% пробега). " +
                    $"Если бы это был пробег автомобиля — ГАБДД давно бы изъяла права."));
            }
        }

        // 2) Много часов + мало ачивок
        foreach (var g in byHours.Take(6))
        {
            if (!achByApp.TryGetValue(g.AppId, out var ach))
                continue;
            var h = g.PlaytimeForeverMinutes / 60.0;
            var pctAch = 100.0 * ach.Unlocked / ach.Total;
            if (h >= 200 && pctAch <= 25 && ach.Total >= 20)
            {
                charges.Add(Pick(rng,
                    $"«{Short(g.Name)}»: {h:0} ч и всего {pctAch:0}% ачивок. " +
                    $"Это уже не игра, а привычка — как чистить зубы: делаешь, но зачем — неясно.",
                    $"В «{Short(g.Name)}» {h:0} часов стажа при {pctAch:0}% достижений. " +
                    $"Комиссия подозревает: запускаешь по памяти мышц, а не по желанию."));
                break;
            }
        }

        // 3) Много часов + почти все ачивки (контраст перфекционизма)
        foreach (var g in byHours.Take(8))
        {
            if (!achByApp.TryGetValue(g.AppId, out var ach))
                continue;
            var h = g.PlaytimeForeverMinutes / 60.0;
            var pctAch = 100.0 * ach.Unlocked / ach.Total;
            if (h >= 40 && pctAch >= 90 && ach.Total >= 20)
            {
                charges.Add(
                    $"Зато в «{Short(g.Name)}» дожал {pctAch:0}% ачивок за {h:0.#} ч — " +
                    $"видимо, тут наконец нашлось, ради чего жить.");
                break;
            }
        }

        // 4) Контраст: тяжёлая игра vs брошенная классика / нулевой пробег
        if (byHours.Count >= 1)
        {
            var heavy = byHours[0];
            var abandoned = profile.Games
                .Where(g => g.PlaytimeForeverMinutes <= 0)
                .OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
                .Take(40)
                .ToList();

            // предпочитаем «узнаваемые» имена
            var classic = abandoned.FirstOrDefault(g => LooksClassic(g.Name))
                          ?? abandoned.FirstOrDefault();

            if (classic is not null && heavy.PlaytimeForeverMinutes >= 60 * 80)
            {
                var hh = heavy.PlaytimeForeverMinutes / 60.0;
                charges.Add(Pick(rng,
                    $"«{Short(heavy.Name)}» — {hh:0} ч, а «{Short(classic.Name)}» так и не запустил. " +
                    $"Иконка в библиотеке есть. Впечатлений — нет.",
                    $"Купил «{Short(classic.Name)}» и забыл. Зато «{Short(heavy.Name)}» на {hh:0} ч — " +
                    $"приоритеты расставлены, комиссия всё поняла."));
            }
        }

        // 5) Две тяжёлые дисциплины
        if (byHours.Count >= 2)
        {
            var a = byHours[0];
            var b = byHours[1];
            var ha = a.PlaytimeForeverMinutes / 60.0;
            var hb = b.PlaytimeForeverMinutes / 60.0;
            if (ha >= 400 && hb >= 300)
            {
                var catsA = GenreMapper.Map(a);
                var catsB = GenreMapper.Map(b);
                if (!catsA.Intersect(catsB).Any())
                {
                    charges.Add(
                        $"Систематическая смена дисциплины: «{Short(a.Name)}» ({ha:0} ч) и «{Short(b.Name)}» ({hb:0} ч). " +
                        $"Рывки без перерыва — как выезд со второстепенной на главную.");
                }
            }
        }

        // 6) Недавняя активность vs склад
        var recent = profile.Games
            .Where(g => g.Playtime2WeeksMinutes > 0)
            .OrderByDescending(g => g.Playtime2WeeksMinutes)
            .FirstOrDefault();
        if (recent is not null && neverPlayed >= 15)
        {
            var r14 = recent.Playtime2WeeksMinutes / 60.0;
            charges.Add(Pick(rng,
                $"За 14 дней «{Short(recent.Name)}» на {r14:0.#} ч — будто там вышел новый контент. " +
                $"А {neverPlayed} игр на складе ждут первого запуска.",
                $"Снова молотишь «{Short(recent.Name)}» ({r14:0.#} ч / 14 дн.), " +
                $"пока {neverPlayed} непройденных собирают пыль. Коллекционируешь иконки."));
        }

        // 7) Брошенные после час-другого
        var shortTries = profile.Games
            .Where(g => g.PlaytimeForeverMinutes is > 0 and < 90)
            .OrderByDescending(g => g.PlaytimeForeverMinutes)
            .Take(3)
            .ToList();
        if (shortTries.Count >= 2)
        {
            var names = string.Join(", ", shortTries.Select(g => $"«{Short(g.Name)}»"));
            charges.Add(
                $"Тест-драйв и в утиль: {names}. Хватило пары заездов — и на свалку ожидания.");
        }

        // 8) Fallback
        if (charges.Count == 0)
        {
            charges.Add(neverPlayed >= 5
                ? $"На складе {neverPlayed} ед. без запуска при пробеге {totalHours:0} ч. Обвинение пока мягкое — но прецедент есть."
                : $"Пока состав преступления размыт: библиотека {fleetSize} игр, пробег {totalHours:0} ч. Комиссия продолжает наблюдение.");
        }

        // стабильный выбор до 5 статей
        var picked = Shuffle(rng, charges.Distinct(StringComparer.OrdinalIgnoreCase))
            .Take(5)
            .ToList();

        return new Result(headline, picked);
    }

    private static string BuildHeadline(
        int years, int neverPlayed, int fleet, double totalHours, Random rng)
    {
        if (neverPlayed >= 20 && years >= 5)
        {
            return Pick(rng,
                $"{years} лет в Steam, {neverPlayed} игр так и не запущены — коллекционируешь иконки, а не впечатления.",
                $"{years} лет стажа, склад из {neverPlayed} непройденных. Парк: {fleet} ед. Впечатления — опциональны.");
        }

        if (neverPlayed >= 10)
        {
            return Pick(rng,
                $"Библиотека на {fleet} игр, из них {neverPlayed} даже не заводил. Это не коллекция — это алиби.",
                $"{neverPlayed} игр без запуска при общем пробеге {totalHours:0} ч. Комиссия делает выводы.");
        }

        if (totalHours >= 8000)
        {
            return Pick(rng,
                $"{years} лет и {totalHours:0} ч пробега. Если бы это были километры — лишили бы прав ещё в прошлом десятилетии.",
                $"Стаж {years} л., пробег {totalHours:0} ч. Формально годен. Фактически — хронический.");
        }

        if (years >= 8)
        {
            return $"{years} лет в Steam, {fleet} ед. в парке, {totalHours:0} ч на спидометре. Дело открыто.";
        }

        return Pick(rng,
            $"По делу владельца библиотеки ({fleet} игр, {totalHours:0} ч): ниже — статьи обвинения.",
            $"Краткая выписка из личного дела Steam: {totalHours:0} ч и характер ниже.");
    }

    private static bool LooksClassic(string name)
    {
        var n = name.ToLowerInvariant();
        string[] hints =
        [
            "half-life", "halflife", "bioshock", "splinter cell", "prince of persia",
            "deus ex", "system shock", "thief", "gothic", "morrowind", "oblivi",
            "portal", "left 4 dead", "team fortress", "quake", "doom ", "doom,",
            "max payne", "silent hill", "resident evil", "metal gear", "hitman",
            "grand theft", "gta ", "witcher", "dragon age", "mass effect",
        ];
        return hints.Any(h => n.Contains(h, StringComparison.Ordinal));
    }

    private static string Short(string name)
    {
        name = name.Replace("™", "").Replace("®", "").Trim();
        return name.Length <= 34 ? name : name[..31] + "…";
    }

    private static List<string> Shuffle(Random rng, IEnumerable<string> items)
    {
        var list = items.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }

    private static string Pick(Random rng, params string[] options) =>
        options[rng.Next(options.Length)];
}
