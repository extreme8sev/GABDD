namespace SteamFun.Domain;

public sealed record OwnedGameInfo(
    int AppId,
    string Name,
    int PlaytimeForeverMinutes,
    int Playtime2WeeksMinutes,
    DateTimeOffset? LastPlayedUtc,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Tags);

public sealed record AchievementProgress(
    int AppId,
    string GameName,
    int Unlocked,
    int Total,
    bool StatsPrivate);

public sealed record SteamProfileSnapshot(
    string SteamId64,
    string PersonaName,
    string AvatarFullUrl,
    DateTimeOffset? AccountCreated,
    int SteamLevel,
    bool VacBanned,
    int NumberOfVacBans,
    IReadOnlyList<OwnedGameInfo> Games,
    IReadOnlyList<AchievementProgress> Top10Achievements);

public sealed record GamerLicense(
    SteamProfileSnapshot Profile,
    int FleetSize,
    int NeverPlayedCount,
    double TotalHours,
    double HoursLast14Days,
    IReadOnlyList<GameCategory> Categories,
    PenaltySummary Penalties,
    string? Citizenship,
    string? ValidUntil,
    string? Residence,
    string? Transmission,
    string? BloodType,
    string? SpecialMarks,
    IReadOnlyList<string> SpecialNotes,
    IReadOnlyList<string> MedicalNotes,
    PersonalityPortrait Personality);

public sealed record SteamFriend(
    string SteamId64,
    string PersonaName,
    string AvatarUrl)
{
    public override string ToString() => $"{PersonaName}  ·  {SteamId64}";
}

/// <summary>Шуточный психопортрет владельца по статистике Steam.</summary>
public sealed record PersonalityPortrait(
    string Archetype,
    string Headline,
    string Summary,
    IReadOnlyList<string> Traits,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Risks,
    string Compatibility,
    string Verdict);
