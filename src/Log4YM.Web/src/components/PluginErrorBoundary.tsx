import { Component, type ReactNode, type ErrorInfo } from 'react';

interface Props {
  pluginId: string;
  children: ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

export class PluginErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false, error: null };

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error(`Plugin "${this.props.pluginId}" crashed:`, error, info.componentStack);
  }

  handleRetry = () => {
    this.setState({ hasError: false, error: null });
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
