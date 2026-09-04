using SteamFun.Domain;

namespace SteamFun.Services;

/// <summary>Шуточный психопортрет — конкретный под телеметрию, без мутных каламбуров.</summary>
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

        var code = BuildTypeCode(shooter, rpg, souls, horror, backlogRatio, hoursLast14Days, rng);
        var archetype = PickArchetype(
            code, shooter, rpg, souls, horror, sandbox, strategy, backlogRatio, hoursLast14Days,
            topName, primarySeries, pr, rng);

        var seriesTag = primarySeries is null
            ? ""
            : $" · серия {primarySeries.DisplayName}×{primarySeries.OwnedCount}";
        var headline =
            $"{profile.PersonaName} · код {code} · «{archetype}»{seriesTag} · Steam LVL {profile.SteamLevel}";

        var summary = BuildSummary(
            profile, fleetSize, neverPlayed, backlogRatio, totalHours, hoursLast14Days,
            topName, topHours, topGames, code, primarySeries, pr, rng);

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
            profile.PersonaName, code, archetype, backlogRatio, hoursLast14Days,
            profile.VacBanned, neverPlayed, topName, primarySeries, pr, rng);

        return new PersonalityPortrait(
            Archetype: $"{archetype} [{code}]",
            Headline: headline,
            Summary: summary,
            Traits: traits,
            Strengths: strengths,
            Risks: risks,
            Compatibility: compatibility,
            Verdict: verdict);
    }

    private static string BuildTypeCode(
        double shooter, double rpg, double souls, double horror,
        double backlogRatio, double recent, Random rng)
    {
        var a = shooter >= Math.Max(rpg, souls) ? "F" :
                souls >= rpg ? "D" :
                "L";
        var b = backlogRatio >= 0.35 ? "W" :
                backlogRatio >= 0.2 ? "B" :
                "C";
        var c = recent >= 25 ? "N" :
                recent <= 1 ? "H" :
                "S";
        var d = horror >= 500 ? "J" :
                shooter >= 2000 ? "R" :
                Pick(rng, "P", "Q", "X");
        return a + b + c + d;
    }

    private static string PickArchetype(
        string code, double shooter, double rpg, double souls, double horror, double sandbox,
        double strategy, double backlogRatio, double recent, string topName,
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
        if (shooter >= 4000 && souls >= 1000)
            pool.AddRange([
                "Ранговый игрок с соулс-стажем",
                $"Человек-прицел, {pr.Which} не боится надписи YOU DIED",
                $"Фанат и «{topName}», и сложных боссов сразу",
            ]);
        if (shooter >= 2500)
            pool.AddRange([
                $"Человек, {pr.Which} вечно крутит чувствительность мыши",
                "Ночной охотник за чужими шагами",
                $"Главный по «ещё одну катку» в «{topName}»",
                $"{pr.Who} слышит перезарядку через стену",
            ]);
        if (souls >= 1000)
            pool.AddRange([
                $"{pr.Who} умирает на боссе 80 раз и всё равно лезет снова",
                "Мастер спокойствия на попытке №83",
                "Чаще гибнет от обрыва в игре, чем от самого босса",
            ]);
        if (rpg >= 1500)
            pool.AddRange([
                "Носитель 47 непрочитанных квестовых меток",
                $"{pr.Who} читает все диалоги… иногда вслух",
                $"Летописец мира «{topName}»",
            ]);
        if (horror >= 400)
            pool.AddRange([
                "Любитель игр, где внезапно орёт динамик",
                "Коллекционер холодного пота",
            ]);
        if (sandbox >= 1500)
            pool.AddRange([
                "Строитель империй, в которые никто не звал",
                "Инженер бессмысленных, но красивых баз",
            ]);
        if (strategy >= 600)
            pool.AddRange([
                "Полководец на паузе",
                "Стратег с табличками в голове",
            ]);
        if (backlogRatio >= 0.3)
            pool.AddRange([
                "Куратор музея нераспечатанного счастья",
                "Посол республики Списка желаний",
                "Смотритель склада «потом поиграю»",
            ]);
        if (recent <= 1)
            pool.AddRange(["Спящий владелец лаунчера", "Призрак вкладки «Библиотека»"]);
        if (recent >= 30)
            pool.AddRange([
                "Человек без режима дня и ночи",
                "Пилот без кнопки выключения",
            ]);

        if (pool.Count == 0)
            pool.AddRange([
                "Универсальный нарушитель спокойствия",
                "Гражданин Габении без ярлыка",
                $"Фанат «{topName}» без диагноза",
            ]);

        var idx = (code.GetHashCode() & 0x7FFFFFFF) % pool.Count;
        return pool[idx];
    }

    private static string BuildSummary(
        SteamProfileSnapshot profile,
        int fleet,
        int neverPlayed,
        double backlogRatio,
        double totalHours,
        double recent,
        string topName,
        double topHours,
        IReadOnlyList<OwnedGameInfo> topGames,
        string code,
        SeriesHit? series,
        PronounSet pr,
        Random rng)
    {
        var years = profile.AccountCreated is null
            ? "?"
            : $"{(int)((DateTimeOffset.Now - profile.AccountCreated.Value).TotalDays / 365.25)}";

        var topLine = string.Join(", ", topGames.Take(3).Select(g =>
            $"«{ShortName(g.Name)}» ({g.PlaytimeForeverMinutes / 60.0:0.#} ч)"));

        var opener = Pick(rng,
            $"Комиссия ГАБДД вскрыла сейф со статистикой {profile.PersonaName} и слегка присвистнула.",
            $"По итогам проверки выяснилось: {profile.PersonaName} — не баг, а штатная функция Steam.",
            $"Если бы личность была персонажем, у {profile.PersonaName} стоял бы тег «слишком увлечённый».");

        var body = Pick(rng,
            $"{years} лет в строю, {totalHours:0} ч общего пробега, библиотека на {fleet} игр. " +
            $"Из них {neverPlayed} до сих пор в заводской плёнке ({backlogRatio:P0} склада). " +
            $"Больше всего времени: {topLine}. Код личности {code} выбит на лобовом стекле лаунчера.",

            $"Главный роман жизни — «{topName}» ({topHours:0.#} ч), остальное — брак сразу с кучей игр. " +
            $"За 14 дней: {recent:0.#} ч. Незапущенных: {neverPlayed}. " +
            $"Комиссия отмечает: человек явно знает, где кнопка «В корзину».");

        var seriesBit = series is null ? null : SeriesAffinity.SummarySnippet(series, pr, rng);

        var closer = backlogRatio >= 0.3
            ? Pick(rng,
                "Прогноз: следующая распродажа будет и оскорблением кошелька, и приглашением одновременно.",
                "Рекомендация родственникам: прятать карту в сезон распродаж Steam.")
            : recent >= 25
                ? "Прогноз: сон станет как дополнение — куплено, но не установлено."
                : "Прогноз: стабильный игровой климат с локальными вспышками «ещё часик».";

        return seriesBit is null
            ? $"{opener} {body} {closer}"
            : $"{opener} {body} {seriesBit} {closer}";
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
                    $"В паспорте невидимо вписано: «{n}», {h:0} ч",
                    $"Кнопки в «{n}» находит с закрытыми глазами (и делает это регулярно)",
                    $"«{n}» для {pr.Gen} не игра, а вторая прописка ({h:0.#} ч)"));
            else if (h >= 200)
                pool.Add($"Серьёзно встречается с «{n}» ({h:0.#} ч), но пока без штампа в паспорте");
        }

        if (shooter >= 2000)
            pool.AddRange(Shuffle(rng, [
                "Слышит шаги лучше, чем собственное имя",
                "Различает 14 видов шагов и 0 видов здорового сна",
                "Считает, что «gg» — это полноценное эмоциональное письмо",
                $"Мышь у {pr.Gen} имеет стаж больше, чем некоторые браки",
            ]));

        if (souls >= 800)
            pool.AddRange(Shuffle(rng, [
                "Умеет сквозь зубы сказать боссу «спасибо за урок»",
                "Верит, что «ещё одна попытка» — это нормальный план на вечер",
                "Чаще гибнет, сорвавшись со скалы в игре, чем от удара врага",
                $"Точка сохранения для {pr.Gen} почти как психотерапевт",
            ]));

        if (rpg >= 1200)
            pool.AddRange(Shuffle(rng, [
                "Пропускает катсцены только если горит ужин (иногда даже тогда нет)",
                "Держит в голове сюжетные ветки лучше, чем планы на неделю",
                "Может поставить жизнь на паузу ради «быстрого» побочного квеста на 3 часа",
            ]));

        if (horror >= 300)
            pool.AddRange(Shuffle(rng, [
                "Громкость наушников — отдельный аттракцион для соседей",
                "Проверяет шкаф не за вещами, а на всякий случай",
            ]));

        if (sandbox >= 800)
            pool.Add("Строит идеальную базу и забывает, зачем она нужна");
        if (survival >= 300)
            pool.Add("Копит палки, камни и чувство ложной безопасности");
        if (racing >= 150)
            pool.Add("В реальном дворе тоже мечтает об откате после ДТП, как в игре");
        if (sim >= 100)
            pool.Add("Может мыть виртуальные машины старательнее, чем реальную кружку");
        if (casual >= 50)
            pool.Add("Иногда притворяется казуалом — комиссия не верит");
        if (fighting >= 20)
            pool.Add("Знает тайминги ударов наизусть — и это уже диагноз");
        if (moba > 0)
            pool.Add("В МОБА заглядывал — и вовремя вышел. Уважение.");

        if (backlogRatio >= 0.25)
            pool.AddRange(Shuffle(rng, [
                $"Покупает быстрее, чем запускает (хвост из {neverPlayed} игр)",
                "Список желаний длиннее списка дел",
                "Считает непройденные игры формой инвестиций",
                "Библиотека как альбом наклеек: главное — собрать комплект",
            ]));

        if (recent >= 30)
            pool.Add(Pick(rng,
                $"За 14 дней {recent:0} ч — это уже не хобби, а вторая смена",
                "Режим сна и бодрствования подал в отставку"));
        if (recent <= 0)
            pool.Add("Лаунчер открывает чаще, чем игры — чистый эстетический опыт");

        if (perfect.Count > 0)
        {
            var p = perfect[rng.Next(perfect.Count)];
            pool.Add(Pick(rng,
                $"Выбил 100% ачивок в «{ShortName(p.GameName)}» — перфекционизм с лицензией",
                $"«{ShortName(p.GameName)}» закрыта полностью: все достижения собраны"));
        }

        if (worstAch is not null && worstAch.Total >= 20)
        {
            var pct = 100.0 * worstAch.Unlocked / worstAch.Total;
            if (pct < 40)
                pool.Add($"В «{ShortName(worstAch.GameName)}» ачивки на {pct:0}% — тут {pr.Nom} человек, а не робот");
        }

        if (profile.SteamLevel >= 70)
            pool.Add($"Steam LVL {profile.SteamLevel}: карточки и значки смотрят с уважением");

        pool.AddRange(Shuffle(rng, [
            $"{Cap(pr.Able)} объяснить, почему «ещё час» длится три",
            "Имеет сложные отношения с кнопкой закрытия игры",
            "Верит в магию «последней катки»",
            "Говорит «я выйду после раунда» и остаётся до титров",
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
            $"Эксперт по «{topName}»: {topHours:0} часов для {pr.Gen} — это разогрев",
            $"Может провести экскурсию по меню «{topName}» с закрытыми глазами"));

        if (totalHours >= 10000)
            pool.Add(Pick(rng,
                $"Общий пробег {totalHours:0} ч: правила игрового мира знает наизусть",
                $"Опыт такой, что новички принимают {pr.Acc} за обучалку"));
        if (shooter >= 2000)
            pool.AddRange(Shuffle(rng, [
                "Отдачу оружия контролирует лучше, чем эмоции в чате",
                "Мини-карту читает как утреннюю газету",
                "Когда остался один против всех — родная стихия",
            ]));
        if (souls >= 800)
            pool.AddRange(Shuffle(rng, [
                "Не бесится после десятой смерти — или хорошо прячет",
                $"После 50 смертей всё ещё почти {pr.Polite} с геймпадом",
            ]));
        if (rpg >= 1200)
            pool.Add("Многозадачность: квест, история, крафт и «куда я шёл» одновременно");
        if (strategy >= 500)
            pool.Add("Видит систему там, где другие видят «просто поиграю»");
        if (sandbox >= 800)
            pool.Add("Креатив уровня «зачем, но красиво»");
        if (perfect.Count > 0)
            pool.Add($"Дожимает контент: {perfect.Count} игр с полным набором ачивок в топ-10");
        if (level >= 50)
            pool.Add($"Steam LVL {level}: {pr.Gen} значки смотрят на чужие значки сверху вниз");
        if (recent >= 15)
            pool.Add($"Форма сейчас горячая — лучше не попадаться {pr.Dat} в матче");

        pool.AddRange(Shuffle(rng, [
            "Умеет гуглить сборки персонажа быстрее, чем признавать поражение",
            "Есть внутренняя инструкция «как не сломаться на поражении» (черновик)",
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
                $"Склад из {neverPlayed} непройденных может обрушиться на совесть",
                "Распродажа включает кошелёк быстрее, чем здравый смысл",
                $"При {fleet} играх легко перепутать библиотеку со складом Ozon",
            ]));

        if (recent >= 30)
            pool.AddRange(Shuffle(rng, [
                "Сон воспринимается как необязательное задание",
                "Риск перепутать «сейчас выйду» с «уже утро»",
                $"Передозировка «{topName}» без рецепта",
            ]));
        else if (recent <= 0)
            pool.Add("Риск стать коллекционером пыли с лицензией Steam");

        if (shooter >= 2500)
            pool.AddRange(Shuffle(rng, [
                "Зависимость от рейтинга и звука чужих кроссовок",
                "Возможны вспышки «это союзники виноваты»",
                "Мышь и нервы изнашиваются синхронно",
            ]));
        if (souls >= 800)
            pool.Add("Давление скачет на крупных боссах — добровольно");
        if (horror >= 400)
            pool.Add("Ночью поглядывает в тёмный угол «на всякий случай»");
        if (rpg >= 1500)
            pool.Add("Риск потерять субботу в «коротком» побочном квесте");

        pool.Add(Pick(rng,
            "Может начать оправдывать покупку дополнения философски",
            "Иногда путает отдых с подбором идеальной сборки персонажа"));

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
                $"В совместной игре {pr.Valuable}, если микрофон не орёт от боли",
                "Идеальный напарник для «давай ещё одну» — предупредите окружающих")
            : souls >= 800
                ? "В совместной игре редкий гость: страдать предпочитает лично"
                : "Для сюжетной игры вдвоём — находка, особенно если не спойлерить";

        var solo = Pick(rng,
            $"В одиночку включает «{topName}» и пропадает с радаров",
            "В одиночку играет отлично — иногда слишком отлично для окружающих");

        var sales = backlogRatio >= 0.25
            ? Pick(rng,
                $"С распродажами {pr.Incompatible} без сопровождающего (на складе уже {neverPlayed})",
                "Список желаний + скидка 70% = чрезвычайная ситуация для кошелька")
            : "К распродажам допущен, но комиссия всё равно следит за глазами";

        var time = recent >= 25
            ? $"Лучшее время для игры: «уже поздно» по мнению всех, кроме {pr.Gen}"
            : "Лучшее время для игры: спокойный вечер без геройства до 5 утра";

        return $"{duo}. {solo}. {sales}. {time}.";
    }

    private static string BuildVerdict(
        string name, string code, string archetype, double backlogRatio, double recent,
        bool vac, int neverPlayed, string topName, SeriesHit? series, PronounSet pr, Random rng)
    {
        if (vac)
            return $"НЕ ГОДЕН. {name} числится на ВАС-учёте — сначала мировая, потом лаунчер.";

        var seriesStamp = series is null ? null : SeriesAffinity.VerdictSnippet(series, pr, rng);

        if (backlogRatio >= 0.3 && recent >= 25)
        {
            var core = Pick(rng,
                $"ГОДЕН С УСЛОВИЯМИ. Диагноз: «{archetype}». Играет как {pr.Cursed}, покупает как {pr.Obsessed}. " +
                $"Предписание: 1 запуск из склада на каждые 3 часа в «{topName}». Код {code}.",
                $"ГОДЕН. Форма «высокопроизводительный хаос». Печать: можно за руль, но карту от распродаж лучше отдать на хранение надёжному человеку.");
            return seriesStamp is null ? core : $"{core} {seriesStamp}";
        }

        if (backlogRatio >= 0.35)
        {
            var core = $"ГОДЕН УСЛОВНО. Архетип «{archetype}», код {code}. " +
                       $"Основной диагноз — коллекционирование потенциала ({neverPlayed} игр ждут своего часа).";
            return seriesStamp is null ? core : $"{core} {seriesStamp}";
        }

        if (recent >= 30)
        {
            var core = Pick(rng,
                $"ГОДЕН. Код {code}. Следить за сном и витамином D. «{topName}» не оправдание перед рассветом.",
                $"ГОДЕН К НОЧНЫМ РЕЙСАМ. Архетип «{archetype}». Выдать термос и запрет на «последнюю катку».");
            return seriesStamp is null ? core : $"{core} {seriesStamp}";
        }

        var baseLine = Pick(rng,
            $"ГОДЕН. {name} стабилен, код {code}, архетип «{archetype}». Можно выдавать права на лаунчер.",
            $"ГОДЕН. Комиссия улыбнулась, штамп поставлен. Пусть «{topName}» будет милостив.",
            $"ГОДЕН К ИГРОВОМУ ДВИЖЕНИЮ. Статистика сходится с диагнозом: «человек играет в игры, и {pr.Dat} норм».");
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
