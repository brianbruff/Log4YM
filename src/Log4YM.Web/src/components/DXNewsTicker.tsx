import { useEffect, useState, useRef } from 'react';
import { api, DXNewsItem } from '../api/client';

export function DXNewsTicker() {
  const [news, setNews] = useState<DXNewsItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);
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
    const interval = setInterval(fetchNews, 30 * 60 * 1000); // 30 minutes

    return () => clearInterval(interval);
  }, []);

  // Calculate animation duration based on content width
  useEffect(() => {
    if (contentRef.current && news.length > 0) {
      const contentWidth = contentRef.current.scrollWidth;
      // Speed: ~100 pixels per second
      const duration = contentWidth / 100;
      contentRef.current.style.animationDuration = `${duration}s`;
    }
  }, [news]);

  if (isLoading || news.length === 0) {
    return null;
  }

  // Duplicate content for seamless loop
  const duplicatedNews = [...news, ...news];

  return (
    <div className="absolute bottom-0 left-0 right-0 h-8 bg-dark-900/90 backdrop-blur-sm border-t border-glass-100 overflow-hidden z-[1000]">
      {/* Gradient fade edges */}
      <div className="absolute left-0 top-0 bottom-0 w-16 bg-gradient-to-r from-dark-900/90 to-transparent z-10 pointer-events-none" />
      <div className="absolute right-0 top-0 bottom-0 w-16 bg-gradient-to-l from-dark-900/90 to-transparent z-10 pointer-events-none" />

      {/* Ticker container */}
      <div ref={tickerRef} className="h-full flex items-center">
        {/* DX NEWS badge */}
        <div className="flex-shrink-0 px-3 py-1 bg-accent-warning/20 text-accent-warning text-xs font-bold tracking-wide border-r border-glass-100">
          DX NEWS
        </div>

        {/* Scrolling content */}
        <div className="flex-1 relative overflow-hidden">
          <div
            ref={contentRef}
            className="flex items-center whitespace-nowrap animate-scroll"
            style={{
              animationTimingFunction: 'linear',
              animationIterationCount: 'infinite',
            }}
          >
            {duplicatedNews.map((item, index) => (
              <div key={index} className="inline-flex items-center px-4">
                <span className="text-orange-400 font-medium text-sm">{item.title}</span>
                {item.description && (
                  <>
                    <span className="mx-2 text-gray-500">•</span>
                    <span className="text-gray-400 text-xs">{item.description}</span>
                  </>
                )}
                {index < duplicatedNews.length - 1 && (
                  <span className="mx-3 text-gray-600">◆</span>
                )}
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
