using SteamFun.Domain;

namespace SteamFun.Services;

public sealed record JokeFields(
    string Citizenship,
    string ValidUntil,
    string Residence,
    string Transmission,
    string BloodType,
    string SpecialMarks,
    IReadOnlyList<string> SpecialNotes,
    IReadOnlyList<string> MedicalNotes);

/// <summary>
/// Шуточные поля удостоверения — от статистики, с лёгким seed от SteamID (стабильно между запусками).
/// </summary>
public static class JokeFieldsBuilder
{
    public static JokeFields Build(
        SteamProfileSnapshot profile,
        IReadOnlyList<GameCategory> categories,
        int fleetSize,
        int neverPlayed,
        double totalHours,
        double hoursLast14Days,
        PenaltySummary penalties,
        PronounSet pronouns)
    {
        var backlogRatio = fleetSize == 0 ? 0 : (double)neverPlayed / fleetSize;
        var rng = new Random(StableSeed(profile.SteamId64));

        var opened = categories.Where(c => c.Status == CategoryStatus.Opened).ToList();
        var revoked = categories.Where(c => c.Status == CategoryStatus.Revoked).ToList();

        // Для характера — недавняя активность, не lifetime (иначе CS2 5-летней давности рулит всем).
        var shooterHours = PlayActivity.CategoryHoursForIdentity(profile.Games, CategoryCode.B);
        var soulsHours = PlayActivity.CategoryHoursForIdentity(profile.Games, CategoryCode.E);
        var rpgHours = PlayActivity.CategoryHoursForIdentity(profile.Games, CategoryCode.C);
        var topCat = opened
            .OrderByDescending(c => PlayActivity.CategoryHoursForIdentity(profile.Games, c.Code))
            .ThenByDescending(c => c.TotalHours)
            .FirstOrDefault();

        var perfectAchievements = profile.Top10Achievements
            .Where(a => !a.StatsPrivate && a.Total > 0 && a.Unlocked == a.Total)
            .ToList();

        var topGame = PlayActivity.TopActiveGame(profile.Games)
                      ?? profile.Games.OrderByDescending(g => g.PlaytimeForeverMinutes).FirstOrDefault();
        var lifetimeTop = profile.Games.OrderByDescending(g => g.PlaytimeForeverMinutes).FirstOrDefault();

        return new JokeFields(
            Citizenship: PickCitizenship(backlogRatio, totalHours, rng),
            ValidUntil: "до Half-Life 3",
            Residence: PickResidence(backlogRatio, neverPlayed, rng),
            Transmission: PickTransmission(shooterHours, soulsHours, rpgHours, hoursLast14Days, totalHours),
            BloodType: PickBloodType(profile.SteamLevel, totalHours, backlogRatio, rng),
            SpecialMarks: BuildSpecialMarks(
                profile,
                neverPlayed,
                backlogRatio,
                shooterHours,
                soulsHours,
                rpgHours,
                hoursLast14Days,
                perfectAchievements,
                topCat,
                topGame,
                lifetimeTop,
                pronouns,
                rng),
            SpecialNotes: BuildSpecialNotes(
                neverPlayed, backlogRatio, hoursLast14Days, revoked, penalties, rng),
            MedicalNotes: BuildMedicalNotes(
                backlogRatio, hoursLast14Days, shooterHours, soulsHours, profile.VacBanned));
    }

    private static int StableSeed(string steamId64)
    {
        unchecked
        {
            var hash = 17;
            foreach (var ch in steamId64)
                hash = hash * 31 + ch;
            return hash;
        }
    }

    private static string PickCitizenship(double backlogRatio, double totalHours, Random rng)
    {
        if (backlogRatio >= 0.45)
            return Pick(rng, "Бэклогистан", "Республика Непройденных", "Независимая Складская Область");
        if (backlogRatio >= 0.25)
            return Pick(rng, "Бэклогистан", "Федерация Отложенных Запусков", "Провинция Списка желаний");
        if (totalHours >= 10_000)
            return Pick(rng, "Часовск", "Великое Пробежье", "Империя Онлайн-Часов");
        return Pick(rng, "Габения", "Портовая зона Steam", "Союз Купи-и-Забудь");
    }

    private static string PickResidence(double backlogRatio, int neverPlayed, Random rng)
    {
        if (neverPlayed >= 40)
            return Pick(rng,
                "Библиотека им. Габена, стеллаж «ещё не тронуто»",
                $"Склад дополнений, бокс №{neverPlayed}",
                "Район Вечных Распродаж, кв. «В корзине»");
        if (backlogRatio >= 0.2)
            return Pick(rng,
                "Библиотека им. Габена",
                "Общежитие при лаунчере",
                "ул. Списка желаний, д. 1");
        return Pick(rng, "Центр игрового движения", "Гараж у CS-сервера", "Дом у игрового костра");
    }

    private static string PickTransmission(
        double shooterHours,
        double soulsHours,
        double rpgHours,
        double hoursLast14Days,
        double totalHours)
    {
        if (shooterHours >= rpgHours && shooterHours >= soulsHours && shooterHours >= 1000)
            return hoursLast14Days >= 20
                ? "Механика (вечный рейтинг, сцепление уже буксовать начинает)"
                : "Механика (пристреляна, но пылится в гараже)";

        if (soulsHours >= 500 && soulsHours >= shooterHours * 0.4)
            return "Полуавтомат: умер — загрузился — пошёл снова";

        if (rpgHours >= 1000)
            return "Вариатор (сюжетный: 120+ часов на «ещё один квест»)";

        if (hoursLast14Days <= 0 && totalHours > 100)
            return "Нейтраль (парк стоит на приколе)";

        if (shooterHours > 0 && rpgHours > 0)
            return "Гибрид (перевоспитан: с шутера на RPG и обратно)";

        return "Автомат «купил — установил — забыл»";
    }

    private static string PickBloodType(int level, double totalHours, double backlogRatio, Random rng)
    {
        if (backlogRatio >= 0.3)
            return Pick(rng, "Молоко (парное)", "0 (нулевой пробег)", "B+ (бэклог-положительный)");
        if (level >= 50)
            return Pick(rng, "A+ (ачивочный)", "Уровень+", "Кофе с энергетиком");
        if (totalHours >= 5000)
            return Pick(rng, "Редкие часы", "Чёрный чай 3 ночи подряд", "Молоко (парное)");
        return Pick(rng, "Молоко (парное)", "Пиксель-отрицательный", "АВ (любая скидка)");
    }

    private static string BuildSpecialMarks(
        SteamProfileSnapshot profile,
        int neverPlayed,
        double backlogRatio,
        double shooterHours,
        double soulsHours,
        double rpgHours,
        double hoursLast14Days,
        IReadOnlyList<AchievementProgress> perfect,
        GameCategory? topCat,
        OwnedGameInfo? topGame,
        OwnedGameInfo? lifetimeTop,
        PronounSet pr,
        Random rng)
    {
        var character = new List<string>();
        var backlog = new List<string>();

        var topName = topGame is null ? null : Truncate(topGame.Name, 28);
        var topHours = (topGame?.PlaytimeForeverMinutes ?? 0) / 60.0;
        var recentNote = topGame is null ? null : PlayActivity.FormatLastPlayed(topGame);

        if (topName is not null && (topHours >= 50 || (topGame?.Playtime2WeeksMinutes ?? 0) > 0))
        {
            var when = recentNote is null ? "" : $" · {recentNote}";
            character.Add(Pick(rng,
                $"сейчас главная лошадка — «{topName}» ({topHours:0.#} ч{when})",
                $"в глазах читается «{topName}»{when}",
                $"актуальный пробег: «{topName}» ({topHours:0.#} ч{when})"));
        }

        if (lifetimeTop is not null
            && topGame is not null
            && lifetimeTop.AppId != topGame.AppId
            && !PlayActivity.IsRecentlyActive(lifetimeTop)
            && lifetimeTop.PlaytimeForeverMinutes >= 60 * 500)
        {
            var legend = Truncate(lifetimeTop.Name, 24);
            var legendWhen = PlayActivity.FormatLastPlayed(lifetimeTop) ?? "давно";
            character.Add(Pick(rng,
                $"когда-то легенда «{legend}», но {legendWhen}",
                $"в трудовой книжке ещё числится «{legend}» ({legendWhen})"));
        }

        if (shooterHours >= 3000)
        {
            character.Add(Pick(rng,
                "в зрачках отражается прицел; слышит шаги через два этажа",
                "вечный запах пороха и звук перезарядки в голове",
                "смотрит на людей как на пиксели на мини-карте"));
        }
        else if (shooterHours >= 1000)
        {
            character.Add(Pick(rng,
                "лёгкий прищур стрелка",
                "рука сама тянется поправить чувствительность мыши"));
        }

        if (soulsHours >= 1000)
        {
            character.Add(Pick(rng,
                "шрам от надписи YOU DIED; чаще падает со скал в игре, чем от боссов",
                $"спокойное лицо человека, {pr.Which} уже {pr.Died} сегодня раз 40",
                "походка осторожная: в играх обрывы кусаются"));
        }
        else if (soulsHours >= 400)
        {
            character.Add("есть опыт «ещё одна попытка» на сложных боссах");
        }

        if (rpgHours >= 1500)
        {
            character.Add(Pick(rng,
                "смотрит вдаль так, будто там квестовая метка",
                "в кармане невидимо лежит журнал на 40 побочных заданий"));
        }

        if (perfect.Count > 0)
        {
            var sample = Truncate(perfect[rng.Next(perfect.Count)].GameName, 28);
            character.Add(Pick(rng,
                $"все ачивки собраны в «{sample}»",
                $"перфекционист: «{sample}» закрыта на 100%"));
        }

        if (hoursLast14Days >= 30)
            character.Add(Pick(rng, "синяки под глазами формата «ещё одну катку»", "ночной режим включён постоянно"));
        else if (hoursLast14Days <= 0 && profile.Games.Count > 0)
            character.Add("выглядит так, будто лаунчер открывает чаще, чем игры");

        if (profile.VacBanned)
            character.Add($"на учёте ВАС ({profile.NumberOfVacBans})");
        else if (topCat is not null && character.Count < 2)
            character.Add($"основная категория сейчас: {topCat.Code} ({topCat.TitleRu})");

        if (profile.SteamLevel >= 70 && character.Count < 2)
            character.Add($"Steam LVL {profile.SteamLevel} — значки смотрят свысока");

        if (neverPlayed >= 20)
        {
            backlog.Add(Pick(rng,
                $"на полке пылится {neverPlayed} игр без запуска",
                $"неприкосновенный запас: {neverPlayed} непройденных",
                $"покупки как наклейки в альбом (+{neverPlayed} без запуска)"));
        }
        else if (neverPlayed >= 5)
        {
            backlog.Add($"{neverPlayed} игр ждут первого запуска");
        }

        if (backlogRatio < 0.1 && neverPlayed < 5)
            character.Add("подозрительно доигрывает купленное — проверить на подмену");

        var parts = character.Concat(backlog).ToList();
        if (parts.Count == 0)
            parts.Add("внешних особых примет не обнаружено (кроме библиотеки)");

        if (parts.Count <= 3)
            return string.Join("; ", parts);

        var head = character.Take(2).ToList();
        if (backlog.Count > 0)
            head.Add(backlog[0]);
        else
            head.AddRange(character.Skip(2).Take(1));

        return string.Join("; ", head.Take(3));
    }

    private static IReadOnlyList<string> BuildSpecialNotes(
        int neverPlayed,
        double backlogRatio,
        double hoursLast14Days,
        IReadOnlyList<GameCategory> revoked,
        PenaltySummary penalties,
        Random rng)
    {
        var notes = new List<string>();

        if (neverPlayed >= 30)
        {
            notes.Add(Pick(rng,
                "запрет на новые покупки до истечения срока (сначала запустить старые)",
                $"квота на новые покупки: 0, пока на складе ≥ {neverPlayed} игр",
                "допуск к распродажам — только в сопровождении взрослых"));
        }
        else if (backlogRatio >= 0.2)
        {
            notes.Add("покупка новой игры — только если есть свидетель первого запуска");
        }

        if (hoursLast14Days >= 30)
            notes.Add("режим усиленного игрового движения: не сидеть за рулём больше 4 часов подряд");
        else if (hoursLast14Days <= 0)
            notes.Add("рекомендовано принудительно поиграть в ближайшие 14 дней");

        foreach (var r in revoked.Take(2))
            notes.Add($"кат. {r.Code}: допуск приостановлен — см. учёт нарушений");

        if (penalties.TotalPoints >= 6)
            notes.Add("приближается порог лишения — явка на комиссию ГАБДД обязательна");

        if (notes.Count == 0)
            notes.Add("особых ограничений нет (пока)");

        return notes.Take(4).ToList();
    }

    private static IReadOnlyList<string> BuildMedicalNotes(
        double backlogRatio,
        double hoursLast14Days,
        double shooterHours,
        double soulsHours,
        bool vacBanned)
    {
        var notes = new List<string>
        {
            backlogRatio >= 0.25
                ? "рефлекс покупки — гиперактивен"
                : "рефлекс покупки — в пределах нормы",
            hoursLast14Days <= 1
                ? "рефлекс игры — почти спит"
                : hoursLast14Days >= 25
                    ? "рефлекс игры — перевозбуждён (см. шутеры/RPG)"
                    : "рефлекс игры — стабильный",
        };

        if (shooterHours >= 2000)
            notes.Add("на звук шагов реагирует отлично; на мысль о сне — никак");

        if (soulsHours >= 1000)
            notes.Add("терпение к смертям повышено (соулс-лайки)");

        notes.Add(vacBanned
            ? "к игровому движению НЕ годен (ВАС-учёт)"
            : backlogRatio >= 0.25 || hoursLast14Days >= 40
                ? "годен к игровому движению с ограничениями (см. отметки)"
                : "годен к игровому движению");

        return notes;
    }

    private static string Pick(Random rng, params string[] options) =>
        options[rng.Next(options.Length)];

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";
}
