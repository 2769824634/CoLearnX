import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import PayPalPackageButtons from './PayPalPackageButtons';
import { creditsApi } from '../api';

vi.mock('../api', () => ({
  creditsApi: {
    paypalConfig: vi.fn(),
    createPayPalOrder: vi.fn(),
    capturePayPalOrder: vi.fn(),
  },
}));

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  document.querySelectorAll('script[data-colearnx-paypal]').forEach((node) => node.remove());
  delete window.paypal;
});

describe('PayPalPackageButtons', () => {
  beforeEach(() => {
    creditsApi.paypalConfig.mockResolvedValue({
      enabled: true,
      clientId: `A${'x'.repeat(79)}`,
      currency: 'AUD',
      mode: 'Sandbox',
    });
  });

  it('does not stay on loading when the SDK script loads without paypal.Buttons', async () => {
    const append = document.body.appendChild.bind(document.body);
    vi.spyOn(document.body, 'appendChild').mockImplementation((node) => {
      const result = append(node);
      if (node?.dataset?.colearnxPaypal) queueMicrotask(() => node.dispatchEvent(new Event('load')));
      return result;
    });

    render(<PayPalPackageButtons packageId={1} />);

    expect(await screen.findByText(/PayPal SDK loaded without buttons/i)).toBeTruthy();
    expect(screen.queryByText('Loading PayPal…')).toBeNull();
  });

  it('treats a setting-name client id as not configured', async () => {
    creditsApi.paypalConfig.mockResolvedValue({
      enabled: true,
      clientId: 'PayPal:ClientId',
      currency: 'AUD',
      mode: 'Sandbox',
    });

    render(<PayPalPackageButtons packageId={1} />);

    expect(await screen.findByText(/PayPal sandbox not configured on server/i)).toBeTruthy();
  });
});
