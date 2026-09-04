using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using SteamFun.Domain;
using SteamFun.Services;
using SteamFun.Steam.Dto;

namespace SteamFun.Steam;

public sealed class SteamApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _genreCacheDir;
    private readonly string _tagCacheDir;
    private readonly ConcurrentDictionary<int, IReadOnlyList<string>> _genreMemory = new();
    private readonly ConcurrentDictionary<int, IReadOnlyList<string>> _tagMemory = new();

    public SteamApiClient(string apiKey, string? cacheRoot = null)
    {
        _apiKey = apiKey;
        var root = cacheRoot ?? Path.Combine(AppContext.BaseDirectory, "cache");
        _genreCacheDir = Path.Combine(root, "genres");
        _tagCacheDir = Path.Combine(root, "tags");
        Directory.CreateDirectory(_genreCacheDir);
        Directory.CreateDirectory(_tagCacheDir);

        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("SteamFun/0.1 (personal; GABDD joke license)");
    }

    public async Task<SteamProfileSnapshot> FetchProfileAsync(string steamId64, CancellationToken ct = default)
    {
        var summaryTask = GetPlayerSummaryAsync(steamId64, ct);
        var gamesTask = GetOwnedGamesAsync(steamId64, ct);
        var levelTask = GetSteamLevelAsync(steamId64, ct);
        var bansTask = GetPlayerBansAsync(steamId64, ct);

        await Task.WhenAll(summaryTask, gamesTask, levelTask, bansTask).ConfigureAwait(false);

        var summary = await summaryTask.ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Профиль {steamId64} не найден.");
        var games = await gamesTask.ConfigureAwait(false);
        var level = await levelTask.ConfigureAwait(false);
        var bans = await bansTask.ConfigureAwait(false);

        Console.WriteLine($"Игр в библиотеке: {games.Count}. Метаданные жанров/тегов (с кэшем)...");
        var enriched = await AttachMetadataAsync(games, ct).ConfigureAwait(false);

        // Ачивки: смесь «сейчас играют» + lifetime-топ, чтобы не игнорировать старые платины.
        var byActivity = PlayActivity.RankByActivity(enriched, 7);
        var byForever = enriched.OrderByDescending(g => g.PlaytimeForeverMinutes).Take(7);
        var top10 = byActivity
            .Concat(byForever)
            .DistinctBy(g => g.AppId)
            .Take(10)
            .ToList();
        if (top10.Count < 10)
        {
            top10 = enriched
                .OrderByDescending(g => g.PlaytimeForeverMinutes)
                .Take(10)
                .ToList();
        }

        Console.WriteLine($"Ачивки для top-{top10.Count} (с приоритетом недавних)...");
        var achievements = await FetchAchievementsForGamesAsync(steamId64, top10, ct).ConfigureAwait(false);

        DateTimeOffset? created = summary.TimeCreated is long unix
            ? DateTimeOffset.FromUnixTimeSeconds(unix)
            : null;

        return new SteamProfileSnapshot(
            SteamId64: steamId64,
            PersonaName: summary.PersonaName,
            AvatarFullUrl: summary.AvatarFull,
            AccountCreated: created,
            SteamLevel: level,
            VacBanned: bans?.VacBanned ?? false,
            NumberOfVacBans: bans?.NumberOfVacBans ?? 0,
            Games: enriched,
            Top10Achievements: achievements);
    }

    /// <summary>
    /// Друзья владельца steamId64 + сам владелец в начале списка.
    /// Список друзей должен быть публичным в настройках приватности Steam.
    /// Результат пишется в cache/friends/{id}.json.
    /// </summary>
    public async Task<IReadOnlyList<SteamFriend>> FetchFriendsAsync(string steamId64, CancellationToken ct = default)
    {
        var friendIds = await GetFriendIdsAsync(steamId64, ct).ConfigureAwait(false);
        var allIds = new List<string> { steamId64 };
        allIds.AddRange(friendIds.Where(id => id != steamId64));

        var summaries = await GetPlayerSummariesAsync(allIds, ct).ConfigureAwait(false);
        var byId = summaries.ToDictionary(s => s.SteamId, StringComparer.Ordinal);

        var result = new List<SteamFriend>(allIds.Count);
        foreach (var id in allIds)
        {
            if (byId.TryGetValue(id, out var s))
            {
                result.Add(new SteamFriend(s.SteamId, s.PersonaName, s.AvatarFull));
            }
            else
            {
                result.Add(new SteamFriend(id, id == steamId64 ? "(я)" : id, ""));
            }
        }

        var ordered = result
            .OrderBy(f => f.SteamId64 == steamId64 ? 0 : 1)
            .ThenBy(f => f.PersonaName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        FriendsCache.Save(steamId64, ordered);
        return ordered;
    }

    private async Task<IReadOnlyList<string>> GetFriendIdsAsync(string steamId64, CancellationToken ct)
    {
        var url =
            $"https://api.steampowered.com/ISteamUser/GetFriendList/v1/?key={_apiKey}&steamid={steamId64}&relationship=friend";

        using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException(
                "Список друзей закрыт. В Steam: Профиль → Настройки приватности → " +
                "«Мои друзья» = Публичный (для своего аккаунта).");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Не удалось получить друзей (HTTP {(int)response.StatusCode}): {TrimForError(body)}");
        }

        var data = JsonSerializer.Deserialize<FriendListResponse>(body, JsonOptions);
        return data?.FriendsList?.Friends
            .Select(f => f.SteamId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList() ?? [];
    }

    private async Task<List<PlayerSummaryDto>> GetPlayerSummariesAsync(
        IReadOnlyList<string> steamIds,
        CancellationToken ct)
    {
        var result = new List<PlayerSummaryDto>();
        foreach (var chunk in steamIds.Chunk(100))
        {
            var ids = string.Join(',', chunk);
            var url =
                $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={_apiKey}&steamids={ids}";
            var data = await GetJsonAsync<PlayerSummariesResponse>(url, ct).ConfigureAwait(false);
            if (data?.Response?.Players is { Count: > 0 } players)
                result.AddRange(players);
        }

        return result;
    }

    private async Task<PlayerSummaryDto?> GetPlayerSummaryAsync(string steamId64, CancellationToken ct)
    {
        var list = await GetPlayerSummariesAsync([steamId64], ct).ConfigureAwait(false);
        return list.FirstOrDefault();
    }
    private async Task<List<OwnedGameInfo>> GetOwnedGamesAsync(string steamId64, CancellationToken ct)
    {
        var url =
            $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?key={_apiKey}&steamid={steamId64}&include_appinfo=1&include_played_free_games=1";
        var data = await GetJsonAsync<OwnedGamesResponse>(url, ct).ConfigureAwait(false);
        var games = data?.Response?.Games ?? [];

        return games
            .Select(g => new OwnedGameInfo(
                g.AppId,
                string.IsNullOrWhiteSpace(g.Name) ? $"App {g.AppId}" : g.Name!,
                g.PlaytimeForever,
                g.Playtime2Weeks ?? 0,
                g.RtimeLastPlayed is > 0 and var unix
                    ? DateTimeOffset.FromUnixTimeSeconds(unix)
                    : null,
                [],
                []))
            .ToList();
    }

    private async Task<int> GetSteamLevelAsync(string steamId64, CancellationToken ct)
    {
        var url =
            $"https://api.steampowered.com/IPlayerService/GetSteamLevel/v1/?key={_apiKey}&steamid={steamId64}";
        var data = await GetJsonAsync<SteamLevelResponse>(url, ct).ConfigureAwait(false);
        return data?.Response?.PlayerLevel ?? 0;
    }

    private async Task<PlayerBanDto?> GetPlayerBansAsync(string steamId64, CancellationToken ct)
    {
        var url =
            $"https://api.steampowered.com/ISteamUser/GetPlayerBans/v1/?key={_apiKey}&steamids={steamId64}";
        var data = await GetJsonAsync<PlayerBansResponse>(url, ct).ConfigureAwait(false);
        return data?.Players.FirstOrDefault();
    }

    private async Task<IReadOnlyList<OwnedGameInfo>> AttachMetadataAsync(
        IReadOnlyList<OwnedGameInfo> games,
        CancellationToken ct)
    {
        var result = new List<OwnedGameInfo>(games.Count);
        var genreNet = 0;
        var genreCache = 0;
        var tagNet = 0;
        var tagCache = 0;

        foreach (var game in games)
        {
            ct.ThrowIfCancellationRequested();

            var (genres, genresCached) = await GetGenresAsync(game.AppId, ct).ConfigureAwait(false);
            if (genresCached) genreCache++;
            else
            {
                genreNet++;
                await Task.Delay(1100, ct).ConfigureAwait(false);
            }

            var (tags, tagsCached) = await GetTagsAsync(game.AppId, ct).ConfigureAwait(false);
            if (tagsCached) tagCache++;
            else
            {
                tagNet++;
                // SteamSpy просит ~1 req/s при массовых запросах
                await Task.Delay(1100, ct).ConfigureAwait(false);
            }

            // Если Store пустой — подстрахуемся genre-строкой из SteamSpy (если уже в тег-кэше нет — ок).
            if (genres.Count == 0 && tags.Count > 0)
            {
                // теги уже есть; жанры могут остаться пустыми — маппер опирается на теги
            }

            result.Add(game with { Genres = genres, Tags = tags });

            var done = result.Count;
            if (done % 25 == 0 || done == games.Count)
            {
                Console.WriteLine(
                    $"  мета: {done}/{games.Count}  " +
                    $"(жанры сеть/кэш {genreNet}/{genreCache}, теги сеть/кэш {tagNet}/{tagCache})");
            }
        }

        Console.WriteLine($"Метаданные готовы. Жанры: сеть {genreNet}, кэш {genreCache}. Теги: сеть {tagNet}, кэш {tagCache}.");
        return result;
    }

    private async Task<(IReadOnlyList<string> Genres, bool FromCache)> GetGenresAsync(int appId, CancellationToken ct)
    {
        if (_genreMemory.TryGetValue(appId, out var mem))
            return (mem, true);

        var cachePath = Path.Combine(_genreCacheDir, $"{appId}.json");
        if (File.Exists(cachePath))
        {
            try
            {
                var cached = await File.ReadAllTextAsync(cachePath, ct).ConfigureAwait(false);
                var list = JsonSerializer.Deserialize<List<string>>(cached) ?? [];
                _genreMemory[appId] = list;
                return (list, true);
            }
            catch
            {
                // повреждённый кэш
            }
        }

        var genres = await FetchStoreGenresAsync(appId, ct).ConfigureAwait(false);
        _genreMemory[appId] = genres;
        await File.WriteAllTextAsync(cachePath, JsonSerializer.Serialize(genres), ct).ConfigureAwait(false);
        return (genres, false);
    }

    private async Task<(IReadOnlyList<string> Tags, bool FromCache)> GetTagsAsync(int appId, CancellationToken ct)
    {
        if (_tagMemory.TryGetValue(appId, out var mem))
            return (mem, true);

        var cachePath = Path.Combine(_tagCacheDir, $"{appId}.json");
        if (File.Exists(cachePath))
        {
            try
            {
                var cached = await File.ReadAllTextAsync(cachePath, ct).ConfigureAwait(false);
                var list = JsonSerializer.Deserialize<List<string>>(cached) ?? [];
                _tagMemory[appId] = list;
                return (list, true);
            }
            catch
            {
                // повреждённый кэш
            }
        }

        var tags = await FetchSteamSpyTagsAsync(appId, ct).ConfigureAwait(false);
        _tagMemory[appId] = tags;
        await File.WriteAllTextAsync(cachePath, JsonSerializer.Serialize(tags), ct).ConfigureAwait(false);
        return (tags, false);
    }

    private async Task<IReadOnlyList<string>> FetchStoreGenresAsync(int appId, CancellationToken ct)
    {
        var url = $"https://store.steampowered.com/api/appdetails?appids={appId}&l=english";
        try
        {
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                Console.WriteLine($"  Store API 429 на app {appId}, ждём 5с...");
                await Task.Delay(5000, ct).ConfigureAwait(false);
                using var retry = await _http.GetAsync(url, ct).ConfigureAwait(false);
                if (!retry.IsSuccessStatusCode)
                    return [];
                return await ParseStoreGenresAsync(retry, appId, ct).ConfigureAwait(false);
            }

            if (!response.IsSuccessStatusCode)
                return [];

            return await ParseStoreGenresAsync(response, appId, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            Console.WriteLine($"  жанры app {appId}: {ex.Message}");
            return [];
        }
    }

    private static async Task<IReadOnlyList<string>> ParseStoreGenresAsync(
        HttpResponseMessage response,
        int appId,
        CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        if (!doc.RootElement.TryGetProperty(appId.ToString(), out var appNode))
            return [];

        var envelope = appNode.Deserialize<StoreAppDetailsEnvelope>(JsonOptions);
        if (envelope is not { Success: true, Data.Genres: { Count: > 0 } genres })
            return [];

        return genres
            .Select(g => g.Description.Trim())
            .Where(g => g.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<IReadOnlyList<string>> FetchSteamSpyTagsAsync(int appId, CancellationToken ct)
    {
        var url = $"https://steamspy.com/api.php?request=appdetails&appid={appId}";
        try
        {
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return [];

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            var root = doc.RootElement;

            if (!root.TryGetProperty("tags", out var tagsNode))
                return [];

            // Обычно объект { "FPS": 123 }, иногда пустой массив [].
            if (tagsNode.ValueKind == JsonValueKind.Array)
                return [];

            if (tagsNode.ValueKind != JsonValueKind.Object)
                return [];

            return tagsNode
                .EnumerateObject()
                .Select(p => (Name: p.Name.Trim(), Votes: p.Value.TryGetInt32(out var v) ? v : 0))
                .Where(t => t.Name.Length > 0)
                .OrderByDescending(t => t.Votes)
                .Take(15)
                .Select(t => t.Name)
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            Console.WriteLine($"  теги app {appId}: {ex.Message}");
            return [];
        }
    }

    private async Task<IReadOnlyList<AchievementProgress>> FetchAchievementsForGamesAsync(
        string steamId64,
        IReadOnlyList<OwnedGameInfo> games,
        CancellationToken ct)
    {
        var list = new List<AchievementProgress>(games.Count);
        foreach (var game in games)
        {
            ct.ThrowIfCancellationRequested();
            var progress = await GetAchievementsAsync(steamId64, game, ct).ConfigureAwait(false);
            list.Add(progress);
            await Task.Delay(200, ct).ConfigureAwait(false);
        }

        return list;
    }

    private async Task<AchievementProgress> GetAchievementsAsync(
        string steamId64,
        OwnedGameInfo game,
        CancellationToken ct)
    {
        var url =
            $"https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v1/?key={_apiKey}&steamid={steamId64}&appid={game.AppId}";
        try
        {
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return new AchievementProgress(game.AppId, game.Name, 0, 0, StatsPrivate: true);

            var data = JsonSerializer.Deserialize<PlayerAchievementsResponse>(body, JsonOptions);
            var achievements = data?.PlayerStats?.Achievements;
            if (achievements is null || !string.IsNullOrEmpty(data?.PlayerStats?.Error))
                return new AchievementProgress(game.AppId, game.Name, 0, 0, StatsPrivate: true);

            var unlocked = achievements.Count(a => a.Achieved == 1);
            return new AchievementProgress(
                game.AppId,
                data!.PlayerStats!.GameName ?? game.Name,
                unlocked,
                achievements.Count,
                StatsPrivate: false);
        }
        catch
        {
            return new AchievementProgress(game.AppId, game.Name, 0, 0, StatsPrivate: true);
        }
    }

    private async Task<T?> GetJsonAsync<T>(string url, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Steam API HTTP {(int)response.StatusCode}: {TrimForError(body)}");
        }

        return JsonSerializer.Deserialize<T>(body, JsonOptions);
    }

    private static string TrimForError(string body) =>
        body.Length <= 200 ? body : body[..200] + "...";

    public void Dispose() => _http.Dispose();
}
