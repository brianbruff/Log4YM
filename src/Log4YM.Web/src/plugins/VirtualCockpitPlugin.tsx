import { useCallback, useState } from 'react';
import { GlassPanel } from '../components/GlassPanel';
import { MapCore } from './MapPlugin';
import { GlobeCore } from './GlobePlugin';
import { RotatorCore } from './RotatorPlugin';
import { LayoutDashboard, Maximize2, Radio, Info } from 'lucide-react';
import { useSettingsStore } from '../store/settingsStore';
import { useAppStore } from '../store/appStore';

export function VirtualCockpitPlugin() {
  const [isFullscreen, setIsFullscreen] = useState(false);
  const { settings } = useSettingsStore();
  const { stationGrid, rotatorPosition } = useAppStore();

  const toggleFullscreen = useCallback(() => {
    const el = document.getElementById('cockpit-plugin-container');
    if (!el) return;

    if (!isFullscreen) {
      el.requestFullscreen?.();
    } else {
      document.exitFullscreen?.();
    }
    setIsFullscreen(!isFullscreen);
  }, [isFullscreen]);

  return (
    <GlassPanel
      title="Virtual Cockpit"
      icon={<LayoutDashboard className="w-5 h-5" />}
      actions={
        <button
          onClick={toggleFullscreen}
          className="glass-button p-1.5"
          title="Fullscreen"
        >
          <Maximize2 className="w-4 h-4" />
        </button>
      }
    >
      <div id="cockpit-plugin-container" className="relative w-full h-full min-h-[500px] bg-dark-900 overflow-hidden font-ui">
        
        {/* Background Map - Full Screen */}
        <div className="absolute inset-0 z-0">
          <MapCore />
        </div>
        
        {/* Z-10: Unified Cockpit Background Shapes (casts the single master shadow) */}
        <div className="absolute top-0 bottom-0 left-0 z-10 pointer-events-none drop-shadow-[15px_0_30px_rgba(0,0,0,0.85)]">
          
          {/* Main Sidebar Base */}
          <div className="absolute top-0 bottom-0 left-0 w-[300px] bg-[#0a0e14] border-r-[2px] border-[#334155] pointer-events-auto" />
          
          {/* Bulge Base (clipped to only show exactly to the right of the sidebar border) */}
          <div className="absolute top-1/2 -translate-y-1/2 left-[298px] w-[202px] h-[500px] overflow-hidden pointer-events-none">
             {/* The circle perfectly aligns with x=0, so its arc perfectly intersects the x=298 straight line */}
             <div className="absolute top-1/2 -translate-y-1/2 left-[-298px] w-[500px] h-[500px] rounded-full bg-[#0a0e14] border-[2px] border-[#334155] pointer-events-auto" />
          </div>

        </div>

        {/* Z-20: Interactive Content Layer */}
        <div className="absolute inset-0 z-20 pointer-events-none">
          
          {/* Globe Component (Touching Left, 500x500) */}
          {/* Since it sits at z-20, it perfectly covers the straight x=298 background border behind it, 
              preventing the straight line from drawing "through" the globe */}
          <div className="absolute top-1/2 -translate-y-1/2 left-0 w-[500px] h-[500px] pointer-events-auto rounded-full overflow-hidden bg-[#020304]">
            <GlobeCore hideOverlays={true} hideCompass={true} />
          </div>

          {/* Sidebar Content (Rendered after Globe to stay on top if screen is very short) */}
          <div className="absolute top-0 bottom-0 left-0 w-[300px] flex flex-col justify-between py-8">
            {/* Top Section: Station Info */}
            <div className="px-6 pointer-events-auto">
               <div className="flex items-center gap-2 mb-4 text-accent-primary font-display font-bold text-sm tracking-wider">
                 <Radio className="w-4 h-4" />
                 <span>STATION</span>
               </div>
               <div className="space-y-2 font-mono text-xs text-dark-300">
                 <div className="flex justify-between items-center border-b border-glass-100/10 pb-2">
                   <span>Call:</span>
                   <span className="text-gray-100 font-bold">{settings.station.callsign || 'N/A'}</span>
                 </div>
                 <div className="flex justify-between items-center border-b border-glass-100/10 pb-2">
                   <span>Grid:</span>
                   <span className="text-gray-100 font-bold">{stationGrid || 'N/A'}</span>
                 </div>
               </div>
            </div>

            {/* Bottom Section: System & Rotator */}
            <div className="px-6 pointer-events-auto">
               <div className="flex items-center gap-2 mb-4 text-accent-info font-display font-bold text-sm tracking-wider">
                 <Info className="w-4 h-4" />
                 <span>SYSTEM ONLINE</span>
               </div>
               
               <div className="font-mono text-xs text-dark-300 flex justify-between items-center border-b border-glass-100/10 pb-3 mb-4">
                  <span>Rotator:</span>
                  {settings.rotator.enabled ? <span className="text-accent-success font-bold">Online</span> : <span className="text-accent-danger font-bold">Offline</span>}
               </div>

               {settings.rotator.enabled && (
                 <div className="flex flex-col items-center justify-center">
                   <span className="text-[10px] text-dark-400 font-ui uppercase tracking-widest mb-1">Current Heading</span>
                   <div className="text-5xl font-display font-bold text-accent-primary drop-shadow-[0_2px_10px_rgba(255,180,50,0.3)] mb-4">
                      {rotatorPosition?.currentAzimuth?.toFixed(0) || 0}&deg;
                   </div>
                   
                   <div className="w-full">
                     <RotatorCore hideCompass={true} integratedMode={true} />
                   </div>
                 </div>
               )}
            </div>
          </div>

        </div>

        {/* Z-30: Globe Border Overlay */}
        {/* Because the Globe at z-20 covered the background circle's right border, 
            we redraw just the protruding arc border over the globe here. */}
        <div className="absolute top-1/2 -translate-y-1/2 left-[298px] w-[202px] h-[500px] overflow-hidden z-30 pointer-events-none">
           <div className="absolute top-1/2 -translate-y-1/2 left-[-298px] w-[500px] h-[500px] rounded-full border-[2px] border-[#334155]" />
        </div>

      </div>
    </GlassPanel>
  );
}
