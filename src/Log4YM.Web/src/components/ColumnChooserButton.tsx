import { useState, useRef, useEffect, useMemo } from 'react';
import { SlidersHorizontal, Check } from 'lucide-react';
import type { GridApi, ColDef } from 'ag-grid-community';

interface ColumnChooserButtonProps {
  gridApi: GridApi | null;
  columnDefs: ColDef[];
}

export function ColumnChooserButton({ gridApi, columnDefs }: ColumnChooserButtonProps) {
  const [open, setOpen] = useState(false);
  const [columnVisibility, setColumnVisibility] = useState<Record<string, boolean>>({});
  const dropdownRef = useRef<HTMLDivElement>(null);

  // Only show columns with both a colId and a non-empty headerName
  const selectableColumns = useMemo(
    () => columnDefs.filter(col => col.colId && col.headerName),
    [columnDefs]
  );

  const refreshState = () => {
    if (!gridApi) return;
    const state = gridApi.getColumnState();
    const vis: Record<string, boolean> = {};
    state.forEach(col => { vis[col.colId] = !col.hide; });
    setColumnVisibility(vis);
  };

  const handleToggle = () => {
    if (!open) refreshState();
    setOpen(o => !o);
  };

  const toggleColumn = (colId: string) => {
    if (!gridApi) return;
    const newVisible = columnVisibility[colId] === false;
    gridApi.setColumnsVisible([colId], newVisible);
    setColumnVisibility(prev => ({ ...prev, [colId]: newVisible }));
  };

  useEffect(() => {
    if (!open) return;
    const onDocClick = (e: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener('mousedown', onDocClick);
    return () => document.removeEventListener('mousedown', onDocClick);
  }, [open]);

  return (
    <div className="relative" ref={dropdownRef}>
      <button
        onClick={handleToggle}
        disabled={!gridApi}
        className={`glass-button p-1.5 ${open ? 'text-accent-primary' : 'text-dark-300'} disabled:opacity-40 disabled:cursor-not-allowed`}
        title="Show/hide columns"
      >
        <SlidersHorizontal className="w-4 h-4" />
      </button>
      {open && (
        <div className="absolute right-0 top-8 z-50 min-w-[160px] bg-dark-800 border border-glass-100 rounded-lg shadow-xl py-1">
          <div className="px-3 py-1.5 text-xs font-ui text-dark-400 border-b border-glass-100">
            Toggle Columns
          </div>
          {selectableColumns.map(col => {
            const colId = col.colId!;
            const visible = columnVisibility[colId] !== false;
            return (
              <button
                key={colId}
                onClick={() => toggleColumn(colId)}
                className="flex items-center gap-2.5 w-full px-3 py-1.5 text-sm text-dark-200 hover:bg-dark-700 transition-colors font-ui"
              >
                <span className={`w-4 h-4 flex items-center justify-center rounded border flex-shrink-0 ${
                  visible ? 'border-accent-primary bg-accent-primary/20' : 'border-glass-200 bg-dark-900'
                }`}>
                  {visible && <Check className="w-2.5 h-2.5 text-accent-primary" />}
                </span>
                {col.headerName}
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}
