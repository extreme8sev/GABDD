namespace SteamFun.Domain;

/// <summary>Обращение в шуточных текстах (Steam пол не отдаёт).</summary>
public enum GenderAddress
{
    He = 0,
    She = 1,
    They = 2,
}

/// <summary>Набор форм для подстановки в шаблоны.</summary>
public sealed record PronounSet(
    string Nom,      // он / она / они
    string Gen,      // его / её / их
    string Dat,      // ему / ей / им
    string Acc,      // его / её / их
    string Prep,     // нём / ней / них  (о нём)
    string Who,      // тот, кто / та, кто / те, кто
    string Which,    // который / которая / которые
    string Person,   // человек (нейтр. существительное)
    // краткие прилагательные / причастия
    string Polite,       // вежлив / вежлива / вежливы
    string Valuable,     // ценен / ценна / ценны
    string Incompatible, // несовместим / несовместима / несовместимы
    string Cursed,       // проклятый / проклятая / проклятые
    string Obsessed,     // одержимый / одержимая / одержимые
    string Able,         // способен / способна / способны
    string Died);        // умер / умерла / умерли

public static class Pronouns
{
    public static PronounSet For(GenderAddress gender) => gender switch
    {
        GenderAddress.She => new(
            Nom: "она", Gen: "её", Dat: "ей", Acc: "её", Prep: "ней",
            Who: "та, кто", Which: "которая", Person: "человек",
            Polite: "вежлива", Valuable: "ценна", Incompatible: "несовместима",
            Cursed: "проклятая", Obsessed: "одержимая", Able: "способна", Died: "умерла"),
        GenderAddress.They => new(
            Nom: "они", Gen: "их", Dat: "им", Acc: "их", Prep: "них",
            Who: "те, кто", Which: "которые", Person: "человек",
            Polite: "вежливы", Valuable: "ценны", Incompatible: "несовместимы",
            Cursed: "проклятые", Obsessed: "одержимые", Able: "способны", Died: "умерли"),
        _ => new(
            Nom: "он", Gen: "его", Dat: "ему", Acc: "его", Prep: "нём",
            Who: "тот, кто", Which: "который", Person: "человек",
            Polite: "вежлив", Valuable: "ценен", Incompatible: "несовместим",
            Cursed: "проклятый", Obsessed: "одержимый", Able: "способен", Died: "умер"),
    };

    public static string DisplayName(GenderAddress gender) => gender switch
    {
        GenderAddress.She => "она",
        GenderAddress.They => "они",
        _ => "он",
    };
}
