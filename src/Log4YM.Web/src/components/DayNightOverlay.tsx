import { useEffect, useRef } from 'react';
import { useMap } from 'react-leaflet';
import L from 'leaflet';
import { getSunPosition, getMoonPosition, computeTerminatorLine } from '../utils/solarCalculations';

interface DayNightOverlayProps {
  opacity?: number;
  showSunMarker?: boolean;
  showMoonMarker?: boolean;
}

/**
 * Day/Night overlay using Leaflet vector polygon, matching OpenHamClock's terminator style.
 * Renders the night side as a filled polygon with transparent stroke (to avoid antimeridian
 * artifact), and the terminator line as a separate dashed polyline.
 */
export function DayNightOverlay({
  opacity = 0.7,
  showSunMarker = true,
  showMoonMarker = true
}: DayNightOverlayProps) {
  const map = useMap();
  const layerGroupRef = useRef<L.LayerGroup | null>(null);

  useEffect(() => {
    const layerGroup = L.layerGroup().addTo(map);
    layerGroupRef.current = layerGroup;

    function updateOverlay() {
      layerGroup.clearLayers();

      const now = new Date();
      const sunPos = getSunPosition(now);
      const termLine = computeTerminatorLine(now, 0, 2);

      if (termLine.length > 0) {
        // Night pole: south during northern summer, north during northern winter
        const nightPole: number = sunPos.lat >= 0 ? -90 : 90;

        // Create 3 copies of the night polygon for world wrapping (-360, 0, +360)
        for (const offset of [-360, 0, 360]) {
          const polygonPoints: L.LatLngExpression[] = [];

          // Terminator line points
          for (const [lat, lon] of termLine) {
            polygonPoints.push([lat, lon + offset]);
          }

          // Close to night pole
          const lastLon = termLine[termLine.length - 1][1] + offset;
          const firstLon = termLine[0][1] + offset;
          polygonPoints.push([nightPole, lastLon]);
          polygonPoints.push([nightPole, firstLon]);

          // Polygon with transparent stroke to avoid antimeridian closing-edge artifact
          L.polygon(polygonPoints, {
            fillColor: '#000020',
            fillOpacity: 0.35 * opacity,
            color: 'transparent',
            weight: 0,
            interactive: false,
          }).addTo(layerGroup);
        }

        // Draw the terminator line separately as a polyline (no closing edge issue)
        for (const offset of [-360, 0, 360]) {
          const linePoints: L.LatLngExpression[] = termLine.map(
            ([lat, lon]) => [lat, lon + offset]
          );
          L.polyline(linePoints, {
            color: '#ffaa00',
            weight: 2,
            dashArray: '5, 5',
            interactive: false,
          }).addTo(layerGroup);
        }
      }

      // Sun marker - radial gradient yellow to orange with amber border
      if (showSunMarker) {
        const sunIcon = L.divIcon({
          className: '',
          html: `<div style="
            width: 24px;
            height: 24px;
            background: radial-gradient(circle, #ffdd00 0%, #ff8800 100%);
            border: 2px solid #ffaa00;
            border-radius: 50%;
            box-shadow: 0 0 12px rgba(255, 221, 0, 0.8);
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 12px;
            line-height: 1;
            color: #000;
            font-weight: bold;
            font-family: monospace;
          ">&#9788;</div>`,
          iconSize: [24, 24],
          iconAnchor: [12, 12],
        });

        L.marker([sunPos.lat, sunPos.lon], { icon: sunIcon, interactive: false })
          .addTo(layerGroup);
      }

      // Moon marker - radial gradient white to gray-blue with light border
      if (showMoonMarker) {
        const moonPos = getMoonPosition(now);
        const moonIcon = L.divIcon({
          className: '',
          html: `<div style="
            width: 24px;
            height: 24px;
            background: radial-gradient(circle, #e8e8f0 0%, #8888aa 100%);
            border: 2px solid #aaaacc;
            border-radius: 50%;
            box-shadow: 0 0 8px rgba(200, 200, 220, 0.5);
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 12px;
            line-height: 1;
            color: #333;
            font-weight: bold;
            font-family: monospace;
          ">&#9789;</div>`,
          iconSize: [24, 24],
          iconAnchor: [12, 12],
        });

        L.marker([moonPos.lat, moonPos.lon], { icon: moonIcon, interactive: false })
          .addTo(layerGroup);
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
  }, [map, opacity, showSunMarker, showMoonMarker]);

  return null;
}
