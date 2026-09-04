using SteamFun.Domain;

namespace SteamFun.Services;

/// <summary>
/// Маппинг Store genres + SteamSpy tags + эвристики по названию → категории ГАБДД.
/// </summary>
public static class GenreMapper
{
    /// <summary>Сколько топ-тегов по голосам учитывать в классификации (слабые хвосты отсекаем).</summary>
    // Top-N по голосам: слабый хвост (типа Shooter у RDR2 на 9-м месте) не открывает категорию.
    public const int TagsUsedForMapping = 8;

    // Точные совпадения лейбла (регистр не важен).
    private static readonly Dictionary<CategoryCode, string[]> ExactLabels = new()
    {
        [CategoryCode.A] = ["MOBA", "Multiplayer Online Battle Arena"],
        [CategoryCode.B] =
        [
            "Shooter", "FPS", "Hero Shooter", "Top-Down Shooter", "Third-Person Shooter",
            "Twin Stick Shooter", "Looter Shooter", "Arena Shooter", "Tactical Shooter",
        ],
        [CategoryCode.C] = ["RPG", "JRPG", "Action RPG", "CRPG", "Party-Based RPG", "Role-Playing"],
        [CategoryCode.V] = ["Action", "Hack and Slash", "Character Action Game", "Beat 'em up"],
        [CategoryCode.D] =
        [
            "Strategy", "RTS", "Turn-Based Strategy", "Grand Strategy", "4X",
            "Tower Defense", "Card Battler", "Auto Battler", "Wargame",
        ],
        [CategoryCode.E] = ["Souls-like", "Soulslike"],
        [CategoryCode.H] =
        [
            "Horror", "Psychological Horror", "Survival Horror", "Lovecraftian",
        ],
        [CategoryCode.P] = ["Platformer", "2D Platformer", "3D Platformer", "Precision Platformer", "Metroidvania"],
        [CategoryCode.G] = ["Roguelike", "Roguelite", "Traditional Roguelike", "Roguelike Deckbuilder", "Dungeon Crawler"],
        [CategoryCode.X] = ["Sandbox", "Open World", "Building", "Base Building", "Open World Survival Craft"],
        [CategoryCode.M] =
        [
            "Casual", "Puzzle", "Hidden Object", "Clicker", "Idle", "Relaxing", "Match 3", "Point & Click",
        ],
        [CategoryCode.F] = ["Fighting", "2D Fighter", "3D Fighter", "Arena Fighter"],
        [CategoryCode.I] =
        [
            "Simulation", "Management", "Life Sim", "Farming Sim", "Immersive Sim",
            "Economy", "Space Sim", "Flight Sim", "Medical Sim",
        ],
        [CategoryCode.R] = ["Racing", "Automobile Sim", "Motocross", "Kart Racing"],
        [CategoryCode.S] = ["Survival", "Survival Crafting", "Open World Survival Craft"],
        [CategoryCode.O] = ["Massively Multiplayer", "MMO", "MMORPG"],
    };

    // «Настоящие» стратежки — если есть шутерные теги, голый Strategy игнорируем.
    private static readonly string[] StrongStrategyLabels =
    [
        "RTS", "Turn-Based Strategy", "Grand Strategy", "4X", "Tower Defense",
        "Card Battler", "Auto Battler", "Wargame",
    ];

    private static readonly string[] StrongShooterLabels =
    [
        "Shooter", "FPS", "Hero Shooter", "Top-Down Shooter", "Third-Person Shooter",
        "Twin Stick Shooter", "Looter Shooter", "Arena Shooter", "Tactical Shooter",
    ];

    private static readonly (CategoryCode Code, string[] NameParts)[] NameRules =
    [
        (CategoryCode.A, ["Dota", "League of Legends", "Heroes of the Storm", "Smite", "Deadlock"]),
        (CategoryCode.B,
        [
            "Counter-Strike", "Call of Duty", "Quake", "Doom", "Battlefield", "Destiny",
            "Apex Legends", "Overwatch", "Valorant", "Halo", "Left 4 Dead", "Team Fortress",
            "PUBG", "PlayerUnknown", "Rainbow Six", "Escape from Tarkov", "Hunt: Showdown",
            "Ultrakill", "Deep Rock Galactic", "Payday", "Borderlands", "Titanfall",
            "Wolfenstein", "Bioshock", "Half-Life", "Warface", "Insurgency",
            "Ready or Not", "Hell Let Loose",
        ]),
        (CategoryCode.C,
        [
            "Witcher", "Baldur", "Fallout", "Cyberpunk", "Divinity", "Pillars of Eternity",
            "Skyrim", "Oblivion", "Dragon Age", "Mass Effect", "Diablo", "Path of Exile", "Final Fantasy",
        ]),
        (CategoryCode.E,
        [
            "Dark Souls", "Elden Ring", "Bloodborne", "Sekiro", "Nioh", "Lies of P",
            "Wo Long", "Another Crab's Treasure", "Black Myth",
        ]),
        (CategoryCode.H,
        [
            "Outlast", "Amnesia", "Phasmophobia", "Resident Evil", "Dead Space", "SOMA",
            "Visage", "Alien: Isolation", "Dead by Daylight", "Layers of Fear", "The Evil Within",
        ]),
        (CategoryCode.G,
        [
            "Hades", "Dead Cells", "Slay the Spire", "The Binding of Isaac", "Enter the Gungeon",
            "Noita", "Risk of Rain", "Vampire Survivors", "Balatro",
        ]),
        (CategoryCode.X,
        [
            "Minecraft", "Terraria", "Garry's Mod", "Rust", "No Man's Sky", "Satisfactory",
            "Factorio", "Space Engineers",
        ]),
        (CategoryCode.S,
        [
            "Don't Starve", "The Forest", "Green Hell", "Subnautica", "Valheim", "ARK:",
            "7 Days to Die", "Project Zomboid",
        ]),
        (CategoryCode.P, ["Celeste", "Hollow Knight", "Ori and", "Cuphead", "Rayman", "Super Meat Boy"]),
        (CategoryCode.F, ["Street Fighter", "Tekken", "Mortal Kombat", "Guilty Gear", "Soulcalibur", "Dragon Ball FighterZ"]),
        (CategoryCode.R, ["Forza", "Gran Turismo", "Need for Speed", "Assetto Corsa", "iRacing", "Dirt Rally", "F1 "]),
        (CategoryCode.O,
        [
            "World of Warcraft", "Final Fantasy XIV", "Elder Scrolls Online", "Lost Ark",
            "New World", "Black Desert", "Guild Wars",
        ]),
        (CategoryCode.D,
        [
            "Civilization", "XCOM", "Age of Empires", "Total War", "Company of Heroes",
            "StarCraft", "Into the Breach",
        ]),
        (CategoryCode.I,
        [
            "The Sims", "Euro Truck", "American Truck", "Microsoft Flight", "Cities: Skylines",
            "PowerWash", "House Flipper",
        ]),
    ];

    public static IReadOnlyCollection<CategoryCode> Map(OwnedGameInfo game) =>
        Map(game.Name, game.Genres, game.Tags);

    public static IReadOnlyCollection<CategoryCode> Map(
        string name,
        IEnumerable<string> genres,
        IEnumerable<string> tags)
    {
        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in genres)
            if (!string.IsNullOrWhiteSpace(g))
                labels.Add(g.Trim());

        foreach (var t in tags.Take(TagsUsedForMapping))
            if (!string.IsNullOrWhiteSpace(t))
                labels.Add(t.Trim());

        var set = new HashSet<CategoryCode>();

        foreach (var (code, exacts) in ExactLabels)
        {
            if (exacts.Any(labels.Contains))
                set.Add(code);
        }

        // Шутер + голый Strategy (как у CS) — не стратегия ГАБДД.
        if (set.Contains(CategoryCode.D) &&
            StrongShooterLabels.Any(labels.Contains) &&
            !StrongStrategyLabels.Any(labels.Contains))
        {
            set.Remove(CategoryCode.D);
        }

        ApplyNameRules(name, set);

        if (set.Contains(CategoryCode.A) &&
            !labels.Contains("MOBA") &&
            !labels.Contains("Multiplayer Online Battle Arena") &&
            !NameMatches(name, NameRules.First(r => r.Code == CategoryCode.A).NameParts))
        {
            set.Remove(CategoryCode.A);
        }

        return set;
    }

    private static void ApplyNameRules(string name, HashSet<CategoryCode> set)
    {
        foreach (var (code, parts) in NameRules)
        {
            if (NameMatches(name, parts))
                set.Add(code);
        }
    }

    private static bool NameMatches(string name, IEnumerable<string> parts) =>
        parts.Any(p => name.Contains(p, StringComparison.OrdinalIgnoreCase));
}
