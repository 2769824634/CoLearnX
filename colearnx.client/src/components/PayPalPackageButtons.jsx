import { useEffect, useRef, useState } from 'react';
import { creditsApi } from '../api';

const SDK_TIMEOUT_MS = 15000;

function isUsablePayPalClientId(clientId) {
  if (!clientId || typeof clientId !== 'string') return false;
  const trimmed = clientId.trim();
  if (trimmed.length < 20) return false;
  if (/PayPal:|:ClientId|ClientSecret/i.test(trimmed)) return false;
  return true;
}

function waitForPayPal(script) {
  if (window.paypal?.Buttons) return Promise.resolve(window.paypal);

  const alreadyDone = script.getAttribute('data-loaded') === '1' || script.readyState === 'complete';
  if (alreadyDone) {
    return window.paypal?.Buttons
      ? Promise.resolve(window.paypal)
      : Promise.reject(new Error('PayPal SDK loaded without buttons. Check the Client ID.'));
  }

  return new Promise((resolve, reject) => {
    const timer = window.setTimeout(() => {
      cleanup();
      reject(new Error('PayPal SDK timed out. Check the network, disable any ad blocker, and confirm paypal.com is reachable.'));
    }, SDK_TIMEOUT_MS);

    const cleanup = () => {
      window.clearTimeout(timer);
      script.removeEventListener('load', onLoad);
      script.removeEventListener('error', onError);
    };

    const onLoad = () => {
      cleanup();
      script.setAttribute('data-loaded', '1');
      if (window.paypal?.Buttons) resolve(window.paypal);
      else reject(new Error('PayPal SDK loaded without buttons. Check the Client ID.'));
    };
    const onError = () => {
      cleanup();
      reject(new Error('Failed to load PayPal SDK'));
    };

    script.addEventListener('load', onLoad);
    script.addEventListener('error', onError);
  });
}

function loadPayPalSdk(clientId, currency) {
  const existing = document.querySelector('script[data-colearnx-paypal]');
  if (existing) return waitForPayPal(existing);

  const params = new URLSearchParams({
    'client-id': clientId,
    currency,
    intent: 'capture',
    components: 'buttons',
    'disable-funding': 'paylater,venmo',
  });

  const script = document.createElement('script');
  script.src = `https://www.paypal.com/sdk/js?${params.toString()}`;
  script.async = true;
  script.dataset.colearnxPaypal = '1';
  const pending = waitForPayPal(script);
  document.body.appendChild(script);
  return pending;
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
        if (!config.enabled || !isUsablePayPalClientId(config.clientId)) {
          if (!cancelled) {
            setStatus('disabled');
            setMessage('PayPal sandbox not configured on server.');
          }
          return;
        }

        const paypal = await loadPayPalSdk(config.clientId, config.currency || 'AUD');
        if (cancelled) return;
        if (!hostRef.current || !paypal?.Buttons) {
          setStatus('error');
          setMessage('PayPal SDK loaded without buttons. Check the Client ID.');
          return;
        }

        hostRef.current.innerHTML = '';
        buttons = paypal.Buttons({
          style: { layout: 'vertical', color: 'gold', shape: 'rect', label: 'paypal' },
          createOrder: async () => {
            try {
              const order = await creditsApi.createPayPalOrder(packageId);
              if (!order?.orderId) throw new Error('Server did not return a PayPal order id.');
              return order.orderId;
            } catch (err) {
              onError?.(err?.message || 'Could not create PayPal order');
              throw err;
            }
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
