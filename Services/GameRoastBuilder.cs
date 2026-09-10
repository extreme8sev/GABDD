using SteamFun.Domain;

namespace SteamFun.Services;

public sealed record GameRoast(
    string Badge,
    string GameName,
    string Joke,
    string HoursText,
    OwnedGameInfo Game);

/// <summary>
/// «Посмотри на себя» — клички и шутки как карточки junk из Fallout Shelter.
/// Сначала кураторский словарь популярных игр, потом тематика по имени, потом generic.
/// </summary>
public static class GameRoastBuilder
{
    public static IReadOnlyList<GameRoast> Build(SteamProfileSnapshot profile, int take = 10)
    {
        var rng = new Random(StableSeed(profile.SteamId64) ^ unchecked((int)0xBEEF01));
        var games = profile.Games.ToList();
        if (games.Count == 0)
            return [];

        var picks = new List<OwnedGameInfo>();

        picks.AddRange(games
            .Where(g => g.PlaytimeForeverMinutes >= 60 * 50)
            .OrderByDescending(g => g.PlaytimeForeverMinutes)
            .Take(4));

        picks.AddRange(PlayActivity.RankByActivity(games, 4));

        picks.AddRange(games
            .Where(g => g.PlaytimeForeverMinutes <= 0)
            .OrderBy(_ => rng.Next())
            .Take(3));

        picks.AddRange(games
            .Where(g => g.PlaytimeForeverMinutes is > 0 and < 120)
            .OrderByDescending(g => g.PlaytimeForeverMinutes)
            .Take(3));

        var unique = picks
            .GroupBy(g => g.AppId)
            .Select(g => g.First())
            .Take(take)
            .ToList();

        if (unique.Count < take)
        {
            foreach (var g in games.OrderByDescending(x => x.PlaytimeForeverMinutes))
            {
                if (unique.Any(u => u.AppId == g.AppId))
                    continue;
                unique.Add(g);
                if (unique.Count >= take)
                    break;
            }
        }

        return unique.Select(g => RoastOne(g, rng)).ToList();
    }

    private static GameRoast RoastOne(OwnedGameInfo game, Random rng)
    {
        var minutes = game.PlaytimeForeverMinutes;
        var name = game.Name;
        var n = Normalize(name);
        var hoursText = FormatHours(minutes);

        if (GameRoastCatalog.TryRoast(game.AppId, n, minutes, rng, out var badge, out var joke))
            return new GameRoast(badge, name, joke, hoursText, game);

        if (TryThemed(n, minutes, rng, out badge, out joke))
            return new GameRoast(badge, name, joke, hoursText, game);

        if (minutes <= 0)
        {
            return new GameRoast(
                Pick(rng, "КУПИЛ, НО НЕ КАЧАЛ", "ИКОНКА-СИРОТА", "ЖДЁТ ПЕРВОГО ЗАПУСКА"),
                name,
                Pick(rng,
                    "Купить и не запустить — талант.",
                    "В библиотеке есть. В голове — нет.",
                    "Иконка красивая. На этом всё."),
                hoursText,
                game);
        }

        if (minutes < 60)
        {
            return new GameRoast(
                Pick(rng, "ТЕСТ-ДРАЙВ", "БРОСИЛ НА СТАРТЕ", "НЕ ЗАВЕЛАСЬ"),
                name,
                Pick(rng,
                    "Пара заездов — и на свалку ожидания.",
                    "Обучение не кончил. Мнение уже есть.",
                    "Увидел заставку. Хватит."),
                hoursText,
                game);
        }

        if (minutes < 180)
        {
            return new GameRoast(
                Pick(rng, "ЗАГЛЯНУЛ И ВЫШЕЛ", "НЕДО-ФАНАТ", "КОРОТКИЙ РОМАН"),
                name,
                Pick(rng,
                    "Почти начал. Почти полюбил. Почти забил.",
                    "Сюжет не разогнался — ты уже ушёл.",
                    "Время было. Желания не нашлось."),
                hoursText,
                game);
        }

        var hours = minutes / 60.0;
        if (hours >= 800)
        {
            return new GameRoast(
                Pick(rng, "ВТОРАЯ ПРОПИСКА", "ХРОНИЧЕСКИЙ", "ГЛАВНАЯ ЛОШАДКА"),
                name,
                Pick(rng,
                    $"{hours:0} ч — уже не хобби, а адрес.",
                    "За часы медаль бы дали. Висела бы на шее.",
                    $"{hours:0} ч. Спорить бесполезно."),
                hoursText,
                game);
        }

        if (hours >= 100)
        {
            return new GameRoast(
                Pick(rng, "СЕРЬЁЗНЫЕ ОТНОШЕНИЯ", "ПОСТОЯЛЕЦ", "СВОЙ ЧЕЛОВЕК"),
                name,
                Pick(rng,
                    $"{hours:0} ч вместе. Можно печать.",
                    "Не бросил. Не добил. Живёте вместе.",
                    "Знает эту игру лучше соседнего двора."),
                hoursText,
                game);
        }

        return new GameRoast(
            Pick(rng, "В ПРОЦЕССЕ", "ЕЩЁ НЕ ВСЁ", "НА ПОЛПУТИ"),
            name,
            Pick(rng,
                "Шанс доиграть есть. Или снова «когда-нибудь».",
                "Сюжет ждёт. Ты тоже. Никто не двигается.",
                "Нормальный стаж. Пока без ярлыка."),
            hoursText,
            game);
    }

    private static bool TryThemed(string n, int minutes, Random rng, out string badge, out string joke)
    {
        badge = "";
        joke = "";
        var h = minutes / 60.0;

        if (Contains(n, "elden ring"))
        {
            badge = minutes <= 0 ? "КУПИЛ, НО НЕ КАЧАЛ" : h >= 100 ? "ПОВЕЛИТЕЛЬ ФАЗ" : "МЕЖДУ КОСТРАМИ";
            joke = minutes <= 0
                ? "Междуземье куплено. Довакин не вызван."
                : h >= 100
                    ? Pick(rng, "Между мирами. Принцессу так и не нашёл.", "Умер столько раз — уже стиль.")
                    : "Костёр видел. Концовку — ещё нет.";
            return true;
        }

        if (Contains(n, "dark souls") || Contains(n, "sekiro") || Contains(n, "bloodborne") || Contains(n, "lies of p"))
        {
            badge = h >= 50 ? "ПАЦИЕНТ КОСТРА" : minutes < 60 ? "YOU DIED (БЫСТРО)" : "ЕЩЁ ОДНА ПОПЫТКА";
            joke = h >= 50
                ? "После 50 смертей всё ещё почти вежлив."
                : "YOU DIED уже знакома.";
            return true;
        }

        if (Contains(n, "resident evil") || Contains(n, "biohazard"))
        {
            badge = h >= 20 ? "БЕГЛЕЦ ИЗ РАККУН-СИТИ" : "НЕ ДОБЕЖАЛ ДО УБЕЖИЩА";
            joke = h >= 20
                ? Pick(rng, "Зомби родной. Ты всё ещё бежишь.", "8 слотов инвентаря — стиль жизни.")
                : "Вышел из участка. Нервы сохранил на потом.";
            return true;
        }

        if (Contains(n, "bioshock"))
        {
            badge = minutes < 90 ? "ЗАБЛУДШИЙ ВО ВРЕМЕНИ" : "ГОСТЬ ВОСТОРГА";
            joke = minutes < 90
                ? "Утонул в Восторге раньше сюжета."
                : "Would you kindly… ещё часок?";
            return true;
        }

        if (Contains(n, "tomb raider"))
        {
            badge = minutes < 120 ? "НЕДО-ИНДИАНА" : "ОХОТНИК ЗА АРТЕФАКТАМИ";
            joke = minutes < 120
                ? "Раскопала пару артефактов. Сдалась на третьем."
                : "Гробницы кончаются. Упорство — нет.";
            return true;
        }

        if (Contains(n, "counter-strike") || n is "cs2" || Contains(n, "cs:go"))
        {
            badge = minutes <= 0 ? "КУПИЛ, НО НЕ КАЧАЛ" : h >= 500 ? "ЖИТЕЛЬ DUST II" : "НА РАЗМИНКЕ";
            joke = minutes <= 0
                ? "Классика в библиотеке. Без запуска."
                : h >= 500
                    ? Pick(rng, "Карту нюхаешь дольше, чем живут в городе.", "«Ещё одну» громче будильника.")
                    : "Размялся. Вроде. Можно домой.";
            return true;
        }

        if (Contains(n, "dota"))
        {
            badge = h >= 500 ? "ЖИТЕЛЬ РЕЙТИНГА" : minutes <= 0 ? "ИКОНКА У ФОНТАНА" : "ОДНА КАТКА";
            joke = h >= 500
                ? "«GG» в лексиконе. Сон — нет."
                : "Принял катку. Потом ещё. Потом объяснял.";
            return true;
        }

        if (Contains(n, "witcher") || Contains(n, "cyberpunk"))
        {
            badge = h >= 80 ? "БЕЛЫЙ ВОЛК БИБЛИОТЕКИ" : "НЕ ДОШЁЛ ДО КОНТИНЕНТА";
            joke = h >= 80
                ? "Побочные длиннее планов на неделю."
                : "Геральт подождёт. Он привык.";
            return true;
        }

        if (Contains(n, "baldur") || Contains(n, "divinity"))
        {
            badge = h >= 60 ? "МАСТЕР КУБИКОВ" : "СОХРАНИЛСЯ У ТУТОРИАЛА";
            joke = h >= 60
                ? "Отряд собрал. Субботу потерял."
                : "Диалог важнее боя. Ушёл раньше обоих.";
            return true;
        }

        if (Contains(n, "half-life") || Contains(n, "portal"))
        {
            badge = minutes <= 0 ? "ЖДЁТ FREEMAN" : "НА ЧЁРНОМ МЕЗОНЕ";
            joke = minutes <= 0
                ? "Классика в библиотеке. Классика без запуска."
                : "Торт был ложью. Часы — нет.";
            return true;
        }

        if (Contains(n, "gta") || Contains(n, "grand theft"))
        {
            badge = h >= 100 ? "ГРАЖДАНИН ЛОС-САНТОСА" : "НЕ ДОЕХАЛ ДО МИССИИ";
            joke = h >= 100
                ? "Радио важнее основного квеста."
                : "Угнал тачку. Ключи бросил на парковке Steam.";
            return true;
        }

        if (Contains(n, "mafia"))
        {
            badge = h >= 15 ? "СДЕЛАЛ СЕМЬЮ" : "НЕ ПРИНЯЛИ В СЕМЬЮ";
            joke = h >= 15
                ? "Омерта соблюдена. Сон — нет."
                : "Постучал в дверь. Ушёл после пролога.";
            return true;
        }

        if (Contains(n, "minecraft") || Contains(n, "terraria") || Contains(n, "valheim"))
        {
            badge = h >= 50 ? "СТРОИТЕЛЬ БЕЗ ЧЕРТЕЖА" : "ПОЛОЖИЛ БЛОК И УШЁЛ";
            joke = h >= 50
                ? "База красивая. Смысл — ещё в разработке."
                : "Мир создал. Ответственность — нет.";
            return true;
        }

        return false;
    }

    private static string FormatHours(int minutes)
    {
        if (minutes <= 0) return "0 ч";
        if (minutes < 60) return $"{minutes} мин";
        var h = minutes / 60.0;
        return h >= 100 ? $"{h:0} ч" : $"{h:0.#} ч";
    }

    private static string Normalize(string name) =>
        name.Replace("™", "").Replace("®", "").Trim().ToLowerInvariant();

    private static bool Contains(string hay, string needle) =>
        hay.Contains(needle, StringComparison.Ordinal);

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

    private static string Pick(Random rng, params string[] options) =>
        options[rng.Next(options.Length)];
}
