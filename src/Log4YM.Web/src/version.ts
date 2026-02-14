/**
 * Application version injected at build time via VITE_APP_VERSION env variable.
 * Falls back to 'dev' when running locally without version injection.
 */
export const APP_VERSION: string = import.meta.env.VITE_APP_VERSION || 'dev';
