import { useState, useEffect } from 'react';
import {
  Database,
  CheckCircle,
  AlertCircle,
  Loader2,
  Eye,
  EyeOff,
  ExternalLink,
  Server,
  Radio,
  HardDrive,
  Cloud,
  Check,
  ArrowLeft,
} from 'lucide-react';
import { useSetupStore } from '../store/setupStore';

interface SetupWizardProps {
  onComplete: () => void;
}

type WizardView = 'choice' | 'cloud-form' | 'success';

export function SetupWizard({ onComplete }: SetupWizardProps) {
  const {
    connectionString,
    databaseName,
    setConnectionString,
    setDatabaseName,
    testConnection,
    configure,
    configureLocal,
    isTesting,
    isLoading,
    testResult,
    error,
    clearError,
    clearTestResult,
  } = useSetupStore();

  const [showConnectionString, setShowConnectionString] = useState(false);
  const [view, setView] = useState<WizardView>('choice');
  const [step, setStep] = useState<'input' | 'tested' | 'success'>('input');
  const [successMessage, setSuccessMessage] = useState('');

  // Reset to input step when connection string changes
  useEffect(() => {
    if (step === 'tested') {
      setStep('input');
      clearTestResult();
    }
  }, [connectionString, databaseName]);

  const handleLocalSetup = async () => {
    clearError();
    const success = await configureLocal();
    if (success) {
      setSuccessMessage('Using local database');
      setView('success');
      setTimeout(onComplete, 1500);
    }
  };

  const handleTest = async () => {
    clearError();
    const success = await testConnection();
    if (success) {
      setStep('tested');
    }
  };

  const handleConfigure = async () => {
    const success = await configure();
    if (success) {
      setSuccessMessage('Connected to MongoDB successfully. Starting Log4YM...');
      setView('success');
      setTimeout(onComplete, 1500);
    }
  };

  return (
    <div className="fixed inset-0 bg-dark-900 flex items-center justify-center z-[200]">
      <div className="glass-panel w-full max-w-2xl mx-4 animate-fade-in border border-glass-200 rounded-xl shadow-2xl">
        {/* Header */}
        <div className="p-6 border-b border-glass-100">
          <div className="flex items-center gap-4">
            <div className="w-14 h-14 bg-orange-500/20 rounded-xl flex items-center justify-center">
              <Radio className="w-8 h-8 text-orange-500" />
            </div>
            <div>
              <h1 className="text-2xl font-bold text-orange-500">LOG4YM</h1>
              <p className="text-sm text-gray-400">Welcome! Let's get you set up.</p>
            </div>
          </div>
        </div>

        {/* Content */}
        <div className="p-6">
          {view === 'success' ? (
            <div className="text-center py-8">
              <CheckCircle className="w-16 h-16 text-green-400 mx-auto mb-4" />
              <h2 className="text-xl font-semibold text-gray-100 mb-2">You're all set!</h2>
              <p className="text-gray-400">{successMessage}</p>
            </div>
          ) : view === 'choice' ? (
            <ChoiceScreen
              onLocalClick={handleLocalSetup}
              onCloudClick={() => {
                setView('cloud-form');
                clearError();
              }}
              isLoading={isLoading}
              error={error}
            />
          ) : (
            <CloudFormScreen
              connectionString={connectionString}
              databaseName={databaseName}
              showConnectionString={showConnectionString}
              setShowConnectionString={setShowConnectionString}
              setConnectionString={setConnectionString}
              setDatabaseName={setDatabaseName}
              step={step}
              isTesting={isTesting}
              isLoading={isLoading}
              testResult={testResult}
              error={error}
              onBack={() => {
                setView('choice');
                setStep('input');
                clearError();
                clearTestResult();
              }}
              onTest={handleTest}
              onConfigure={handleConfigure}
            />
          )}
        </div>
      </div>
    </div>
  );
}

function ChoiceScreen({
  onLocalClick,
  onCloudClick,
  isLoading,
  error,
}: {
  onLocalClick: () => void;
  onCloudClick: () => void;
  isLoading: boolean;
  error: string | null;
}) {
  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        {/* Local Database Card */}
        <div className="relative p-6 rounded-xl border border-accent-success/30 hover:border-accent-success/50 hover:bg-dark-700/30 transition-all duration-200 flex flex-col">
          <span className="absolute top-3 right-3 bg-accent-success/20 text-accent-success text-xs px-2 py-0.5 rounded-full">
            Recommended
          </span>
          <div className="flex flex-col items-center text-center flex-1">
            <HardDrive className="w-10 h-10 text-accent-success mb-4" />
            <h3 className="text-lg font-semibold text-dark-200 mb-3">Local Database</h3>
            <ul className="space-y-2 text-sm text-dark-300 mb-6">
              <li className="flex items-center gap-2">
                <Check className="w-4 h-4 text-accent-success flex-shrink-0" />
                Works offline
              </li>
              <li className="flex items-center gap-2">
                <Check className="w-4 h-4 text-accent-success flex-shrink-0" />
                No setup needed
              </li>
              <li className="flex items-center gap-2">
                <Check className="w-4 h-4 text-accent-success flex-shrink-0" />
                Data stays on this computer
              </li>
            </ul>
          </div>
          <button
            onClick={onLocalClick}
            disabled={isLoading}
            className="w-full glass-button-success px-6 py-2.5 flex items-center justify-center gap-2 disabled:opacity-50"
          >
            {isLoading ? (
              <>
                <Loader2 className="w-4 h-4 animate-spin" />
                Setting up...
              </>
            ) : (
              'Get Started'
            )}
          </button>
        </div>

        {/* Cloud Database Card */}
        <div className="p-6 rounded-xl border border-accent-info/30 hover:border-accent-info/50 hover:bg-dark-700/30 transition-all duration-200 flex flex-col">
          <div className="flex flex-col items-center text-center flex-1">
            <Cloud className="w-10 h-10 text-accent-info mb-4 mt-6" />
            <h3 className="text-lg font-semibold text-dark-200 mb-3">Cloud Database</h3>
            <ul className="space-y-2 text-sm text-dark-300 mb-6">
              <li className="flex items-center gap-2">
                <Check className="w-4 h-4 text-accent-info flex-shrink-0" />
                MongoDB Atlas
              </li>
              <li className="flex items-center gap-2">
                <Check className="w-4 h-4 text-accent-info flex-shrink-0" />
                Multi-device sync
              </li>
              <li className="flex items-center gap-2">
                <Check className="w-4 h-4 text-accent-info flex-shrink-0" />
                Cloud backup
              </li>
            </ul>
          </div>
          <button
            onClick={onCloudClick}
            className="w-full glass-button px-6 py-2.5"
          >
            Configure
          </button>
        </div>
      </div>

      {/* Error Display */}
      {error && (
        <div className="p-4 rounded-lg border bg-red-500/10 border-red-500/30">
          <div className="flex items-start gap-3">
            <AlertCircle className="w-5 h-5 text-red-400 flex-shrink-0 mt-0.5" />
            <p className="text-red-400">{error}</p>
          </div>
        </div>
      )}

      <p className="text-center text-xs text-dark-300">
        You can switch between local and cloud at any time in Settings.
      </p>
    </div>
  );
}

function CloudFormScreen({
  connectionString,
  databaseName,
  showConnectionString,
  setShowConnectionString,
  setConnectionString,
  setDatabaseName,
  step,
  isTesting,
  isLoading,
  testResult,
  error,
  onBack,
  onTest,
  onConfigure,
}: {
  connectionString: string;
  databaseName: string;
  showConnectionString: boolean;
  setShowConnectionString: (v: boolean) => void;
  setConnectionString: (v: string) => void;
  setDatabaseName: (v: string) => void;
  step: 'input' | 'tested' | 'success';
  isTesting: boolean;
  isLoading: boolean;
  testResult: { success: boolean; message: string; serverInfo?: { databaseCount: number } } | null;
  error: string | null;
  onBack: () => void;
  onTest: () => void;
  onConfigure: () => void;
}) {
  return (
    <div className="space-y-6">
      {/* Database Section */}
      <div>
        <div className="flex items-center gap-2 mb-4">
          <Database className="w-5 h-5 text-accent-primary" />
          <h2 className="text-lg font-semibold text-gray-100">Cloud Database Setup</h2>
        </div>

        <p className="text-sm text-gray-400 mb-4">
          Log4YM uses MongoDB to store your QSOs and settings. You can use a free MongoDB
          Atlas cluster or a local MongoDB installation.
        </p>

        {/* MongoDB Atlas link */}
        <a
          href="https://www.mongodb.com/atlas/database"
          target="_blank"
          rel="noopener noreferrer"
          className="inline-flex items-center gap-2 text-sm text-accent-primary hover:underline mb-6"
        >
          <ExternalLink className="w-4 h-4" />
          Get a free MongoDB Atlas cluster
        </a>

        {/* Connection String Input */}
        <div className="space-y-4">
          <div className="space-y-2">
            <label className="flex items-center gap-2 text-sm font-medium text-gray-300">
              <Server className="w-4 h-4 text-accent-primary" />
              MongoDB Connection String
            </label>
            <div className="relative">
              <input
                type={showConnectionString ? 'text' : 'password'}
                value={connectionString}
                onChange={(e) => setConnectionString(e.target.value)}
                placeholder="mongodb+srv://user:password@cluster.mongodb.net/"
                className="glass-input w-full pr-10 font-mono text-sm"
              />
              <button
                type="button"
                onClick={() => setShowConnectionString(!showConnectionString)}
                className="absolute right-2 top-1/2 -translate-y-1/2 p-1 text-gray-500 hover:text-gray-300"
              >
                {showConnectionString ? (
                  <EyeOff className="w-4 h-4" />
                ) : (
                  <Eye className="w-4 h-4" />
                )}
              </button>
            </div>
            <p className="text-xs text-gray-600">
              Your connection string is stored locally and never sent anywhere except
              MongoDB.
            </p>
          </div>

          {/* Database Name Input */}
          <div className="space-y-2">
            <label className="text-sm font-medium text-gray-300">Database Name</label>
            <input
              type="text"
              value={databaseName}
              onChange={(e) => setDatabaseName(e.target.value)}
              placeholder="Log4YM"
              className="glass-input w-full font-mono"
            />
            <p className="text-xs text-gray-600">
              The database will be created automatically if it doesn't exist.
            </p>
          </div>
        </div>
      </div>

      {/* Test Result */}
      {testResult && (
        <div
          className={`p-4 rounded-lg border ${
            testResult.success
              ? 'bg-green-500/10 border-green-500/30'
              : 'bg-red-500/10 border-red-500/30'
          }`}
        >
          <div className="flex items-start gap-3">
            {testResult.success ? (
              <CheckCircle className="w-5 h-5 text-green-400 flex-shrink-0 mt-0.5" />
            ) : (
              <AlertCircle className="w-5 h-5 text-red-400 flex-shrink-0 mt-0.5" />
            )}
            <div>
              <p
                className={`font-medium ${
                  testResult.success ? 'text-green-400' : 'text-red-400'
                }`}
              >
                {testResult.message}
              </p>
              {testResult.serverInfo && (
                <p className="text-sm text-gray-400 mt-1">
                  Found {testResult.serverInfo.databaseCount} database(s) on server
                </p>
              )}
            </div>
          </div>
        </div>
      )}

      {/* Error Display */}
      {error && (
        <div className="p-4 rounded-lg border bg-red-500/10 border-red-500/30">
          <div className="flex items-start gap-3">
            <AlertCircle className="w-5 h-5 text-red-400 flex-shrink-0 mt-0.5" />
            <p className="text-red-400">{error}</p>
          </div>
        </div>
      )}

      {/* Footer Actions */}
      <div className="flex justify-between pt-2">
        <button
          onClick={onBack}
          className="glass-button px-4 py-2 flex items-center gap-2"
        >
          <ArrowLeft className="w-4 h-4" />
          Back
        </button>
        <div className="flex gap-3">
          {step === 'input' ? (
            <button
              onClick={onTest}
              disabled={!connectionString || isTesting}
              className="glass-button-primary px-6 py-2 flex items-center gap-2 disabled:opacity-50"
            >
              {isTesting ? (
                <>
                  <Loader2 className="w-4 h-4 animate-spin" />
                  Testing...
                </>
              ) : (
                <>
                  <Database className="w-4 h-4" />
                  Test Connection
                </>
              )}
            </button>
          ) : (
            <button
              onClick={onConfigure}
              disabled={isLoading}
              className="glass-button-success px-6 py-2 flex items-center gap-2 disabled:opacity-50"
            >
              {isLoading ? (
                <>
                  <Loader2 className="w-4 h-4 animate-spin" />
                  Saving...
                </>
              ) : (
                <>
                  <CheckCircle className="w-4 h-4" />
                  Save & Continue
                </>
              )}
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
