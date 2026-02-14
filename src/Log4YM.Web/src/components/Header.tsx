import { useEffect, useState } from 'react';
import { Settings, Wind } from 'lucide-react';
import { useSettingsStore } from '../store/settingsStore';
import { api, SpaceWeatherData } from '../api/client';

export function Header() {
  const { openSettings, settings, updateHeaderSettings } = useSettingsStore();
  const [currentTime, setCurrentTime] = useState(new Date());
  const [spaceWeather, setSpaceWeather] = useState<SpaceWeatherData | null>(null);
  const [weather, setWeather] = useState<WeatherData | null>(null);

  const { timeFormat, showWeather } = settings.header;
  const { callsign } = settings.station;

  // Update time every second
  useEffect(() => {
    const timer = setInterval(() => {
      setCurrentTime(new Date());
    }, 1000);

    return () => clearInterval(timer);
  }, []);

  // Fetch space weather data every 15 minutes
  useEffect(() => {
    const fetchSpaceWeather = async () => {
      try {
        const data = await api.getSpaceWeather();
        setSpaceWeather(data);
      } catch (error) {
        console.error('Failed to fetch space weather:', error);
      }
    };

    fetchSpaceWeather();
    const interval = setInterval(fetchSpaceWeather, 15 * 60 * 1000); // 15 minutes

    return () => clearInterval(interval);
  }, []);

  // Fetch local weather if location is set
  useEffect(() => {
    if (!showWeather || !settings.station.latitude || !settings.station.longitude) {
      return;
    }

    const fetchWeather = async () => {
      try {
        // Using Open-Meteo free API (no API key required)
        const { latitude, longitude } = settings.station;
        const url = `https://api.open-meteo.com/v1/forecast?latitude=${latitude}&longitude=${longitude}&current=temperature_2m,wind_speed_10m,weather_code&temperature_unit=fahrenheit&wind_speed_unit=mph`;

        const response = await fetch(url);
        const data = await response.json();

        if (data.current) {
          setWeather({
            temperature: Math.round(data.current.temperature_2m),
            temperatureC: Math.round((data.current.temperature_2m - 32) * 5 / 9),
            windSpeed: Math.round(data.current.wind_speed_10m),
            weatherCode: data.current.weather_code,
          });
        }
      } catch (error) {
        console.error('Failed to fetch weather:', error);
      }
    };

    fetchWeather();
    const interval = setInterval(fetchWeather, 30 * 60 * 1000); // 30 minutes

    return () => clearInterval(interval);
  }, [showWeather, settings.station.latitude, settings.station.longitude]);

  const toggleTimeFormat = () => {
    updateHeaderSettings({ timeFormat: timeFormat === '12h' ? '24h' : '12h' });
  };

  const formatTime = (date: Date, use24h: boolean) => {
    const hours = date.getHours();
    const minutes = date.getMinutes();
    const seconds = date.getSeconds();

    if (use24h) {
      return `${hours.toString().padStart(2, '0')}:${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`;
    } else {
      const period = hours >= 12 ? 'PM' : 'AM';
      const hours12 = hours % 12 || 12;
      return `${hours12}:${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')} ${period}`;
    }
  };

  const formatDate = (date: Date) => {
    const days = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
    const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    return `${days[date.getDay()]} ${months[date.getMonth()]} ${date.getDate()}`;
  };

  const getWeatherIcon = (code: number) => {
    // WMO Weather interpretation codes
    if (code === 0) return '☀️';
    if (code <= 3) return '⛅';
    if (code <= 48) return '☁️';
    if (code <= 67) return '🌧️';
    if (code <= 77) return '🌨️';
    if (code <= 82) return '🌧️';
    if (code <= 86) return '🌨️';
    if (code <= 99) return '⛈️';
    return '☁️';
  };

  // Calculate K-Index color (green normally, red at >=4 for storm warning)
  const getKIndexColor = (kIndex: number) => {
    if (kIndex >= 4) return 'text-red-500';
    return 'text-green-500';
  };

  const utcTime = new Date(currentTime.toLocaleString('en-US', { timeZone: 'UTC' }));
  const baseFontSize = 16;

  return (
    <header
      className="bg-dark-800/90 backdrop-blur-xl border-b border-glass-100 flex items-center justify-between px-4"
      style={{
        minHeight: '56px',
        fontSize: `${baseFontSize}px`
      }}
    >
      {/* Left: Callsign */}
      <div className="flex items-center gap-4">
        <button
          onClick={openSettings}
          className="transition-all duration-200 hover:scale-105"
          title="Open Settings"
        >
          <span
            className="font-bold tracking-wide text-orange-500"
            style={{ fontSize: `${baseFontSize * 1.75}px` }}
          >
            {callsign || 'N0CALL'}
          </span>
        </button>
      </div>

      {/* Center: UTC Time and Local Time */}
      <div className="flex items-center gap-8">
        {/* UTC Time */}
        <div className="text-center">
          <div
            className="font-mono font-bold text-cyan-400 tabular-nums"
            style={{ fontSize: `${baseFontSize * 2}px`, lineHeight: '1.2' }}
          >
            {formatTime(utcTime, true)}
          </div>
          <div
            className="text-cyan-400/70 text-xs uppercase tracking-wide"
            style={{ fontSize: `${baseFontSize * 0.625}px` }}
          >
            {formatDate(utcTime)} UTC
          </div>
        </div>

        {/* Local Time */}
        <button
          onClick={toggleTimeFormat}
          className="text-center transition-all duration-200 hover:scale-105"
          title="Click to toggle 12h/24h format"
        >
          <div
            className="font-mono font-bold text-amber-400 tabular-nums"
            style={{ fontSize: `${baseFontSize * 2}px`, lineHeight: '1.2' }}
          >
            {formatTime(currentTime, timeFormat === '24h')}
          </div>
          <div
            className="text-amber-400/70 text-xs uppercase tracking-wide"
            style={{ fontSize: `${baseFontSize * 0.625}px` }}
          >
            {formatDate(currentTime)} Local
          </div>
        </button>

        {/* Weather (if available) */}
        {showWeather && weather && (
          <div className="flex items-center gap-2 px-3 py-1 bg-dark-700/50 rounded-lg">
            <span style={{ fontSize: `${baseFontSize * 1.25}px` }}>
              {getWeatherIcon(weather.weatherCode)}
            </span>
            <div className="text-left">
              <div
                className="font-bold text-gray-200"
                style={{ fontSize: `${baseFontSize * 0.875}px` }}
              >
                {weather.temperature}°F ({weather.temperatureC}°C)
              </div>
              <div
                className="text-gray-400 flex items-center gap-1"
                style={{ fontSize: `${baseFontSize * 0.625}px` }}
              >
                <Wind className="w-3 h-3" />
                {weather.windSpeed} mph
              </div>
            </div>
          </div>
        )}
      </div>

      {/* Right: Space Weather Indices */}
      <div className="flex items-center gap-6">
        {spaceWeather && (
          <>
            {/* SFI (Solar Flux Index) */}
            <div className="text-center">
              <div
                className="font-bold text-amber-400 tabular-nums"
                style={{ fontSize: `${baseFontSize * 1.25}px` }}
              >
                {spaceWeather.solarFluxIndex}
              </div>
              <div
                className="text-gray-400 text-xs uppercase tracking-wide"
                style={{ fontSize: `${baseFontSize * 0.625}px` }}
              >
                SFI
              </div>
            </div>

            {/* K-Index */}
            <div className="text-center">
              <div
                className={`font-bold tabular-nums ${getKIndexColor(spaceWeather.kIndex)}`}
                style={{ fontSize: `${baseFontSize * 1.25}px` }}
              >
                {spaceWeather.kIndex}
              </div>
              <div
                className="text-gray-400 text-xs uppercase tracking-wide"
                style={{ fontSize: `${baseFontSize * 0.625}px` }}
              >
                K-Index
              </div>
            </div>

            {/* SSN (Sunspot Number) */}
            <div className="text-center">
              <div
                className="font-bold text-cyan-400 tabular-nums"
                style={{ fontSize: `${baseFontSize * 1.25}px` }}
              >
                {spaceWeather.sunspotNumber}
              </div>
              <div
                className="text-gray-400 text-xs uppercase tracking-wide"
                style={{ fontSize: `${baseFontSize * 0.625}px` }}
              >
                SSN
              </div>
            </div>
          </>
        )}

        {/* Settings Button */}
        <button onClick={openSettings} className="glass-button p-2" title="Settings">
          <Settings className="w-4 h-4" />
        </button>
      </div>
    </header>
  );
}

interface WeatherData {
  temperature: number;
  temperatureC: number;
  windSpeed: number;
  weatherCode: number;
}
