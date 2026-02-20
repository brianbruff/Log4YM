using FluentAssertions;
using Log4YM.Server.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using Xunit;

namespace Log4YM.Server.Tests.Tests.Services;

[Trait("Category", "Unit")]
public class SpaceWeatherServiceTests
{
    private readonly Mock<ILogger<SpaceWeatherService>> _loggerMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;

    public SpaceWeatherServiceTests()
    {
        _loggerMock = new Mock<ILogger<SpaceWeatherService>>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
    }

    [Fact]
    public async Task GetCurrentAsync_ParsesNOAATimeTag_Correctly()
    {
        // Arrange
        var expectedDate = new DateTime(2025, 2, 19, 0, 0, 0, DateTimeKind.Unspecified);
        var noaaResponse = """
            [
                {
                    "time-tag": "2025-02-19",
                    "ssn": 125.5,
                    "f10.7": 180.3
                }
            ]
            """;

        var kIndexResponse = """
            [
                {
                    "time_tag": "2025-02-19T12:00:00",
                    "kp_index": 3.0
                }
            ]
            """;

        SetupHttpResponse("https://services.swpc.noaa.gov/json/solar-cycle/observed-solar-cycle-indices.json", noaaResponse);
        SetupHttpResponse("https://services.swpc.noaa.gov/json/planetary_k_index_1m.json", kIndexResponse);

        var service = new SpaceWeatherService(_httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var result = await service.GetCurrentAsync();

        // Assert
        result.SolarFluxIndex.Should().Be(180);
        result.KIndex.Should().Be(3);
        result.SunspotNumber.Should().Be(126);
        result.Timestamp.Date.Should().Be(expectedDate.Date);
    }

    [Fact]
    public async Task GetCurrentAsync_HandlesNOAATimeTagWithTime_Correctly()
    {
        // Arrange
        var expectedDateTime = new DateTime(2025, 2, 19, 12, 30, 0, DateTimeKind.Unspecified);
        var noaaResponse = """
            [
                {
                    "time-tag": "2025-02-19T12:30:00",
                    "ssn": 100.0,
                    "f10.7": 150.0
                }
            ]
            """;

        var kIndexResponse = """
            [
                {
                    "time_tag": "2025-02-19T12:00:00",
                    "kp_index": 2.0
                }
            ]
            """;

        SetupHttpResponse("https://services.swpc.noaa.gov/json/solar-cycle/observed-solar-cycle-indices.json", noaaResponse);
        SetupHttpResponse("https://services.swpc.noaa.gov/json/planetary_k_index_1m.json", kIndexResponse);

        var service = new SpaceWeatherService(_httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var result = await service.GetCurrentAsync();

        // Assert
        result.Timestamp.Should().Be(expectedDateTime);
    }

    [Fact]
    public async Task GetCurrentAsync_FallsBackToCurrentTime_WhenTimeTagIsInvalid()
    {
        // Arrange
        var beforeCall = DateTime.UtcNow;
        var noaaResponse = """
            [
                {
                    "time-tag": "invalid-date",
                    "ssn": 100.0,
                    "f10.7": 150.0
                }
            ]
            """;

        var kIndexResponse = """
            [
                {
                    "time_tag": "2025-02-19T12:00:00",
                    "kp_index": 2.0
                }
            ]
            """;

        SetupHttpResponse("https://services.swpc.noaa.gov/json/solar-cycle/observed-solar-cycle-indices.json", noaaResponse);
        SetupHttpResponse("https://services.swpc.noaa.gov/json/planetary_k_index_1m.json", kIndexResponse);

        var service = new SpaceWeatherService(_httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var result = await service.GetCurrentAsync();
        var afterCall = DateTime.UtcNow;

        // Assert
        result.Timestamp.Should().BeOnOrAfter(beforeCall).And.BeOnOrBefore(afterCall);

        // Verify warning was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to parse NOAA TimeTag")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetCurrentAsync_CachesResults_For15Minutes()
    {
        // Arrange
        var noaaResponse = """
            [
                {
                    "time-tag": "2025-02-19",
                    "ssn": 125.5,
                    "f10.7": 180.3
                }
            ]
            """;

        var kIndexResponse = """
            [
                {
                    "time_tag": "2025-02-19T12:00:00",
                    "kp_index": 3.0
                }
            ]
            """;

        SetupHttpResponse("https://services.swpc.noaa.gov/json/solar-cycle/observed-solar-cycle-indices.json", noaaResponse);
        SetupHttpResponse("https://services.swpc.noaa.gov/json/planetary_k_index_1m.json", kIndexResponse);

        var service = new SpaceWeatherService(_httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var result1 = await service.GetCurrentAsync();
        var result2 = await service.GetCurrentAsync();

        // Assert
        result1.Should().Be(result2);

        // Verify HTTP was only called once (cached on second call)
        _httpMessageHandlerMock.Protected()
            .Verify("SendAsync", Times.Exactly(2), // Once for NOAA solar, once for K-index
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
    }

    private void SetupHttpResponse(string url, string responseContent)
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString() == url),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });
    }
}
