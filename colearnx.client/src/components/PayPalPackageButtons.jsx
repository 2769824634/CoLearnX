import { useEffect, useRef, useState } from 'react';
import { creditsApi } from '../api';

function loadPayPalSdk(clientId, currency) {
  const existing = document.querySelector('script[data-colearnx-paypal]');
  if (existing) {
    return window.paypal
      ? Promise.resolve(window.paypal)
      : new Promise((resolve, reject) => {
          existing.addEventListener('load', () => resolve(window.paypal));
          existing.addEventListener('error', reject);
        });
  }

  const params = new URLSearchParams({
    'client-id': clientId,
    currency,
    intent: 'capture',
    components: 'buttons',
    'disable-funding': 'paylater,venmo',
  });

  return new Promise((resolve, reject) => {
    const script = document.createElement('script');
    script.src = `https://www.paypal.com/sdk/js?${params.toString()}`;
    script.async = true;
    script.dataset.colearnxPaypal = '1';
    script.onload = () => resolve(window.paypal);
    script.onerror = () => reject(new Error('Failed to load PayPal SDK'));
    document.body.appendChild(script);
  });
}

// Renders PayPal Buttons for one CreditPackage.
export default function PayPalPackageButtons({ packageId, onCaptured, onError }) {
  const hostRef = useRef(null);
  const [status, setStatus] = useState('loading');
  const [message, setMessage] = useState('Loading PayPal…');

  useEffect(() => {
    let cancelled = false;
    let buttons;

    async function mount() {
      try {
        const config = await creditsApi.paypalConfig();
        if (!config.enabled || !config.clientId) {
          if (!cancelled) {
            setStatus('disabled');
            setMessage('PayPal sandbox not configured on server.');
          }
          return;
        }

        const paypal = await loadPayPalSdk(config.clientId, config.currency || 'AUD');
        if (cancelled || !hostRef.current || !paypal?.Buttons) return;

        hostRef.current.innerHTML = '';
        buttons = paypal.Buttons({
          style: { layout: 'vertical', color: 'gold', shape: 'rect', label: 'paypal' },
          createOrder: async () => {
            const order = await creditsApi.createPayPalOrder(packageId);
            return order.orderId;
          },
          onApprove: async (data) => {
            try {
              const ledger = await creditsApi.capturePayPalOrder(data.orderID);
              onCaptured?.(ledger);
            } catch (err) {
              onError?.(err?.message || 'PayPal capture failed');
              throw err;
            }
          },
          onError: (err) => {
            onError?.(err?.message || String(err) || 'PayPal checkout failed');
          },
          onCancel: () => {
            onError?.('Payment cancelled');
          },
        });

        if (await buttons.isEligible()) {
          await buttons.render(hostRef.current);
          if (!cancelled) setStatus('ready');
        } else if (!cancelled) {
          setStatus('disabled');
          setMessage('PayPal buttons not eligible in this browser.');
        }
      } catch (e) {
        if (!cancelled) {
          setStatus('error');
          setMessage(e.message || 'PayPal init failed');
          onError?.(e.message || 'PayPal init failed');
        }
      }
    }

    mount();
    return () => {
      cancelled = true;
      try {
        buttons?.close?.();
      } catch {
        /* ignore */
      }
    };
  }, [packageId, onCaptured, onError]);

  return (
    <div>
      {status !== 'ready' ? (
        <p style={{ fontSize: 12, color: status === 'error' ? 'var(--danger)' : 'var(--slate)', marginBottom: 8 }}>
          {message}
        </p>
      ) : null}
      <div ref={hostRef} />
    </div>
  );
}
