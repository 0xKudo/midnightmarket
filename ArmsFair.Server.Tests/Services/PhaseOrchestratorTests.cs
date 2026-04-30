using ArmsFair.Server.Data;
using ArmsFair.Server.Hubs;
using ArmsFair.Server.Services;
using ArmsFair.Server.Simulation;
using ArmsFair.Shared.Enums;
using ArmsFair.Shared.Models;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArmsFair.Server.Tests.Services;

public class PhaseOrchestratorTests : IDisposable
{
    private readonly ArmsFairDb _db;
    private readonly Mock<IHubContext<GameHub>> _hubMock;
    private readonly PhaseOrchestrator _sut;

    public PhaseOrchestratorTests()
    {
        var opts = new DbContextOptionsBuilder<ArmsFairDb>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ArmsFairDb(opts);

        _hubMock = new Mock<IHubContext<GameHub>>();

        // Hub mock: swallow all SendAsync calls
        var clientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(clientProxyMock.Object);
        _hubMock.Setup(h => h.Clients).Returns(clientsMock.Object);

        _sut = new PhaseOrchestrator(_hubMock.Object, _db, NullLogger<PhaseOrchestrator>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private static GameState MakeState(GamePhase phase, int round = 1) => new()
    {
        GameId    = "test-game",
        Round     = round,
        Phase     = phase,
        Tracks    = WorldTracks.Initial(GameMode.EqualWorld),
        Countries = [],
        Players   = []
    };

    [Fact]
    public async Task Advance_FromProcurement_MovesToNegotiation()
    {
        var state = MakeState(GamePhase.Procurement);

        var (result, ending) = await _sut.AdvanceAsync("test-game", state, [], []);

        result.Phase.Should().Be(GamePhase.Negotiation);
        ending.Should().BeNull();
    }

    [Fact]
    public async Task Advance_FromNegotiation_MovesToSales()
    {
        var state = MakeState(GamePhase.Negotiation);

        var (result, _) = await _sut.AdvanceAsync("test-game", state, [], []);

        result.Phase.Should().Be(GamePhase.Sales);
    }

    [Fact]
    public async Task Advance_FromConsequences_IncrementsRound()
    {
        var state = MakeState(GamePhase.Consequences, round: 3);

        var (result, _) = await _sut.AdvanceAsync("test-game", state, [], []);

        result.Phase.Should().Be(GamePhase.WorldUpdate);
        result.Round.Should().Be(4);
    }

    [Fact]
    public async Task Advance_FromSales_MovesToReveal_NoPendingActions()
    {
        var state = MakeState(GamePhase.Sales);

        var (result, _) = await _sut.AdvanceAsync("test-game", state, [], []);

        result.Phase.Should().Be(GamePhase.Reveal);
    }

    [Fact]
    public async Task Advance_WorldUpdate_DoesNotSpread_WhenNoHotWarCountries()
    {
        var state = MakeState(GamePhase.Consequences) with
        {
            Countries =
            [
                new CountryState { Iso = "SYR", Name = "Syria", Stage = CountryStage.Active },
                new CountryState { Iso = "TUR", Name = "Turkey", Stage = CountryStage.Dormant }
            ]
        };

        var (result, _) = await _sut.AdvanceAsync("test-game", state, [], []);

        // Active (stage 2) does not trigger spread — only HotWar (3+) does
        result.Countries.Should().AllSatisfy(c => ((int)c.Stage).Should().BeLessOrEqualTo(2));
    }

    [Fact]
    public async Task Advance_EndingConditionMet_ReturnsEnding()
    {
        // Stability at 100 triggers TotalWar
        var state = MakeState(GamePhase.Consequences) with
        {
            Tracks = new WorldTracks
            {
                MarketHeat    = 0,
                CivilianCost  = 0,
                Stability     = 100,
                SanctionsRisk = 0,
                GeoTension    = 0
            }
        };

        var (_, ending) = await _sut.AdvanceAsync("test-game", state, [], []);

        ending.Should().NotBeNull();
        ending!.Type.Should().Be("total_war");
    }

    [Fact]
    public async Task Advance_BroadcastsPhaseStart_ToGroup()
    {
        var state = MakeState(GamePhase.Procurement);

        await _sut.AdvanceAsync("test-game", state, [], []);

        var clientProxyMock = _hubMock.Object.Clients.Group("test-game");
        Mock.Get(clientProxyMock).Verify(
            c => c.SendCoreAsync("PhaseStart", It.IsAny<object[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
