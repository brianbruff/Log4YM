/**
 * Solar and lunar position calculations for map overlays
 * Based on astronomical calculations for day/night terminator and gray line visualization
 */

/**
 * Calculate Julian Date from a Date object
 */
function getJulianDate(date: Date): number {
  const time = date.getTime();
  return (time / 86400000) + 2440587.5;
}

/**
 * Calculate Greenwich Mean Sidereal Time
 */
function getGMST(jd: number): number {
  const t = (jd - 2451545.0) / 36525.0;
  let gmst = 280.46061837 + 360.98564736629 * (jd - 2451545.0) + 0.000387933 * t * t - (t * t * t) / 38710000.0;
  return gmst % 360;
}

/**
 * Calculate sun's ecliptic coordinates
 */
function getSunEclipticCoordinates(jd: number): { longitude: number; declination: number } {
  const n = jd - 2451545.0;
  const L = (280.460 + 0.9856474 * n) % 360;
  const g = ((357.528 + 0.9856003 * n) % 360) * Math.PI / 180;

  const lambda = (L + 1.915 * Math.sin(g) + 0.020 * Math.sin(2 * g)) * Math.PI / 180;
  const epsilon = (23.439 - 0.0000004 * n) * Math.PI / 180;

  const declination = Math.asin(Math.sin(epsilon) * Math.sin(lambda));

  return {
    longitude: lambda * 180 / Math.PI,
    declination: declination * 180 / Math.PI
  };
}

/**
 * Calculate sun's subsolar point (latitude and longitude where sun is directly overhead)
 */
export function getSunPosition(date: Date = new Date()): { lat: number; lon: number } {
  const jd = getJulianDate(date);
  const gmst = getGMST(jd);
  const sunCoords = getSunEclipticCoordinates(jd);

  // Convert ecliptic longitude to right ascension
  const epsilon = (23.439 - 0.0000004 * (jd - 2451545.0)) * Math.PI / 180;
  const lambda = sunCoords.longitude * Math.PI / 180;
  const alpha = Math.atan2(Math.cos(epsilon) * Math.sin(lambda), Math.cos(lambda)) * 180 / Math.PI;

  // Calculate subsolar longitude
  let sunLon = alpha - gmst;

  // Normalize to -180 to 180
  while (sunLon > 180) sunLon -= 360;
  while (sunLon < -180) sunLon += 360;

  return {
    lat: sunCoords.declination,
    lon: sunLon
  };
}

/**
 * Calculate moon's approximate position
 * Simplified lunar calculation for educational/visualization purposes
 */
export function getMoonPosition(date: Date = new Date()): { lat: number; lon: number } {
  const jd = getJulianDate(date);
  const T = (jd - 2451545.0) / 36525.0;

  // Mean longitude of the Moon
  const L0 = (218.316 + 481267.881 * T) % 360;

  // Mean anomaly of the Moon
  const M1 = (134.963 + 477198.867 * T) % 360;

  // Moon's argument of latitude
  const F = (93.272 + 483202.018 * T) % 360;

  // Convert to radians
  const M1_rad = M1 * Math.PI / 180;
  const F_rad = F * Math.PI / 180;

  // Calculate ecliptic longitude
  let lambda = L0 + 6.289 * Math.sin(M1_rad);
  lambda = lambda % 360;

  // Calculate ecliptic latitude
  const beta = 5.128 * Math.sin(F_rad);

  // Convert ecliptic to equatorial coordinates
  const epsilon = (23.439 - 0.0000004 * (jd - 2451545.0)) * Math.PI / 180;
  const lambda_rad = lambda * Math.PI / 180;
  const beta_rad = beta * Math.PI / 180;

  const alpha = Math.atan2(
    Math.sin(lambda_rad) * Math.cos(epsilon) - Math.tan(beta_rad) * Math.sin(epsilon),
    Math.cos(lambda_rad)
  );

  const delta = Math.asin(
    Math.sin(beta_rad) * Math.cos(epsilon) + Math.cos(beta_rad) * Math.sin(epsilon) * Math.sin(lambda_rad)
  );

  // Calculate sublunar point
  const gmst = getGMST(jd);
  let moonLon = (alpha * 180 / Math.PI) - gmst;

  // Normalize to -180 to 180
  while (moonLon > 180) moonLon -= 360;
  while (moonLon < -180) moonLon += 360;

  return {
    lat: delta * 180 / Math.PI,
    lon: moonLon
  };
}

/**
 * Calculate solar elevation angle at a given point
 * Positive = above horizon (day), negative = below horizon (night)
 */
export function getSolarElevation(lat: number, lon: number, date: Date = new Date()): number {
  const sunPos = getSunPosition(date);

  // Convert to radians
  const latRad = lat * Math.PI / 180;
  const sunLatRad = sunPos.lat * Math.PI / 180;
  const lonDiff = (lon - sunPos.lon) * Math.PI / 180;

  // Calculate solar elevation using spherical trigonometry
  const sinAlt = Math.sin(latRad) * Math.sin(sunLatRad) +
                 Math.cos(latRad) * Math.cos(sunLatRad) * Math.cos(lonDiff);

  return Math.asin(sinAlt) * 180 / Math.PI;
}

/**
 * Generate day/night terminator line coordinates
 * Returns an array of [lon, lat] points marking the boundary between day and night
 */
export function getDayNightTerminator(date: Date = new Date()): [number, number][] {
  const points: [number, number][] = [];

  // Generate points along the terminator (where solar elevation = 0)
  for (let lon = -180; lon <= 180; lon += 2) {
    // For each longitude, find the latitude where solar elevation is approximately 0
    // This is a simplified approach - we calculate for a grid and interpolate

    let bestLat = 0;
    let minDiff = Infinity;

    // Search for latitude where elevation is closest to 0
    for (let lat = -90; lat <= 90; lat += 0.5) {
      const elevation = getSolarElevation(lat, lon, date);
      const diff = Math.abs(elevation);

      if (diff < minDiff) {
        minDiff = diff;
        bestLat = lat;
      }
    }

    points.push([lon, bestLat]);
  }

  return points;
}

/**
 * Calculate if a point is in daylight
 */
export function isInDaylight(lat: number, lon: number, date: Date = new Date()): boolean {
  const elevation = getSolarElevation(lat, lon, date);
  return elevation > 0;
}

/**
 * Calculate gray line boundaries (civil twilight)
 * Returns two lines: dawn terminator (sun at -6°) and dusk terminator (sun at -6°)
 */
export function getGrayLineBoundaries(date: Date = new Date()): {
  civilDawn: [number, number][];
  civilDusk: [number, number][];
} {
  const civilDawn: [number, number][] = [];

  // Civil twilight occurs when the sun is between 0° and -6° below the horizon
  const civilTwilightAngle = -6;

  for (let lon = -180; lon <= 180; lon += 2) {
    let bestLatDawn = 0;
    let minDiffDawn = Infinity;

    // Search for latitude where elevation is closest to -6° (civil twilight)
    for (let lat = -90; lat <= 90; lat += 0.5) {
      const elevation = getSolarElevation(lat, lon, date);
      const diff = Math.abs(elevation - civilTwilightAngle);

      if (diff < minDiffDawn) {
        minDiffDawn = diff;
        bestLatDawn = lat;
      }
    }

    civilDawn.push([lon, bestLatDawn]);
  }

  // For this simplified implementation, civil dusk is the same line
  // In a more sophisticated version, you'd calculate the opposite side
  return {
    civilDawn,
    civilDusk: civilDawn
  };
}

/**
 * Check if a point is in the gray line (civil twilight zone)
 */
export function isInGrayLine(lat: number, lon: number, date: Date = new Date()): boolean {
  const elevation = getSolarElevation(lat, lon, date);
  return elevation >= -6 && elevation <= 0;
}
