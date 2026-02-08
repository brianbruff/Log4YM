import { useEffect, useRef } from 'react';
import { useMap } from 'react-leaflet';
import L from 'leaflet';
import { getSolarElevation } from '../utils/solarCalculations';

interface GrayLineOverlayProps {
  opacity?: number;
  color?: string;
}

export function GrayLineOverlay({
  opacity = 0.6,
  color = '#ff6b35'
}: GrayLineOverlayProps) {
  const map = useMap();
  const canvasLayerRef = useRef<L.Layer | null>(null);

  useEffect(() => {
    // Custom canvas layer for gray line visualization
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

        // Clear canvas
        ctx.clearRect(0, 0, canvas.width, canvas.height);

        // Draw gray line (civil twilight zone where sun is between 0° and -6°)
        const now = new Date();
        const imageData = ctx.createImageData(canvas.width, canvas.height);
        const data = imageData.data;

        // Sample resolution for performance
        const step = 2;

        // Parse color
        const hexColor = color.replace('#', '');
        const r = parseInt(hexColor.substring(0, 2), 16);
        const g = parseInt(hexColor.substring(2, 4), 16);
        const b = parseInt(hexColor.substring(4, 6), 16);

        for (let x = 0; x < canvas.width; x += step) {
          for (let y = 0; y < canvas.height; y += step) {
            const point = map.containerPointToLatLng([x, y]);
            const lat = point.lat;
            const lon = point.lng;

            // Calculate solar elevation
            const elevation = getSolarElevation(lat, lon, now);

            // Gray line is the zone where sun is between -6° and 0° (civil twilight)
            // We'll also show a gradient extending a bit beyond for visual effect
            let alpha = 0;

            if (elevation >= -6 && elevation <= 0) {
              // Core gray line - full intensity with gradient
              // At -3° (middle of twilight), full intensity
              const twilightCenter = -3;
              const distance = Math.abs(elevation - twilightCenter);
              alpha = Math.round(180 * (1 - distance / 3));
            } else if (elevation > 0 && elevation < 2) {
              // Gradient on day side
              alpha = Math.round(120 * (1 - elevation / 2));
            } else if (elevation < -6 && elevation > -8) {
              // Gradient on night side
              alpha = Math.round(120 * (1 - Math.abs(elevation + 6) / 2));
            }

            // Fill the pixel block
            if (alpha > 0) {
              for (let dx = 0; dx < step && x + dx < canvas.width; dx++) {
                for (let dy = 0; dy < step && y + dy < canvas.height; dy++) {
                  const idx = ((y + dy) * canvas.width + (x + dx)) * 4;
                  data[idx] = r;
                  data[idx + 1] = g;
                  data[idx + 2] = b;
                  data[idx + 3] = alpha;
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
    }, 60000);

    // Cleanup
    return () => {
      clearInterval(interval);
      if (canvasLayerRef.current) {
        map.removeLayer(canvasLayerRef.current);
      }
    };
  }, [map, opacity, color]);

  return null;
}
