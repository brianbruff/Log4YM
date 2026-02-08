import * as satellite from 'satellite.js';

// Common amateur radio satellites with their NORAD catalog numbers
export const AMATEUR_SATELLITES = {
  'ISS': 25544,
  'AO-91': 43017,
  'AO-92': 43137,
  'SO-50': 27607,
  'PO-101': 43678,
  'RS-44': 44909,
  'IO-117': 52934,
  'TEVEL-1': 50988,
  'TEVEL-2': 50989,
  'TEVEL-3': 50990,
  'TEVEL-4': 50991,
  'TEVEL-5': 50992,
  'TEVEL-6': 50993,
  'TEVEL-7': 50994,
  'TEVEL-8': 50995,
};

export interface SatellitePosition {
  name: string;
  latitude: number;
  longitude: number;
  altitude: number; // km above Earth
  velocity: number; // km/s
  footprintRadius: number; // km (coverage area)
  azimuth?: number; // degrees from observer
  elevation?: number; // degrees from observer
  range?: number; // km from observer
  eclipsed?: boolean; // Is satellite in Earth's shadow
}

export interface SatelliteTLE {
  name: string;
  line1: string;
  line2: string;
}

// Calculate satellite position from TLE at given time
export function calculateSatellitePosition(
  tle: SatelliteTLE,
  date: Date,
  observerLat?: number,
  observerLon?: number,
  observerAlt?: number // km
): SatellitePosition | null {
  try {
    const satrec = satellite.twoline2satrec(tle.line1, tle.line2);
    const positionAndVelocity = satellite.propagate(satrec, date);

    if (
      typeof positionAndVelocity.position === 'boolean' ||
      typeof positionAndVelocity.velocity === 'boolean'
    ) {
      return null;
    }

    const positionEci = positionAndVelocity.position;
    const velocityEci = positionAndVelocity.velocity;

    // Convert ECI to geodetic coordinates
    const gmst = satellite.gstime(date);
    const positionGd = satellite.eciToGeodetic(positionEci, gmst);

    const latitude = satellite.degreesLat(positionGd.latitude);
    const longitude = satellite.degreesLong(positionGd.longitude);
    const altitude = positionGd.height;

    // Calculate velocity magnitude
    const velocity = Math.sqrt(
      velocityEci.x * velocityEci.x +
      velocityEci.y * velocityEci.y +
      velocityEci.z * velocityEci.z
    );

    // Calculate footprint radius (horizon circle)
    // Using simple geometric formula: radius = sqrt(h * (2*R + h))
    // where h is altitude, R is Earth radius (6371 km)
    const earthRadius = 6371;
    const footprintRadius = Math.sqrt(altitude * (2 * earthRadius + altitude));

    // Calculate observer-relative position if observer location provided
    let azimuth: number | undefined;
    let elevation: number | undefined;
    let range: number | undefined;

    if (observerLat !== undefined && observerLon !== undefined) {
      const observerAltKm = observerAlt ?? 0;
      const observerGd: satellite.GeodeticLocation = {
        latitude: observerLat * (Math.PI / 180),
        longitude: observerLon * (Math.PI / 180),
        height: observerAltKm,
      };

      const positionEcf = satellite.eciToEcf(positionEci, gmst);
      const lookAngles = satellite.ecfToLookAngles(observerGd, positionEcf);

      azimuth = lookAngles.azimuth * (180 / Math.PI);
      elevation = lookAngles.elevation * (180 / Math.PI);
      range = lookAngles.rangeSat;
    }

    // Determine if satellite is eclipsed (in Earth's shadow)
    const eclipsed = isSatelliteEclipsed(positionEci, date);

    return {
      name: tle.name,
      latitude,
      longitude,
      altitude,
      velocity,
      footprintRadius,
      azimuth,
      elevation,
      range,
      eclipsed,
    };
  } catch (error) {
    console.error(`Error calculating position for ${tle.name}:`, error);
    return null;
  }
}

// Check if satellite is in Earth's shadow (eclipsed)
function isSatelliteEclipsed(
  positionEci: satellite.EciVec3<number>,
  date: Date
): boolean {
  // Get sun position in ECI coordinates
  const sunEci = getSunPositionEci(date);

  // Vector from Earth center to satellite
  const satVector = {
    x: positionEci.x,
    y: positionEci.y,
    z: positionEci.z,
  };

  // Vector from Earth center to sun (normalized)
  const sunMag = Math.sqrt(sunEci.x * sunEci.x + sunEci.y * sunEci.y + sunEci.z * sunEci.z);
  const sunUnit = {
    x: sunEci.x / sunMag,
    y: sunEci.y / sunMag,
    z: sunEci.z / sunMag,
  };

  // Dot product to check if satellite is on night side
  const dotProduct = satVector.x * sunUnit.x + satVector.y * sunUnit.y + satVector.z * sunUnit.z;

  if (dotProduct < 0) {
    // Satellite is on night side, check if it's in umbra
    const satMag = Math.sqrt(satVector.x * satVector.x + satVector.y * satVector.y + satVector.z * satVector.z);
    const earthRadius = 6371; // km

    // Simple umbra test: if satellite is close enough to anti-sun line
    return satMag < earthRadius * 2;
  }

  return false;
}

// Simplified sun position calculation in ECI coordinates
function getSunPositionEci(date: Date): satellite.EciVec3<number> {
  // This is a simplified calculation - for production use consider more accurate ephemeris
  const jd = getJulianDate(date);
  const n = jd - 2451545.0;
  const L = (280.460 + 0.9856474 * n) % 360;
  const g = ((357.528 + 0.9856003 * n) % 360) * Math.PI / 180;
  const lambda = (L + 1.915 * Math.sin(g) + 0.020 * Math.sin(2 * g)) * Math.PI / 180;

  const AU = 149597870.7; // km
  const R = 1.00014 - 0.01671 * Math.cos(g) - 0.00014 * Math.cos(2 * g);
  const distance = R * AU;

  return {
    x: distance * Math.cos(lambda),
    y: distance * Math.sin(lambda),
    z: 0,
  };
}

// Calculate Julian Date from JavaScript Date
function getJulianDate(date: Date): number {
  return date.getTime() / 86400000 + 2440587.5;
}

// Calculate orbital track points for visualization
export function calculateOrbitTrack(
  tle: SatelliteTLE,
  startDate: Date,
  durationMinutes: number,
  stepMinutes: number = 1
): Array<{ lat: number; lon: number }> {
  const points: Array<{ lat: number; lon: number }> = [];
  const steps = Math.floor(durationMinutes / stepMinutes);

  for (let i = 0; i <= steps; i++) {
    const date = new Date(startDate.getTime() + i * stepMinutes * 60000);
    const position = calculateSatellitePosition(tle, date);

    if (position) {
      points.push({
        lat: position.latitude,
        lon: position.longitude,
      });
    }
  }

  return points;
}

// Fetch TLE data from Celestrak or other source
export async function fetchTLEData(satellites: string[]): Promise<Map<string, SatelliteTLE>> {
  const tleMap = new Map<string, SatelliteTLE>();

  try {
    // Use the backend API endpoint to fetch TLE data
    const response = await fetch('/api/satellites/tle', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ satellites }),
    });

    if (!response.ok) {
      throw new Error('Failed to fetch TLE data');
    }

    const data = await response.json();

    // Parse TLE data into map
    for (const [name, tle] of Object.entries(data)) {
      tleMap.set(name, tle as SatelliteTLE);
    }

    return tleMap;
  } catch (error) {
    console.error('Error fetching TLE data:', error);
    return tleMap;
  }
}
