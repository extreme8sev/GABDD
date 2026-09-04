using System.Text.Json;
using SteamFun.Domain;

namespace SteamFun.Steam;

internal sealed class FriendsCacheFile
{
    public string OwnerSteamId { get; set; } = "";
    public DateTimeOffset SavedAt { get; set; }
    public List<SteamFriend> Friends { get; set; } = [];
}

internal sealed class AppUiStateFile
{
    public string? LastOwnerSteamId { get; set; }
    public string? GenderAddress { get; set; }
}

/// <summary>Дисковый кэш списка друзей + последний выбранный владелец.</summary>
public static class FriendsCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string GetCacheRoot() =>
        Path.Combine(Directory.GetCurrentDirectory(), "cache");

    private static string FriendsPath(string ownerSteamId) =>
        Path.Combine(GetCacheRoot(), "friends", $"{ownerSteamId}.json");

    private static string UiStatePath() =>
        Path.Combine(GetCacheRoot(), "ui-state.json");

    public static IReadOnlyList<SteamFriend>? TryLoad(string ownerSteamId, out DateTimeOffset? savedAt)
    {
        savedAt = null;
        var path = FriendsPath(ownerSteamId);
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<FriendsCacheFile>(json, JsonOptions);
            if (data?.Friends is not { Count: > 0 })
                return null;

            savedAt = data.SavedAt;
            return data.Friends;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(string ownerSteamId, IReadOnlyList<SteamFriend> friends)
    {
        var dir = Path.Combine(GetCacheRoot(), "friends");
        Directory.CreateDirectory(dir);

        var data = new FriendsCacheFile
        {
            OwnerSteamId = ownerSteamId,
            SavedAt = DateTimeOffset.Now,
            Friends = friends.ToList(),
        };

        File.WriteAllText(FriendsPath(ownerSteamId), JsonSerializer.Serialize(data, JsonOptions));
        SaveLastOwnerSteamId(ownerSteamId);
    }

    public static string? TryLoadLastOwnerSteamId()
    {
        var path = UiStatePath();
        if (!File.Exists(path))
            return null;

        try
        {
            var data = JsonSerializer.Deserialize<AppUiStateFile>(File.ReadAllText(path), JsonOptions);
            return data?.LastOwnerSteamId;
        }
        catch
        {
            return null;
        }
    }

    public static GenderAddress TryLoadGenderAddress()
    {
        var path = UiStatePath();
        if (!File.Exists(path))
            return GenderAddress.He;

        try
        {
            var data = JsonSerializer.Deserialize<AppUiStateFile>(File.ReadAllText(path), JsonOptions);
            if (data?.GenderAddress is string raw
                && Enum.TryParse<GenderAddress>(raw, ignoreCase: true, out var g))
                return g;
        }
        catch
        {
            // ignore
        }

        return GenderAddress.He;
    }

    public static void SaveLastOwnerSteamId(string ownerSteamId) =>
        SaveUiState(ownerSteamId, gender: null);

    public static void SaveGenderAddress(GenderAddress gender) =>
        SaveUiState(ownerSteamId: null, gender);

    private static void SaveUiState(string? ownerSteamId, GenderAddress? gender)
    {
        Directory.CreateDirectory(GetCacheRoot());
        var path = UiStatePath();
        AppUiStateFile data;
        try
        {
            data = File.Exists(path)
                ? JsonSerializer.Deserialize<AppUiStateFile>(File.ReadAllText(path), JsonOptions) ?? new()
                : new();
        }
        catch
        {
            data = new();
        }

        if (ownerSteamId is not null)
            data.LastOwnerSteamId = ownerSteamId;
        if (gender is not null)
            data.GenderAddress = gender.Value.ToString();

        File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOptions));
    }
}
