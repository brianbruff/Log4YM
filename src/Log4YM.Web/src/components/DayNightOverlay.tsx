import { useEffect, useRef } from 'react';
import { useMap } from 'react-leaflet';
import L from 'leaflet';
import { getSunPosition, getMoonPosition, getSolarElevation } from '../utils/solarCalculations';

interface DayNightOverlayProps {
  opacity?: number;
  showSunMarker?: boolean;
  showMoonMarker?: boolean;
}

export function DayNightOverlay({
  opacity = 0.5,
  showSunMarker = true,
  showMoonMarker = true
}: DayNightOverlayProps) {
  const map = useMap();
  const canvasLayerRef = useRef<L.Layer | null>(null);
  const sunMarkerRef = useRef<L.Marker | null>(null);
  const moonMarkerRef = useRef<L.Marker | null>(null);

  useEffect(() => {
    // Custom canvas layer for night overlay
    const CanvasLayer = L.Layer.extend({
      onAdd: function (map: L.Map) {
        this._map = map;
        this._canvas = L.DomUtil.create('canvas', 'leaflet-layer');
        this._context = this._canvas.getContext('2d');

        const size = map.getSize();
        this._canvas.width = size.x;
        this._canvas.height = size.y;

        const animated = map.options.zoomAnimation && L.Browser.any3d;
        L.DomUtil.addClass(this._canvas, `leaflet-zoom-${animated ? 'animated' : 'hide'}`);

        map.getPanes().overlayPane?.appendChild(this._canvas);

        map.on('moveend', this._reset, this);
        map.on('zoom', this._reset, this);
        map.on('resize', this._resize, this);

        this._reset();
      },

      onRemove: function (map: L.Map) {
        L.DomUtil.remove(this._canvas);
        map.off('moveend', this._reset, this);
        map.off('zoom', this._reset, this);
        map.off('resize', this._resize, this);
      },

      _resize: function () {
        const size = this._map.getSize();
        this._canvas.width = size.x;
        this._canvas.height = size.y;
        this._reset();
      },

      _reset: function () {
        this._draw();
      },

      _draw: function () {
        const canvas = this._canvas;
        const ctx = this._context;
        const map = this._map;
        const bounds = map.getBounds();

        // Clear canvas
        ctx.clearRect(0, 0, canvas.width, canvas.height);

        // Draw night overlay by checking each pixel
        const now = new Date();
        const imageData = ctx.createImageData(canvas.width, canvas.height);
        const data = imageData.data;

        // Sample resolution (check every N pixels for performance)
        const step = 2;

        for (let x = 0; x < canvas.width; x += step) {
          for (let y = 0; y < canvas.height; y += step) {
            const point = map.containerPointToLatLng([x, y]);
            const lat = point.lat;
            const lon = point.lng;

            // Calculate solar elevation
            const elevation = getSolarElevation(lat, lon, now);

            // Determine darkness level
            let alpha = 0;
            if (elevation < -18) {
              // Astronomical twilight - full night
              alpha = 180;
            } else if (elevation < -12) {
              // Nautical twilight
              alpha = 160;
            } else if (elevation < -6) {
              // Civil twilight
              alpha = 120;
            } else if (elevation < 0) {
              // Near sunset/sunrise
              alpha = 80;
            }

            // Fill the pixel block
            if (alpha > 0) {
              for (let dx = 0; dx < step && x + dx < canvas.width; dx++) {
                for (let dy = 0; dy < step && y + dy < canvas.height; dy++) {
                  const idx = ((y + dy) * canvas.width + (x + dx)) * 4;
                  data[idx] = 0;     // R
                  data[idx + 1] = 0; // G
                  data[idx + 2] = 30; // B (slight blue tint)
                  data[idx + 3] = alpha; // A
                }
              }
            }
          }
        }

        ctx.putImageData(imageData, 0, 0);

        // Apply overall opacity
        canvas.style.opacity = opacity.toString();
      }
    });

    const canvasLayer = new CanvasLayer();
    canvasLayer.addTo(map);
    canvasLayerRef.current = canvasLayer;

    // Update every 60 seconds
    const interval = setInterval(() => {
      canvasLayer._reset();
      updateMarkers();
    }, 60000);

    // Create sun marker
    if (showSunMarker) {
      const sunIcon = L.divIcon({
        className: 'sun-marker',
        html: `
          <div style="
            width: 20px;
            height: 20px;
            background: radial-gradient(circle, #ffeb3b, #ff9800);
            border: 2px solid white;
            border-radius: 50%;
            box-shadow: 0 0 12px rgba(255, 235, 59, 0.8);
          "></div>
        `,
        iconSize: [20, 20],
        iconAnchor: [10, 10],
      });

      const sunPos = getSunPosition();
      const sunMarker = L.marker([sunPos.lat, sunPos.lon], { icon: sunIcon })
        .bindPopup('<strong>☀️ Sun Subsolar Point</strong>')
        .addTo(map);
      sunMarkerRef.current = sunMarker;
    }

    // Create moon marker
    if (showMoonMarker) {
      const moonIcon = L.divIcon({
        className: 'moon-marker',
        html: `
          <div style="
            width: 16px;
            height: 16px;
            background: radial-gradient(circle, #e0e0e0, #9e9e9e);
            border: 2px solid white;
            border-radius: 50%;
            box-shadow: 0 0 8px rgba(224, 224, 224, 0.6);
          "></div>
        `,
        iconSize: [16, 16],
        iconAnchor: [8, 8],
      });

      const moonPos = getMoonPosition();
      const moonMarker = L.marker([moonPos.lat, moonPos.lon], { icon: moonIcon })
        .bindPopup('<strong>🌙 Moon Sublunar Point</strong>')
        .addTo(map);
      moonMarkerRef.current = moonMarker;
    }

    // Function to update markers
    function updateMarkers() {
      if (sunMarkerRef.current && showSunMarker) {
        const sunPos = getSunPosition();
        sunMarkerRef.current.setLatLng([sunPos.lat, sunPos.lon]);
      }

      if (moonMarkerRef.current && showMoonMarker) {
        const moonPos = getMoonPosition();
        moonMarkerRef.current.setLatLng([moonPos.lat, moonPos.lon]);
      }
    }

    // Cleanup
    return () => {
      clearInterval(interval);
      if (canvasLayerRef.current) {
        map.removeLayer(canvasLayerRef.current);
      }
      if (sunMarkerRef.current) {
        map.removeLayer(sunMarkerRef.current);
      }
      if (moonMarkerRef.current) {
        map.removeLayer(moonMarkerRef.current);
      }
    };
  }, [map, opacity, showSunMarker, showMoonMarker]);

  return null;
}
