using SteamFun.Domain;

namespace SteamFun.Services;

/// <summary>
/// Шуточный психопортрет в тоне Fallout Shelter: короткие deadpan-карточки, без канцелярита.
/// </summary>
public static class PersonalityBuilder
{
    public static PersonalityPortrait Build(
        SteamProfileSnapshot profile,
        IReadOnlyList<GameCategory> categories,
        int fleetSize,
        int neverPlayed,
        double totalHours,
        double hoursLast14Days,
        PronounSet pr)
    {
        var backlogRatio = fleetSize == 0 ? 0 : (double)neverPlayed / fleetSize;
        var rng = new Random(StableSeed(profile.SteamId64) ^ unchecked((int)0xBADC0FFE));

        var shooter = PlayActivity.CategoryHoursForIdentity(profile.Games, CategoryCode.B);
        var rpg = PlayActivity.CategoryHoursForIdentity(profile.Games, CategoryCode.C);
        var souls = PlayActivity.CategoryHoursForIdentity(profile.Games, CategoryCode.E);
        var horror = PlayActivity.CategoryHoursForIdentity(profile.Games, CategoryCode.H);
        var sandbox = PlayActivity.CategoryHoursForIdentity(profile.Games, CategoryCode.X);
        var strategy = PlayActivity.CategoryHoursForIdentity(profile.Games, CategoryCode.D);
        var survival = PlayActivity.CategoryHoursForIdentity(profile.Games, CategoryCode.S);
        var racing = PlayActivity.CategoryHoursForIdentity(profile.Games, CategoryCode.R);
        var sim = PlayActivity.CategoryHoursForIdentity(profile.Games, CategoryCode.I);
        var casual = PlayActivity.CategoryHoursForIdentity(profile.Games, CategoryCode.M);
        var fighting = PlayActivity.CategoryHoursForIdentity(profile.Games, CategoryCode.F);
        var moba = PlayActivity.CategoryHoursForIdentity(profile.Games, CategoryCode.A);

        var topGames = PlayActivity.RankByActivity(profile.Games, 5);
        if (topGames.Count == 0)
        {
            topGames = profile.Games
                .OrderByDescending(g => g.PlaytimeForeverMinutes)
                .Take(5)
                .ToList();
        }

        var topGame = topGames.FirstOrDefault();
        var topName = ShortName(topGame?.Name ?? "неизвестная игра");
        var topHours = (topGame?.PlaytimeForeverMinutes ?? 0) / 60.0;

        var perfect = profile.Top10Achievements
            .Where(a => !a.StatsPrivate && a.Total > 0 && a.Unlocked == a.Total)
            .ToList();
        var worstAch = profile.Top10Achievements
            .Where(a => !a.StatsPrivate && a.Total > 0)
            .OrderBy(a => (double)a.Unlocked / a.Total)
            .FirstOrDefault();

        var seriesHits = SeriesAffinity.Detect(profile.Games, take: 3);
        var primarySeries = seriesHits.FirstOrDefault();

        var typeLabel = BuildTypeLabel(
            shooter, rpg, souls, horror, moba, backlogRatio, hoursLast14Days, rng);
        var archetype = PickArchetype(
            typeLabel, shooter, rpg, souls, horror, sandbox, strategy, moba, backlogRatio, hoursLast14Days,
            topName, primarySeries, pr, rng);

        var indictment = IndictmentBuilder.Build(
            profile, fleetSize, neverPlayed, totalHours, rng);

        var seriesTag = primarySeries is null
            ? ""
            : $" · серия {primarySeries.DisplayName}×{primarySeries.OwnedCount}";
        var headline =
            $"{profile.PersonaName} · «{archetype}»{seriesTag} · Steam LVL {profile.SteamLevel}";

        var summary = indictment.Headline;

        var traits = BuildTraits(
            profile, topGames, shooter, rpg, souls, horror, sandbox, survival, racing, sim, casual,
            fighting, moba, backlogRatio, neverPlayed, hoursLast14Days, perfect, worstAch,
            seriesHits, pr, rng);

        var strengths = BuildStrengths(
            topName, topHours, shooter, rpg, souls, strategy, sandbox, perfect, profile.SteamLevel,
            totalHours, hoursLast14Days, primarySeries, pr, rng);

        var risks = BuildRisks(
            topName, backlogRatio, neverPlayed, hoursLast14Days, shooter, souls, horror, rpg,
            fleetSize, primarySeries, pr, rng);

        var compatibility = BuildCompatibility(
            topName, shooter, rpg, souls, backlogRatio, hoursLast14Days, neverPlayed,
            primarySeries, pr, rng);

        var verdict = BuildVerdict(
            profile.PersonaName, typeLabel, archetype, backlogRatio, hoursLast14Days,
            profile.VacBanned, neverPlayed, topName, primarySeries, pr, rng);

        return new PersonalityPortrait(
            Archetype: typeLabel,
            Headline: headline,
            Summary: summary,
            Charges: indictment.Charges,
            Traits: traits,
            Strengths: strengths,
            Risks: risks,
            Compatibility: compatibility,
            Verdict: verdict);
    }

    private static string BuildTypeLabel(
        double shooter, double rpg, double souls, double horror, double moba,
        double backlogRatio, double recent, Random rng)
    {
        var pool = new List<string>();

        if (moba >= 2000)
            pool.AddRange(["Клинический каточник", "Пациент рейтинга", "Жертва «ещё одну»"]);
        else if (moba >= 500)
            pool.AddRange(["Каточник со стажем", "Человек привычки к пати"]);

        if (shooter >= 2500)
            pool.AddRange(["Человек-прицел", "Охотник за шагами", "Пациент DPI"]);
        else if (shooter >= 800)
            pool.Add("Стрелок по совместительству");

        if (souls >= 1000)
            pool.AddRange(["Пациент костра", "Мастер попытки №83"]);
        else if (souls >= 400)
            pool.Add("Знаком с YOU DIED");

        if (rpg >= 1500)
            pool.AddRange(["Носитель непрочитанных квестов", "Летописец побочных"]);
        else if (rpg >= 500)
            pool.Add("Сюжетник со стажем");

        if (horror >= 400)
            pool.AddRange(["Коллекционер холодного пота", "Любитель внезапного ора"]);

        if (backlogRatio >= 0.35)
            pool.AddRange(["Директор склада Steam", "Хранитель нераспечатанного"]);
        else if (backlogRatio >= 0.2)
            pool.Add("Коллекционер потенциала");

        if (recent >= 30)
            pool.AddRange(["Бессонница с лицензией", "Ночная смена в лаунчере"]);
        else if (recent <= 1)
            pool.AddRange(["Спящий владелец библиотеки", "Призрак вкладки «Игры»"]);

        if (pool.Count == 0)
            pool.AddRange([
                "Обычный нарушитель спокойствия",
                "Гражданин Габении",
                "Игрок средней тяжести",
            ]);

        return Pick(rng, pool.ToArray());
    }

    private static string PickArchetype(
        string code, double shooter, double rpg, double souls, double horror, double sandbox,
        double strategy, double moba, double backlogRatio, double recent, string topName,
        SeriesHit? series, PronounSet pr, Random rng)
    {
        if (series is not null && series.OwnedCount >= 2)
        {
            var seriesArchetype = SeriesAffinity.PickArchetype(series, pr, rng);
            if (seriesArchetype is not null)
                return seriesArchetype;
        }

        if (series is not null && series.TotalHours >= 15)
        {
            var seriesArchetype = SeriesAffinity.PickArchetype(series, pr, rng);
            if (seriesArchetype is not null && rng.NextDouble() < 0.85)
                return seriesArchetype;
        }

        var pool = new List<string>();

        if (series is not null)
        {
            var s = SeriesAffinity.PickArchetype(series, pr, rng);
            if (s is not null)
                pool.Add(s);
        }

        if (moba >= 2000)
            pool.AddRange([
                "Житель рейтинга",
                "Тот, для кого «GG» — приветствие и прощание",
                $"Главный по «ещё одну» в «{topName}»",
            ]);
        else if (moba >= 400)
            pool.AddRange(["Каточник с трудовым стажем", "Знает цену плохого пика"]);

        if (shooter >= 4000 && souls >= 1000)
            pool.AddRange([
                "Ранговый с соулс-стажем",
                $"Прицел, {pr.Which} не боится YOU DIED",
            ]);
        if (shooter >= 2500)
            pool.AddRange([
                $"Тот, {pr.Which} вечно крутит DPI",
                "Ночной охотник за шагами",
                $"{pr.Who} слышит перезарядку через стену",
            ]);
        if (souls >= 1000)
            pool.AddRange([
                $"{pr.Who} умирает 80 раз и лезет снова",
                "Спокойствие на попытке №83",
                "Гибнет от обрыва чаще, чем от босса",
            ]);
        if (rpg >= 1500)
            pool.AddRange([
                "Носитель 47 квестовых меток",
                $"{pr.Who} читает все диалоги",
                $"Летописец «{topName}»",
            ]);
        if (horror >= 400)
            pool.AddRange(["Любитель внезапного ора динамика", "Коллекционер холодного пота"]);
        if (sandbox >= 1500)
            pool.AddRange(["Строитель империй без приглашения", "Инженер красивых бессмысленных баз"]);
        if (strategy >= 600)
            pool.AddRange(["Полководец на паузе", "Стратег с табличками в голове"]);
        if (backlogRatio >= 0.3)
            pool.AddRange([
                "Куратор музея нераспечатанного",
                "Посол Списка желаний",
                "Смотритель склада «потом»",
            ]);
        if (recent <= 1)
            pool.AddRange(["Спящий владелец лаунчера", "Призрак вкладки «Библиотека»"]);
        if (recent >= 30)
            pool.AddRange(["Человек без режима дня", "Пилот без кнопки выключения"]);

        if (pool.Count == 0)
            pool.AddRange([
                "Универсальный нарушитель спокойствия",
                "Гражданин Габении без ярлыка",
                $"Фанат «{topName}» без ярлыка",
            ]);

        var idx = (code.GetHashCode() & 0x7FFFFFFF) % pool.Count;
        return pool[idx];
    }

    private static IReadOnlyList<string> BuildTraits(
        SteamProfileSnapshot profile,
        IReadOnlyList<OwnedGameInfo> topGames,
        double shooter, double rpg, double souls, double horror, double sandbox, double survival,
        double racing, double sim, double casual, double fighting, double moba,
        double backlogRatio, int neverPlayed, double recent,
        IReadOnlyList<AchievementProgress> perfect,
        AchievementProgress? worstAch,
        IReadOnlyList<SeriesHit> seriesHits,
        PronounSet pr,
        Random rng)
    {
        var pool = new List<string>();
        var pinned = new List<string>();

        foreach (var hit in seriesHits.Take(2))
            pinned.AddRange(SeriesAffinity.Traits(hit, pr, rng).Take(1));

        foreach (var g in topGames.Take(3))
        {
            var h = g.PlaytimeForeverMinutes / 60.0;
            var n = ShortName(g.Name);
            if (h >= 800)
                pool.Add(Pick(rng,
                    $"«{n}» — вторая прописка ({h:0} ч).",
                    $"Кнопки в «{n}» находит вслепую.",
                    $"«{n}» для {pr.Gen} не игра, а адрес."));
            else if (h >= 200)
                pool.Add($"Серьёзно встречается с «{n}» ({h:0.#} ч).");
        }

        if (shooter >= 2000)
            pool.AddRange(Shuffle(rng, [
                "Слышит шаги лучше собственного имени.",
                "14 видов шагов. 0 видов здорового сна.",
                "Считает «gg» полноценным письмом.",
                $"Мышь у {pr.Gen} старше некоторых браков.",
                "DPI крутит чаще, чем признаёт поражения.",
            ]));

        if (souls >= 800)
            pool.AddRange(Shuffle(rng, [
                "Боссу сквозь зубы: «спасибо за урок».",
                "«Ещё одна попытка» — план на вечер.",
                "Гибнет от обрыва чаще, чем от удара.",
                $"Точка сохранения для {pr.Gen} — почти терапевт.",
            ]));

        if (rpg >= 1200)
            pool.AddRange(Shuffle(rng, [
                "Катсцены пропускает только если горит ужин.",
                "Сюжетные ветки помнит лучше планов на неделю.",
                "Жизнь на паузу ради «быстрого» побочного.",
            ]));

        if (horror >= 300)
            pool.AddRange(Shuffle(rng, [
                "Громкость наушников — аттракцион для соседей.",
                "Проверяет шкаф. Не за вещами.",
            ]));

        if (sandbox >= 800)
            pool.Add("Строит идеальную базу. Забывает зачем.");
        if (survival >= 300)
            pool.Add("Копит палки, камни и ложную безопасность.");
        if (racing >= 150)
            pool.Add("Во дворе тоже мечтает об откате после ДТП.");
        if (sim >= 100)
            pool.Add("Моет виртуальные машины старательнее кружки.");
        if (casual >= 50)
            pool.Add("Притворяется казуалом. Никто не верит.");
        if (fighting >= 20)
            pool.Add("Тайминги ударов наизусть. Это уже ярлык.");
        if (moba >= 2000)
            pool.AddRange(Shuffle(rng, [
                $"МОБА на {moba:0} ч — вторая прописка.",
                "«Одна катка» звучит как «на пять минут».",
                "Союзники в чате — отдельный вид спорта.",
            ]));
        else if (moba >= 300)
            pool.AddRange(Shuffle(rng, [
                $"В МОБА {moba:0} ч. Понятно.",
                "«Решающая» катка редко последняя.",
            ]));
        else if (moba >= 30)
            pool.Add($"МОБА на {moba:0.#} ч: заглянул и сделал выводы.");
        else if (moba > 0)
            pool.Add("В МОБА заглянул коротко. И вовремя вышел.");

        if (backlogRatio >= 0.25)
            pool.AddRange(Shuffle(rng, [
                $"Покупает быстрее, чем запускает ({neverPlayed} в хвосте).",
                "Список желаний длиннее списка дел.",
                "Непройденные игры — форма инвестиций.",
                "Библиотека как альбом наклеек.",
            ]));

        if (recent >= 30)
            pool.Add(Pick(rng,
                $"За 14 дней {recent:0} ч — вторая смена.",
                "Режим сна подал в отставку."));
        if (recent <= 0)
            pool.Add("Лаунчер открывает чаще игр. Эстетика.");

        if (perfect.Count > 0)
        {
            var p = perfect[rng.Next(perfect.Count)];
            pool.Add($"100% в «{ShortName(p.GameName)}». Блестит.");
        }

        if (worstAch is not null && worstAch.Total >= 20)
        {
            var pct = 100.0 * worstAch.Unlocked / worstAch.Total;
            if (pct < 40)
                pool.Add($"В «{ShortName(worstAch.GameName)}» ачивки {pct:0}%. Человек, не робот.");
        }

        if (profile.SteamLevel >= 70)
            pool.Add($"Steam LVL {profile.SteamLevel}. Значки кивают.");

        pool.AddRange(Shuffle(rng, [
            $"{Cap(pr.Able)} объяснить, почему «ещё час» длится три.",
            "Сложные отношения с кнопкой закрытия.",
            "Верит в магию «последней катки».",
            "«Выйду после раунда» — остаётся до титров.",
        ]));

        return DistinctTake(pool, 5, rng, pinned);
    }

    private static IReadOnlyList<string> BuildStrengths(
        string topName, double topHours, double shooter, double rpg, double souls, double strategy,
        double sandbox, IReadOnlyList<AchievementProgress> perfect, int level, double totalHours,
        double recent, SeriesHit? series, PronounSet pr, Random rng)
    {
        var pool = new List<string>();
        var pinned = new List<string>();

        if (series is not null)
            pinned.AddRange(SeriesAffinity.Strengths(series, pr, rng).Take(1));

        pool.Add(Pick(rng,
            $"Эксперт по «{topName}». {topHours:0} ч — разогрев.",
            $"Экскурсия по меню «{topName}» — с закрытыми глазами."));

        if (totalHours >= 10000)
            pool.Add(Pick(rng,
                $"Пробег {totalHours:0} ч. Правила наизусть.",
                $"Новички принимают {pr.Acc} за обучалку."));
        if (shooter >= 2000)
            pool.AddRange(Shuffle(rng, [
                "Отдачу контролирует лучше эмоций в чате.",
                "Мини-карту читает как утреннюю газету.",
                "Один против всех — родная стихия.",
            ]));
        if (souls >= 800)
            pool.AddRange(Shuffle(rng, [
                "После десятой смерти не бесится. Или прячет.",
                $"После 50 смертей всё ещё почти {pr.Polite}.",
            ]));
        if (rpg >= 1200)
            pool.Add("Квест, лор, крафт и «куда шёл» — одновременно.");
        if (strategy >= 500)
            pool.Add("Видит систему там, где другие «просто играют».");
        if (sandbox >= 800)
            pool.Add("Креатив уровня «зачем, но красиво».");
        if (perfect.Count > 0)
            pool.Add($"Дожимает: {perfect.Count} игр с полным набором ачивок.");
        if (level >= 50)
            pool.Add($"Steam LVL {level}. Смотрит на чужие значки сверху.");
        if (recent >= 15)
            pool.Add($"Форма горячая. Лучше не попадаться {pr.Dat}.");

        pool.AddRange(Shuffle(rng, [
            "Гуглит сборки быстрее, чем признаёт поражение.",
            "Есть инструкция «как не сломаться». Черновик.",
        ]));

        return DistinctTake(pool, 4, rng, pinned);
    }

    private static IReadOnlyList<string> BuildRisks(
        string topName, double backlogRatio, int neverPlayed, double recent,
        double shooter, double souls, double horror, double rpg, int fleet,
        SeriesHit? series, PronounSet pr, Random rng)
    {
        var pool = new List<string>();
        var pinned = new List<string>();

        if (series is not null)
            pinned.AddRange(SeriesAffinity.Risks(series, pr, rng).Take(1));

        if (backlogRatio >= 0.2)
            pool.AddRange(Shuffle(rng, [
                $"Склад из {neverPlayed} непройденных может обрушиться.",
                "Распродажа включает кошелёк быстрее здравого смысла.",
                $"При {fleet} играх библиотека похожа на склад.",
            ]));

        if (recent >= 30)
            pool.AddRange(Shuffle(rng, [
                "Сон — необязательное задание.",
                "Риск перепутать «сейчас выйду» с «уже утро».",
                $"Передозировка «{topName}» без рецепта.",
            ]));
        else if (recent <= 0)
            pool.Add("Риск стать коллекционером пыли.");

        if (shooter >= 2500)
            pool.AddRange(Shuffle(rng, [
                "Зависимость от рейтинга и чужих кроссовок.",
                "Вспышки «это союзники виноваты».",
                "Мышь и нервы изнашиваются синхронно.",
            ]));
        if (souls >= 800)
            pool.Add("Давление скачет на боссах. Добровольно.");
        if (horror >= 400)
            pool.Add("Ночью глядит в тёмный угол. На всякий.");
        if (rpg >= 1500)
            pool.Add("Риск потерять субботу в «коротком» побочном.");

        pool.Add(Pick(rng,
            "Может оправдать покупку DLC философски.",
            "Путает отдых с подбором идеальной сборки."));

        return DistinctTake(pool, 4, rng, pinned);
    }

    private static string BuildCompatibility(
        string topName, double shooter, double rpg, double souls,
        double backlogRatio, double recent, int neverPlayed,
        SeriesHit? series, PronounSet pr, Random rng)
    {
        var duo = series is not null
            ? SeriesAffinity.CompatibilitySnippet(series, pr, rng)!
            : shooter >= 1500
            ? Pick(rng,
                $"Вдвоём {pr.Valuable}, если микрофон не орёт.",
                "Напарник для «ещё одну». Предупредите соседей.")
            : souls >= 800
                ? "Вдвоём редкий гость. Страдать любит лично."
                : "Для сюжета вдвоём — находка. Без спойлеров.";

        var solo = Pick(rng,
            $"В одиночку включает «{topName}» и пропадает.",
            "В одиночку играет отлично. Иногда слишком.");

        var sales = backlogRatio >= 0.25
            ? Pick(rng,
                $"С распродажами {pr.Incompatible} без сопровождающего ({neverPlayed} на складе).",
                "Список желаний + 70% = ЧС для кошелька.")
            : "К распродажам допущен. Глаза всё равно следят.";

        var time = recent >= 25
            ? $"Лучшее время: «уже поздно» для всех, кроме {pr.Gen}."
            : "Лучшее время: спокойный вечер. Без рейдов до 5 утра.";

        return $"{duo} {solo} {sales} {time}";
    }

    private static string BuildVerdict(
        string name, string code, string archetype, double backlogRatio, double recent,
        bool vac, int neverPlayed, string topName, SeriesHit? series, PronounSet pr, Random rng)
    {
        if (vac)
            return $"НЕ ГОДЕН. {name} на VAC-учёте. Сначала мировая, потом лаунчер.";

        var seriesStamp = series is null ? null : SeriesAffinity.VerdictSnippet(series, pr, rng);

        if (backlogRatio >= 0.3 && recent >= 25)
        {
            var core = Pick(rng,
                $"ГОДЕН С УСЛОВИЯМИ. «{archetype}». Играет как {pr.Cursed}, покупает как {pr.Obsessed}. " +
                $"1 запуск со склада на каждые 3 ч в «{topName}».",
                "ГОДЕН. Высокопроизводительный хаос. Карту от распродаж — на хранение.");
            return seriesStamp is null ? core : $"{core} {seriesStamp}";
        }

        if (backlogRatio >= 0.35)
        {
            var core = $"ГОДЕН УСЛОВНО. «{archetype}». {neverPlayed} игр ждут своего часа.";
            return seriesStamp is null ? core : $"{core} {seriesStamp}";
        }

        if (recent >= 30)
        {
            var core = Pick(rng,
                $"ГОДЕН. Следить за сном. «{topName}» — не оправдание перед рассветом.",
                $"ГОДЕН К НОЧНЫМ РЕЙСАМ. «{archetype}». Выдать термос.");
            return seriesStamp is null ? core : $"{core} {seriesStamp}";
        }

        var baseLine = Pick(rng,
            $"ГОДЕН. {name} стабилен. «{code}». Права на лаунчер можно.",
            $"ГОДЕН. Штамп поставлен. Пусть «{topName}» будет милостив.",
            $"ГОДЕН. Играет в игры, и {pr.Dat} норм.");
        return seriesStamp is null ? baseLine : $"{baseLine} {seriesStamp}";
    }

    private static string ShortName(string name)
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

    private static List<string> DistinctTake(
        List<string> pool,
        int n,
        Random rng,
        IReadOnlyList<string>? pinned = null)
    {
        var pin = (pinned ?? Array.Empty<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(0, n))
            .ToList();

        var restNeed = Math.Max(0, n - pin.Count);
        var rest = Shuffle(
                rng,
                pool.Where(s => pin.All(p => !string.Equals(p, s, StringComparison.OrdinalIgnoreCase)))
                    .Distinct(StringComparer.OrdinalIgnoreCase))
            .Take(restNeed);

        return pin.Concat(rest).ToList();
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

    private static string Cap(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

    private static string Pick(Random rng, params string[] options) =>
        options[rng.Next(options.Length)];
}
