/**
 * Calculate sunrise and sunset times for a given location and date
 * Based on simplified sunrise equation
 */

/**
 * Calculate sunrise and sunset times for a given location and date
 *
 * @param lat Latitude in degrees
 * @param lon Longitude in degrees
 * @param date Date to calculate for (defaults to today)
 * @returns Object with sunrise and sunset times, or null if not calculable
 */
export function calculateSunTimes(
  lat: number,
  lon: number,
  date: Date = new Date()
): { sunrise: Date; sunset: Date } | null {
  // Solar zenith angle for sunrise/sunset (90.833 degrees for standard sunrise/sunset)
  const zenith = 90.833;

  // Get day of year
  const startOfYear = new Date(date.getFullYear(), 0, 0);
  const diff = date.getTime() - startOfYear.getTime();
  const oneDay = 1000 * 60 * 60 * 24;
  const dayOfYear = Math.floor(diff / oneDay);

  // Calculate longitude hour value
  const lngHour = lon / 15;

  // Calculate approximate sunrise and sunset times
  const tRise = dayOfYear + ((6 - lngHour) / 24);
  const tSet = dayOfYear + ((18 - lngHour) / 24);

  // Calculate sun's mean anomaly
  const mRise = (0.9856 * tRise) - 3.289;
  const mSet = (0.9856 * tSet) - 3.289;

  // Calculate sun's true longitude
  let lRise = mRise + (1.916 * Math.sin(mRise * Math.PI / 180)) + (0.020 * Math.sin(2 * mRise * Math.PI / 180)) + 282.634;
  let lSet = mSet + (1.916 * Math.sin(mSet * Math.PI / 180)) + (0.020 * Math.sin(2 * mSet * Math.PI / 180)) + 282.634;

  // Normalize to 0-360
  lRise = lRise % 360;
  if (lRise < 0) lRise += 360;
  lSet = lSet % 360;
  if (lSet < 0) lSet += 360;

  // Calculate sun's right ascension
  let raRise = Math.atan(0.91764 * Math.tan(lRise * Math.PI / 180)) * 180 / Math.PI;
  let raSet = Math.atan(0.91764 * Math.tan(lSet * Math.PI / 180)) * 180 / Math.PI;

  // Normalize to 0-360
  raRise = raRise % 360;
  if (raRise < 0) raRise += 360;
  raSet = raSet % 360;
  if (raSet < 0) raSet += 360;

  // Right ascension should be in the same quadrant as L
  const lQuadrantRise = (Math.floor(lRise / 90)) * 90;
  const raQuadrantRise = (Math.floor(raRise / 90)) * 90;
  raRise = raRise + (lQuadrantRise - raQuadrantRise);

  const lQuadrantSet = (Math.floor(lSet / 90)) * 90;
  const raQuadrantSet = (Math.floor(raSet / 90)) * 90;
  raSet = raSet + (lQuadrantSet - raQuadrantSet);

  // Convert right ascension to hours
  raRise = raRise / 15;
  raSet = raSet / 15;

  // Calculate sun's declination
  const sinDecRise = 0.39782 * Math.sin(lRise * Math.PI / 180);
  const cosDecRise = Math.cos(Math.asin(sinDecRise));

  const sinDecSet = 0.39782 * Math.sin(lSet * Math.PI / 180);
  const cosDecSet = Math.cos(Math.asin(sinDecSet));

  // Calculate sun's local hour angle
  const cosHRise = (Math.cos(zenith * Math.PI / 180) - (sinDecRise * Math.sin(lat * Math.PI / 180))) / (cosDecRise * Math.cos(lat * Math.PI / 180));
  const cosHSet = (Math.cos(zenith * Math.PI / 180) - (sinDecSet * Math.sin(lat * Math.PI / 180))) / (cosDecSet * Math.cos(lat * Math.PI / 180));

  // Check if the sun never rises or sets (polar regions)
  if (cosHRise > 1 || cosHSet > 1) {
    // Sun never rises
    return null;
  }
  if (cosHRise < -1 || cosHSet < -1) {
    // Sun never sets
    return null;
  }

  // Calculate local hour angle
  const hRise = 360 - Math.acos(cosHRise) * 180 / Math.PI;
  const hSet = Math.acos(cosHSet) * 180 / Math.PI;

  // Convert to hours
  const hRiseHours = hRise / 15;
  const hSetHours = hSet / 15;

  // Calculate local mean time
  const tLocalRise = hRiseHours + raRise - (0.06571 * tRise) - 6.622;
  const tLocalSet = hSetHours + raSet - (0.06571 * tSet) - 6.622;

  // Adjust to UTC
  let utcRise = tLocalRise - lngHour;
  let utcSet = tLocalSet - lngHour;

  // Normalize to 0-24
  utcRise = utcRise % 24;
  if (utcRise < 0) utcRise += 24;
  utcSet = utcSet % 24;
  if (utcSet < 0) utcSet += 24;

  // Convert to Date objects
  const sunrise = new Date(date);
  sunrise.setUTCHours(Math.floor(utcRise), Math.floor((utcRise % 1) * 60), 0, 0);

  const sunset = new Date(date);
  sunset.setUTCHours(Math.floor(utcSet), Math.floor((utcSet % 1) * 60), 0, 0);

  return { sunrise, sunset };
}

/**
 * Format time as HH:MM for display
 */
export function formatTime(date: Date): string {
  const hours = date.getHours().toString().padStart(2, '0');
  const minutes = date.getMinutes().toString().padStart(2, '0');
  return `${hours}:${minutes}`;
}
