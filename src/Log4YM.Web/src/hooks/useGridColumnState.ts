import { useRef, useState, useCallback } from 'react';
import type { GridApi, GridReadyEvent, ColumnMovedEvent, ColumnVisibleEvent } from 'ag-grid-community';
import type { AgGridReact } from 'ag-grid-react';

export function useGridColumnState<TData = unknown>(storageKey: string) {
  const gridRef = useRef<AgGridReact<TData>>(null);
  const [gridApi, setGridApi] = useState<GridApi | null>(null);

  const onGridReady = useCallback((event: GridReadyEvent) => {
    setGridApi(event.api);
    const saved = localStorage.getItem(storageKey);
    if (saved) {
      try {
        event.api.applyColumnState({ state: JSON.parse(saved), applyOrder: true });
      } catch {
        // ignore invalid saved state
      }
    }
  }, [storageKey]);

  const saveState = useCallback((api: GridApi) => {
    localStorage.setItem(storageKey, JSON.stringify(api.getColumnState()));
  }, [storageKey]);

  const onColumnMoved = useCallback((event: ColumnMovedEvent) => {
    if (event.finished) saveState(event.api);
  }, [saveState]);

  const onColumnVisible = useCallback((event: ColumnVisibleEvent) => {
    saveState(event.api);
  }, [saveState]);

  return { gridRef, gridApi, onGridReady, onColumnMoved, onColumnVisible };
}
