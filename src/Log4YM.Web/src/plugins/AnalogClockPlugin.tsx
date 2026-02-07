import { useState, useEffect, useRef } from 'react';
import { Clock, Sun, Moon } from 'lucide-react';
import { GlassPanel } from '../components/GlassPanel';
import { useAppStore } from '../store/appStore';
import { gridToLatLon } from '../utils/maidenhead';
import { calculateSunTimes, formatTime } from '../utils/sunTimes';

const WEEKDAYS = ['SUN', 'MON', 'TUE', 'WED', 'THU', 'FRI', 'SAT'];
const MONTHS = ['JAN', 'FEB', 'MAR', 'APR', 'MAY', 'JUN', 'JUL', 'AUG', 'SEP', 'OCT', 'NOV', 'DEC'];

export function AnalogClockPlugin() {
  const [time, setTime] = useState(new Date());
  const [containerSize, setContainerSize] = useState({ width: 0, height: 0 });
  const containerRef = useRef<HTMLDivElement>(null);
  const { stationGrid } = useAppStore();

  // Update time every second
  useEffect(() => {
    const interval = setInterval(() => {
      setTime(new Date());
    }, 1000);
    return () => clearInterval(interval);
  }, []);

  // Observe container size
  useEffect(() => {
    if (!containerRef.current) return;

    const resizeObserver = new ResizeObserver((entries) => {
      for (const entry of entries) {
        const { width, height } = entry.contentRect;
        setContainerSize({ width, height });
      }
    });

    resizeObserver.observe(containerRef.current);
    return () => resizeObserver.disconnect();
  }, []);

  // Calculate sunrise/sunset times
  const sunTimes = (() => {
    if (!stationGrid) return null;

    const coords = gridToLatLon(stationGrid);
    if (!coords) return null;

    return calculateSunTimes(coords.lat, coords.lon, time);
  })();

  // Calculate clock dimensions
  const size = Math.min(containerSize.width, containerSize.height) * 0.8;
  const centerX = containerSize.width / 2;
  const centerY = containerSize.height / 2;
  const radius = size / 2;

  // Calculate hand angles
  const hours = time.getHours();
  const minutes = time.getMinutes();
  const seconds = time.getSeconds();

  const secondAngle = (seconds / 60) * 360;
  const minuteAngle = (minutes / 60) * 360 + (seconds / 60) * 6;
  const hourAngle = ((hours % 12) / 12) * 360 + (minutes / 60) * 30;

  // Calculate hand lengths
  const hourHandLength = radius * 0.5;
  const minuteHandLength = radius * 0.7;
  const secondHandLength = radius * 0.85;

  // Calculate hand positions
  const getHandPosition = (angle: number, length: number) => {
    const rad = (angle - 90) * (Math.PI / 180);
    return {
      x: centerX + length * Math.cos(rad),
      y: centerY + length * Math.sin(rad),
    };
  };

  const hourHand = getHandPosition(hourAngle, hourHandLength);
  const minuteHand = getHandPosition(minuteAngle, minuteHandLength);
  const secondHand = getHandPosition(secondAngle, secondHandLength);

  // Get date info
  const dayOfWeek = WEEKDAYS[time.getDay()];
  const month = MONTHS[time.getMonth()];
  const date = time.getDate();

  // Calculate tick positions
  const renderTicks = () => {
    const ticks = [];
    for (let i = 0; i < 60; i++) {
      const angle = (i / 60) * 360;
      const isMajor = i % 5 === 0;
      const tickLength = isMajor ? radius * 0.1 : radius * 0.05;
      const tickWidth = isMajor ? 2 : 1;
      const startRadius = radius * 0.9;

      const rad = (angle - 90) * (Math.PI / 180);
      const x1 = centerX + startRadius * Math.cos(rad);
      const y1 = centerY + startRadius * Math.sin(rad);
      const x2 = centerX + (startRadius - tickLength) * Math.cos(rad);
      const y2 = centerY + (startRadius - tickLength) * Math.sin(rad);

      ticks.push(
        <line
          key={i}
          x1={x1}
          y1={y1}
          x2={x2}
          y2={y2}
          stroke={isMajor ? '#9ca3af' : '#4b5563'}
          strokeWidth={tickWidth}
          strokeLinecap="round"
        />
      );
    }
    return ticks;
  };

  // Calculate hour number positions
  const renderHourNumbers = () => {
    const numbers = [];
    for (let i = 1; i <= 12; i++) {
      const angle = (i / 12) * 360;
      const rad = (angle - 90) * (Math.PI / 180);
      const numberRadius = radius * 0.75;
      const x = centerX + numberRadius * Math.cos(rad);
      const y = centerY + numberRadius * Math.sin(rad);

      numbers.push(
        <text
          key={i}
          x={x}
          y={y}
          textAnchor="middle"
          dominantBaseline="central"
          fill="#9ca3af"
          fontSize={radius * 0.12}
          fontWeight="500"
          fontFamily="system-ui, -apple-system, sans-serif"
        >
          {i}
        </text>
      );
    }
    return numbers;
  };

  return (
    <GlassPanel
      title="Analog Clock"
      icon={<Clock className="w-5 h-5" />}
    >
      <div ref={containerRef} className="relative w-full h-full flex flex-col items-center justify-center p-4">
        {/* Date info above clock */}
        <div className="absolute top-6 left-0 right-0 flex justify-between px-8">
          <div className="text-sm font-semibold text-gray-300">
            {dayOfWeek}
          </div>
          <div className="text-sm font-semibold text-gray-300">
            {month} {date}
          </div>
        </div>

        {/* Clock face */}
        {size > 0 && (
          <svg
            width={containerSize.width}
            height={containerSize.height * 0.7}
            className="flex-1"
          >
            {/* Clock circle */}
            <circle
              cx={centerX}
              cy={centerY}
              r={radius}
              fill="transparent"
              stroke="#374151"
              strokeWidth={2}
            />

            {/* Hour ticks */}
            {renderTicks()}

            {/* Hour numbers */}
            {renderHourNumbers()}

            {/* LOCAL label */}
            <text
              x={centerX}
              y={centerY - radius * 0.4}
              textAnchor="middle"
              fill="#9ca3af"
              fontSize={radius * 0.08}
              fontWeight="400"
              fontFamily="system-ui, -apple-system, sans-serif"
            >
              LOCAL
            </text>

            {/* Hour hand */}
            <line
              x1={centerX}
              y1={centerY}
              x2={hourHand.x}
              y2={hourHand.y}
              stroke="#d1d5db"
              strokeWidth={radius * 0.04}
              strokeLinecap="round"
            />

            {/* Minute hand */}
            <line
              x1={centerX}
              y1={centerY}
              x2={minuteHand.x}
              y2={minuteHand.y}
              stroke="#d1d5db"
              strokeWidth={radius * 0.03}
              strokeLinecap="round"
            />

            {/* Second hand */}
            <line
              x1={centerX}
              y1={centerY}
              x2={secondHand.x}
              y2={secondHand.y}
              stroke="#ef4444"
              strokeWidth={radius * 0.015}
              strokeLinecap="round"
            />

            {/* Center dot */}
            <circle
              cx={centerX}
              cy={centerY}
              r={radius * 0.05}
              fill="#d1d5db"
            />
          </svg>
        )}

        {/* Sunrise/Sunset info below clock */}
        <div className="absolute bottom-6 left-0 right-0 flex justify-between px-8">
          <div className="flex items-center gap-2 text-sm text-gray-300">
            <Sun className="w-4 h-4 text-yellow-500" />
            <span className="font-mono">
              {sunTimes ? formatTime(sunTimes.sunrise) : '--:--'}
            </span>
          </div>
          <div className="flex items-center gap-2 text-sm text-gray-300">
            <Moon className="w-4 h-4 text-blue-300" />
            <span className="font-mono">
              {sunTimes ? formatTime(sunTimes.sunset) : '--:--'}
            </span>
          </div>
        </div>
      </div>
    </GlassPanel>
  );
}
