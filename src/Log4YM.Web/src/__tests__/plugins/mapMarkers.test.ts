import { describe, it, expect } from 'vitest';

// Since createCallsignImageIcon is defined inline in MapPlugin.tsx and depends on
// Leaflet's L.DivIcon, we replicate the HTML generation logic here for testing.
// This follows the existing pattern in pluginHelpers.test.ts.

interface CallsignImageIconParams {
  imageUrl: string | undefined | null;
  callsign: string;
  scale: '1x' | '2x';
}

interface CallsignImageIconResult {
  html: string;
  size: number;
  borderWidth: number;
  borderColor: string;
  shadowSpread: number;
  fontSize: number;
}

// Replicated from MapPlugin.tsx createCallsignImageIcon (lines 97-152)
function createCallsignImageIconHtml(params: CallsignImageIconParams): CallsignImageIconResult {
  const { imageUrl, callsign, scale } = params;
  const size = scale === '2x' ? 56 : 44;
  const borderWidth = scale === '2x' ? 3 : 2;
  const borderColor = scale === '2x' ? '#ffb432' : '#00ddff';
  const shadowSpread = scale === '2x' ? 8 : 5;
  const fontSize = scale === '2x' ? 11 : 10;

  const safeCallsign = callsign.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

  const contentHtml = imageUrl
    ? `<img
            src="${imageUrl}"
            alt="${safeCallsign}"
            style="width: 100%; height: 100%; object-fit: cover; display: block;"
            onerror="this.parentElement.innerHTML='<div style=\\'display:flex;align-items:center;justify-content:center;width:100%;height:100%;font-size:${Math.round(size * 0.5)}px\\'>📻</div>'"
          />`
    : `<div style="display:flex;align-items:center;justify-content:center;width:100%;height:100%;font-size:${Math.round(size * 0.5)}px">📻</div>`;

  const html = `
      <div style="
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 2px;
        transform: translate(-50%, -50%);
        pointer-events: auto;
      ">
        <div style="
          width: ${size}px;
          height: ${size}px;
          border-radius: 50%;
          border: ${borderWidth}px solid ${borderColor};
          box-shadow: 0 0 ${shadowSpread}px ${borderColor}80;
          overflow: hidden;
          background: #1a1e26;
          flex-shrink: 0;
        ">
          ${contentHtml}
        </div>
        <span style="
          font-family: monospace;
          font-size: ${fontSize}px;
          font-weight: bold;
          color: ${borderColor};
          text-shadow: 0 0 4px rgba(0,0,0,0.8), 0 1px 2px rgba(0,0,0,0.9);
          white-space: nowrap;
          line-height: 1;
        ">${safeCallsign}</span>
      </div>
    `;

  return { html, size, borderWidth, borderColor, shadowSpread, fontSize };
}

// Replicated from MapPlugin.tsx visibleCallsignImages logic (lines 652-658)
interface CallsignMapImage {
  callsign: string;
  imageUrl?: string;
  latitude: number;
  longitude: number;
  name?: string;
  country?: string;
  grid?: string;
  savedAt: string;
}

function computeVisibleCallsignImages(
  callsignMapImages: CallsignMapImage[],
  showCallsignImages: boolean,
  maxCallsignImages: number,
  focusedCallsign: string | undefined
): CallsignMapImage[] {
  if (!showCallsignImages) return [];
  const focusedCall = focusedCallsign?.toUpperCase();
  return callsignMapImages
    .filter(img => img.callsign.toUpperCase() !== focusedCall)
    .slice(0, maxCallsignImages);
}

// --- Tests ---

describe('createCallsignImageIcon', () => {
  describe('with image URL', () => {
    it('renders <img> tag with the image URL at 2x scale', () => {
      const result = createCallsignImageIconHtml({
        imageUrl: 'https://qrz.com/photos/W1AW.jpg',
        callsign: 'W1AW',
        scale: '2x',
      });

      expect(result.html).toContain('<img');
      expect(result.html).toContain('src="https://qrz.com/photos/W1AW.jpg"');
      expect(result.html).toContain('alt="W1AW"');
    });

    it('renders <img> tag with the image URL at 1x scale', () => {
      const result = createCallsignImageIconHtml({
        imageUrl: 'https://qrz.com/photos/EI2KC.jpg',
        callsign: 'EI2KC',
        scale: '1x',
      });

      expect(result.html).toContain('<img');
      expect(result.html).toContain('src="https://qrz.com/photos/EI2KC.jpg"');
    });

    it('includes onerror fallback to radio emoji', () => {
      const result = createCallsignImageIconHtml({
        imageUrl: 'https://example.com/photo.jpg',
        callsign: 'W1AW',
        scale: '2x',
      });

      expect(result.html).toContain('onerror=');
    });
  });

  describe('without image URL', () => {
    it('renders radio emoji placeholder when imageUrl is undefined', () => {
      const result = createCallsignImageIconHtml({
        imageUrl: undefined,
        callsign: 'W1AW',
        scale: '2x',
      });

      expect(result.html).not.toContain('<img');
      expect(result.html).toContain('📻');
    });

    it('renders radio emoji placeholder when imageUrl is null', () => {
      const result = createCallsignImageIconHtml({
        imageUrl: null,
        callsign: 'EI2KC',
        scale: '1x',
      });

      expect(result.html).not.toContain('<img');
      expect(result.html).toContain('📻');
    });

    it('renders radio emoji placeholder when imageUrl is empty string', () => {
      const result = createCallsignImageIconHtml({
        imageUrl: '',
        callsign: 'W1AW',
        scale: '2x',
      });

      // Empty string is falsy, so should render placeholder
      expect(result.html).not.toContain('<img');
      expect(result.html).toContain('📻');
    });
  });

  describe('2x scale (focused/active marker)', () => {
    it('uses 56px size', () => {
      const result = createCallsignImageIconHtml({
        imageUrl: 'https://example.com/photo.jpg',
        callsign: 'W1AW',
        scale: '2x',
      });

      expect(result.size).toBe(56);
      expect(result.html).toContain('width: 56px');
      expect(result.html).toContain('height: 56px');
    });

    it('uses 3px amber border', () => {
      const result = createCallsignImageIconHtml({
        imageUrl: 'https://example.com/photo.jpg',
        callsign: 'W1AW',
        scale: '2x',
      });

      expect(result.borderWidth).toBe(3);
      expect(result.borderColor).toBe('#ffb432');
      expect(result.html).toContain('border: 3px solid #ffb432');
    });

    it('uses 11px font size for label', () => {
      const result = createCallsignImageIconHtml({
        imageUrl: undefined,
        callsign: 'W1AW',
        scale: '2x',
      });

      expect(result.fontSize).toBe(11);
      expect(result.html).toContain('font-size: 11px');
    });

    it('uses 28px emoji font size (56 * 0.5) for placeholder', () => {
      const result = createCallsignImageIconHtml({
        imageUrl: undefined,
        callsign: 'W1AW',
        scale: '2x',
      });

      expect(result.html).toContain('font-size:28px');
    });

    it('uses 8px shadow spread', () => {
      const result = createCallsignImageIconHtml({
        imageUrl: undefined,
        callsign: 'W1AW',
        scale: '2x',
      });

      expect(result.shadowSpread).toBe(8);
    });
  });

  describe('1x scale (logged/saved marker)', () => {
    it('uses 44px size', () => {
      const result = createCallsignImageIconHtml({
        imageUrl: 'https://example.com/photo.jpg',
        callsign: 'EI2KC',
        scale: '1x',
      });

      expect(result.size).toBe(44);
      expect(result.html).toContain('width: 44px');
      expect(result.html).toContain('height: 44px');
    });

    it('uses 2px cyan border', () => {
      const result = createCallsignImageIconHtml({
        imageUrl: 'https://example.com/photo.jpg',
        callsign: 'EI2KC',
        scale: '1x',
      });

      expect(result.borderWidth).toBe(2);
      expect(result.borderColor).toBe('#00ddff');
      expect(result.html).toContain('border: 2px solid #00ddff');
    });

    it('uses 10px font size for label', () => {
      const result = createCallsignImageIconHtml({
        imageUrl: undefined,
        callsign: 'EI2KC',
        scale: '1x',
      });

      expect(result.fontSize).toBe(10);
      expect(result.html).toContain('font-size: 10px');
    });

    it('uses 22px emoji font size (44 * 0.5) for placeholder', () => {
      const result = createCallsignImageIconHtml({
        imageUrl: undefined,
        callsign: 'EI2KC',
        scale: '1x',
      });

      expect(result.html).toContain('font-size:22px');
    });

    it('uses 5px shadow spread', () => {
      const result = createCallsignImageIconHtml({
        imageUrl: undefined,
        callsign: 'EI2KC',
        scale: '1x',
      });

      expect(result.shadowSpread).toBe(5);
    });
  });

  describe('callsign label', () => {
    it('displays callsign text in the marker', () => {
      const result = createCallsignImageIconHtml({
        imageUrl: 'https://example.com/photo.jpg',
        callsign: 'W1AW',
        scale: '2x',
      });

      expect(result.html).toContain('>W1AW</span>');
    });

    it('escapes HTML special characters in callsign to prevent XSS', () => {
      const result = createCallsignImageIconHtml({
        imageUrl: undefined,
        callsign: '<script>alert("xss")</script>',
        scale: '1x',
      });

      expect(result.html).not.toContain('<script>');
      expect(result.html).toContain('&lt;script&gt;');
    });

    it('escapes ampersands in callsign', () => {
      const result = createCallsignImageIconHtml({
        imageUrl: undefined,
        callsign: 'A&B',
        scale: '1x',
      });

      expect(result.html).toContain('A&amp;B');
    });

    it('uses amber color label at 2x', () => {
      const result = createCallsignImageIconHtml({
        imageUrl: undefined,
        callsign: 'W1AW',
        scale: '2x',
      });

      expect(result.html).toContain('color: #ffb432');
    });

    it('uses cyan color label at 1x', () => {
      const result = createCallsignImageIconHtml({
        imageUrl: undefined,
        callsign: 'EI2KC',
        scale: '1x',
      });

      expect(result.html).toContain('color: #00ddff');
    });

    it('uses monospace font', () => {
      const result = createCallsignImageIconHtml({
        imageUrl: undefined,
        callsign: 'W1AW',
        scale: '2x',
      });

      expect(result.html).toContain('font-family: monospace');
    });
  });
});

describe('visibleCallsignImages', () => {
  const sampleImages: CallsignMapImage[] = [
    { callsign: 'W1AW', imageUrl: 'https://example.com/w1aw.jpg', latitude: 41.7, longitude: -72.7, savedAt: '2024-01-03T00:00:00Z' },
    { callsign: 'EI2KC', imageUrl: 'https://example.com/ei2kc.jpg', latitude: 52.6, longitude: -8.6, savedAt: '2024-01-02T00:00:00Z' },
    { callsign: 'JA1ABC', latitude: 35.6, longitude: 139.6, savedAt: '2024-01-01T00:00:00Z' },
    { callsign: 'VK3ABC', imageUrl: 'https://example.com/vk3abc.jpg', latitude: -37.8, longitude: 144.9, savedAt: '2024-01-04T00:00:00Z' },
  ];

  it('returns empty array when showCallsignImages is false', () => {
    const result = computeVisibleCallsignImages(sampleImages, false, 100, undefined);
    expect(result).toHaveLength(0);
  });

  it('returns all images when showCallsignImages is true and no focused callsign', () => {
    const result = computeVisibleCallsignImages(sampleImages, true, 100, undefined);
    expect(result).toHaveLength(4);
  });

  it('excludes the focused callsign from results', () => {
    const result = computeVisibleCallsignImages(sampleImages, true, 100, 'W1AW');
    expect(result).toHaveLength(3);
    expect(result.find(img => img.callsign === 'W1AW')).toBeUndefined();
  });

  it('excludes focused callsign case-insensitively', () => {
    const result = computeVisibleCallsignImages(sampleImages, true, 100, 'w1aw');
    expect(result).toHaveLength(3);
    expect(result.find(img => img.callsign === 'W1AW')).toBeUndefined();
  });

  it('respects maxCallsignImages limit', () => {
    const result = computeVisibleCallsignImages(sampleImages, true, 2, undefined);
    expect(result).toHaveLength(2);
    // Should be the first 2 in order
    expect(result[0].callsign).toBe('W1AW');
    expect(result[1].callsign).toBe('EI2KC');
  });

  it('includes entries without imageUrl (placeholder markers)', () => {
    const result = computeVisibleCallsignImages(sampleImages, true, 100, undefined);
    const noImageEntry = result.find(img => img.callsign === 'JA1ABC');
    expect(noImageEntry).toBeDefined();
    expect(noImageEntry!.imageUrl).toBeUndefined();
  });

  it('returns empty array for empty input', () => {
    const result = computeVisibleCallsignImages([], true, 100, undefined);
    expect(result).toHaveLength(0);
  });

  it('limit of 0 returns empty array', () => {
    const result = computeVisibleCallsignImages(sampleImages, true, 0, undefined);
    expect(result).toHaveLength(0);
  });
});

describe('save guard logic', () => {
  // Replicated from LogHub.cs FocusCallsign (line 182):
  //   if (info.Latitude.HasValue && info.Longitude.HasValue)
  // Previously also required: !string.IsNullOrEmpty(info.ImageUrl)

  function shouldSaveToMongo(info: {
    latitude?: number | null;
    longitude?: number | null;
    imageUrl?: string | null;
  }): boolean {
    // Current logic: only requires lat/lon, NOT imageUrl
    return info.latitude != null && info.longitude != null;
  }

  it('saves callsign with image and lat/lon', () => {
    expect(shouldSaveToMongo({
      latitude: 41.7,
      longitude: -72.7,
      imageUrl: 'https://example.com/photo.jpg',
    })).toBe(true);
  });

  it('saves callsign without image when lat/lon exist', () => {
    expect(shouldSaveToMongo({
      latitude: 35.6,
      longitude: 139.6,
      imageUrl: null,
    })).toBe(true);
  });

  it('saves callsign with empty imageUrl when lat/lon exist', () => {
    expect(shouldSaveToMongo({
      latitude: 52.6,
      longitude: -8.6,
      imageUrl: '',
    })).toBe(true);
  });

  it('skips save when latitude is missing', () => {
    expect(shouldSaveToMongo({
      latitude: null,
      longitude: -72.7,
      imageUrl: 'https://example.com/photo.jpg',
    })).toBe(false);
  });

  it('skips save when longitude is missing', () => {
    expect(shouldSaveToMongo({
      latitude: 41.7,
      longitude: null,
      imageUrl: 'https://example.com/photo.jpg',
    })).toBe(false);
  });

  it('skips save when both lat and lon are missing', () => {
    expect(shouldSaveToMongo({
      latitude: null,
      longitude: null,
      imageUrl: null,
    })).toBe(false);
  });

  it('skips save when lat/lon are undefined', () => {
    expect(shouldSaveToMongo({
      latitude: undefined,
      longitude: undefined,
      imageUrl: 'https://example.com/photo.jpg',
    })).toBe(false);
  });
});
