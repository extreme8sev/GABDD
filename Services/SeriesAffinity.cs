using System.Text.RegularExpressions;
using SteamFun.Domain;

namespace SteamFun.Services;

/// <summary>Найденная серия в библиотеке (кураторская или по общему stem).</summary>
public sealed record SeriesHit(
    string Id,
    string DisplayName,
    int OwnedCount,
    double TotalHours,
    double ActivityScore,
    string? BestTitle,
    bool IsCurated);

/// <summary>
/// Распознаёт игровые серии по именам в библиотеке и даёт шутки для психопортрета.
/// Без Store franchise API: каталог + группировка по stem.
/// </summary>
public static partial class SeriesAffinity
{
    private const double MinHoursSingle = 8.0;

    private sealed record SeriesDef(
        string Id,
        string DisplayName,
        HashSet<int> AppIds,
        Func<string, bool> Matches,
        string[] Archetypes,
        string[] Traits,
        string[] Strengths,
        string[] Risks,
        string[] Compatibility,
        string[] SummaryBits,
        string[] VerdictBits);

    public static IReadOnlyList<SeriesHit> Detect(IReadOnlyList<OwnedGameInfo> games, int take = 3)
    {
        var playable = games
            .Where(g => !IsJunkTitle(Normalize(g.Name)))
            .ToList();

        var hits = new List<SeriesHit>();
        var claimed = new HashSet<int>();

        foreach (var def in Catalog)
        {
            var matched = playable
                .Where(g => def.AppIds.Contains(g.AppId) || def.Matches(Normalize(g.Name)))
                .ToList();
            if (matched.Count == 0)
                continue;

            var hit = ToHit(def.Id, def.DisplayName, matched, curated: true);
            if (!IsFan(hit))
                continue;

            hits.Add(hit);
            foreach (var g in matched)
                claimed.Add(g.AppId);
        }

        // Fallback: общий stem у 2+ игр, не попавших в каталог.
        var groups = playable
            .Where(g => !claimed.Contains(g.AppId))
            .Select(g => (Game: g, Stem: ExtractStem(Normalize(g.Name))))
            .Where(x => x.Stem is { Length: >= 4 })
            .GroupBy(x => x.Stem!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() >= 2)
            .OrderByDescending(g => LoyaltyScore(ToHit($"stem:{g.Key}", g.Key, g.Select(x => x.Game).ToList(), false)));

        foreach (var group in groups)
        {
            var matched = group.Select(x => x.Game).ToList();
            var display = ToTitleCase(group.Key);
            var hit = ToHit($"stem:{group.Key}", display, matched, curated: false);
            if (!IsFan(hit))
                continue;
            hits.Add(hit);
        }

        // Лояльность к серии (число частей + часы), а не только «играл на этой неделе».
        return hits
            .OrderByDescending(LoyaltyScore)
            .ThenByDescending(h => h.OwnedCount)
            .ThenByDescending(h => h.TotalHours)
            .Take(take)
            .ToList();
    }

    /// <summary>Насколько серия «про личность» человека, а не разовый заход.</summary>
    public static double LoyaltyScore(SeriesHit hit) =>
        hit.OwnedCount * 8_000.0
        + hit.TotalHours * 55.0
        + Math.Min(hit.ActivityScore, 8_000.0) * 0.35
        + (hit.IsCurated ? 2_500.0 : 0)
        + (hit.OwnedCount >= 3 ? 4_000.0 : 0)
        + (hit.OwnedCount >= 2 ? 3_000.0 : 0);

    public static string? PickArchetype(SeriesHit hit, PronounSet pr, Random rng)
    {
        var def = FindDef(hit.Id);
        if (def is null)
        {
            return hit.OwnedCount >= 2
                ? Pick(rng,
                    $"Коллекционер серии «{hit.DisplayName}»",
                    $"Фанат «{hit.DisplayName}» ×{hit.OwnedCount}")
                : null;
        }

        return Format(Pick(rng, def.Archetypes), hit, pr);
    }

    public static IEnumerable<string> Traits(SeriesHit hit, PronounSet pr, Random rng) =>
        FlavorLines(hit, pr, rng, curated: d => d.Traits, generic: h =>
        [
            $"«{h.DisplayName}» ×{h.OwnedCount}. Уже не случайность.",
            $"Серия «{h.DisplayName}»: {h.TotalHours:0} ч. Вторая прописка.",
        ]);

    public static IEnumerable<string> Strengths(SeriesHit hit, PronounSet pr, Random rng) =>
        FlavorLines(hit, pr, rng, curated: d => d.Strengths, generic: h =>
        [
            $"В «{h.DisplayName}» ориентируется лучше, чем в закладках.",
            h.BestTitle is null
                ? $"«{h.DisplayName}» — личная сага."
                : $"Лучшая глава — «{Short(h.BestTitle)}».",
        ]);

    public static IEnumerable<string> Risks(SeriesHit hit, PronounSet pr, Random rng) =>
        FlavorLines(hit, pr, rng, curated: d => d.Risks, generic: h =>
        [
            $"Риск купить следующую «{h.DisplayName}» «чтобы была».",
            $"Может объяснить лор «{h.DisplayName}» без запроса.",
        ]);

    public static string? CompatibilitySnippet(SeriesHit hit, PronounSet pr, Random rng)
    {
        var def = FindDef(hit.Id);
        if (def is null)
            return $"Вдвоём по «{hit.DisplayName}» — без спойлеров лора.";

        return Format(Pick(rng, def.Compatibility), hit, pr);
    }

    public static string? SummarySnippet(SeriesHit hit, PronounSet pr, Random rng)
    {
        var def = FindDef(hit.Id);
        if (def is null)
        {
            return hit.OwnedCount >= 2
                ? $"Серия «{hit.DisplayName}»: {hit.OwnedCount} части, {hit.TotalHours:0.#} ч."
                : null;
        }

        return Format(Pick(rng, def.SummaryBits), hit, pr);
    }

    public static string? VerdictSnippet(SeriesHit hit, PronounSet pr, Random rng)
    {
        var def = FindDef(hit.Id);
        if (def is null)
            return $"Печать: «{hit.DisplayName}» ×{hit.OwnedCount}.";

        return Format(Pick(rng, def.VerdictBits), hit, pr);
    }

    private static IEnumerable<string> FlavorLines(
        SeriesHit hit,
        PronounSet pr,
        Random rng,
        Func<SeriesDef, string[]> curated,
        Func<SeriesHit, string[]> generic)
    {
        var def = FindDef(hit.Id);
        var pool = def is null ? generic(hit) : curated(def);
        if (pool.Length == 0)
            yield break;

        var count = Math.Min(2, pool.Length);
        foreach (var line in Shuffle(rng, pool).Take(count))
            yield return Format(line, hit, pr);
    }

    private static SeriesHit ToHit(
        string id, string display, IReadOnlyList<OwnedGameInfo> matched, bool curated)
    {
        var hours = matched.Sum(g => g.PlaytimeForeverMinutes) / 60.0;
        var activity = matched.Sum(PlayActivity.Score);
        var best = matched
            .OrderByDescending(PlayActivity.Score)
            .ThenByDescending(g => g.PlaytimeForeverMinutes)
            .FirstOrDefault();

        return new SeriesHit(
            Id: id,
            DisplayName: display,
            OwnedCount: matched.Count,
            TotalHours: hours,
            ActivityScore: activity,
            BestTitle: best is null ? null : Short(best.Name),
            IsCurated: curated);
    }

    private static bool IsFan(SeriesHit hit) =>
        hit.OwnedCount >= 2
        || hit.TotalHours >= MinHoursSingle
        || hit.ActivityScore >= 200;

    private static SeriesDef? FindDef(string id) =>
        Catalog.FirstOrDefault(d => d.Id == id);

    private static string Format(string template, SeriesHit hit, PronounSet pr) =>
        template
            .Replace("{name}", hit.DisplayName, StringComparison.Ordinal)
            .Replace("{count}", hit.OwnedCount.ToString(), StringComparison.Ordinal)
            .Replace("{hours}", hit.TotalHours.ToString("0.#"), StringComparison.Ordinal)
            .Replace("{best}", hit.BestTitle ?? hit.DisplayName, StringComparison.Ordinal)
            .Replace("{nom}", pr.Nom, StringComparison.Ordinal)
            .Replace("{gen}", pr.Gen, StringComparison.Ordinal)
            .Replace("{dat}", pr.Dat, StringComparison.Ordinal)
            .Replace("{acc}", pr.Acc, StringComparison.Ordinal)
            .Replace("{which}", pr.Which, StringComparison.Ordinal)
            .Replace("{who}", pr.Who, StringComparison.Ordinal)
            .Replace("{polite}", pr.Polite, StringComparison.Ordinal);

    private static string Normalize(string name)
    {
        var s = name.Replace("™", "", StringComparison.Ordinal)
            .Replace("®", "", StringComparison.Ordinal)
            .Replace("–", "-", StringComparison.Ordinal)
            .Replace("—", "-", StringComparison.Ordinal)
            .Trim()
            .ToLowerInvariant();

        s = EditionJunkRegex().Replace(s, " ");
        s = MultiSpaceRegex().Replace(s, " ").Trim();
        return s;
    }

    private static bool IsJunkTitle(string normalized)
    {
        if (normalized.Length < 2)
            return true;

        return JunkRegex().IsMatch(normalized);
    }

    private static string? ExtractStem(string normalized)
    {
        if (IsJunkTitle(normalized))
            return null;

        var head = normalized.Split(':', 2)[0].Trim();
        head = TrailingNumberRegex().Replace(head, "").Trim();
        head = MultiSpaceRegex().Replace(head, " ").Trim();

        if (head.Length < 4)
            return null;

        // слишком общие слова — не серия
        if (head is "the" or "game" or "war" or "world" or "super" or "new")
            return null;

        return head;
    }

    private static string ToTitleCase(string stem)
    {
        var parts = stem.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            parts[i] = p.Length == 0 ? p : char.ToUpperInvariant(p[0]) + p[1..];
        }

        return string.Join(' ', parts);
    }

    private static string Short(string name)
    {
        name = name.Replace("™", "").Replace("®", "").Trim();
        if (name.Length <= 36) return name;
        return name[..33] + "…";
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

    private static bool StartsWithWord(string n, string word) =>
        n == word || n.StartsWith(word + " ", StringComparison.Ordinal)
                  || n.StartsWith(word + ":", StringComparison.Ordinal)
                  || n.StartsWith(word + "-", StringComparison.Ordinal);

    private static bool ContainsPhrase(string n, string phrase) =>
        n.Contains(phrase, StringComparison.Ordinal);

    private static HashSet<int> Ids(params int[] ids) => new(ids);

    // --- catalog ---

    private static readonly SeriesDef[] Catalog =
    [
        new(
            Id: "dota",
            DisplayName: "Dota 2",
            AppIds: Ids(570),
            Matches: n => StartsWithWord(n, "dota"),
            Archetypes:
            [
                "Житель рейтинга · Dota 2",
                "Тот, для кого «одна катка» — жанр",
                "Ветеран паба с хроническим «ещё одну»",
            ],
            Traits:
            [
                "Dota 2 на {hours} ч. Работа без зарплаты.",
                "«GG» быстрее, чем «доброе утро».",
                "Союзники в чате — отдельный босс.",
            ],
            Strengths:
            [
                "Рошана помнит лучше списка покупок.",
                "Умеет объяснить: поражение — «из-за пика».",
            ],
            Risks:
            [
                "Риск превратить вечер в серию «последних».",
                "После тильта — ещё одна «для настроения».",
            ],
            Compatibility:
            [
                "В пати — только без «report support» на 3-й минуте.",
            ],
            SummaryBits:
            [
                "Dota 2 — {hours} ч. Печать «каточник».",
            ],
            VerdictBits:
            [
                "Допуск к рейтингу: Dota 2 ({hours} ч).",
            ]),

        new(
            Id: "mafia",
            DisplayName: "Mafia",
            AppIds: Ids(
                1030840, // Mafia: Definitive Edition
                1030830, // Mafia II: Definitive Edition
                50130,   // Mafia II (Classic)
                360430,  // Mafia III: Definitive Edition
                1941540, // Mafia: The Old Country
                40990),  // Mafia (classic, если ещё в библиотеке)
            Matches: n =>
                StartsWithWord(n, "mafia")
                || ContainsPhrase(n, "mafia ii")
                || ContainsPhrase(n, "mafia iii")
                || ContainsPhrase(n, "mafia the old country"),
            Archetypes:
            [
                "Хранитель омерты · Mafia",
                "Солдато библиотеки Hangar 13",
                "Тот, для кого «семья» — Mafia I–III",
                "Летописец Empire Bay",
            ],
            Traits:
            [
                "Mafia ×{count}. Семья уже не DLC.",
                "Городские прогулки — миссия для семьи.",
                "«Ещё одна сигара» — план на вечер.",
                "«{best}» — запись в трудовой семьи.",
            ],
            Strengths:
            [
                "Empire Bay и New Bordeaux различает лучше районов города.",
                "Ремейк Mafia — уважение к первоисточнику. Знает.",
                "В криминальном экшене держит темп.",
            ],
            Risks:
            [
                "Риск романтизировать 1930-е сильнее сна.",
                "Может купить саундтрек раньше кампании.",
                "После титров — проверка, нет ли ещё части.",
            ],
            Compatibility:
            [
                "Вдвоём в Mafia — без спойлеров про семью.",
                "После катсцены: «ну ты видел этот поворот?»",
            ],
            SummaryBits:
            [
                "Mafia ×{count}, {hours} ч. Семейный подряд.",
                "Лояльность к Mafia ({count} изд., «{best}»).",
            ],
            VerdictBits:
            [
                "Допуск к семейным делам: Mafia ×{count}.",
                "Штамп «омерта». {hours} ч в Mafia.",
            ]),

        new(
            Id: "witcher",
            DisplayName: "The Witcher",
            AppIds: Ids(20900, 20920, 292030, 499450),
            Matches: n => StartsWithWord(n, "the witcher") || StartsWithWord(n, "witcher")
                           || ContainsPhrase(n, "witcher 3") || ContainsPhrase(n, "witcher iii"),
            Archetypes:
            [
                "Ведьмак с подпиской на побочные",
                "Хранитель Континента · Witcher",
            ],
            Traits:
            [
                "Witcher ×{count}. Гвинт всё ещё не отпущен.",
                "«{best}» — второй паспорт с гербом Вольюгии.",
            ],
            Strengths:
            [
                "Диалоги читает внимательнее уведомлений с работы.",
                "Знает: «быстрый» квест у CDPR — три вечера.",
            ],
            Risks:
            [
                "Риск NG+ вместо реальных дел.",
                "Любой RPG сравнит с Witcher 3. Не в пользу первого.",
            ],
            Compatibility:
            [
                "Вдвоём в Witcher — редкий зверь. Страдает лично.",
            ],
            SummaryBits:
            [
                "Witcher ×{count}, {hours} ч. Белый Волк кивает.",
            ],
            VerdictBits:
            [
                "Годен к Континенту. Witcher ({hours} ч).",
            ]),

        new(
            Id: "mass-effect",
            DisplayName: "Mass Effect",
            AppIds: Ids(17460, 24980, 1238020, 1328670),
            Matches: n => StartsWithWord(n, "mass effect"),
            Archetypes:
            [
                "Командор Шепард библиотеки",
                "Тот, кто снова выбирает цвет брони",
            ],
            Traits:
            [
                "Mass Effect ×{count}. Нормандия рядом.",
                "Диалоги для {gen} важнее урона.",
            ],
            Strengths:
            [
                "Отряд помнит лучше состава холодильника.",
                "Legendary Edition — повторная служба.",
            ],
            Risks:
            [
                "Риск третьего прохождения «на другую ветку».",
            ],
            Compatibility:
            [
                "Кооп в ME — споры «кого взять», не стрельба.",
            ],
            SummaryBits:
            [
                "Mass Effect ×{count}, {hours} ч. Жнецы подождут.",
            ],
            VerdictBits:
            [
                "Годен к Нормандии. Mass Effect — {hours} ч.",
            ]),

        new(
            Id: "elder-scrolls",
            DisplayName: "The Elder Scrolls",
            AppIds: Ids(22330, 22300, 72850, 489830, 306130, 1151340),
            Matches: n => StartsWithWord(n, "the elder scrolls")
                          || StartsWithWord(n, "elder scrolls")
                          || StartsWithWord(n, "skyrim")
                          || StartsWithWord(n, "oblivion")
                          || StartsWithWord(n, "morrowind"),
            Archetypes:
            [
                "Довакин с мод-менеджером",
                "Гражданин Тамриэля без регистрации",
            ],
            Traits:
            [
                "Elder Scrolls ×{count}. {hours} ч ссылки.",
                "«Ещё один мод» для {gen} = ещё пять часов.",
            ],
            Strengths:
            [
                "Теряется в стороне от основного квеста профессионально.",
            ],
            Risks:
            [
                "Риск вечера на порядок в модах вместо игры.",
            ],
            Compatibility:
            [
                "Вдвоём в Skyrim — без спойлеров про драконов.",
            ],
            SummaryBits:
            [
                "Тамриэль: Elder Scrolls ×{count}, {hours} ч.",
            ],
            VerdictBits:
            [
                "Годен к Скайриму. Elder Scrolls в деле.",
            ]),

        new(
            Id: "fallout",
            DisplayName: "Fallout",
            AppIds: Ids(377160, 22370, 22380, 235870, 1151340, 1716740),
            Matches: n => StartsWithWord(n, "fallout"),
            Archetypes:
            [
                "Житель убежища · Fallout",
                "Тот, кто проверяет радиацию из привычки",
            ],
            Traits:
            [
                "Fallout ×{count}. Пустошь как дача.",
                "VATS включается чаще будильника.",
            ],
            Strengths:
            [
                "Лут сортирует быстрее мыслей о сне.",
            ],
            Risks:
            [
                "Риск «быстрого захода» на 4 часа.",
            ],
            Compatibility:
            [
                "Совместная пустошь — ок, если не красть хлам.",
            ],
            SummaryBits:
            [
                "Fallout ×{count}, {hours} ч. Радиация в норме.",
            ],
            VerdictBits:
            [
                "Годен к пустоши. Fallout ({hours} ч).",
            ]),

        new(
            Id: "assassins-creed",
            DisplayName: "Assassin's Creed",
            AppIds: Ids(812140, 311560, 368500, 582160, 359550, 2208920),
            Matches: n => StartsWithWord(n, "assassin's creed")
                          || StartsWithWord(n, "assassins creed")
                          || StartsWithWord(n, "assassin’s creed"),
            Archetypes:
            [
                "Ассасин с абонементом на паркур",
                "Коллекционер синдикатов и эпох",
            ],
            Traits:
            [
                "AC ×{count}. История как DLC к паркуру.",
                "Синхронизация точки обзора — ритуал.",
            ],
            Strengths:
            [
                "Карту читает раньше сюжета.",
            ],
            Risks:
            [
                "Риск купить следующую эпоху «чисто посмотреть».",
            ],
            Compatibility:
            [
                "Кооп в AC — без споров про канон Анимуса.",
            ],
            SummaryBits:
            [
                "AC ×{count}, {hours} ч.",
            ],
            VerdictBits:
            [
                "Годен к прыжку веры. AC в деле.",
            ]),

        new(
            Id: "resident-evil",
            DisplayName: "Resident Evil",
            AppIds: Ids(883710, 952060, 1196590, 2050650, 21690, 304240),
            Matches: n => StartsWithWord(n, "resident evil")
                          || StartsWithWord(n, "biohazard"),
            Archetypes:
            [
                "Спец по вирусам и узким коридорам",
                "Тот, кто экономит патроны даже в меню",
            ],
            Traits:
            [
                "RE ×{count}. 8 слотов — стиль жизни.",
                "«{best}» орала громче соседей.",
            ],
            Strengths:
            [
                "Спокойно открывает дверь, за которой орёт.",
            ],
            Risks:
            [
                "Риск ночных забегов с наушниками на максимуме.",
            ],
            Compatibility:
            [
                "Вдвоём в RE — договорённость «кто орёт первым».",
            ],
            SummaryBits:
            [
                "Resident Evil ×{count}, {hours} ч.",
            ],
            VerdictBits:
            [
                "Годен к Umbrella. RE — {hours} ч.",
            ]),

        new(
            Id: "gta",
            DisplayName: "Grand Theft Auto",
            AppIds: Ids(271590, 12210, 12120, 12110, 12100, 12200),
            Matches: n => StartsWithWord(n, "grand theft auto")
                          || StartsWithWord(n, "gta")
                          || n is "gta5" or "gta 5" or "gta v"
                          || ContainsPhrase(n, "gta v")
                          || ContainsPhrase(n, "gta iv")
                          || ContainsPhrase(n, "gta online"),
            Archetypes:
            [
                "Гражданин Лос-Сантоса · GTA",
                "Тот, для кого «ещё одна миссия» — позиция",
            ],
            Traits:
            [
                "GTA ×{count}, {hours} ч хаоса.",
                "Радио для {gen} важнее основного квеста.",
            ],
            Strengths:
            [
                "Карту города знает лучше навигатору.",
            ],
            Risks:
            [
                "Риск «пяти минуток» в Online до рассвета.",
            ],
            Compatibility:
            [
                "Кооп в GTA — если микрофон не кричит про хелик.",
            ],
            SummaryBits:
            [
                "GTA ×{count}, {hours} ч.",
            ],
            VerdictBits:
            [
                "Годен к угону и сюжету. GTA отмечена.",
            ]),

        new(
            Id: "fromsoft",
            DisplayName: "FromSoftware",
            AppIds: Ids(570940, 374320, 2369390, 1245620, 814380, 1888930),
            Matches: n => StartsWithWord(n, "dark souls")
                          || StartsWithWord(n, "elden ring")
                          || StartsWithWord(n, "bloodborne")
                          || StartsWithWord(n, "sekiro")
                          || StartsWithWord(n, "demon's souls")
                          || StartsWithWord(n, "demons souls")
                          || StartsWithWord(n, "armored core"),
            Archetypes:
            [
                "Паломник FromSoftware",
                "Тот, кто говорит «ещё одна попытка» всерьёз",
            ],
            Traits:
            [
                "FromSoftware ×{count}. YOU DIED как приветствие.",
                "«{best}» — главный экзаменатор терпения.",
            ],
            Strengths:
            [
                "После 50 смертей всё ещё почти {polite}.",
            ],
            Risks:
            [
                "Риск объяснить билд вместо сна.",
            ],
            Compatibility:
            [
                "Кооп в душах — редкий праздник. Обычно соло.",
            ],
            SummaryBits:
            [
                "FromSoftware ×{count}, {hours} ч.",
            ],
            VerdictBits:
            [
                "Годен к костру. FromSoftware в трудовой.",
            ]),

        new(
            Id: "baldurs-gate",
            DisplayName: "Baldur's Gate",
            AppIds: Ids(1086940, 228280, 2619610),
            Matches: n => StartsWithWord(n, "baldur's gate")
                          || StartsWithWord(n, "baldurs gate")
                          || StartsWithWord(n, "baldur’s gate"),
            Archetypes:
            [
                "Мастер кубиков · Baldur's Gate",
                "Тот, кто сохраняется перед каждым диалогом",
            ],
            Traits:
            [
                "Baldur's Gate ×{count}, {hours} ч последствий.",
                "Отряд собирает дольше обеда.",
            ],
            Strengths:
            [
                "Проваливает проверку харизмы — и отыгрывает красиво.",
            ],
            Risks:
            [
                "Риск третьего прохождения «за злую сторону».",
            ],
            Compatibility:
            [
                "Мультиплеер BG3 — если все ждут у сундука.",
            ],
            SummaryBits:
            [
                "Baldur's Gate ×{count}, {hours} ч.",
            ],
            VerdictBits:
            [
                "Годен к d20. Baldur's Gate в деле.",
            ]),

        new(
            Id: "cyberpunk",
            DisplayName: "Cyberpunk",
            AppIds: Ids(1091500),
            Matches: n => StartsWithWord(n, "cyberpunk"),
            Archetypes:
            [
                "Гость Найт-Сити · Cyberpunk",
                "Тот, кто апгрейдит билду чаще причёску",
            ],
            Traits:
            [
                "Cyberpunk: {hours} ч в неоне.",
                "«{best}» — вторая прописка в Найт-Сити.",
            ],
            Strengths:
            [
                "Стиль для {gen} — часть геймплея, не косметика.",
            ],
            Risks:
            [
                "Риск «быстрого» проезда по району на час.",
            ],
            Compatibility:
            [
                "Вдвоём катсцены — ок. Спойлерить финал — нет.",
            ],
            SummaryBits:
            [
                "Cyberpunk, {hours} ч.",
            ],
            VerdictBits:
            [
                "Годен к Найт-Сити. Cyberpunk отмечен.",
            ]),

        new(
            Id: "dragon-age",
            DisplayName: "Dragon Age",
            AppIds: Ids(47810, 1238020, 1237970, 1845910),
            Matches: n => StartsWithWord(n, "dragon age"),
            Archetypes:
            [
                "Серый Страж с архивом романов",
            ],
            Traits:
            [
                "Dragon Age ×{count}. Выборы важнее билдов.",
            ],
            Strengths:
            [
                "Помнит, кого обидел в Origins, лучше паролей.",
            ],
            Risks:
            [
                "Риск читать вики лора вместо сна.",
            ],
            Compatibility:
            [
                "Обсуждение DA — да. Спойлеры про архонта — нет.",
            ],
            SummaryBits:
            [
                "Dragon Age ×{count}, {hours} ч.",
            ],
            VerdictBits:
            [
                "Годен к Серым Стражам. Dragon Age в печати.",
            ]),
    ];

    [GeneratedRegex(
        @"\b(soundtrack|artbook|art book|ost|dlc|pack|outfit|cosmetic|bonus content|digital extras|wallpapers?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JunkRegex();

    [GeneratedRegex(
        @"\b(definitive edition|game of the year( edition)?|goty|complete edition|gold edition|deluxe edition|ultimate edition|remastered|hd remaster|director'?s cut|enhanced edition|legendary edition)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EditionJunkRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpaceRegex();

    [GeneratedRegex(
        @"\s+(ii|iii|iv|v|vi|vii|viii|ix|x|\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TrailingNumberRegex();
}
