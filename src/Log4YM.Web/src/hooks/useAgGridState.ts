import { useCallback, useRef } from 'react';
import { ColumnState, GridReadyEvent, ColumnMovedEvent, ColumnResizedEvent, ColumnVisibleEvent, ColumnPinnedEvent, SortChangedEvent } from 'ag-grid-community';
import { useSettingsStore } from '../store/settingsStore';

/**
 * Hook for persisting AG Grid column state (order, width, visibility, sort, pinned)
 * to the database via the settings API.
 *
 * Usage:
 *   const { onGridReady, onColumnChanged, onSortChanged } = useAgGridState('logHistory');
 *   <AgGridReact onGridReady={onGridReady} onColumnMoved={onColumnChanged} ... />
 */
export function useAgGridState(tableId: string) {
  const saveTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const gridApiRef = useRef<GridReadyEvent['api'] | null>(null);

  const saveColumnState = useCallback((api: GridReadyEvent['api']) => {
    // Debounce saves to avoid hammering the API during rapid column resizing
    if (saveTimerRef.current) {
      clearTimeout(saveTimerRef.current);
    }
    saveTimerRef.current = setTimeout(async () => {
      const columnState = api.getColumnState();
      const json = JSON.stringify(columnState);

      // Update the store locally
      const store = useSettingsStore.getState();
      store.settings.gridStates[tableId] = json;

      // Save directly to the dedicated endpoint (avoids sending full settings)
      try {
        await fetch(`/api/settings/grid-state/${tableId}`, {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(json),
        });
      } catch (error) {
        console.warn(`Failed to save grid state for ${tableId}:`, error);
      }
    }, 500);
  }, [tableId]);

  const onGridReady = useCallback((event: GridReadyEvent) => {
    gridApiRef.current = event.api;

    // Restore saved column state if available
    const { settings } = useSettingsStore.getState();
    const savedState = settings.gridStates[tableId];
    if (savedState) {
      try {
        const columnState: ColumnState[] = JSON.parse(savedState);
        event.api.applyColumnState({ state: columnState, applyOrder: true });
      } catch (error) {
        console.warn(`Failed to restore grid state for ${tableId}:`, error);
      }
    }
  }, [tableId]);

  const onColumnChanged = useCallback((event: ColumnMovedEvent | ColumnResizedEvent | ColumnVisibleEvent | ColumnPinnedEvent) => {
    // Only save on finished events (not during drag)
    if ('finished' in event && !event.finished) return;
    saveColumnState(event.api);
  }, [saveColumnState]);

  const onSortChanged = useCallback((event: SortChangedEvent) => {
    saveColumnState(event.api);
  }, [saveColumnState]);

  return { onGridReady, onColumnChanged, onSortChanged };
}
