using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Log4YM.Contracts.Models;
using Log4YM.Server.Core.Database;
using Log4YM.Server.Hubs;
using Log4YM.Server.Services;

namespace Log4YM.Server.Tests.Manual;

[Trait("Category", "Manual")]
public class AdifImportManualTest
{
    [Fact]
    public void ManualTest_ParseAdifFile_VerifiesDxccLookups()
    {
        // Arrange
        var qsoRepoMock = new Mock<IQsoRepository>();
        var settingsRepoMock = new Mock<ISettingsRepository>();
        var hubMock = new Mock<IHubContext<LogHub, ILogHubClient>>();
        var loggerMock = new Mock<ILogger<AdifService>>();

        var service = new AdifService(
            qsoRepoMock.Object,
            settingsRepoMock.Object,
            hubMock.Object,
            loggerMock.Object);

        var adifContent = @"ADIF Export Test
<ADIF_VER:5>3.1.4
<PROGRAMID:8>TestData
<EOH>

<CALL:5>W1AW <QSO_DATE:8>20240101 <TIME_ON:4>1234 <BAND:3>20m <MODE:3>SSB <RST_SENT:2>59 <RST_RCVD:2>59 <EOR>
<CALL:6>VE3ABC <QSO_DATE:8>20240102 <TIME_ON:4>1400 <BAND:3>40m <MODE:2>CW <RST_SENT:3>599 <RST_RCVD:3>599 <EOR>
<CALL:5>JA1XX <QSO_DATE:8>20240103 <TIME_ON:4>0800 <BAND:3>15m <MODE:3>SSB <RST_SENT:2>57 <RST_RCVD:2>58 <EOR>
<CALL:5>DL1XX <QSO_DATE:8>20240104 <TIME_ON:4>1200 <BAND:3>20m <MODE:4>FT8 <RST_SENT:3>-10 <RST_RCVD:3>-05 <EOR>
<CALL:5>G3ABC <QSO_DATE:8>20240105 <TIME_ON:4>1530 <BAND:2>10m <MODE:3>SSB <RST_SENT:2>59 <RST_RCVD:2>59 <EOR>
<CALL:6>ZL1ABC <QSO_DATE:8>20240106 <TIME_ON:4>2200 <BAND:3>20m <MODE:2>CW <RST_SENT:3>579 <RST_RCVD:3>599 <EOR>
<CALL:5>PY1XX <QSO_DATE:8>20240107 <TIME_ON:4>1800 <BAND:3>40m <MODE:3>SSB <RST_SENT:2>59 <RST_RCVD:2>58 <EOR>
<CALL:6>VK3ABC <QSO_DATE:8>20240108 <TIME_ON:4>0600 <BAND:3>15m <MODE:4>FT8 <RST_SENT:3>-12 <RST_RCVD:3>-08 <EOR>";

        // Act
        var qsos = service.ParseAdif(adifContent).ToList();

        // Assert
        qsos.Should().HaveCount(8);

        // Verify all QSOs have DXCC data populated
        qsos.Should().AllSatisfy(qso =>
        {
            qso.Country.Should().NotBeNullOrEmpty($"Callsign {qso.Callsign} should have country");
            qso.Continent.Should().NotBeNullOrEmpty($"Callsign {qso.Callsign} should have continent");
            qso.Station.Should().NotBeNull();
            qso.Station!.Country.Should().NotBeNullOrEmpty($"Callsign {qso.Callsign} should have station country");
            qso.Station.Continent.Should().NotBeNullOrEmpty($"Callsign {qso.Callsign} should have station continent");
        });

        // Verify specific countries
        var w1aw = qsos.First(q => q.Callsign == "W1AW");
        w1aw.Country.Should().Be("United States");
        w1aw.Continent.Should().Be("NA");

        var ve3abc = qsos.First(q => q.Callsign == "VE3ABC");
        ve3abc.Country.Should().Be("Canada");
        ve3abc.Continent.Should().Be("NA");

        var ja1xx = qsos.First(q => q.Callsign == "JA1XX");
        ja1xx.Country.Should().Be("Japan");
        ja1xx.Continent.Should().Be("AS");

        var dl1xx = qsos.First(q => q.Callsign == "DL1XX");
        dl1xx.Country.Should().Be("Fed. Rep. of Germany");
        dl1xx.Continent.Should().Be("EU");

        var g3abc = qsos.First(q => q.Callsign == "G3ABC");
        g3abc.Country.Should().Be("England");
        g3abc.Continent.Should().Be("EU");

        var zl1abc = qsos.First(q => q.Callsign == "ZL1ABC");
        zl1abc.Country.Should().Be("New Zealand");
        zl1abc.Continent.Should().Be("OC");

        var py1xx = qsos.First(q => q.Callsign == "PY1XX");
        py1xx.Country.Should().Be("Brazil");
        py1xx.Continent.Should().Be("SA");

        var vk3abc = qsos.First(q => q.Callsign == "VK3ABC");
        vk3abc.Country.Should().Be("Australia");
        vk3abc.Continent.Should().Be("OC");
    }
}
