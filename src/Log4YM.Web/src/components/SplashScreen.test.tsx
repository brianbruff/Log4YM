import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, act } from '@testing-library/react';
import { SplashScreen } from './SplashScreen';

vi.mock('../version', () => ({
  APP_VERSION: '1.2.3',
}));

let mockConnectionState = 'disconnected';
vi.mock('../store/appStore', () => ({
  useAppStore: (selector: (s: { connectionState: string }) => unknown) =>
    selector({ connectionState: mockConnectionState }),
}));

describe('SplashScreen', () => {
  beforeEach(() => {
    mockConnectionState = 'disconnected';
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders the splash image', () => {
    render(<SplashScreen />);
    const img = screen.getByAltText('Log4YM');
    expect(img).toBeInTheDocument();
    expect(img).toHaveAttribute('src', './splash.webp');
  });

  it('shows the version text', () => {
    render(<SplashScreen />);
    expect(screen.getByText('v1.2.3')).toBeInTheDocument();
  });

  it('shows a loading spinner', () => {
    const { container } = render(<SplashScreen />);
    const spinner = container.querySelector('.animate-spin');
    expect(spinner).toBeInTheDocument();
  });

  it('starts fully visible', () => {
    const { container } = render(<SplashScreen />);
    const overlay = container.firstElementChild as HTMLElement;
    expect(overlay.className).toContain('opacity-100');
    expect(overlay.className).not.toContain('opacity-0');
  });

  it('fades out when connection state becomes connected', () => {
    const { container, rerender } = render(<SplashScreen />);
    mockConnectionState = 'connected';
    rerender(<SplashScreen />);

    const overlay = container.firstElementChild as HTMLElement;
    expect(overlay.className).toContain('opacity-0');
  });

  it('removes from DOM after fade-out completes', () => {
    const { container, rerender } = render(<SplashScreen />);
    mockConnectionState = 'connected';
    rerender(<SplashScreen />);

    // After fade-out, a 500ms timer removes from DOM
    act(() => {
      vi.advanceTimersByTime(500);
    });

    expect(container.firstElementChild).toBeNull();
  });

  it('auto-dismisses after 8 second timeout', () => {
    const { container } = render(<SplashScreen />);

    act(() => {
      vi.advanceTimersByTime(8000);
    });

    const overlay = container.firstElementChild as HTMLElement;
    expect(overlay.className).toContain('opacity-0');

    act(() => {
      vi.advanceTimersByTime(500);
    });

    expect(container.firstElementChild).toBeNull();
  });

  it('does not fade out before timeout if still disconnected', () => {
    const { container } = render(<SplashScreen />);

    act(() => {
      vi.advanceTimersByTime(7999);
    });

    const overlay = container.firstElementChild as HTMLElement;
    expect(overlay.className).toContain('opacity-100');
  });
});
