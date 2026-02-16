/**
 * Unwrap longitudes so no consecutive pair jumps more than 180°.
 * This prevents Leaflet from drawing lines/edges across the entire map
 * when a polygon or polyline crosses the antimeridian (±180° boundary).
 *
 * Based on the approach from OpenHamClock's gray line plugin.
 */
export function unwrapLongitudes(points: [number, number][]): [number, number][] {
  if (points.length < 2) return points.map(p => [p[0], p[1]]);

  const unwrapped: [number, number][] = points.map(p => [p[0], p[1]]);
  for (let i = 1; i < unwrapped.length; i++) {
    while (unwrapped[i][1] - unwrapped[i - 1][1] > 180) unwrapped[i][1] -= 360;
    while (unwrapped[i][1] - unwrapped[i - 1][1] < -180) unwrapped[i][1] += 360;
  }
  return unwrapped;
}
