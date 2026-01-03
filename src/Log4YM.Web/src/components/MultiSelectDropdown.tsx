import { useState, useRef, useEffect } from 'react';
import { ChevronDown, X, Check } from 'lucide-react';

export interface MultiSelectOption {
  value: string;
  label: string;
}

interface MultiSelectDropdownProps {
  options: MultiSelectOption[];
  selected: string[];
  onChange: (selected: string[]) => void;
  placeholder: string;
  className?: string;
}

export function MultiSelectDropdown({
  options,
  selected,
  onChange,
  placeholder,
  className = '',
}: MultiSelectDropdownProps) {
  const [isOpen, setIsOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  // Close dropdown when clicking outside
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const toggleOption = (value: string) => {
    if (selected.includes(value)) {
      onChange(selected.filter((v) => v !== value));
    } else {
      onChange([...selected, value]);
    }
  };

  const clearAll = (e: React.MouseEvent) => {
    e.stopPropagation();
    onChange([]);
  };

  const hasSelection = selected.length > 0;
  const displayText = hasSelection
    ? `${selected.length} selected`
    : placeholder;

  return (
    <div ref={dropdownRef} className={`relative ${className}`}>
      {/* Trigger Button */}
      <button
        type="button"
        onClick={() => setIsOpen(!isOpen)}
        className={`
          w-full flex items-center justify-between gap-2 px-3 py-2
          bg-dark-800 border rounded-lg text-sm text-left
          transition-colors duration-150
          ${hasSelection
            ? 'border-accent-primary/50 text-gray-200'
            : 'border-glass-100 text-gray-400'
          }
          ${isOpen ? 'border-accent-primary/70' : ''}
          hover:border-glass-200 focus:outline-none focus:border-accent-primary/50
        `}
      >
        <span className={hasSelection ? 'text-gray-200' : 'text-gray-500'}>
          {displayText}
        </span>
        <div className="flex items-center gap-1">
          {hasSelection && (
            <span
              role="button"
              tabIndex={-1}
              onClick={clearAll}
              onKeyDown={(e) => e.key === 'Enter' && clearAll(e as unknown as React.MouseEvent)}
              className="p-0.5 rounded hover:bg-dark-600 text-gray-400 hover:text-gray-200 cursor-pointer"
              title="Clear all"
            >
              <X className="w-3.5 h-3.5" />
            </span>
          )}
          <ChevronDown
            className={`w-4 h-4 text-gray-400 transition-transform ${isOpen ? 'rotate-180' : ''}`}
          />
        </div>
      </button>

      {/* Dropdown Menu */}
      {isOpen && (
        <div className="absolute z-50 w-full mt-1 py-1 bg-dark-800 border border-glass-200 rounded-lg shadow-xl max-h-60 overflow-y-auto">
          {/* Clear All Option */}
          {hasSelection && (
            <>
              <button
                onClick={clearAll}
                className="w-full px-3 py-2 text-left text-sm text-accent-warning hover:bg-dark-700 flex items-center gap-2"
              >
                <X className="w-4 h-4" />
                Clear All ({selected.length})
              </button>
              <div className="border-t border-glass-100 my-1" />
            </>
          )}

          {/* Options */}
          {options.map((option) => {
            const isSelected = selected.includes(option.value);
            return (
              <button
                key={option.value}
                onClick={() => toggleOption(option.value)}
                className={`
                  w-full px-3 py-2 text-left text-sm flex items-center gap-2
                  ${isSelected
                    ? 'bg-accent-primary/10 text-accent-primary'
                    : 'text-gray-300 hover:bg-dark-700'
                  }
                `}
              >
                <div
                  className={`
                    w-4 h-4 rounded border flex items-center justify-center
                    ${isSelected
                      ? 'bg-accent-primary border-accent-primary'
                      : 'border-glass-200'
                    }
                  `}
                >
                  {isSelected && <Check className="w-3 h-3 text-white" />}
                </div>
                {option.label}
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}
