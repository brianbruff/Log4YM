using FluentAssertions;
using Log4YM.Contracts.Events;
using Log4YM.Server.Hubs;
using Log4YM.Server.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Log4YM.Server.Tests.Tests.Services;

[Trait("Category", "Unit")]
public class CwKeyerServiceTests
{
    private readonly Mock<ILogger<CwKeyerService>> _mockLogger;
    private readonly Mock<IHubContext<LogHub, ILogHubClient>> _mockHubContext;
    private readonly Mock<ILogHubClient> _mockAllClients;
    private readonly Mock<TciRadioService> _mockTci;
    private readonly Mock<HamlibService> _mockHamlib;
    private readonly CwKeyerService _service;

    public CwKeyerServiceTests()
    {
        _mockLogger = new Mock<ILogger<CwKeyerService>>();
        _mockHubContext = new Mock<IHubContext<LogHub, ILogHubClient>>();
        _mockAllClients = new Mock<ILogHubClient>();

        _mockHubContext.Setup(h => h.Clients.All).Returns(_mockAllClients.Object);

        // Create mocks for TCI and Hamlib services (methods are virtual)
        _mockTci = new Mock<TciRadioService>(
            Mock.Of<ILogger<TciRadioService>>(),
            Mock.Of<IHubContext<LogHub, ILogHubClient>>(),
            Mock.Of<IServiceScopeFactory>());

        _mockHamlib = new Mock<HamlibService>(
            Mock.Of<ILogger<HamlibService>>(),
            Mock.Of<IHubContext<LogHub, ILogHubClient>>(),
            Mock.Of<IServiceScopeFactory>(),
            Mock.Of<IUserConfigService>());

        _service = new CwKeyerService(
            _mockLogger.Object,
            _mockHubContext.Object,
            _mockTci.Object,
            _mockHamlib.Object);
    }

    #region GetStatus

    [Fact]
    public void GetStatus_UnknownRadio_ReturnsNull()
    {
        _service.GetStatus("unknown-radio").Should().BeNull();
    }

    [Fact]
    public async Task GetStatus_AfterSendCw_ReturnsKeyingState()
    {
        _mockTci.Setup(t => t.SendCwAsync("radio1", "CQ CQ", 25))
            .ReturnsAsync(true);

        await _service.SendCwAsync("radio1", "CQ CQ", 25);

        var status = _service.GetStatus("radio1");
        status.Should().NotBeNull();
        status!.RadioId.Should().Be("radio1");
        status.IsKeying.Should().BeTrue();
        status.SpeedWpm.Should().Be(25);
        status.CurrentMessage.Should().Be("CQ CQ");
    }

    #endregion

    #region SendCwAsync

    [Fact]
    public async Task SendCwAsync_RoutesToTci_WhenTciAvailable()
    {
        _mockTci.Setup(t => t.SendCwAsync("radio1", "CQ", 20))
            .ReturnsAsync(true);

        await _service.SendCwAsync("radio1", "CQ", 20);

        _mockTci.Verify(t => t.SendCwAsync("radio1", "CQ", 20), Times.Once);
        _mockHamlib.Verify(t => t.SendCwAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task SendCwAsync_FallsBackToHamlib_WhenTciUnavailable()
    {
        _mockTci.Setup(t => t.SendCwAsync("radio1", "CQ", 20))
            .ReturnsAsync(false);
        _mockHamlib.Setup(h => h.SendCwAsync("radio1", "CQ", 20))
            .ReturnsAsync(true);

        await _service.SendCwAsync("radio1", "CQ", 20);

        _mockTci.Verify(t => t.SendCwAsync("radio1", "CQ", 20), Times.Once);
        _mockHamlib.Verify(h => h.SendCwAsync("radio1", "CQ", 20), Times.Once);
    }

    [Fact]
    public async Task SendCwAsync_ThrowsInvalidOperation_WhenNoRadioAvailable()
    {
        _mockTci.Setup(t => t.SendCwAsync("radio1", "CQ", 20))
            .ReturnsAsync(false);
        _mockHamlib.Setup(h => h.SendCwAsync("radio1", "CQ", 20))
            .ReturnsAsync(false);

        var act = () => _service.SendCwAsync("radio1", "CQ", 20);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not support CW keying*");
    }

    [Fact]
    public async Task SendCwAsync_UsesDefaultWpm_WhenSpeedNotProvided()
    {
        _mockTci.Setup(t => t.SendCwAsync("radio1", "CQ", 25))
            .ReturnsAsync(true);

        await _service.SendCwAsync("radio1", "CQ");

        // Default WPM is 25
        _mockTci.Verify(t => t.SendCwAsync("radio1", "CQ", 25), Times.Once);
    }

    [Fact]
    public async Task SendCwAsync_BroadcastsStatus()
    {
        _mockTci.Setup(t => t.SendCwAsync("radio1", "CQ", 20))
            .ReturnsAsync(true);

        await _service.SendCwAsync("radio1", "CQ", 20);

        _mockAllClients.Verify(c => c.OnCwKeyerStatus(
            It.Is<CwKeyerStatusEvent>(e =>
                e.RadioId == "radio1" &&
                e.IsKeying == true &&
                e.SpeedWpm == 20 &&
                e.CurrentMessage == "CQ")),
            Times.Once);
    }

    [Fact]
    public async Task SendCwAsync_UpdatesState_ForMultipleRadios()
    {
        _mockTci.Setup(t => t.SendCwAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(true);

        await _service.SendCwAsync("radio1", "CQ CQ", 20);
        await _service.SendCwAsync("radio2", "73 GL", 30);

        var status1 = _service.GetStatus("radio1");
        var status2 = _service.GetStatus("radio2");

        status1!.CurrentMessage.Should().Be("CQ CQ");
        status1.SpeedWpm.Should().Be(20);

        status2!.CurrentMessage.Should().Be("73 GL");
        status2.SpeedWpm.Should().Be(30);
    }

    #endregion

    #region StopCwAsync

    [Fact]
    public async Task StopCwAsync_ClearsKeyingState()
    {
        _mockTci.Setup(t => t.SendCwAsync("radio1", "CQ CQ", 20))
            .ReturnsAsync(true);

        await _service.SendCwAsync("radio1", "CQ CQ", 20);
        await _service.StopCwAsync("radio1");

        var status = _service.GetStatus("radio1");
        status!.IsKeying.Should().BeFalse();
        status.CurrentMessage.Should().BeNull();
    }

    [Fact]
    public async Task StopCwAsync_BroadcastsStoppedStatus()
    {
        _mockTci.Setup(t => t.SendCwAsync("radio1", "CQ", 20))
            .ReturnsAsync(true);

        await _service.SendCwAsync("radio1", "CQ", 20);
        await _service.StopCwAsync("radio1");

        // Should broadcast twice: once for SendCw (keying=true), once for Stop (keying=false)
        _mockAllClients.Verify(c => c.OnCwKeyerStatus(
            It.Is<CwKeyerStatusEvent>(e =>
                e.RadioId == "radio1" && e.IsKeying == false)),
            Times.Once);
    }

    [Fact]
    public async Task StopCwAsync_NoOp_WhenRadioNotTracked()
    {
        // Should not throw for unknown radio
        await _service.StopCwAsync("unknown-radio");

        _mockAllClients.Verify(
            c => c.OnCwKeyerStatus(It.IsAny<CwKeyerStatusEvent>()),
            Times.Never);
    }

    #endregion

    #region SetSpeedAsync

    [Fact]
    public async Task SetSpeedAsync_UpdatesStateAndBroadcasts()
    {
        _mockTci.Setup(t => t.SetCwSpeedAsync("radio1", 35))
            .ReturnsAsync(true);

        await _service.SetSpeedAsync("radio1", 35);

        var status = _service.GetStatus("radio1");
        status!.SpeedWpm.Should().Be(35);

        _mockAllClients.Verify(c => c.OnCwKeyerStatus(
            It.Is<CwKeyerStatusEvent>(e =>
                e.RadioId == "radio1" && e.SpeedWpm == 35)),
            Times.Once);
    }

    [Fact]
    public async Task SetSpeedAsync_SendsToTciFirst()
    {
        _mockTci.Setup(t => t.SetCwSpeedAsync("radio1", 30))
            .ReturnsAsync(true);

        await _service.SetSpeedAsync("radio1", 30);

        _mockTci.Verify(t => t.SetCwSpeedAsync("radio1", 30), Times.Once);
        _mockHamlib.Verify(h => h.SetCwSpeedAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task SetSpeedAsync_FallsBackToHamlib()
    {
        _mockTci.Setup(t => t.SetCwSpeedAsync("radio1", 30))
            .ReturnsAsync(false);
        _mockHamlib.Setup(h => h.SetCwSpeedAsync("radio1", 30))
            .ReturnsAsync(true);

        await _service.SetSpeedAsync("radio1", 30);

        _mockHamlib.Verify(h => h.SetCwSpeedAsync("radio1", 30), Times.Once);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(61)]
    [InlineData(100)]
    public async Task SetSpeedAsync_ThrowsForOutOfRange(int invalidSpeed)
    {
        var act = () => _service.SetSpeedAsync("radio1", invalidSpeed);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*between 5 and 60*");
    }

    [Theory]
    [InlineData(5)]
    [InlineData(60)]
    public async Task SetSpeedAsync_AcceptsBoundaryValues(int validSpeed)
    {
        _mockTci.Setup(t => t.SetCwSpeedAsync("radio1", validSpeed))
            .ReturnsAsync(true);

        await _service.SetSpeedAsync("radio1", validSpeed);

        var status = _service.GetStatus("radio1");
        status!.SpeedWpm.Should().Be(validSpeed);
    }

    #endregion

    #region State Isolation

    [Fact]
    public async Task StopCw_OnlyAffectsTargetRadio()
    {
        _mockTci.Setup(t => t.SendCwAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(true);

        await _service.SendCwAsync("radio1", "CQ CQ", 20);
        await _service.SendCwAsync("radio2", "73 GL", 25);

        await _service.StopCwAsync("radio1");

        _service.GetStatus("radio1")!.IsKeying.Should().BeFalse();
        _service.GetStatus("radio2")!.IsKeying.Should().BeTrue();
    }

    [Fact]
    public async Task SetSpeed_OnlyAffectsTargetRadio()
    {
        _mockTci.Setup(t => t.SendCwAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(true);
        _mockTci.Setup(t => t.SetCwSpeedAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(true);

        await _service.SendCwAsync("radio1", "CQ", 20);
        await _service.SendCwAsync("radio2", "CQ", 25);

        await _service.SetSpeedAsync("radio1", 35);

        _service.GetStatus("radio1")!.SpeedWpm.Should().Be(35);
        _service.GetStatus("radio2")!.SpeedWpm.Should().Be(25);
    }

    #endregion
}
