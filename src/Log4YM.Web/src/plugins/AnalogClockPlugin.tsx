import { useState, useEffect, useRef } from 'react';
import { Clock, Sun, Moon } from 'lucide-react';
import { GlassPanel } from '../components/GlassPanel';
import { useAppStore } from '../store/appStore';
import { gridToLatLon } from '../utils/maidenhead';
import { calculateSunTimes, formatTime } from '../utils/sunTimes';

const WEEKDAYS = ['SUN', 'MON', 'TUE', 'WED', 'THU', 'FRI', 'SAT'];
const MONTHS = ['JAN', 'FEB', 'MAR', 'APR', 'MAY', 'JUN', 'JUL', 'AUG', 'SEP', 'OCT', 'NOV', 'DEC'];

// SVG viewBox is fixed; we scale via the viewBox to fill the container
const VB = 200; // viewBox size (square)
const CX = VB / 2; // center X
const CY = VB / 2; // center Y
const R = 85; // clock radius within the 200x200 viewBox

export function AnalogClockPlugin() {
  const [time, setTime] = useState(new Date());
  const containerRef = useRef<HTMLDivElement>(null);
  const { stationGrid } = useAppStore();

  // Update time every second
  useEffect(() => {
    const interval = setInterval(() => {
      setTime(new Date());
    }, 1000);
    return () => clearInterval(interval);
  }, []);

  // Calculate sunrise/sunset times
  const sunTimes = (() => {
    if (!stationGrid) return null;
    const coords = gridToLatLon(stationGrid);
    if (!coords) return null;
    return calculateSunTimes(coords.lat, coords.lon, time);
  })();

  // Calculate hand angles
  const hours = time.getHours();
  const minutes = time.getMinutes();
  const seconds = time.getSeconds();

  const secondAngle = (seconds / 60) * 360;
  const minuteAngle = (minutes / 60) * 360 + (seconds / 60) * 6;
  const hourAngle = ((hours % 12) / 12) * 360 + (minutes / 60) * 30;

  // Hand endpoint helper
  const handEnd = (angle: number, length: number) => {
    const rad = (angle - 90) * (Math.PI / 180);
    return {
      x: CX + length * Math.cos(rad),
      y: CY + length * Math.sin(rad),
    };
  };

  const hourHand = handEnd(hourAngle, R * 0.5);
  const minuteHand = handEnd(minuteAngle, R * 0.7);
  const secondHand = handEnd(secondAngle, R * 0.85);

  // Date info
  const dayOfWeek = WEEKDAYS[time.getDay()];
  const month = MONTHS[time.getMonth()];
  const date = time.getDate();

  // Tick marks
  const ticks = [];
  for (let i = 0; i < 60; i++) {
    const angle = (i / 60) * 360;
    const isMajor = i % 5 === 0;
    const rad = (angle - 90) * (Math.PI / 180);
    const outerR = R * 0.95;
    const innerR = isMajor ? R * 0.82 : R * 0.88;

    ticks.push(
      <line
        key={i}
        x1={CX + outerR * Math.cos(rad)}
        y1={CY + outerR * Math.sin(rad)}
        x2={CX + innerR * Math.cos(rad)}
        y2={CY + innerR * Math.sin(rad)}
        stroke={isMajor ? '#a0b0c0' : '#5a7090'}
        strokeWidth={isMajor ? 1.5 : 0.5}
        strokeLinecap="round"
      />
    );
  }

  // Hour numbers
  const hourNumbers = [];
  for (let i = 1; i <= 12; i++) {
    const angle = (i / 12) * 360;
    const rad = (angle - 90) * (Math.PI / 180);
    const numR = R * 0.7;
    hourNumbers.push(
      <text
        key={i}
        x={CX + numR * Math.cos(rad)}
        y={CY + numR * Math.sin(rad)}
        textAnchor="middle"
        dominantBaseline="central"
        fill="#a0b0c0"
        fontSize={R * 0.14}
        fontWeight="500"
        fontFamily="Orbitron, system-ui, sans-serif"
      >
        {i}
      </text>
    );
  }

  return (
    <GlassPanel
      title="Analog Clock"
      icon={<Clock className="w-5 h-5" />}
    >
      <div ref={containerRef} className="w-full h-full flex flex-col items-center justify-center p-2 select-none">
        {/* Date info above clock */}
        <div className="flex w-full justify-between px-4 pb-1 shrink-0">
          <span className="text-xs font-display font-semibold text-dark-300 tracking-wider">{dayOfWeek}</span>
          <span className="text-xs font-display font-semibold text-dark-300 tracking-wider">{month} {date}</span>
        </div>

        {/* Clock face — SVG uses viewBox so it scales to fill available space */}
        <div className="flex-1 w-full min-h-0">
          <svg
            viewBox={`0 0 ${VB} ${VB}`}
            width="100%"
            height="100%"
            preserveAspectRatio="xMidYMid meet"
          >
            {/* Clock circle */}
            <circle
              cx={CX}
              cy={CY}
              r={R}
              fill="#0a0e14"
              stroke="#5a7090"
              strokeWidth={1.5}
            />

            {/* Tick marks */}
            {ticks}

            {/* Hour numbers */}
            {hourNumbers}

            {/* LOCAL label */}
            <text
              x={CX}
              y={CY - R * 0.35}
              textAnchor="middle"
              fill="#5a7090"
              fontSize={R * 0.09}
              fontWeight="400"
              letterSpacing="0.1em"
              fontFamily="Space Grotesk, system-ui, sans-serif"
            >
              LOCAL
            </text>

            {/* Hour hand */}
            <line
              x1={CX}
              y1={CY}
              x2={hourHand.x}
              y2={hourHand.y}
              stroke="#ffb432"
              strokeWidth={3}
              strokeLinecap="round"
            />

            {/* Minute hand */}
            <line
              x1={CX}
              y1={CY}
              x2={minuteHand.x}
              y2={minuteHand.y}
              stroke="#ffb432"
              strokeWidth={2}
              strokeLinecap="round"
            />

            {/* Second hand */}
            <line
              x1={CX}
              y1={CY}
              x2={secondHand.x}
              y2={secondHand.y}
              stroke="#ff4466"
              strokeWidth={0.8}
              strokeLinecap="round"
            />

            {/* Center dot */}
            <circle
              cx={CX}
              cy={CY}
              r={3}
              fill="#ffb432"
            />
            <circle
              cx={CX}
              cy={CY}
              r={1.5}
              fill="#ff4466"
            />
          </svg>
        </div>

        {/* Sunrise/Sunset info below clock */}
        <div className="flex w-full justify-between px-4 pt-1 shrink-0">
          <div className="flex items-center gap-1.5 text-xs text-dark-200">
            <Sun className="w-3.5 h-3.5 text-accent-primary" />
            <span className="font-mono">
              {sunTimes ? formatTime(sunTimes.sunrise) : '--:--'}
            </span>
          </div>
          <div className="flex items-center gap-1.5 text-xs text-dark-200">
            <Moon className="w-3.5 h-3.5 text-accent-secondary" />
            <span className="font-mono">
              {sunTimes ? formatTime(sunTimes.sunset) : '--:--'}
            </span>
          </div>
        </div>
      </div>
    </GlassPanel>
  );
}
