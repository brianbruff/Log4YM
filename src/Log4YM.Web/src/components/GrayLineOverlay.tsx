import { useEffect, useRef } from 'react';
import { useMap } from 'react-leaflet';
import L from 'leaflet';
import { computeTerminatorLine } from '../utils/solarCalculations';
import { unwrapLongitudes } from '../utils/geoUtils';

interface GrayLineOverlayProps {
  opacity?: number;
}

/**
 * Gray Line overlay using Leaflet vector layers, matching OpenHamClock's gray line plugin.
 * Renders the solar terminator line, enhanced DX propagation zone, and twilight boundaries.
 */
export function GrayLineOverlay({
  opacity = 0.7
}: GrayLineOverlayProps) {
  const map = useMap();
  const layerGroupRef = useRef<L.LayerGroup | null>(null);

  useEffect(() => {
    const layerGroup = L.layerGroup().addTo(map);
    layerGroupRef.current = layerGroup;

    function updateOverlay() {
      layerGroup.clearLayers();

      const now = new Date();

      // Compute lines at different solar altitudes
      const terminatorLine = computeTerminatorLine(now, 0, 2);
      const dxZoneUpper = computeTerminatorLine(now, 5, 2);
      const dxZoneLower = computeTerminatorLine(now, -5, 2);
      const civilLine = computeTerminatorLine(now, -6, 2);
      const nauticalLine = computeTerminatorLine(now, -12, 2);
      const astronomicalLine = computeTerminatorLine(now, -18, 2);

      // Enhanced DX zone polygon (between +5° and -5° solar altitude)
      if (dxZoneUpper.length > 0 && dxZoneLower.length > 0) {
        // Build closed ring: forward upper + reversed lower, then unwrap
        const baseRing: [number, number][] = [
          ...dxZoneUpper,
          ...dxZoneLower.slice().reverse(),
        ];
        const unwrapped = unwrapLongitudes(baseRing);

        for (const offset of [-360, 0, 360]) {
          const polygonPoints: L.LatLngExpression[] = unwrapped.map(
            ([lat, lon]) => [lat, lon + offset]
          );
          L.polygon(polygonPoints, {
            color: '#ffaa00',
            fillColor: '#ffaa00',
            fillOpacity: opacity * 0.15,
            weight: 1,
            opacity: opacity * 0.3,
            interactive: false,
          }).addTo(layerGroup);
        }
      }

      // Main solar terminator line (most prominent)
      addPolyline(terminatorLine, {
        color: '#ff6600',
        weight: 3,
        opacity: opacity * 0.8,
        dashArray: '10, 5',
      });

      // Civil twilight line (-6°)
      addPolyline(civilLine, {
        color: '#4488ff',
        weight: 2,
        opacity: opacity * 0.6,
        dashArray: '5, 5',
      });

      // Nautical twilight line (-12°)
      addPolyline(nauticalLine, {
        color: '#6666ff',
        weight: 1.5,
        opacity: opacity * 0.4,
        dashArray: '3, 3',
      });

      // Astronomical twilight line (-18°)
      addPolyline(astronomicalLine, {
        color: '#8888ff',
        weight: 1,
        opacity: opacity * 0.3,
        dashArray: '2, 2',
      });
    }

    function addPolyline(points: [number, number][], style: L.PolylineOptions) {
      const unwrapped = unwrapLongitudes(points);
      for (const offset of [-360, 0, 360]) {
        const offsetPoints: L.LatLngExpression[] = unwrapped.map(
          ([lat, lon]) => [lat, lon + offset]
        );
        L.polyline(offsetPoints, { ...style, interactive: false }).addTo(layerGroup);
      }
    }

    updateOverlay();
    const interval = setInterval(updateOverlay, 60000);

    return () => {
      clearInterval(interval);
      if (layerGroupRef.current) {
        map.removeLayer(layerGroupRef.current);
      }
    };
  }, [map, opacity]);

  return null;
}
