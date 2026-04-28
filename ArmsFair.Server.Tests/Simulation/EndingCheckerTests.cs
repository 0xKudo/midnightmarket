using ArmsFair.Shared;
using ArmsFair.Shared.Enums;
using ArmsFair.Shared.Models;
using ArmsFair.Server.Simulation;
using FluentAssertions;
using System.Collections.Generic;

namespace ArmsFair.Server.Tests.Simulation;

public class EndingCheckerTests
{
    private static WorldTracks SafeTracks() => new()
    {
        MarketHeat = 30, CivilianCost = 20, Stability = 25,
        SanctionsRisk = 10, GeoTension = 35
    };

    private static GameState BaseState(WorldTracks? tracks = null, List<CountryState>? countries = null) => new()
    {
        GameId    = "test",
        Round     = 5,
        Phase     = GamePhase.Reveal,
        Tracks    = tracks ?? SafeTracks(),
        Countries = countries ?? new List<CountryState>(),
        Players   = new List<PlayerProfile>()
    };

    // ── Total War (Stability == 100) ──────────────────────────────────────

    [Fact]
    public void TotalWar_WhenStabilityAt100()
    {
        var state = BaseState(SafeTracks() with { Stability = 100 });
        var ending = EndingChecker.Check(state, ceaseFireVoters: new HashSet<string>());
        ending.Should().NotBeNull();
        ending!.Type.Should().Be("total_war");
    }

    [Fact]
    public void NoEnding_WhenStabilityBelow100()
    {
        var state = BaseState(SafeTracks() with { Stability = 99 });
        var ending = EndingChecker.Check(state, ceaseFireVoters: new HashSet<string>());
        ending.Should().BeNull();
    }

    // ── Global Sanctions (CivilianCost == 100) ───────────────────────────

    [Fact]
    public void GlobalSanctions_WhenCivilianCostAt100()
    {
        var state = BaseState(SafeTracks() with { CivilianCost = 100 });
        var ending = EndingChecker.Check(state, ceaseFireVoters: new HashSet<string>());
        ending.Should().NotBeNull();
        ending!.Type.Should().Be("global_sanctions");
    }

    // ── Great Power Confrontation (GeoTension == 100) ────────────────────

    [Fact]
    public void GreatPowerConfrontation_WhenGeoTensionAt100()
    {
        var state = BaseState(SafeTracks() with { GeoTension = 100 });
        var ending = EndingChecker.Check(state, ceaseFireVoters: new HashSet<string>());
        ending.Should().NotBeNull();
        ending!.Type.Should().Be("great_power_confrontation");
    }

    // ── Market Saturation (40% of stage2+ countries at FailedState) ──────

    [Fact]
    public void MarketSaturation_When40PercentFailedState()
    {
        // 4 initial stage2+ countries, 2 now FailedState = 50% ≥ 40%
        var countries = new List<CountryState>
        {
            MakeCountry("A", CountryStage.FailedState,  initialStage2Plus: true),
            MakeCountry("B", CountryStage.FailedState,  initialStage2Plus: true),
            MakeCountry("C", CountryStage.Active,        initialStage2Plus: true),
            MakeCountry("D", CountryStage.Active,        initialStage2Plus: true),
            MakeCountry("E", CountryStage.Dormant,       initialStage2Plus: false),
        };
        var state = BaseState(countries: countries);
        var ending = EndingChecker.Check(state, ceaseFireVoters: new HashSet<string>());
        ending.Should().NotBeNull();
        ending!.Type.Should().Be("market_saturation");
    }

    [Fact]
    public void NoMarketSaturation_WhenBelow40Percent()
    {
        // 4 initial stage2+ countries, only 1 FailedState = 25% < 40%
        var countries = new List<CountryState>
        {
            MakeCountry("A", CountryStage.FailedState, initialStage2Plus: true),
            MakeCountry("B", CountryStage.Active,       initialStage2Plus: true),
            MakeCountry("C", CountryStage.Active,       initialStage2Plus: true),
            MakeCountry("D", CountryStage.Active,       initialStage2Plus: true),
        };
        var state = BaseState(countries: countries);
        var ending = EndingChecker.Check(state, ceaseFireVoters: new HashSet<string>());
        ending.Should().BeNull();
    }

    // ── Negotiated Peace (stability < 20 + all players voted) ────────────

    [Fact]
    public void NegotiatedPeace_WhenLowStabilityAndAllVoted()
    {
        var players = new List<PlayerProfile>
        {
            MakePlayer("p1"), MakePlayer("p2")
        };
        var state = BaseState(SafeTracks() with { Stability = 15 }) with { Players = players };
        var voters = new HashSet<string> { "p1", "p2" };
        var ending = EndingChecker.Check(state, ceaseFireVoters: voters);
        ending.Should().NotBeNull();
        ending!.Type.Should().Be("negotiated_peace");
    }

    [Fact]
    public void NoNegotiatedPeace_WhenNotAllVoted()
    {
        var players = new List<PlayerProfile>
        {
            MakePlayer("p1"), MakePlayer("p2")
        };
        var state = BaseState(SafeTracks() with { Stability = 15 }) with { Players = players };
        var voters = new HashSet<string> { "p1" }; // only one of two
        var ending = EndingChecker.Check(state, ceaseFireVoters: voters);
        ending.Should().BeNull();
    }

    [Fact]
    public void NoNegotiatedPeace_WhenStabilityTooHigh()
    {
        var players = new List<PlayerProfile>
        {
            MakePlayer("p1"), MakePlayer("p2")
        };
        var state = BaseState(SafeTracks() with { Stability = 20 }) with { Players = players };
        var voters = new HashSet<string> { "p1", "p2" };
        var ending = EndingChecker.Check(state, ceaseFireVoters: voters);
        ending.Should().BeNull();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static CountryState MakeCountry(string iso, CountryStage stage, bool initialStage2Plus) => new()
    {
        Iso               = iso,
        Name              = iso,
        Stage             = stage,
        Tension           = 50,
        DemandType        = "open",
        InitialStage2Plus = initialStage2Plus
    };

    private static PlayerProfile MakePlayer(string id) => new()
    {
        Id           = id,
        Username     = id,
        HomeNation   = "US",
        CompanyName  = id,
        Status       = "active",
        Capital      = 50,
        Reputation   = 75,
        SharePrice   = 100,
        PeaceCredits = 0,
        LatentRisk   = 0
    };
}
