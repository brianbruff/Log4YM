import { ReactNode } from 'react';

interface GlassPanelProps {
  children: ReactNode;
  className?: string;
  title?: string;
  icon?: ReactNode;
  actions?: ReactNode;
}

export function GlassPanel({ children, className = '', title, icon, actions }: GlassPanelProps) {
  return (
    <div className={`glass-panel h-full flex flex-col ${className}`}>
      {(title || actions) && (
        <div className="flex items-center justify-between px-4 py-3 border-b border-glass-100">
          <div className="flex items-center gap-2">
            {icon && <span className="text-accent-secondary">{icon}</span>}
            {title && <h3 className="font-semibold text-accent-success font-ui text-sm tracking-wide uppercase">{title}</h3>}
          </div>
          {actions && <div className="flex items-center gap-2">{actions}</div>}
        </div>
      )}
      <div className="flex-1 overflow-auto">
        {children}
      </div>
    </div>
  );
}

interface GlassCardProps {
  children: ReactNode;
  className?: string;
  hover?: boolean;
  onClick?: () => void;
}

export function GlassCard({ children, className = '', hover = false, onClick }: GlassCardProps) {
  return (
    <div
      className={`
        bg-dark-700/50 backdrop-blur-sm border border-glass-100 rounded-lg p-4
        ${hover ? 'hover:bg-dark-600/50 hover:border-glass-200 cursor-pointer transition-all duration-200' : ''}
        ${className}
      `}
      onClick={onClick}
    >
      {children}
    </div>
  );
}
