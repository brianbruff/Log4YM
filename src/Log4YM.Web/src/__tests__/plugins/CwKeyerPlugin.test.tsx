import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CwKeyerPlugin } from '../../plugins/CwKeyerPlugin';

// Mock useAppStore
const mockAppState = {
  cwKeyerStatus: new Map(),
  discoveredRadios: new Map(),
  selectedRadioId: null as string | null,
  radioConnectionStates: new Map(),
};

vi.mock('../../store/appStore', () => ({
  useAppStore: () => mockAppState,
}));

// Mock useSignalR
const mockSendCwKey = vi.fn();
const mockStopCwKey = vi.fn();
const mockSetCwSpeed = vi.fn();

vi.mock('../../hooks/useSignalR', () => ({
  useSignalR: () => ({
    sendCwKey: mockSendCwKey,
    stopCwKey: mockStopCwKey,
    setCwSpeed: mockSetCwSpeed,
  }),
}));

// Mock lucide-react icons to avoid rendering issues
vi.mock('lucide-react', () => ({
  Radio: ({ className }: { className?: string }) => <span data-testid="icon-radio" className={className} />,
  Play: ({ className }: { className?: string }) => <span data-testid="icon-play" className={className} />,
  Square: ({ className }: { className?: string }) => <span data-testid="icon-square" className={className} />,
  Settings: ({ className }: { className?: string }) => <span data-testid="icon-settings" className={className} />,
  Zap: ({ className }: { className?: string }) => <span data-testid="icon-zap" className={className} />,
}));

function setupConnectedRadio(radioId = 'radio-1') {
  mockAppState.discoveredRadios = new Map([[radioId, { id: radioId, name: 'Test Radio' }]]);
  mockAppState.radioConnectionStates = new Map([[radioId, 'Connected']]);
  mockAppState.selectedRadioId = radioId;
}

function resetMockState() {
  mockAppState.cwKeyerStatus = new Map();
  mockAppState.discoveredRadios = new Map();
  mockAppState.selectedRadioId = null;
  mockAppState.radioConnectionStates = new Map();
}

describe('CwKeyerPlugin', () => {
  beforeEach(() => {
    resetMockState();
    vi.clearAllMocks();
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  describe('rendering', () => {
    it('renders the CW Keyer panel', () => {
      render(<CwKeyerPlugin />);
      expect(screen.getByText('CW Keyer')).toBeDefined();
    });

    it('shows no radio warning when no radio is connected', () => {
      render(<CwKeyerPlugin />);
      expect(screen.getByText('No radio connected')).toBeDefined();
    });

    it('hides no radio warning when a radio is connected', () => {
      setupConnectedRadio();
      render(<CwKeyerPlugin />);
      expect(screen.queryByText('No radio connected')).toBeNull();
    });

    it('renders all 6 default macro buttons', () => {
      render(<CwKeyerPlugin />);
      expect(screen.getByText('CQ')).toBeDefined();
      expect(screen.getByText('599')).toBeDefined();
      expect(screen.getByText('QRZ')).toBeDefined();
      expect(screen.getByText('73')).toBeDefined();
      expect(screen.getByText('AGN')).toBeDefined();
      expect(screen.getByText('QSL')).toBeDefined();
    });

    it('displays current WPM in the header', () => {
      render(<CwKeyerPlugin />);
      expect(screen.getByText('20 WPM')).toBeDefined();
    });

    it('shows keying indicator when radio is keying', () => {
      setupConnectedRadio();
      mockAppState.cwKeyerStatus = new Map([['radio-1', {
        radioId: 'radio-1',
        isKeying: true,
        speedWpm: 20,
        currentMessage: 'CQ CQ',
      }]]);

      render(<CwKeyerPlugin />);
      expect(screen.getByText('Keying')).toBeDefined();
      expect(screen.getByText('Sending: CQ CQ')).toBeDefined();
    });
  });

  describe('button states', () => {
    it('disables Transmit and Stop when no radio is connected', () => {
      render(<CwKeyerPlugin />);
      const transmitBtn = screen.getByText('Transmit').closest('button')!;
      const stopBtn = screen.getByText('Stop').closest('button')!;

      expect(transmitBtn.disabled).toBe(true);
      expect(stopBtn.disabled).toBe(true);
    });

    it('disables Transmit when message is empty', () => {
      setupConnectedRadio();
      render(<CwKeyerPlugin />);

      const transmitBtn = screen.getByText('Transmit').closest('button')!;
      expect(transmitBtn.disabled).toBe(true);
    });

    it('enables Transmit when radio is connected and message is present', async () => {
      setupConnectedRadio();
      render(<CwKeyerPlugin />);

      const textarea = screen.getByPlaceholderText('Type your CW message here...');
      fireEvent.change(textarea, { target: { value: 'CQ CQ' } });

      const transmitBtn = screen.getByText('Transmit').closest('button')!;
      expect(transmitBtn.disabled).toBe(false);
    });

    it('disables Stop when not keying', () => {
      setupConnectedRadio();
      render(<CwKeyerPlugin />);

      const stopBtn = screen.getByText('Stop').closest('button')!;
      expect(stopBtn.disabled).toBe(true);
    });

    it('enables Stop when keying', () => {
      setupConnectedRadio();
      mockAppState.cwKeyerStatus = new Map([['radio-1', {
        radioId: 'radio-1',
        isKeying: true,
        speedWpm: 20,
        currentMessage: 'CQ',
      }]]);

      render(<CwKeyerPlugin />);
      const stopBtn = screen.getByText('Stop').closest('button')!;
      expect(stopBtn.disabled).toBe(false);
    });

    it('disables macro buttons when no radio is connected', () => {
      render(<CwKeyerPlugin />);
      const cqBtn = screen.getByText('CQ').closest('button')!;
      expect(cqBtn.disabled).toBe(true);
    });

    it('enables macro buttons when radio is connected', () => {
      setupConnectedRadio();
      render(<CwKeyerPlugin />);
      const cqBtn = screen.getByText('CQ').closest('button')!;
      expect(cqBtn.disabled).toBe(false);
    });

    it('disables Transmit when transmit-as-you-type is enabled', () => {
      setupConnectedRadio();
      render(<CwKeyerPlugin />);

      const textarea = screen.getByPlaceholderText('Type your CW message here...');
      fireEvent.change(textarea, { target: { value: 'CQ CQ' } });

      const checkbox = screen.getByRole('checkbox');
      fireEvent.click(checkbox);

      const transmitBtn = screen.getByText('Transmit').closest('button')!;
      expect(transmitBtn.disabled).toBe(true);
    });
  });

  describe('transmit functionality', () => {
    it('sends CW message on Transmit button click', async () => {
      setupConnectedRadio();
      render(<CwKeyerPlugin />);

      const textarea = screen.getByPlaceholderText('Type your CW message here...');
      fireEvent.change(textarea, { target: { value: 'CQ CQ DE W1AW' } });

      const transmitBtn = screen.getByText('Transmit').closest('button')!;
      fireEvent.click(transmitBtn);

      expect(mockSendCwKey).toHaveBeenCalledWith('radio-1', 'CQ CQ DE W1AW', 20);
    });

    it('sends stop command on Stop button click', async () => {
      setupConnectedRadio();
      mockAppState.cwKeyerStatus = new Map([['radio-1', {
        radioId: 'radio-1',
        isKeying: true,
        speedWpm: 20,
        currentMessage: 'CQ',
      }]]);

      render(<CwKeyerPlugin />);
      const stopBtn = screen.getByText('Stop').closest('button')!;
      fireEvent.click(stopBtn);

      expect(mockStopCwKey).toHaveBeenCalledWith('radio-1');
    });

    it('does not transmit whitespace-only messages', () => {
      setupConnectedRadio();
      render(<CwKeyerPlugin />);

      const textarea = screen.getByPlaceholderText('Type your CW message here...');
      fireEvent.change(textarea, { target: { value: '   ' } });

      const transmitBtn = screen.getByText('Transmit').closest('button')!;
      // Button should be disabled for whitespace-only
      expect(transmitBtn.disabled).toBe(true);
    });
  });

  describe('transmit as you type', () => {
    it('sends after 500ms pause when enabled', async () => {
      setupConnectedRadio();
      render(<CwKeyerPlugin />);

      const checkbox = screen.getByRole('checkbox');
      fireEvent.click(checkbox);

      const textarea = screen.getByPlaceholderText('Type your CW message here...');
      fireEvent.change(textarea, { target: { value: 'CQ' } });

      // Should not have sent yet
      expect(mockSendCwKey).not.toHaveBeenCalled();

      // Advance timer past debounce
      vi.advanceTimersByTime(500);

      expect(mockSendCwKey).toHaveBeenCalledWith('radio-1', 'CQ', 20);
    });

    it('debounces rapid typing', () => {
      setupConnectedRadio();
      render(<CwKeyerPlugin />);

      const checkbox = screen.getByRole('checkbox');
      fireEvent.click(checkbox);

      const textarea = screen.getByPlaceholderText('Type your CW message here...');

      // Simulate rapid typing
      fireEvent.change(textarea, { target: { value: 'C' } });
      vi.advanceTimersByTime(200);
      fireEvent.change(textarea, { target: { value: 'CQ' } });
      vi.advanceTimersByTime(200);
      fireEvent.change(textarea, { target: { value: 'CQ C' } });
      vi.advanceTimersByTime(500);

      // Should only send the final value
      expect(mockSendCwKey).toHaveBeenCalledTimes(1);
      expect(mockSendCwKey).toHaveBeenCalledWith('radio-1', 'CQ C', 20);
    });

    it('does not send when disabled', () => {
      setupConnectedRadio();
      render(<CwKeyerPlugin />);

      // transmit-as-you-type is off by default
      const textarea = screen.getByPlaceholderText('Type your CW message here...');
      fireEvent.change(textarea, { target: { value: 'CQ CQ' } });

      vi.advanceTimersByTime(1000);
      expect(mockSendCwKey).not.toHaveBeenCalled();
    });
  });

  describe('speed control', () => {
    it('sends speed change to backend', () => {
      setupConnectedRadio();
      render(<CwKeyerPlugin />);

      const slider = screen.getByRole('slider');
      fireEvent.change(slider, { target: { value: '35' } });

      expect(mockSetCwSpeed).toHaveBeenCalledWith('radio-1', 35);
    });

    it('updates displayed WPM', () => {
      setupConnectedRadio();
      render(<CwKeyerPlugin />);

      const slider = screen.getByRole('slider');
      fireEvent.change(slider, { target: { value: '45' } });

      expect(screen.getByText('45 WPM')).toBeDefined();
    });
  });

  describe('macro buttons', () => {
    it('sends macro text on click', () => {
      setupConnectedRadio();
      render(<CwKeyerPlugin />);

      const agnBtn = screen.getByText('AGN').closest('button')!;
      fireEvent.click(agnBtn);

      // AGN macro text is 'AGN PSE' (no variables to substitute)
      expect(mockSendCwKey).toHaveBeenCalledWith('radio-1', 'AGN PSE', 20);
    });

    it('substitutes {MYCALL} variable in macro', () => {
      setupConnectedRadio();
      render(<CwKeyerPlugin />);

      const qrzBtn = screen.getByText('QRZ').closest('button')!;
      fireEvent.click(qrzBtn);

      // QRZ macro is '{MYCALL} QRZ' -> 'STATION QRZ'
      expect(mockSendCwKey).toHaveBeenCalledWith('radio-1', 'STATION QRZ', 20);
    });

    it('substitutes {MYGRID} variable in macro', () => {
      setupConnectedRadio();
      render(<CwKeyerPlugin />);

      const btn599 = screen.getByText('599').closest('button')!;
      fireEvent.click(btn599);

      // 599 macro is 'TU 599 {MYGRID} {MYGRID} K' -> 'TU 599 FN31 FN31 K'
      expect(mockSendCwKey).toHaveBeenCalledWith('radio-1', 'TU 599 FN31 FN31 K', 20);
    });

    it('substitutes both {MYCALL} and {MYGRID}', () => {
      setupConnectedRadio();
      render(<CwKeyerPlugin />);

      const cqBtn = screen.getByText('CQ').closest('button')!;
      fireEvent.click(cqBtn);

      // CQ macro: 'CQ CQ CQ DE {MYCALL} {MYCALL} K' -> 'CQ CQ CQ DE STATION STATION K'
      expect(mockSendCwKey).toHaveBeenCalledWith('radio-1', 'CQ CQ CQ DE STATION STATION K', 20);
    });

    it('sets message text to processed macro', () => {
      setupConnectedRadio();
      render(<CwKeyerPlugin />);

      const qslBtn = screen.getByText('QSL').closest('button')!;
      fireEvent.click(qslBtn);

      const textarea = screen.getByPlaceholderText('Type your CW message here...') as HTMLTextAreaElement;
      expect(textarea.value).toBe('QSL TU 73');
    });
  });

  describe('macro settings modal', () => {
    it('opens settings modal on Settings button click', () => {
      render(<CwKeyerPlugin />);

      const settingsBtn = screen.getByTitle('Macro Settings');
      fireEvent.click(settingsBtn);

      expect(screen.getByText('CW Macro Settings')).toBeDefined();
    });

    it('closes modal on Cancel', () => {
      render(<CwKeyerPlugin />);

      const settingsBtn = screen.getByTitle('Macro Settings');
      fireEvent.click(settingsBtn);
      expect(screen.getByText('CW Macro Settings')).toBeDefined();

      const cancelBtn = screen.getByText('Cancel');
      fireEvent.click(cancelBtn);

      expect(screen.queryByText('CW Macro Settings')).toBeNull();
    });

    it('saves edited macros', () => {
      setupConnectedRadio();
      render(<CwKeyerPlugin />);

      // Open settings
      const settingsBtn = screen.getByTitle('Macro Settings');
      fireEvent.click(settingsBtn);

      // Edit the first macro label
      const labelInputs = screen.getAllByPlaceholderText('Label');
      fireEvent.change(labelInputs[0], { target: { value: 'TEST' } });

      // Save
      const saveBtn = screen.getByText('Save');
      fireEvent.click(saveBtn);

      // Modal should close
      expect(screen.queryByText('CW Macro Settings')).toBeNull();

      // New label should appear
      expect(screen.getByText('TEST')).toBeDefined();
    });

    it('resets macros to defaults', () => {
      render(<CwKeyerPlugin />);

      // Open settings
      const settingsBtn = screen.getByTitle('Macro Settings');
      fireEvent.click(settingsBtn);

      // Edit a label
      const labelInputs = screen.getAllByPlaceholderText('Label');
      fireEvent.change(labelInputs[0], { target: { value: 'CHANGED' } });

      // Reset
      const resetBtn = screen.getByText('Reset to Defaults');
      fireEvent.click(resetBtn);

      // Should be back to 'CQ'
      const labelInputsAfter = screen.getAllByPlaceholderText('Label');
      expect((labelInputsAfter[0] as HTMLInputElement).value).toBe('CQ');
    });
  });

  describe('radio selection', () => {
    it('uses selectedRadioId when available', () => {
      setupConnectedRadio('my-radio');
      render(<CwKeyerPlugin />);

      const textarea = screen.getByPlaceholderText('Type your CW message here...');
      fireEvent.change(textarea, { target: { value: 'CQ' } });

      const transmitBtn = screen.getByText('Transmit').closest('button')!;
      fireEvent.click(transmitBtn);

      expect(mockSendCwKey).toHaveBeenCalledWith('my-radio', 'CQ', 20);
    });

    it('falls back to first connected radio when no selectedRadioId', () => {
      mockAppState.selectedRadioId = null;
      mockAppState.discoveredRadios = new Map([['fallback-radio', { id: 'fallback-radio', name: 'Fallback' }]]);
      mockAppState.radioConnectionStates = new Map([['fallback-radio', 'Connected']]);

      render(<CwKeyerPlugin />);

      const textarea = screen.getByPlaceholderText('Type your CW message here...');
      fireEvent.change(textarea, { target: { value: 'CQ' } });

      const transmitBtn = screen.getByText('Transmit').closest('button')!;
      fireEvent.click(transmitBtn);

      expect(mockSendCwKey).toHaveBeenCalledWith('fallback-radio', 'CQ', 20);
    });

    it('picks Monitoring radio as fallback', () => {
      mockAppState.selectedRadioId = null;
      mockAppState.discoveredRadios = new Map([['mon-radio', { id: 'mon-radio', name: 'Monitor' }]]);
      mockAppState.radioConnectionStates = new Map([['mon-radio', 'Monitoring']]);

      render(<CwKeyerPlugin />);
      expect(screen.queryByText('No radio connected')).toBeNull();
    });
  });
});
