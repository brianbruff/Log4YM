import { useEffect, useRef, useCallback, useState } from 'react';
import { BarChart3, Pause, Play } from 'lucide-react';
import { GlassPanel } from '../components/GlassPanel';
import { setSpectrumDataCallback, type SpectrumDataEvent } from '../api/signalr';
import { signalRService } from '../api/signalr';
import { useAppStore } from '../store/appStore';

// Pre-compute a 256-entry colormap: black -> blue -> cyan -> green -> yellow -> red -> white
const COLORMAP = buildColormap();

function buildColormap(): Uint8Array {
  const lut = new Uint8Array(256 * 3);
  const stops = [
    { pos: 0, r: 0, g: 0, b: 0 },       // black
    { pos: 36, r: 0, g: 0, b: 180 },     // blue
    { pos: 72, r: 0, g: 180, b: 220 },   // cyan
    { pos: 120, r: 0, g: 200, b: 0 },    // green
    { pos: 170, r: 240, g: 240, b: 0 },  // yellow
    { pos: 210, r: 255, g: 60, b: 0 },   // red
    { pos: 255, r: 255, g: 255, b: 255 },// white
  ];
  for (let s = 0; s < stops.length - 1; s++) {
    const a = stops[s];
    const b = stops[s + 1];
    for (let i = a.pos; i <= b.pos; i++) {
      const t = (i - a.pos) / (b.pos - a.pos);
      lut[i * 3] = Math.round(a.r + (b.r - a.r) * t);
      lut[i * 3 + 1] = Math.round(a.g + (b.g - a.g) * t);
      lut[i * 3 + 2] = Math.round(a.b + (b.b - a.b) * t);
    }
  }
  return lut;
}

const SPECTRUM_RATIO = 0.3; // top 30% for spectrum line
const AXIS_HEIGHT = 20;     // pixels for frequency axis between spectrum and waterfall
const NO_DATA_MSG = 'No spectrum data \u2014 enable N1MM+ Spectrum Display in Thetis (UDP port 13064)';

export function PanadapterPlugin() {
  const containerRef = useRef<HTMLDivElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const spectrumRef = useRef<SpectrumDataEvent | null>(null);
  const rafIdRef = useRef<number>(0);
  const waterfallBufRef = useRef<HTMLCanvasElement | null>(null);
  const [paused, setPaused] = useState(false);
  const pausedRef = useRef(false);
  const hasDataRef = useRef(false);
  const peakRef = useRef(100); // rolling peak for auto-scaling amplitude

  // Keep pausedRef in sync
  useEffect(() => {
    pausedRef.current = paused;
  }, [paused]);

  // Render loop
  const render = useCallback(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const dpr = window.devicePixelRatio || 1;
    const w = Math.round(canvas.width / dpr);
    const h = Math.round(canvas.height / dpr);
    if (w === 0 || h === 0) {
      // Tab is hidden (FlexLayout display:none) — keep the loop alive
      rafIdRef.current = requestAnimationFrame(render);
      return;
    }

    const spectrumH = Math.floor((h - AXIS_HEIGHT) * SPECTRUM_RATIO);
    const waterfallY = spectrumH + AXIS_HEIGHT;
    const waterfallH = h - waterfallY;

    const data = spectrumRef.current;

    // Clear whole canvas
    ctx.fillStyle = '#0a0e14';
    ctx.fillRect(0, 0, w, h);

    if (!data || data.data.length === 0) {
      // No-data message
      ctx.fillStyle = '#6b7280';
      ctx.font = '13px system-ui, sans-serif';
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.fillText(NO_DATA_MSG, w / 2, h / 2);
      rafIdRef.current = requestAnimationFrame(render);
      return;
    }

    hasDataRef.current = true;
    const points = data.data;
    const len = points.length;

    // Auto-scale: find max in this frame, decay peak slowly for stable display
    let frameMax = 1;
    for (let i = 0; i < len; i++) {
      if (points[i] > frameMax) frameMax = points[i];
    }
    // Peak hold with slow decay
    if (frameMax > peakRef.current) {
      peakRef.current = frameMax;
    } else {
      peakRef.current = peakRef.current * 0.995 + frameMax * 0.005;
    }
    const scale = Math.max(peakRef.current * 1.1, 1); // 10% headroom

    // --- Spectrum line graph ---
    ctx.save();
    ctx.beginPath();
    for (let i = 0; i < len; i++) {
      const x = (i / (len - 1)) * w;
      const normalized = Math.min(points[i] / scale, 1);
      const y = spectrumH - normalized * spectrumH;
      if (i === 0) ctx.moveTo(x, y);
      else ctx.lineTo(x, y);
    }
    ctx.strokeStyle = '#00e5ff';
    ctx.lineWidth = 1;
    ctx.stroke();

    // Fill under the curve
    ctx.lineTo(w, spectrumH);
    ctx.lineTo(0, spectrumH);
    ctx.closePath();
    ctx.fillStyle = 'rgba(0, 229, 255, 0.08)';
    ctx.fill();
    ctx.restore();

    // --- Frequency axis ---
    const lowHz = data.lowFrequencyHz;
    const highHz = data.highFrequencyHz;
    const rangeHz = highHz - lowHz;

    ctx.fillStyle = '#1a1f2e';
    ctx.fillRect(0, spectrumH, w, AXIS_HEIGHT);

    if (rangeHz > 0) {
      // Determine nice tick spacing in Hz
      const targetTicks = Math.floor(w / 100);
      const rawStep = rangeHz / Math.max(targetTicks, 1);
      const magnitude = Math.pow(10, Math.floor(Math.log10(rawStep)));
      const nice = [1, 2, 5, 10].find(m => m * magnitude >= rawStep) ?? 10;
      const stepHz = nice * magnitude;

      const firstTick = Math.ceil(lowHz / stepHz) * stepHz;
      ctx.fillStyle = '#9ca3af';
      ctx.font = '10px system-ui, sans-serif';
      ctx.textAlign = 'center';
      ctx.textBaseline = 'top';

      for (let freq = firstTick; freq <= highHz; freq += stepHz) {
        const x = ((freq - lowHz) / rangeHz) * w;
        // Tick mark
        ctx.strokeStyle = '#4b5563';
        ctx.beginPath();
        ctx.moveTo(x, spectrumH);
        ctx.lineTo(x, spectrumH + 4);
        ctx.stroke();
        // Label
        const mhz = freq / 1e6;
        ctx.fillText(mhz.toFixed(mhz >= 100 ? 2 : 3), x, spectrumH + 5);
      }
    }

    // --- Waterfall ---
    if (!pausedRef.current && waterfallH > 0) {
      // Get or create waterfall buffer canvas
      let wfBuf = waterfallBufRef.current;
      if (!wfBuf || wfBuf.width !== w || wfBuf.height !== waterfallH) {
        wfBuf = document.createElement('canvas');
        wfBuf.width = w;
        wfBuf.height = waterfallH;
        waterfallBufRef.current = wfBuf;
      }
      const wfCtx = wfBuf.getContext('2d')!;

      // Scroll existing content down by 1 pixel
      wfCtx.drawImage(wfBuf, 0, 0, w, waterfallH - 1, 0, 1, w, waterfallH - 1);

      // Draw new row at y=0
      const imgData = wfCtx.createImageData(w, 1);
      const pixels = imgData.data;
      for (let x = 0; x < w; x++) {
        const idx = Math.min(Math.floor((x / w) * len), len - 1);
        const val = Math.min(Math.floor((points[idx] / scale) * 255), 255);
        const ci = val * 3;
        const pi = x * 4;
        pixels[pi] = COLORMAP[ci];
        pixels[pi + 1] = COLORMAP[ci + 1];
        pixels[pi + 2] = COLORMAP[ci + 2];
        pixels[pi + 3] = 255;
      }
      wfCtx.putImageData(imgData, 0, 0);

      // Copy waterfall buffer to main canvas
      ctx.drawImage(wfBuf, 0, waterfallY);
    } else if (waterfallBufRef.current && waterfallH > 0) {
      // Paused: just draw the existing waterfall buffer
      ctx.drawImage(waterfallBufRef.current, 0, waterfallY);
    }

    // --- VFO indicator ---
    if (rangeHz > 0) {
      // Get current VFO frequency from app store (direct access, no subscription)
      const radioStates = useAppStore.getState().radioStates;
      let vfoHz: number | null = null;
      for (const [, state] of radioStates) {
        if (state.frequencyHz) {
          vfoHz = state.frequencyHz;
          break;
        }
      }

      if (vfoHz !== null && vfoHz >= lowHz && vfoHz <= highHz) {
        const vfoX = ((vfoHz - lowHz) / rangeHz) * w;
        ctx.save();
        ctx.strokeStyle = '#ff4444';
        ctx.lineWidth = 1;
        ctx.setLineDash([4, 3]);
        ctx.beginPath();
        ctx.moveTo(vfoX, 0);
        ctx.lineTo(vfoX, h);
        ctx.stroke();
        ctx.restore();
      }
    }

    rafIdRef.current = requestAnimationFrame(render);
  }, []);

  // ResizeObserver to keep canvas pixel-perfect
  useEffect(() => {
    const container = containerRef.current;
    const canvas = canvasRef.current;
    if (!container || !canvas) return;

    const resize = () => {
      const rect = container.getBoundingClientRect();
      const dpr = window.devicePixelRatio || 1;
      canvas.width = Math.floor(rect.width * dpr);
      canvas.height = Math.floor(rect.height * dpr);
      canvas.style.width = `${rect.width}px`;
      canvas.style.height = `${rect.height}px`;
      const ctx = canvas.getContext('2d');
      if (ctx) ctx.scale(dpr, dpr);
      // Reset waterfall buffer on resize
      waterfallBufRef.current = null;
    };

    const observer = new ResizeObserver(resize);
    observer.observe(container);
    resize();

    return () => observer.disconnect();
  }, []);

  // Spectrum data callback + render loop
  useEffect(() => {
    setSpectrumDataCallback((evt) => {
      if (!pausedRef.current) {
        spectrumRef.current = evt;
      }
    });

    rafIdRef.current = requestAnimationFrame(render);

    return () => {
      setSpectrumDataCallback(null);
      if (rafIdRef.current) cancelAnimationFrame(rafIdRef.current);
    };
  }, [render]);

  // Click-to-tune handler
  const handleClick = useCallback((e: React.MouseEvent<HTMLCanvasElement>) => {
    const data = spectrumRef.current;
    if (!data) return;

    const canvas = canvasRef.current;
    if (!canvas) return;

    const rect = canvas.getBoundingClientRect();
    const xRatio = (e.clientX - rect.left) / rect.width;
    const rangeHz = data.highFrequencyHz - data.lowFrequencyHz;
    if (rangeHz <= 0) return;

    const freqHz = Math.round(data.lowFrequencyHz + xRatio * rangeHz);
    signalRService.tuneToFrequency(freqHz);
  }, []);

  return (
    <GlassPanel
      title="Panadapter"
      icon={<BarChart3 className="w-4 h-4" />}
      actions={
        <button
          onClick={() => setPaused(p => !p)}
          className="glass-button p-1.5"
          title={paused ? 'Resume' : 'Pause'}
        >
          {paused ? <Play className="w-3.5 h-3.5" /> : <Pause className="w-3.5 h-3.5" />}
        </button>
      }
    >
      <div ref={containerRef} className="w-full h-full relative overflow-hidden" style={{ minHeight: 120 }}>
        <canvas
          ref={canvasRef}
          className="absolute inset-0 cursor-crosshair"
          onClick={handleClick}
        />
      </div>
    </GlassPanel>
  );
}
