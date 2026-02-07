const API_BASE = '/api';

export interface QsoResponse {
  id: string;
  callsign: string;
  qsoDate: string;
  timeOn: string;
  timeOff?: string;
  band: string;
  mode: string;
  frequency?: number;
  rstSent?: string;
  rstRcvd?: string;
  name?: string;
  grid?: string;
  country?: string;
  station?: StationInfo;
  comment?: string;
  createdAt: string;
}

export interface StationInfo {
  name?: string;
  grid?: string;
  country?: string;
  dxcc?: number;
  state?: string;
  continent?: string;
  latitude?: number;
  longitude?: number;
}

export interface CreateQsoRequest {
  callsign: string;
  qsoDate: string;
  timeOn: string;
  band: string;
  mode: string;
  frequency?: number;
  rstSent?: string;
  rstRcvd?: string;
  name?: string;
  grid?: string;
  country?: string;
  comment?: string;
  notes?: string;
}

export interface UpdateQsoRequest {
  callsign?: string;
  qsoDate?: string;
  timeOn?: string;
  band?: string;
  mode?: string;
  frequency?: number;
  rstSent?: string;
  rstRcvd?: string;
  name?: string;
  grid?: string;
  country?: string;
  comment?: string;
}

export interface QsoStatistics {
  totalQsos: number;
  uniqueCallsigns: number;
  uniqueCountries: number;
  uniqueGrids: number;
  qsosToday: number;
  qsosByBand: Record<string, number>;
  qsosByMode: Record<string, number>;
}

export interface Spot {
  id: string;
  dxCall: string;
  spotter: string;
  frequency: number;
  mode?: string;
  comment?: string;
  source?: string;
  timestamp: string;
  country?: string;
  dxStation?: {
    country?: string;
    dxcc?: number;
    grid?: string;
    continent?: string;
  };
}

export interface QsoQuery {
  callsign?: string;
  name?: string;
  band?: string;
  mode?: string;
  fromDate?: string;
  toDate?: string;
  page?: number;
  pageSize?: number;
}

export interface PaginatedQsoResponse {
  items: QsoResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface SpotQuery {
  band?: string;
  mode?: string;
  limit?: number;
}

class ApiClient {
  private async fetch<T>(url: string, options?: RequestInit): Promise<T> {
    const response = await fetch(`${API_BASE}${url}`, {
      ...options,
      headers: {
        'Content-Type': 'application/json',
        ...options?.headers,
      },
    });

    if (!response.ok) {
      throw new Error(`API error: ${response.status}`);
    }

    return response.json();
  }

  // QSOs
  async getQsos(query?: QsoQuery): Promise<PaginatedQsoResponse> {
    const params = new URLSearchParams();
    if (query?.callsign) params.append('callsign', query.callsign);
    if (query?.name) params.append('name', query.name);
    if (query?.band) params.append('band', query.band);
    if (query?.mode) params.append('mode', query.mode);
    if (query?.fromDate) params.append('fromDate', query.fromDate);
    if (query?.toDate) params.append('toDate', query.toDate);
    if (query?.page) params.append('page', query.page.toString());
    if (query?.pageSize) params.append('pageSize', query.pageSize.toString());
    const qs = params.toString();
    return this.fetch<PaginatedQsoResponse>(`/qsos${qs ? `?${qs}` : ''}`);
  }

  async getQso(id: string): Promise<QsoResponse> {
    return this.fetch<QsoResponse>(`/qsos/${id}`);
  }

  async createQso(qso: CreateQsoRequest): Promise<QsoResponse> {
    return this.fetch<QsoResponse>('/qsos', {
      method: 'POST',
      body: JSON.stringify(qso),
    });
  }

  async updateQso(id: string, qso: UpdateQsoRequest): Promise<QsoResponse> {
    return this.fetch<QsoResponse>(`/qsos/${id}`, {
      method: 'PUT',
      body: JSON.stringify(qso),
    });
  }

  async deleteQso(id: string): Promise<void> {
    await fetch(`${API_BASE}/qsos/${id}`, { method: 'DELETE' });
  }

  async getStatistics(): Promise<QsoStatistics> {
    return this.fetch<QsoStatistics>('/qsos/statistics');
  }

  // Spots
  async getSpots(query?: SpotQuery): Promise<Spot[]> {
    const params = new URLSearchParams();
    if (query?.band) params.append('band', query.band);
    if (query?.mode) params.append('mode', query.mode);
    if (query?.limit) params.append('limit', query.limit.toString());
    const qs = params.toString();
    return this.fetch<Spot[]>(`/spots${qs ? `?${qs}` : ''}`);
  }

  // Health
  async getHealth(): Promise<{ status: string; timestamp: string }> {
    return this.fetch('/health');
  }

  // Plugins
  async getPlugins(): Promise<Array<{ id: string; name: string; version: string; enabled: boolean }>> {
    return this.fetch('/plugins');
  }

  // QRZ
  async getQrzSubscription(): Promise<QrzSubscriptionResponse> {
    return this.fetch('/qrz/subscription');
  }

  async updateQrzSettings(settings: QrzSettingsRequest): Promise<{ success: boolean; message: string; hasXmlSubscription: boolean }> {
    return this.fetch('/qrz/settings', {
      method: 'PUT',
      body: JSON.stringify(settings),
    });
  }

  async uploadToQrz(qsoIds: string[]): Promise<QrzUploadResponse> {
    return this.fetch('/qrz/upload', {
      method: 'POST',
      body: JSON.stringify({ qsoIds }),
    });
  }

  async syncToQrz(): Promise<QrzUploadResponse> {
    return this.fetch('/qrz/sync', {
      method: 'POST',
    });
  }

  async cancelQrzSync(): Promise<{ message: string }> {
    return this.fetch('/qrz/sync/cancel', {
      method: 'POST',
    });
  }

  async lookupCallsignQrz(callsign: string): Promise<QrzCallsignResponse> {
    return this.fetch(`/qrz/lookup/${encodeURIComponent(callsign)}`);
  }

  // POTA
  async getPotaSpots(): Promise<PotaSpot[]> {
    return this.fetch<PotaSpot[]>('/pota/spots');
  }

  // ADIF
  async importAdif(
    file: File,
    options: {
      skipDuplicates?: boolean;
      markAsSyncedToQrz?: boolean;
      clearExistingLogs?: boolean;
    } = {}
  ): Promise<AdifImportResponse> {
    const {
      skipDuplicates = true,
      markAsSyncedToQrz = true,
      clearExistingLogs = false,
    } = options;

    const formData = new FormData();
    formData.append('file', file);

    const params = new URLSearchParams();
    params.append('skipDuplicates', String(skipDuplicates));
    params.append('markAsSyncedToQrz', String(markAsSyncedToQrz));
    params.append('clearExistingLogs', String(clearExistingLogs));

    const response = await fetch(`${API_BASE}/adif/import?${params}`, {
      method: 'POST',
      body: formData,
    });

    if (!response.ok) {
      throw new Error(`API error: ${response.status}`);
    }

    return response.json();
  }

  async exportAdif(request?: AdifExportRequest): Promise<Blob> {
    const params = new URLSearchParams();
    if (request?.callsign) params.append('callsign', request.callsign);
    if (request?.band) params.append('band', request.band);
    if (request?.mode) params.append('mode', request.mode);
    if (request?.fromDate) params.append('fromDate', request.fromDate);
    if (request?.toDate) params.append('toDate', request.toDate);
    const qs = params.toString();

    const response = await fetch(`${API_BASE}/adif/export${qs ? `?${qs}` : ''}`, {
      method: 'GET',
    });

    if (!response.ok) {
      throw new Error(`API error: ${response.status}`);
    }

    return response.blob();
  }

  async exportSelectedQsos(qsoIds: string[]): Promise<Blob> {
    const response = await fetch(`${API_BASE}/adif/export`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ qsoIds }),
    });

    if (!response.ok) {
      throw new Error(`API error: ${response.status}`);
    }

    return response.blob();
  }
}

// QRZ Types
export interface QrzSubscriptionResponse {
  isValid: boolean;
  hasXmlSubscription: boolean;
  username?: string;
  message?: string;
  expirationDate?: string;
}

export interface QrzSettingsRequest {
  username: string;
  password: string;
  apiKey?: string;
  enabled?: boolean;
}

export interface QrzUploadResponse {
  totalCount: number;
  successCount: number;
  failedCount: number;
  results: QrzUploadResult[];
}

export interface QrzUploadResult {
  success: boolean;
  logId?: string;
  message?: string;
  qsoId?: string;
}

export interface QrzCallsignResponse {
  callsign: string;
  name?: string;
  firstName?: string;
  address?: string;
  city?: string;
  state?: string;
  country?: string;
  grid?: string;
  latitude?: number;
  longitude?: number;
  dxcc?: number;
  cqZone?: number;
  ituZone?: number;
  email?: string;
  qslManager?: string;
  imageUrl?: string;
  licenseExpiration?: string;
}

// POTA Types
export interface PotaSpot {
  spotId: number;
  activator: string;
  frequency: string;
  mode: string;
  reference: string;
  parkName: string;
  spotTime: string;
  spotter: string;
  comments: string;
  source: string;
  invalid?: boolean;
  name?: string;
  locationDesc?: string;
  grid4?: string;
  grid6?: string;
  latitude?: number;
  longitude?: number;
}

// ADIF Types
export interface AdifImportResponse {
  totalRecords: number;
  importedCount: number;
  skippedDuplicates: number;
  errorCount: number;
  errors: string[];
}

export interface AdifExportRequest {
  callsign?: string;
  band?: string;
  mode?: string;
  fromDate?: string;
  toDate?: string;
  qsoIds?: string[];
}

export const api = new ApiClient();
