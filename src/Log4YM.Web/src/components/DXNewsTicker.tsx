import { useEffect, useState, useRef } from 'react';
import { api, DXNewsItem } from '../api/client';

export function DXNewsTicker() {
  const [news, setNews] = useState<DXNewsItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [animDuration, setAnimDuration] = useState(120);
  const tickerRef = useRef<HTMLDivElement>(null);
  const contentRef = useRef<HTMLDivElement>(null);

  // Fetch DX news on mount and refresh every 30 minutes
  useEffect(() => {
    const fetchNews = async () => {
      try {
        const data = await api.getDXNews();
        setNews(data);
        setIsLoading(false);
      } catch (error) {
        console.error('Failed to fetch DX news:', error);
        setIsLoading(false);
      }
    };

    fetchNews();
    const interval = setInterval(fetchNews, 30 * 60 * 1000);
    return () => clearInterval(interval);
  }, []);

  // Calculate animation duration based on content width (~90px/s like OpenHamClock)
  useEffect(() => {
    if (contentRef.current && tickerRef.current && news.length > 0) {
      const contentWidth = contentRef.current.scrollWidth;
      const containerWidth = tickerRef.current.offsetWidth;
      const duration = Math.max(20, (contentWidth + containerWidth) / 90);
      setAnimDuration(duration);
    }
  }, [news]);

  if (isLoading || news.length === 0) {
    return null;
  }

  // Duplicate content for seamless loop
  const tickerItems = news.map(item => ({
    title: item.title,
    description: item.description,
  }));

  return (
    <div className="absolute bottom-0 left-0 right-0 h-7 bg-dark-900/90 backdrop-blur-sm border-t border-glass-100 overflow-hidden z-[1000]">
      {/* Ticker container */}
      <div ref={tickerRef} className="h-full flex items-center">
        {/* DX NEWS badge */}
        <div className="flex-shrink-0 px-2 h-full flex items-center bg-accent-warning/20 text-accent-warning text-[10px] font-bold tracking-wide border-r border-glass-100 font-mono">
          DX NEWS
        </div>

        {/* Scrolling content with mask fade edges */}
        <div
          className="flex-1 overflow-hidden relative h-full"
          style={{
            maskImage: 'linear-gradient(to right, transparent 0%, black 3%, black 97%, transparent 100%)',
            WebkitMaskImage: 'linear-gradient(to right, transparent 0%, black 3%, black 97%, transparent 100%)',
          }}
        >
          <div
            ref={contentRef}
            className="inline-flex items-center h-full whitespace-nowrap"
            style={{
              animation: `dxnews-scroll ${animDuration}s linear infinite`,
              paddingLeft: '100%',
            }}
          >
            {tickerItems.map((item, i) => (
              <span key={i} className="inline-flex items-center">
                <span className="text-orange-400 font-bold text-[11px] font-mono mr-1.5">{item.title}</span>
                {item.description && (
                  <span className="text-gray-400 text-[11px] font-mono mr-3">{item.description}</span>
                )}
                <span className="text-gray-600 text-[10px] mr-3">◆</span>
              </span>
            ))}
            {/* Duplicate for seamless loop */}
            {tickerItems.map((item, i) => (
              <span key={`dup-${i}`} className="inline-flex items-center">
                <span className="text-orange-400 font-bold text-[11px] font-mono mr-1.5">{item.title}</span>
                {item.description && (
                  <span className="text-gray-400 text-[11px] font-mono mr-3">{item.description}</span>
                )}
                <span className="text-gray-600 text-[10px] mr-3">◆</span>
              </span>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
