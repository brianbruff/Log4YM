import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { AboutDialog } from './AboutDialog';

vi.mock('../version', () => ({
  APP_VERSION: '1.2.3',
}));

describe('AboutDialog', () => {
  it('does not render when isOpen is false', () => {
    const { container } = render(<AboutDialog isOpen={false} onClose={vi.fn()} />);
    expect(container.firstElementChild).toBeNull();
  });

  it('renders when isOpen is true', () => {
    render(<AboutDialog isOpen={true} onClose={vi.fn()} />);
    expect(screen.getByText('Log4YM')).toBeInTheDocument();
  });

  it('shows the logo', () => {
    render(<AboutDialog isOpen={true} onClose={vi.fn()} />);
    const logo = screen.getByAltText('Log4YM Logo');
    expect(logo).toBeInTheDocument();
    expect(logo).toHaveAttribute('src', './logo.webp');
  });

  it('shows the app name and description', () => {
    render(<AboutDialog isOpen={true} onClose={vi.fn()} />);
    expect(screen.getByText('Log4YM')).toBeInTheDocument();
    expect(screen.getByText('Amateur Radio Logging Software')).toBeInTheDocument();
  });

  it('shows the version', () => {
    render(<AboutDialog isOpen={true} onClose={vi.fn()} />);
    expect(screen.getByText('Version 1.2.3')).toBeInTheDocument();
  });

  it('shows GitHub and Report Issue links', () => {
    render(<AboutDialog isOpen={true} onClose={vi.fn()} />);
    expect(screen.getByText('GitHub')).toBeInTheDocument();
    expect(screen.getByText('Report Issue')).toBeInTheDocument();
  });

  it('calls onClose when the X close button is clicked', () => {
    const onClose = vi.fn();
    render(<AboutDialog isOpen={true} onClose={onClose} />);
    const closeButton = screen.getByTitle('Close');
    fireEvent.click(closeButton);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('calls onClose when the Close button is clicked', () => {
    const onClose = vi.fn();
    render(<AboutDialog isOpen={true} onClose={onClose} />);
    const closeButton = screen.getByText('Close');
    fireEvent.click(closeButton);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('calls onClose when Escape key is pressed', () => {
    const onClose = vi.fn();
    render(<AboutDialog isOpen={true} onClose={onClose} />);
    fireEvent.keyDown(window, { key: 'Escape' });
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('calls onClose when backdrop is clicked', () => {
    const onClose = vi.fn();
    const { container } = render(<AboutDialog isOpen={true} onClose={onClose} />);
    const backdrop = container.firstElementChild as HTMLElement;
    fireEvent.click(backdrop);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('does not call onClose when dialog content is clicked', () => {
    const onClose = vi.fn();
    render(<AboutDialog isOpen={true} onClose={onClose} />);
    const dialogContent = screen.getByText('Log4YM').closest('.glass-panel') as HTMLElement;
    fireEvent.click(dialogContent);
    expect(onClose).not.toHaveBeenCalled();
  });
});
