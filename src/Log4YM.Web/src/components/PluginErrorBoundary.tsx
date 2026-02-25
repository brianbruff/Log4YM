import { Component, type ReactNode, type ErrorInfo } from 'react';

interface Props {
  pluginId: string;
  children: ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
  retryCount: number;
}

const MAX_AUTO_RETRIES = 3;
const AUTO_RETRY_DELAY_MS = 500;

export class PluginErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false, error: null, retryCount: 0 };
  private autoRetryTimer: ReturnType<typeof setTimeout> | null = null;

  static getDerivedStateFromError(error: Error): Partial<State> {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error(`Plugin "${this.props.pluginId}" crashed:`, error, info.componentStack);
    // Auto-retry a few times before showing the manual retry UI
    if (this.state.retryCount < MAX_AUTO_RETRIES) {
      this.autoRetryTimer = setTimeout(() => {
        this.setState((s) => ({ hasError: false, error: null, retryCount: s.retryCount + 1 }));
      }, AUTO_RETRY_DELAY_MS);
    }
  }

  componentWillUnmount() {
    if (this.autoRetryTimer) clearTimeout(this.autoRetryTimer);
  }

  handleRetry = () => {
    this.setState({ hasError: false, error: null, retryCount: 0 });
  };

  render() {
    if (this.state.hasError) {
      return (
        <div className="flex flex-col items-center justify-center h-full p-6 text-dark-300">
          <div className="text-accent-danger text-sm font-medium mb-2">
            Plugin failed to load
          </div>
          <div className="text-xs text-dark-400 mb-4 text-center max-w-xs font-mono">
            {this.state.error?.message || 'Unknown error'}
          </div>
          <button
            onClick={this.handleRetry}
            className="px-4 py-2 text-xs font-medium bg-dark-700 text-dark-200 rounded-lg hover:bg-dark-600 transition-all border border-glass-100"
          >
            Retry
          </button>
        </div>
      );
    }

    return this.props.children;
  }
}
