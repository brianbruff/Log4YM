/**
 * Utility functions for working with Maidenhead locator grid squares
 */

/**
 * Convert Maidenhead grid square to latitude and longitude coordinates
 * Supports 4-character (field+square) and 6-character (field+square+subsquare) grid squares
 * 
 * @param grid Maidenhead grid square (e.g., "JO20cx", "IO63", "FN31pr")
 * @returns { lat, lon } coordinates, or null if invalid
 * 
 * Examples:
 * - JO20cx (Belgium) -> { lat: 50.979, lon: 4.208 }
 * - IO63 (Ireland) -> { lat: 53.5, lon: -7.0 }
 */
export function gridToLatLon(grid: string): { lat: number; lon: number } | null {
  if (!grid) return null;
  
  // Normalize: uppercase and trim
  grid = grid.toUpperCase().trim();
  
  // Must be 4 or 6 characters
  if (grid.length !== 4 && grid.length !== 6) {
    return null;
  }
  
  // Validate format
  // First two: letters A-R (field)
  // Next two: digits 0-9 (square)
  // Optional next two: letters a-x (subsquare)
  const fieldPattern = /^[A-R]{2}$/;
  const squarePattern = /^[0-9]{2}$/;
  const subsquarePattern = /^[A-X]{2}$/;
  
  const field = grid.substring(0, 2);
  const square = grid.substring(2, 4);
  const subsquare = grid.length === 6 ? grid.substring(4, 6) : null;
  
  if (!fieldPattern.test(field)) return null;
  if (!squarePattern.test(square)) return null;
  if (subsquare && !subsquarePattern.test(subsquare)) return null;
  
  // Convert field (letters) to longitude and latitude offsets
  // Each field is 20° longitude × 10° latitude
  const fieldLon = (field.charCodeAt(0) - 'A'.charCodeAt(0)) * 20;
  const fieldLat = (field.charCodeAt(1) - 'A'.charCodeAt(0)) * 10;
  
  // Convert square (digits) to offsets within the field
  // Each square is 2° longitude × 1° latitude
  const squareLon = parseInt(square[0]) * 2;
  const squareLat = parseInt(square[1]) * 1;
  
  // Convert subsquare (letters) to offsets within the square
  // Each square contains 24x24 subsquares
  const SUBSQUARES_PER_SQUARE = 24;
  let subsquareLon = 0;
  let subsquareLat = 0;
  if (subsquare) {
    subsquareLon = (subsquare.charCodeAt(0) - 'A'.charCodeAt(0)) * (2 / SUBSQUARES_PER_SQUARE);  // 2° / 24 subsquares
    subsquareLat = (subsquare.charCodeAt(1) - 'A'.charCodeAt(0)) * (1 / SUBSQUARES_PER_SQUARE);  // 1° / 24 subsquares
  }
  
  // Calculate final coordinates
  // Grid squares start at -180° longitude, -90° latitude
  // Center of the grid square (not southwest corner)
  let lon = -180 + fieldLon + squareLon + subsquareLon;
  let lat = -90 + fieldLat + squareLat + subsquareLat;
  
  // Add half the grid square size to get center
  if (subsquare) {
    // Subsquare specified: center of subsquare
    lon += (2 / SUBSQUARES_PER_SQUARE) / 2;  // Half subsquare width
    lat += (1 / SUBSQUARES_PER_SQUARE) / 2;  // Half subsquare height
  } else {
    // No subsquare: center of square
    lon += 1;  // Half of 2° square width
    lat += 0.5;  // Half of 1° square height
  }
  
  return { lat, lon };
}

/**
 * Calculate distance between two points in kilometers (Haversine formula)
 */
export function calculateDistance(lat1: number, lon1: number, lat2: number, lon2: number): number {
  const R = 6371; // Earth's radius in km
  const toRad = Math.PI / 180;
  
  const dLat = (lat2 - lat1) * toRad;
  const dLon = (lon2 - lon1) * toRad;
  
  const a = 
    Math.sin(dLat / 2) * Math.sin(dLat / 2) +
    Math.cos(lat1 * toRad) * Math.cos(lat2 * toRad) *
    Math.sin(dLon / 2) * Math.sin(dLon / 2);
  
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
  
  return R * c;
}

/**
 * Calculate bearing/azimuth from point 1 to point 2 (0-360 degrees, 0=North)
 */
export function calculateBearing(lat1: number, lon1: number, lat2: number, lon2: number): number {
  const toRad = Math.PI / 180;
  const toDeg = 180 / Math.PI;

  const dLon = (lon2 - lon1) * toRad;
  const lat1Rad = lat1 * toRad;
  const lat2Rad = lat2 * toRad;

  const y = Math.sin(dLon) * Math.cos(lat2Rad);
  const x = Math.cos(lat1Rad) * Math.sin(lat2Rad) -
      Math.sin(lat1Rad) * Math.cos(lat2Rad) * Math.cos(dLon);

  let bearing = Math.atan2(y, x) * toDeg;
  bearing = (bearing + 360) % 360;

  return bearing;
}

/**
 * Get animation duration in seconds based on distance between two points
 * Uses distance thresholds for smooth visual experience:
 * - < 500 km: 2 seconds (same country/nearby)
 * - 500-2000 km: 3 seconds (regional)
 * - 2000-5000 km: 4 seconds (continental)
 * - > 5000 km: 5 seconds (intercontinental)
 *
 * @param distanceKm Distance in kilometers
 * @returns Animation duration in seconds
 */
export function getAnimationDuration(distanceKm: number): number {
  if (distanceKm < 500) return 2;
  if (distanceKm < 2000) return 3;
  if (distanceKm < 5000) return 4;
  return 5;
}
