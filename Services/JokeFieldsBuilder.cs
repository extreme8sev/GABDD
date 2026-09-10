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
/// Шуточные поля удостоверения в тоне Fallout Shelter: короткая deadpan-карточка предмета.
/// Правила: 1 предложение; конкретный образ → подрыв; без канцелярита комиссии.
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

        var shooterHours = PlayActivity.CategoryHoursForIdentity(profile.Games, CategoryCode.B);
        var soulsHours = PlayActivity.CategoryHoursForIdentity(profile.Games, CategoryCode.E);
        var rpgHours = PlayActivity.CategoryHoursForIdentity(profile.Games, CategoryCode.C);
        var horrorHours = PlayActivity.CategoryHoursForIdentity(profile.Games, CategoryCode.H);
        var mobaHours = PlayActivity.CategoryHoursForIdentity(profile.Games, CategoryCode.A);

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

        var identity = ResolveIdentity(shooterHours, soulsHours, rpgHours, horrorHours, mobaHours, backlogRatio);

        return new JokeFields(
            Citizenship: PickCitizenship(identity, backlogRatio, totalHours, rng),
            ValidUntil: Pick(rng,
                "До Half-Life 3.",
                "До перезарядки.",
                "До конца распродажи.",
                "Пока не кончится эстус."),
            Residence: PickResidence(identity, backlogRatio, neverPlayed, rng),
            Transmission: PickTransmission(
                shooterHours, soulsHours, rpgHours, mobaHours, hoursLast14Days, totalHours),
            BloodType: PickBloodType(identity, profile.SteamLevel, backlogRatio, rng),
            SpecialMarks: BuildSpecialMarks(
                identity,
                profile,
                neverPlayed,
                backlogRatio,
                shooterHours,
                soulsHours,
                rpgHours,
                mobaHours,
                hoursLast14Days,
                perfectAchievements,
                topCat,
                topGame,
                lifetimeTop,
                pronouns,
                rng),
            SpecialNotes: BuildSpecialNotes(
                identity, neverPlayed, backlogRatio, hoursLast14Days, revoked, penalties, topGame, rng),
            MedicalNotes: BuildMedicalNotes(
                identity, backlogRatio, hoursLast14Days, shooterHours, soulsHours, mobaHours,
                profile.VacBanned));
    }

    private enum IdentityKind
    {
        Shooter,
        Souls,
        Rpg,
        Horror,
        Moba,
        Backlog,
        Mixed,
    }

    private static IdentityKind ResolveIdentity(
        double shooter, double souls, double rpg, double horror, double moba, double backlogRatio)
    {
        if (backlogRatio >= 0.4 && Math.Max(shooter, Math.Max(souls, Math.Max(rpg, moba))) < 800)
            return IdentityKind.Backlog;

        var ranked = new (IdentityKind Kind, double Hours)[]
        {
            (IdentityKind.Shooter, shooter),
            (IdentityKind.Souls, souls),
            (IdentityKind.Rpg, rpg),
            (IdentityKind.Horror, horror),
            (IdentityKind.Moba, moba),
        }.OrderByDescending(x => x.Hours).ToList();

        if (ranked[0].Hours < 120)
            return backlogRatio >= 0.25 ? IdentityKind.Backlog : IdentityKind.Mixed;

        return ranked[0].Kind;
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

    private static string PickCitizenship(
        IdentityKind identity, double backlogRatio, double totalHours, Random rng)
    {
        if (backlogRatio >= 0.45)
            return Pick(rng,
                "Бэклогистан. Здесь покупают быстрее, чем запускают.",
                "Республика Непройденных. Въезд свободный, выезд — нет.",
                "Складская Область. Население — иконки.");

        return identity switch
        {
            IdentityKind.Shooter => Pick(rng,
                "Респавния. Здесь умирают бесплатно.",
                "Пороховск. Воздух пахнет гильзами.",
                "Федерация Мини-Карты. Границы рисуют заново каждый раунд."),
            IdentityKind.Souls => Pick(rng,
                "Костроград. Тепло есть. Жизней нет.",
                "Лордранская область. Туризм не рекомендован.",
                "Зона YOU DIED. Паспорт уже проштампован."),
            IdentityKind.Rpg => Pick(rng,
                "Квестляндия. Главный сюжет где-то рядом.",
                "Провинция Побочных. Дороги ведут в никуда полезное.",
                "Вольюжский край. Диалоги длиннее границ."),
            IdentityKind.Horror => Pick(rng,
                "Темногорск. Свет включён — уже подозрительно.",
                "Уезд Холодного Пота. Климат стабильный."),
            IdentityKind.Moba => Pick(rng,
                "Рейтинговая Республика. Гражданство обновляется каждую катку.",
                "Патистан. Один за всех, пока не проиграли.",
                "Зона Базы. Фонтан — единственный надёжный адрес."),
            IdentityKind.Backlog => Pick(rng,
                "Бэклогистан. Здесь покупают быстрее, чем запускают.",
                "Провинция Списка желаний. Столица — скидка 70%."),
            _ => totalHours >= 10_000
                ? Pick(rng,
                    "Часовск. Время здесь считают пачками.",
                    "Великое Пробежье. Паспорт уже стёрт от печатей.")
                : Pick(rng,
                    "Габения. Визовый режим — лаунчер.",
                    "Портовая зона Steam. Корабли стоят, иконки плывут."),
        };
    }

    private static string PickResidence(
        IdentityKind identity, double backlogRatio, int neverPlayed, Random rng)
    {
        if (neverPlayed >= 40)
            return Pick(rng,
                $"Склад дополнений, бокс №{neverPlayed}. Ключ потерян.",
                "Библиотека им. Габена. Стеллаж «ещё не тронуто».",
                "Район Вечных Распродаж. Квартира «В корзине».");

        return identity switch
        {
            IdentityKind.Shooter => Pick(rng,
                "Респаун, койка №3. Соседи тоже не спят.",
                "Гараж у CS-сервера. Масло меняют гильзами.",
                "Бункер у точки A. Вид на бомбу."),
            IdentityKind.Souls => Pick(rng,
                "Костёр у тумана. Место №1, очередь бесконечная.",
                "Общежитие при боссе. Заселение после смерти.",
                "Уступ над обрывом. Вид хороший. Пол — нет."),
            IdentityKind.Rpg => Pick(rng,
                "Таверна «Ещё один квест». Ночлег включён.",
                "ул. Квестовых Меток, д. 47. Домофон не отвечает.",
                "Библиотека непрочитанных диалогов. Тихо. Слишком."),
            IdentityKind.Horror => Pick(rng,
                "Дом с плохо закрытой дверью. Замок декоративный.",
                "Квартира без света в коридоре. Экономия."),
            IdentityKind.Moba => Pick(rng,
                "Скамья запасных у фонтана. Вид на тильт.",
                "Общага рейтинга, комн. «one more»."),
            _ when backlogRatio >= 0.2 => Pick(rng,
                "Библиотека им. Габена. Пыль — родная.",
                "Общежитие при лаунчере. Заселение без залога.",
                "ул. Списка желаний, д. 1. Лифт не работает."),
            _ => Pick(rng,
                "Центр игрового движения. Парковка занята.",
                "Дом у игрового костра. Тёплый, пока горит."),
        };
    }

    private static string PickTransmission(
        double shooterHours,
        double soulsHours,
        double rpgHours,
        double mobaHours,
        double hoursLast14Days,
        double totalHours)
    {
        if (mobaHours >= 1500 && mobaHours >= shooterHours * 0.7)
            return hoursLast14Days >= 15
                ? "Механика. Сцепление горит, но едет."
                : "Механика. Рейтинг в гараже, ключ на видном месте.";

        if (shooterHours >= rpgHours && shooterHours >= soulsHours && shooterHours >= 1000)
            return hoursLast14Days >= 20
                ? "Механика. Вечный рейтинг — коробка уже скрипит."
                : "Механика. Пристреляна. Пылится.";

        if (soulsHours >= 500 && soulsHours >= shooterHours * 0.4)
            return "Полуавтомат. Умер — загрузился — пошёл.";

        if (rpgHours >= 1000)
            return "Вариатор. 120 часов на «быстрый» квест.";

        if (hoursLast14Days <= 0 && totalHours > 100)
            return "Нейтраль. Парк на приколе.";

        if (shooterHours > 0 && rpgHours > 0)
            return "Гибрид. Шутер утром, RPG вечером. Или наоборот.";

        return "Автомат. Купил — установил — забыл.";
    }

    private static string PickBloodType(
        IdentityKind identity, int level, double backlogRatio, Random rng)
    {
        if (backlogRatio >= 0.35)
            return Pick(rng,
                "Молоко (парное). Не взбалтывать.",
                "0 (нулевой пробег). Почти новый.",
                "B+ (бэклог-положительный). Заразно.");

        return identity switch
        {
            IdentityKind.Shooter => Pick(rng,
                "Порох (I). Не пить. Ну почти.",
                "A+ (аимовый). Свежий.",
                "Кофе с адреналином. Без сахара — и так бьёт."),
            IdentityKind.Souls => Pick(rng,
                "Эстус (подогретый). Хватает на три глотка.",
                "RH− (после босса). Редкий.",
                "Чёрный чай. Три ночи подряд."),
            IdentityKind.Rpg => Pick(rng,
                "Чернила квестов. Не стирать.",
                "A (сюжетная). Медленно течёт.",
                "Травяной чай с лором. Горький."),
            IdentityKind.Horror => Pick(rng,
                "Холодный пот. Охлаждает мгновенно.",
                "0− (в темноте). Не светить.",
                "Валерьянка с энергетиком. Взрывной коктейль."),
            IdentityKind.Moba => Pick(rng,
                "MMR+. Летуч.",
                "Энергетик (командный). Делить с пати.",
                "Кофе «одна катка». Ложь в каждом глотке."),
            _ => level >= 50
                ? Pick(rng,
                    "A+ (ачивочный). Редкий штамп.",
                    "Уровень+. Концентрированный.",
                    "Кофе с энергетиком. Для значков.")
                : Pick(rng,
                    "Молоко (парное). Не взбалтывать.",
                    "Пиксель-отрицательный. Совместим со всеми.",
                    "АВ (любая скидка). Универсальный донор."),
        };
    }

    private static string BuildSpecialMarks(
        IdentityKind identity,
        SteamProfileSnapshot profile,
        int neverPlayed,
        double backlogRatio,
        double shooterHours,
        double soulsHours,
        double rpgHours,
        double mobaHours,
        double hoursLast14Days,
        IReadOnlyList<AchievementProgress> perfect,
        GameCategory? topCat,
        OwnedGameInfo? topGame,
        OwnedGameInfo? lifetimeTop,
        PronounSet pr,
        Random rng)
    {
        if (profile.VacBanned)
            return $"VAC-учёт ({profile.NumberOfVacBans}). Светится в темноте.";

        var topName = topGame is null ? null : Truncate(topGame.Name, 28);
        var topHours = (topGame?.PlaytimeForeverMinutes ?? 0) / 60.0;
        if (topName is not null && topHours >= 800)
            return Pick(rng,
                $"«{topName}» — вторая прописка ({topHours:0} ч).",
                $"Главная лошадка: «{topName}». Корм — часы.");

        if (lifetimeTop is not null
            && topGame is not null
            && lifetimeTop.AppId != topGame.AppId
            && !PlayActivity.IsRecentlyActive(lifetimeTop)
            && lifetimeTop.PlaytimeForeverMinutes >= 60 * 500)
        {
            var legend = Truncate(lifetimeTop.Name, 24);
            var legendWhen = PlayActivity.FormatLastPlayed(lifetimeTop) ?? "давно";
            return $"Была легенда «{legend}». Сейчас — {legendWhen}.";
        }

        return identity switch
        {
            IdentityKind.Shooter when shooterHours >= 2000 => Pick(rng,
                "Вскидывает прицел при скрипе двери.",
                "Слышит шаги через два этажа.",
                "Смотрит на людей как на пиксели."),
            IdentityKind.Shooter => Pick(rng,
                "Лёгкий прищур стрелка.",
                "Рука сама тянется к DPI."),
            IdentityKind.Souls when soulsHours >= 800 => Pick(rng,
                "Шрам от надписи YOU DIED.",
                $"Спокойное лицо: {pr.Which} уже {pr.Died} раз 40.",
                "Походка осторожная. Обрывы кусаются."),
            IdentityKind.Souls => "Опыт «ещё одна попытка». Не стирается.",
            IdentityKind.Rpg => Pick(rng,
                "Смотрит вдаль — там квестовая метка.",
                "В кармане журнал на 40 побочных."),
            IdentityKind.Horror => Pick(rng,
                "Проверяет шкаф. Не за вещами.",
                "Вздрагивает, когда динамик орёт."),
            IdentityKind.Moba when mobaHours >= 1000 => Pick(rng,
                "Говорит «gg» вместо «доброе утро».",
                "После «последней» обычно ещё одна."),
            IdentityKind.Moba => "Лёгкая зависимость от «принять».",
            IdentityKind.Backlog => Pick(rng,
                $"На полке {Math.Max(neverPlayed, 5)} игр без запуска.",
                "Глаза загораются на скидке 70%."),
            _ when hoursLast14Days >= 25 => "Синяки под глазами формата «ещё часик».",
            _ when neverPlayed >= 25 => $"{neverPlayed} игр ждут первого запуска.",
            _ when perfect.Count > 0 => $"100% в «{Truncate(perfect[0].GameName, 24)}». Блестит.",
            _ when topCat is not null => $"Категория {topCat.Code}. {topCat.TitleRu}.",
            _ => "Внешне спокоен. Для геймера подозрительно.",
        };
    }

    private static IReadOnlyList<string> BuildSpecialNotes(
        IdentityKind identity,
        int neverPlayed,
        double backlogRatio,
        double hoursLast14Days,
        IReadOnlyList<GameCategory> revoked,
        PenaltySummary penalties,
        OwnedGameInfo? topGame,
        Random rng)
    {
        var notes = new List<string>();

        switch (identity)
        {
            case IdentityKind.Shooter:
                notes.Add(Pick(rng,
                    "К шутерам — с упреждением 0,3 с.",
                    "Паркур без страховки запрещён."));
                break;
            case IdentityKind.Souls:
                notes.Add(Pick(rng,
                    "Перед боссом — глоток воды.",
                    "К обрывам — только с верёвкой."));
                break;
            case IdentityKind.Rpg:
                notes.Add("Побочные: не больше одного «быстрого» за вечер.");
                break;
            case IdentityKind.Horror:
                notes.Add("Наушники на ночь — с разрешения соседей.");
                break;
            case IdentityKind.Moba:
                notes.Add(Pick(rng,
                    "После поражения — 15 минут вне очереди.",
                    "Фраза «ещё одну» — только при свидетелях."));
                break;
        }

        if (neverPlayed >= 30)
        {
            notes.Add(Pick(rng,
                "К распродажам — только со взрослым.",
                $"Новые покупки: 0, пока склад ≥ {neverPlayed}."));
        }
        else if (backlogRatio >= 0.2)
        {
            notes.Add("Новая игра — только со свидетелем запуска.");
        }

        if (hoursLast14Days >= 30)
            notes.Add("За рулём — не больше 4 часов подряд.");
        else if (hoursLast14Days <= 0)
            notes.Add("Рекомендовано поиграть. В ближайшие 14 дней.");

        if (topGame is not null && topGame.PlaytimeForeverMinutes / 60.0 >= 1500)
            notes.Add($"«{Truncate(topGame.Name, 22)}» — на особом учёте.");

        foreach (var r in revoked.Take(1))
            notes.Add($"Кат. {r.Code}: допуск приостановлен.");

        if (penalties.TotalPoints >= 6)
            notes.Add("Порог лишения близко. Явка на комиссию.");

        if (notes.Count == 0)
            notes.Add("Особых ограничений нет. Пока.");

        return notes.Take(4).ToList();
    }

    private static IReadOnlyList<string> BuildMedicalNotes(
        IdentityKind identity,
        double backlogRatio,
        double hoursLast14Days,
        double shooterHours,
        double soulsHours,
        double mobaHours,
        bool vacBanned)
    {
        var notes = new List<string>();

        switch (identity)
        {
            case IdentityKind.Shooter:
                notes.Add(shooterHours >= 2000
                    ? "Прицел: гипертрофия."
                    : "Прицел: повышен.");
                notes.Add("Слух: шаги да, будильник нет.");
                break;
            case IdentityKind.Souls:
                notes.Add("Перекат: гипертрофия.");
                notes.Add("Сон: по расписанию боссов.");
                break;
            case IdentityKind.Rpg:
                notes.Add("Диалоги: внимание избыточное.");
                notes.Add("Суббота: риск «короткого» квеста.");
                break;
            case IdentityKind.Horror:
                notes.Add("Громкий звук: сверхнорма.");
                notes.Add("Сон: фрагменты и шкаф.");
                break;
            case IdentityKind.Moba:
                notes.Add(mobaHours >= 1500
                    ? "Очередь: клиническая тяга."
                    : "«Одна катка»: выраженная.");
                notes.Add("Сон: откладывается после поражения.");
                break;
            case IdentityKind.Backlog:
                notes.Add("Покупка: гиперактивна.");
                notes.Add("Запуск: почти спит.");
                break;
            default:
                notes.Add(backlogRatio >= 0.25
                    ? "Покупка: гиперактивна."
                    : "Покупка: в норме.");
                notes.Add(hoursLast14Days >= 25
                    ? "Игра: перевозбуждение."
                    : hoursLast14Days <= 1
                        ? "Игра: почти спит."
                        : "Игра: стабильна.");
                break;
        }

        if (soulsHours >= 1000 && identity != IdentityKind.Souls)
            notes.Add("Терпение к смертям: повышено.");

        if (shooterHours >= 2000 && identity != IdentityKind.Shooter)
            notes.Add("На шаги реагирует отлично.");

        notes.Add(vacBanned
            ? "К движению НЕ годен (VAC)."
            : backlogRatio >= 0.25 || hoursLast14Days >= 40 || mobaHours >= 2000
                ? "Годен с ограничениями."
                : "Годен к игровому движению.");

        return notes.Take(4).ToList();
    }

    private static string Pick(Random rng, params string[] options) =>
        options[rng.Next(options.Length)];

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";
}
