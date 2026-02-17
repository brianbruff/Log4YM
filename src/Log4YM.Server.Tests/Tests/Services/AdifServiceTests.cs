using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Log4YM.Contracts.Models;
using Log4YM.Server.Core.Database;
using Log4YM.Server.Hubs;
using Log4YM.Server.Services;
using Xunit;

namespace Log4YM.Server.Tests.Tests.Services;

[Trait("Category", "Unit")]
public class AdifServiceTests
{
    private readonly AdifService _service;
    private readonly Mock<IQsoRepository> _qsoRepoMock = new();
    private readonly Mock<ISettingsRepository> _settingsRepoMock = new();
    private readonly Mock<IHubContext<LogHub, ILogHubClient>> _hubMock = new();

    public AdifServiceTests()
    {
        var loggerMock = new Mock<ILogger<AdifService>>();
        _service = new AdifService(
            _qsoRepoMock.Object,
            _settingsRepoMock.Object,
            _hubMock.Object,
            loggerMock.Object);
    }

    #region ParseAdif - Basic Parsing

    [Fact]
    public void ParseAdif_SimpleRecord_ParsesCorrectly()
    {
        var adif = "<CALL:5>W1AW <QSO_DATE:8>20240101 <TIME_ON:4>1234 <BAND:3>20m <MODE:3>SSB <EOR>";

        var qsos = _service.ParseAdif(adif).ToList();

        qsos.Should().HaveCount(1);
        qsos[0].Callsign.Should().Be("W1AW");
        qsos[0].Band.Should().Be("20m");
        qsos[0].Mode.Should().Be("SSB");
        qsos[0].TimeOn.Should().Be("1234");
    }

    [Fact]
    public void ParseAdif_MultipleRecords_ParsesAll()
    {
        var adif = @"
<CALL:5>W1AW <QSO_DATE:8>20240101 <TIME_ON:4>1234 <BAND:3>20m <MODE:3>SSB <EOR>
<CALL:6>VE3ABC <QSO_DATE:8>20240102 <TIME_ON:4>1500 <BAND:3>40m <MODE:2>CW <EOR>";

        var qsos = _service.ParseAdif(adif).ToList();

        qsos.Should().HaveCount(2);
        qsos[0].Callsign.Should().Be("W1AW");
        qsos[1].Callsign.Should().Be("VE3ABC");
        qsos[1].Band.Should().Be("40m");
        qsos[1].Mode.Should().Be("CW");
    }

    [Fact]
    public void ParseAdif_WithHeader_SkipsHeader()
    {
        var adif = @"ADIF Export from Log4YM
<ADIF_VER:5>3.1.4
<PROGRAMID:6>Log4YM
<EOH>
<CALL:5>W1AW <QSO_DATE:8>20240101 <TIME_ON:4>1234 <BAND:3>20m <MODE:3>SSB <EOR>";

        var qsos = _service.ParseAdif(adif).ToList();

        qsos.Should().HaveCount(1);
        qsos[0].Callsign.Should().Be("W1AW");
    }

    [Fact]
    public void ParseAdif_MissingCall_SkipsRecord()
    {
        var adif = "<QSO_DATE:8>20240101 <TIME_ON:4>1234 <BAND:3>20m <MODE:3>SSB <EOR>";

        var qsos = _service.ParseAdif(adif).ToList();

        qsos.Should().BeEmpty();
    }

    [Fact]
    public void ParseAdif_MissingDate_SkipsRecord()
    {
        var adif = "<CALL:5>W1AW <TIME_ON:4>1234 <BAND:3>20m <MODE:3>SSB <EOR>";

        var qsos = _service.ParseAdif(adif).ToList();

        qsos.Should().BeEmpty();
    }

    [Fact]
    public void ParseAdif_EmptyContent_ReturnsEmpty()
    {
        var qsos = _service.ParseAdif("").ToList();

        qsos.Should().BeEmpty();
    }

    [Fact]
    public void ParseAdif_HeaderOnly_ReturnsEmpty()
    {
        var adif = "<ADIF_VER:5>3.1.4 <EOH>";

        var qsos = _service.ParseAdif(adif).ToList();

        qsos.Should().BeEmpty();
    }

    #endregion

    #region ParseAdif - Optional Fields

    [Fact]
    public void ParseAdif_WithOptionalFields_ParsesAll()
    {
        var adif = "<CALL:5>W1AW <QSO_DATE:8>20240101 <TIME_ON:4>1234 <BAND:3>20m <MODE:3>SSB " +
                   "<FREQ:8>14.20000 <RST_SENT:2>59 <RST_RCVD:2>57 <NAME:4>John " +
                   "<GRIDSQUARE:6>FN31pr <COUNTRY:13>United States <COMMENT:10>Good chat! <EOR>";

        var qsos = _service.ParseAdif(adif).ToList();

        qsos.Should().HaveCount(1);
        var qso = qsos[0];
        qso.Frequency.Should().BeApproximately(14.2, 0.001);
        qso.RstSent.Should().Be("59");
        qso.RstRcvd.Should().Be("57");
        qso.Station!.Name.Should().Be("John");
        qso.Station.Grid.Should().Be("FN31pr");
        qso.Station.Country.Should().Be("United States");
        qso.Comment.Should().Be("Good chat!");
    }

    [Fact]
    public void ParseAdif_WithContestFields_ParsesContest()
    {
        var adif = "<CALL:5>W1AW <QSO_DATE:8>20240101 <TIME_ON:4>1234 <BAND:3>20m <MODE:3>SSB " +
                   "<CONTEST_ID:10>CQ-WW-SSB <STX:3>001 <SRX:3>042 <EOR>";

        var qsos = _service.ParseAdif(adif).ToList();

        qsos.Should().HaveCount(1);
        qsos[0].Contest.Should().NotBeNull();
        qsos[0].Contest!.ContestId.Should().Be("CQ-WW-SSB");
        qsos[0].Contest.SerialSent.Should().Be("001");
        qsos[0].Contest.SerialRcvd.Should().Be("042");
    }

    [Fact]
    public void ParseAdif_WithDxccField_ParsesNumericField()
    {
        var adif = "<CALL:5>W1AW <QSO_DATE:8>20240101 <TIME_ON:4>1234 <BAND:3>20m <MODE:3>SSB <DXCC:3>291 <EOR>";

        var qsos = _service.ParseAdif(adif).ToList();

        qsos.Should().HaveCount(1);
        qsos[0].Station!.Dxcc.Should().Be(291);
    }

    [Fact]
    public void ParseAdif_WithQslFields_ParsesQslStatus()
    {
        var adif = "<CALL:5>W1AW <QSO_DATE:8>20240101 <TIME_ON:4>1234 <BAND:3>20m <MODE:3>SSB " +
                   "<QSL_SENT:1>Y <QSL_RCVD:1>N <LOTW_QSL_SENT:1>Y <EQSL_QSL_RCVD:1>Y <EOR>";

        var qsos = _service.ParseAdif(adif).ToList();

        qsos.Should().HaveCount(1);
        qsos[0].Qsl.Should().NotBeNull();
        qsos[0].Qsl!.Sent.Should().Be("Y");
        qsos[0].Qsl.Rcvd.Should().Be("N");
        qsos[0].Qsl.Lotw!.Sent.Should().Be("Y");
        qsos[0].Qsl.Eqsl!.Rcvd.Should().Be("Y");
    }

    #endregion

    #region ParseAdif - Time Parsing

    [Fact]
    public void ParseAdif_SixDigitTime_ParsesCorrectly()
    {
        var adif = "<CALL:5>W1AW <QSO_DATE:8>20240101 <TIME_ON:6>123456 <BAND:3>20m <MODE:3>SSB <EOR>";

        var qsos = _service.ParseAdif(adif).ToList();

        qsos.Should().HaveCount(1);
        qsos[0].QsoDate.Hour.Should().Be(12);
        qsos[0].QsoDate.Minute.Should().Be(34);
    }

    [Fact]
    public void ParseAdif_FourDigitTime_ParsesCorrectly()
    {
        var adif = "<CALL:5>W1AW <QSO_DATE:8>20240615 <TIME_ON:4>0830 <BAND:3>20m <MODE:3>SSB <EOR>";

        var qsos = _service.ParseAdif(adif).ToList();

        qsos.Should().HaveCount(1);
        qsos[0].QsoDate.Year.Should().Be(2024);
        qsos[0].QsoDate.Month.Should().Be(6);
        qsos[0].QsoDate.Day.Should().Be(15);
        qsos[0].TimeOn.Should().Be("0830");
    }

    #endregion

    #region ParseAdif - Callsign Normalization

    [Fact]
    public void ParseAdif_LowercaseCallsign_ConvertsToUppercase()
    {
        var adif = "<CALL:5>w1aw <QSO_DATE:8>20240101 <TIME_ON:4>1234 <BAND:3>20m <MODE:3>SSB <EOR>";

        var qsos = _service.ParseAdif(adif).ToList();

        qsos.Should().HaveCount(1);
        qsos[0].Callsign.Should().Be("W1AW");
    }

    #endregion

    #region ParseAdif - Band Derivation from Frequency

    [Fact]
    public void ParseAdif_NoBandWithFrequency_DerivesBand()
    {
        var adif = "<CALL:5>W1AW <QSO_DATE:8>20240101 <TIME_ON:4>1234 <MODE:3>SSB <FREQ:8>14.20000 <EOR>";

        var qsos = _service.ParseAdif(adif).ToList();

        qsos.Should().HaveCount(1);
        qsos[0].Band.Should().Be("20m");
    }

    [Fact]
    public void ParseAdif_NoBandNoFrequency_Defaults20m()
    {
        var adif = "<CALL:5>W1AW <QSO_DATE:8>20240101 <TIME_ON:4>1234 <MODE:3>SSB <EOR>";

        var qsos = _service.ParseAdif(adif).ToList();

        qsos.Should().HaveCount(1);
        qsos[0].Band.Should().Be("20m");
    }

    #endregion

    #region ParseAdif - Extra Fields Preservation

    [Fact]
    public void ParseAdif_UnknownFields_PreservedInAdifExtra()
    {
        var adif = "<CALL:5>W1AW <QSO_DATE:8>20240101 <TIME_ON:4>1234 <BAND:3>20m <MODE:3>SSB " +
                   "<MY_ANTENNA:6>Dipole <TX_PWR:3>100 <EOR>";

        var qsos = _service.ParseAdif(adif).ToList();

        qsos.Should().HaveCount(1);
        qsos[0].AdifExtra.Should().NotBeNull();
        qsos[0].AdifExtra!.Contains("my_antenna").Should().BeTrue();
    }

    [Fact]
    public void ParseAdif_BooleanLikeExtraFields_StoredAsStrings()
    {
        var adif = "<CALL:5>W1AW <QSO_DATE:8>20240101 <TIME_ON:4>1234 <BAND:3>20m <MODE:3>SSB " +
                   "<DIGI:1>Y <SOTA:1>N <POTA_REF:10>K-0817 <EOR>";

        var qsos = _service.ParseAdif(adif).ToList();

        qsos.Should().HaveCount(1);
        qsos[0].AdifExtra.Should().NotBeNull();
        qsos[0].AdifExtra!.Contains("digi").Should().BeTrue();
        qsos[0].AdifExtra!["digi"].BsonType.Should().Be(MongoDB.Bson.BsonType.String);
        qsos[0].AdifExtra!["digi"].AsString.Should().Be("Y");
        qsos[0].AdifExtra!["sota"].AsString.Should().Be("N");
        qsos[0].AdifExtra!["pota_ref"].AsString.Should().Be("K-0817");
    }

    [Fact]
    public void ParseAdif_NumericExtraFields_StoredAsStrings()
    {
        // Test that numeric values in extra fields are also normalized to strings
        // to prevent type conflicts when fields might be stored as numbers elsewhere
        var adif = "<CALL:5>W1AW <QSO_DATE:8>20240101 <TIME_ON:4>1234 <BAND:3>20m <MODE:3>SSB " +
                   "<AGE:2>42 <CUSTOM_NUM:3>123 <IOTA:6>NA-001 <EOR>";

        var qsos = _service.ParseAdif(adif).ToList();

        qsos.Should().HaveCount(1);
        qsos[0].AdifExtra.Should().NotBeNull();
        qsos[0].AdifExtra!.Contains("age").Should().BeTrue();
        qsos[0].AdifExtra!["age"].BsonType.Should().Be(MongoDB.Bson.BsonType.String);
        qsos[0].AdifExtra!["age"].AsString.Should().Be("42");
        qsos[0].AdifExtra!["custom_num"].AsString.Should().Be("123");
        qsos[0].AdifExtra!["iota"].AsString.Should().Be("NA-001");
    }

    [Fact]
    public void ParseAdif_MixedTypeExtraFields_AllNormalizedToStrings()
    {
        // This test validates the fix for the BSON type cast error
        // All extra fields should be BsonString regardless of their apparent type
        var adif = "<CALL:5>W1AW <QSO_DATE:8>20240101 <TIME_ON:4>1234 <BAND:3>20m <MODE:3>SSB " +
                   "<BOOL_FIELD:1>Y <NUM_FIELD:3>100 <TEXT_FIELD:4>Test <EMPTY_FIELD:0> <EOR>";

        var qsos = _service.ParseAdif(adif).ToList();

        qsos.Should().HaveCount(1);
        var extra = qsos[0].AdifExtra!;

        // All fields should be BsonString type
        extra["bool_field"].BsonType.Should().Be(MongoDB.Bson.BsonType.String);
        extra["num_field"].BsonType.Should().Be(MongoDB.Bson.BsonType.String);
        extra["text_field"].BsonType.Should().Be(MongoDB.Bson.BsonType.String);

        // Verify values are preserved correctly
        extra["bool_field"].AsString.Should().Be("Y");
        extra["num_field"].AsString.Should().Be("100");
        extra["text_field"].AsString.Should().Be("Test");
    }

    #endregion

    #region ExportToAdif

    [Fact]
    public void ExportToAdif_SingleQso_ProducesValidAdif()
    {
        var qso = CreateTestQso("W1AW", "20m", "SSB", new DateTime(2024, 1, 15, 14, 30, 0, DateTimeKind.Utc));

        var adif = _service.ExportToAdif(new[] { qso });

        adif.Should().Contain("<CALL:4>W1AW");
        adif.Should().Contain("<BAND:3>20m");
        adif.Should().Contain("<MODE:3>SSB");
        adif.Should().Contain("<QSO_DATE:8>20240115");
        adif.Should().Contain("<EOR>");
        adif.Should().Contain("<EOH>");
        adif.Should().Contain("ADIF_VER");
        adif.Should().Contain("PROGRAMID");
    }

    [Fact]
    public void ExportToAdif_WithStationCallsign_IncludesIt()
    {
        var qso = CreateTestQso("W1AW", "20m", "SSB", new DateTime(2024, 1, 15, 14, 30, 0, DateTimeKind.Utc));

        var adif = _service.ExportToAdif(new[] { qso }, "EI2KK");

        adif.Should().Contain("STATION_CALLSIGN");
        adif.Should().Contain("EI2KK");
    }

    [Fact]
    public void ExportToAdif_EmptyCollection_ProducesHeaderOnly()
    {
        var adif = _service.ExportToAdif(Array.Empty<Qso>());

        adif.Should().Contain("<EOH>");
        adif.Should().NotContain("<EOR>");
    }

    [Fact]
    public void ExportToAdif_MultipleQsos_ExportsAll()
    {
        var qsos = new[]
        {
            CreateTestQso("W1AW", "20m", "SSB", new DateTime(2024, 1, 15, 14, 30, 0, DateTimeKind.Utc)),
            CreateTestQso("VE3ABC", "40m", "CW", new DateTime(2024, 1, 16, 10, 0, 0, DateTimeKind.Utc)),
        };

        var adif = _service.ExportToAdif(qsos);

        adif.Should().Contain("<CALL:4>W1AW");
        adif.Should().Contain("<CALL:6>VE3ABC");
        // Two EOR markers
        adif.Split("<EOR>", StringSplitOptions.None).Length.Should().Be(3);
    }

    [Fact]
    public void ExportToAdif_WithFrequency_ExportsInMhz()
    {
        var qso = CreateTestQso("W1AW", "20m", "SSB", new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        qso.Frequency = 14200.0; // kHz

        var adif = _service.ExportToAdif(new[] { qso });

        // Frequency should be in MHz in ADIF: 14200 kHz / 1000 = 14.2 MHz
        adif.Should().Contain("<FREQ:");
        adif.Should().Contain("14.200000");
    }

    #endregion

    #region Roundtrip Tests

    [Fact]
    public void ParseAndExport_Roundtrip_PreservesData()
    {
        var originalAdif = @"<ADIF_VER:5>3.1.4
<PROGRAMID:6>Log4YM
<EOH>
<CALL:5>W1AW <QSO_DATE:8>20240115 <TIME_ON:4>1430 <BAND:3>20m <MODE:3>SSB <RST_SENT:2>59 <RST_RCVD:2>57 <NAME:4>John <GRIDSQUARE:6>FN31pr <COUNTRY:13>United States <EOR>";

        var parsed = _service.ParseAdif(originalAdif).ToList();
        parsed.Should().HaveCount(1);

        var reExported = _service.ExportToAdif(parsed);

        reExported.Should().Contain("<CALL:4>W1AW");
        reExported.Should().Contain("<QSO_DATE:8>20240115");
        reExported.Should().Contain("<BAND:3>20m");
        reExported.Should().Contain("<MODE:3>SSB");
        reExported.Should().Contain("<RST_SENT:2>59");
        reExported.Should().Contain("<RST_RCVD:2>57");
        reExported.Should().Contain("<NAME:4>John");
        reExported.Should().Contain("<GRIDSQUARE:6>FN31pr");
        reExported.Should().Contain("<COUNTRY:13>United States");
    }

    [Fact]
    public void ParseAndExport_Roundtrip_MultipleRecords()
    {
        var originalAdif = @"<EOH>
<CALL:5>W1AW <QSO_DATE:8>20240115 <TIME_ON:4>1430 <BAND:3>20m <MODE:3>SSB <EOR>
<CALL:6>VE3ABC <QSO_DATE:8>20240116 <TIME_ON:4>1000 <BAND:3>40m <MODE:2>CW <EOR>
<CALL:5>JA1XX <QSO_DATE:8>20240117 <TIME_ON:4>0530 <BAND:3>15m <MODE:3>FT8 <EOR>";

        var parsed = _service.ParseAdif(originalAdif).ToList();
        parsed.Should().HaveCount(3);

        var reExported = _service.ExportToAdif(parsed);

        // Re-parse the export
        var reParsed = _service.ParseAdif(reExported).ToList();
        reParsed.Should().HaveCount(3);
        reParsed[0].Callsign.Should().Be("W1AW");
        reParsed[1].Callsign.Should().Be("VE3ABC");
        reParsed[2].Callsign.Should().Be("JA1XX");
    }

    #endregion

    #region ParseAdif - Case Insensitivity

    [Fact]
    public void ParseAdif_UppercaseFieldNames_ParsesCorrectly()
    {
        var adif = "<CALL:5>W1AW <QSO_DATE:8>20240101 <TIME_ON:4>1234 <BAND:3>20m <MODE:3>SSB <EOR>";

        var qsos = _service.ParseAdif(adif).ToList();
        qsos.Should().HaveCount(1);
    }

    [Fact]
    public void ParseAdif_LowercaseFieldNames_ParsesCorrectly()
    {
        var adif = "<call:5>W1AW <qso_date:8>20240101 <time_on:4>1234 <band:3>20m <mode:3>SSB <eor>";

        var qsos = _service.ParseAdif(adif).ToList();
        qsos.Should().HaveCount(1);
        qsos[0].Callsign.Should().Be("W1AW");
    }

    [Fact]
    public void ParseAdif_MixedCaseFieldNames_ParsesCorrectly()
    {
        var adif = "<Call:5>W1AW <Qso_Date:8>20240101 <Time_On:4>1234 <Band:3>20m <Mode:3>SSB <Eor>";

        var qsos = _service.ParseAdif(adif).ToList();
        qsos.Should().HaveCount(1);
    }

    #endregion

    #region ParseAdif - Stream Overload

    [Fact]
    public void ParseAdif_FromStream_ParsesCorrectly()
    {
        var adif = "<CALL:5>W1AW <QSO_DATE:8>20240101 <TIME_ON:4>1234 <BAND:3>20m <MODE:3>SSB <EOR>";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(adif));

        var qsos = _service.ParseAdif(stream).ToList();

        qsos.Should().HaveCount(1);
        qsos[0].Callsign.Should().Be("W1AW");
    }

    #endregion

    #region Helpers

    private static Qso CreateTestQso(string callsign, string band, string mode, DateTime qsoDate)
    {
        return new Qso
        {
            Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
            Callsign = callsign,
            Band = band,
            Mode = mode,
            QsoDate = qsoDate,
            TimeOn = qsoDate.ToString("HHmm"),
            Station = new StationInfo()
        };
    }

    #endregion
}
