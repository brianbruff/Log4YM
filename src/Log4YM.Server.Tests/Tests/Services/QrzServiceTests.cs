using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Log4YM.Contracts.Models;
using Log4YM.Server.Core.Database;
using Log4YM.Server.Services;
using System.Net;
using System.Text;
using Xunit;

namespace Log4YM.Server.Tests.Tests.Services;

[Trait("Category", "Unit")]
public class QrzServiceTests
{
    private readonly Mock<ISettingsRepository> _settingsRepoMock = new();
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();
    private readonly Mock<ILogger<QrzService>> _loggerMock = new();
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock = new();
    private readonly QrzService _service;

    public QrzServiceTests()
    {
        var client = new HttpClient(_httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient("QRZ")).Returns(client);

        _service = new QrzService(
            _settingsRepoMock.Object,
            _httpClientFactoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task UploadQsoAsync_SendsFrequencyInMhzWithDot()
    {
        // Arrange
        var qso = new Qso
        {
            Id = "507f1f77bcf86cd799439011",
            Callsign = "W1AW",
            QsoDate = new DateTime(2024, 1, 1),
            TimeOn = "1234",
            Band = "20m",
            Mode = "SSB",
            Frequency = 14250.5 // kHz
        };

        var settings = new UserSettings
        {
            Qrz = new QrzSettings
            {
                ApiKey = "test-api-key",
                Enabled = true
            }
        };

        _settingsRepoMock.Setup(r => r.GetAsync()).ReturnsAsync(settings);

        string? capturedAdif = null;
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken token) =>
            {
                var content = request.Content as FormUrlEncodedContent;
                var dict = content!.ReadAsStringAsync().Result.Split('&')
                    .Select(p => p.Split('='))
                    .ToDictionary(p => p[0], p => WebUtility.UrlDecode(p[1]));
                
                capturedAdif = dict["ADIF"];

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("RESULT=OK&LOGID=12345")
                };
            });

        // Act
        var result = await _service.UploadQsoAsync(qso);

        // Assert
        result.Success.Should().BeTrue();
        capturedAdif.Should().Contain("<FREQ:9>14.250500");
    }
}
