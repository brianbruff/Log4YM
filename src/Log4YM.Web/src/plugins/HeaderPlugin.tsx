import { useEffect, useState } from 'react';
import { useSettingsStore } from '../store/settingsStore';
import { api, SpaceWeatherData } from '../api/client';

interface WeatherData {
  temperature: number;
  temperatureC: number;
  windSpeed: number;
  weatherCode: number;
}

const getWeatherIcon = (code: number) => {
  if (code === 0) return '\u2600\uFE0F';
  if (code <= 3) return '\u26C5';
  if (code <= 48) return '\u2601\uFE0F';
  if (code <= 67) return '\uD83C\uDF27\uFE0F';
  if (code <= 77) return '\uD83C\uDF28\uFE0F';
  if (code <= 82) return '\uD83C\uDF27\uFE0F';
  if (code <= 86) return '\uD83C\uDF28\uFE0F';
  if (code <= 99) return '\u26C8\uFE0F';
  return '\u2601\uFE0F';
};

export function HeaderPlugin() {
  const { settings } = useSettingsStore();
  const [currentTime, setCurrentTime] = useState(new Date());
  const [spaceWeather, setSpaceWeather] = useState<SpaceWeatherData | null>(null);
  const [weather, setWeather] = useState<WeatherData | null>(null);

  const { callsign } = settings.station;
  const { showWeather } = settings.header;

  useEffect(() => {
    const timer = setInterval(() => setCurrentTime(new Date()), 1000);
    return () => clearInterval(timer);
  }, []);

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
    const interval = setInterval(fetchSpaceWeather, 15 * 60 * 1000);
    return () => clearInterval(interval);
  }, []);

  useEffect(() => {
    if (!showWeather || !settings.station.latitude || !settings.station.longitude) return;
    const fetchWeather = async () => {
      try {
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
    const interval = setInterval(fetchWeather, 30 * 60 * 1000);
    return () => clearInterval(interval);
  }, [showWeather, settings.station.latitude, settings.station.longitude]);

  const pad = (n: number) => n.toString().padStart(2, '0');

  const utcHours = currentTime.getUTCHours();
  const utcMinutes = currentTime.getUTCMinutes();
  const utcSeconds = currentTime.getUTCSeconds();
  const utcYear = currentTime.getUTCFullYear();
  const utcMonth = pad(currentTime.getUTCMonth() + 1);
  const utcDay = pad(currentTime.getUTCDate());

  const localHours = currentTime.getHours();
  const localMinutes = currentTime.getMinutes();
  const localSeconds = currentTime.getSeconds();

  const days = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
  const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  const localDateStr = `${days[currentTime.getDay()]}, ${months[currentTime.getMonth()]} ${currentTime.getDate()}`;

  return (
    <div className="header-plugin">
      {/* Callsign + Version */}
      <div className="header-plugin__group">
        <span className="header-plugin__callsign">{callsign || 'N0CALL'}</span>
        <span className="header-plugin__version">v1.0.0</span>
      </div>

      {/* Separator */}
      <div className="header-plugin__sep" />

      {/* UTC Time */}
      <div className="header-plugin__group">
        <span className="header-plugin__label">UTC</span>
        <span className="header-plugin__time">
          {pad(utcHours)}:{pad(utcMinutes)}:{pad(utcSeconds)}
        </span>
        <span className="header-plugin__date">{utcYear}-{utcMonth}-{utcDay}</span>
      </div>

      {/* Separator */}
      <div className="header-plugin__sep" />

      {/* Local Time */}
      <div className="header-plugin__group">
        <span className="header-plugin__label">LOCAL</span>
        <span className="header-plugin__time">
          {pad(localHours)}:{pad(localMinutes)}:{pad(localSeconds)}
        </span>
        <span className="header-plugin__date">{localDateStr}</span>
      </div>

      {/* Separator */}
      <div className="header-plugin__sep" />

      {/* Weather */}
      {showWeather && weather && (
        <>
          <div className="header-plugin__group">
            <span className="header-plugin__weather-icon">{getWeatherIcon(weather.weatherCode)}</span>
            <span className="header-plugin__weather-temp">
              {weather.temperature}&deg;F/{weather.temperatureC}&deg;C
            </span>
          </div>
          <div className="header-plugin__sep" />
        </>
      )}

      {/* Space Weather Indices */}
      {spaceWeather && (
        <div className="header-plugin__group header-plugin__indices">
          <span className="header-plugin__label">SFI</span>
          <span className="header-plugin__value">{spaceWeather.solarFluxIndex}</span>
          <span className="header-plugin__label" style={{ marginLeft: 12 }}>K</span>
          <span className={`header-plugin__value${spaceWeather.kIndex >= 4 ? ' header-plugin__value--danger' : ''}`}>
            {spaceWeather.kIndex}
          </span>
          <span className="header-plugin__label" style={{ marginLeft: 12 }}>SSN</span>
          <span className="header-plugin__value">{spaceWeather.sunspotNumber}</span>
        </div>
      )}
    </div>
  );
}
