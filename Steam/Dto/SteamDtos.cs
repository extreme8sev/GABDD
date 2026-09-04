using System.Text.Json.Serialization;

namespace SteamFun.Steam.Dto;

internal sealed class PlayerSummariesResponse
{
    [JsonPropertyName("response")]
    public PlayerSummariesBody? Response { get; set; }
}

internal sealed class PlayerSummariesBody
{
    [JsonPropertyName("players")]
    public List<PlayerSummaryDto> Players { get; set; } = [];
}

internal sealed class PlayerSummaryDto
{
    [JsonPropertyName("steamid")]
    public string SteamId { get; set; } = "";

    [JsonPropertyName("personaname")]
    public string PersonaName { get; set; } = "";

    [JsonPropertyName("avatarfull")]
    public string AvatarFull { get; set; } = "";

    [JsonPropertyName("timecreated")]
    public long? TimeCreated { get; set; }

    [JsonPropertyName("communityvisibilitystate")]
    public int CommunityVisibilityState { get; set; }
}

internal sealed class OwnedGamesResponse
{
    [JsonPropertyName("response")]
    public OwnedGamesBody? Response { get; set; }
}

internal sealed class OwnedGamesBody
{
    [JsonPropertyName("game_count")]
    public int GameCount { get; set; }

    [JsonPropertyName("games")]
    public List<OwnedGameDto> Games { get; set; } = [];
}

internal sealed class OwnedGameDto
{
    [JsonPropertyName("appid")]
    public int AppId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("playtime_forever")]
    public int PlaytimeForever { get; set; }

    [JsonPropertyName("playtime_2weeks")]
    public int? Playtime2Weeks { get; set; }

    [JsonPropertyName("rtime_last_played")]
    public long? RtimeLastPlayed { get; set; }
}

internal sealed class SteamLevelResponse
{
    [JsonPropertyName("response")]
    public SteamLevelBody? Response { get; set; }
}

internal sealed class SteamLevelBody
{
    [JsonPropertyName("player_level")]
    public int PlayerLevel { get; set; }
}

internal sealed class PlayerBansResponse
{
    [JsonPropertyName("players")]
    public List<PlayerBanDto> Players { get; set; } = [];
}

internal sealed class PlayerBanDto
{
    [JsonPropertyName("SteamId")]
    public string SteamId { get; set; } = "";

    [JsonPropertyName("VACBanned")]
    public bool VacBanned { get; set; }

    [JsonPropertyName("NumberOfVACBans")]
    public int NumberOfVacBans { get; set; }

    [JsonPropertyName("NumberOfGameBans")]
    public int NumberOfGameBans { get; set; }
}

internal sealed class PlayerAchievementsResponse
{
    [JsonPropertyName("playerstats")]
    public PlayerStatsBody? PlayerStats { get; set; }
}

internal sealed class PlayerStatsBody
{
    [JsonPropertyName("steamID")]
    public string? SteamId { get; set; }

    [JsonPropertyName("gameName")]
    public string? GameName { get; set; }

    [JsonPropertyName("achievements")]
    public List<AchievementDto>? Achievements { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

internal sealed class AchievementDto
{
    [JsonPropertyName("apiname")]
    public string ApiName { get; set; } = "";

    [JsonPropertyName("achieved")]
    public int Achieved { get; set; }
}

// Store API: https://store.steampowered.com/api/appdetails?appids=570
internal sealed class StoreAppDetailsEnvelope
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public StoreAppData? Data { get; set; }
}

internal sealed class StoreAppData
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("genres")]
    public List<StoreGenreDto>? Genres { get; set; }

    [JsonPropertyName("categories")]
    public List<StoreCategoryDto>? Categories { get; set; }
}

internal sealed class StoreGenreDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
}

internal sealed class StoreCategoryDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
}

// https://steamspy.com/api.php?request=appdetails&appid=730
internal sealed class SteamSpyAppDetails
{
    [JsonPropertyName("appid")]
    public int AppId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("genre")]
    public string? Genre { get; set; }

    [JsonPropertyName("tags")]
    public Dictionary<string, int>? Tags { get; set; }
}

internal sealed class FriendListResponse
{
    [JsonPropertyName("friendslist")]
    public FriendListBody? FriendsList { get; set; }
}

internal sealed class FriendListBody
{
    [JsonPropertyName("friends")]
    public List<FriendDto> Friends { get; set; } = [];
}

internal sealed class FriendDto
{
    [JsonPropertyName("steamid")]
    public string SteamId { get; set; } = "";

    [JsonPropertyName("relationship")]
    public string? Relationship { get; set; }

    [JsonPropertyName("friend_since")]
    public long FriendSince { get; set; }
}
