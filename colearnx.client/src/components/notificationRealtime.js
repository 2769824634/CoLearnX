import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';

const offline = { connected: false, stop: async () => {} };

export function connectNotificationRealtime(token, onNotification) {
  let connection;
  try {
    connection = new HubConnectionBuilder()
      .withUrl(new URL('/hubs/notifications', window.location.origin).href, { accessTokenFactory: () => token })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.None)
      .build();
  } catch {
    // Polling in MemberNotificationsProvider keeps the inbox current without a hub connection.
    return offline;
  }
  const state = { connected: false };
  const refresh = () => { state.connected = connection.state === HubConnectionState.Connected; };
  connection.on('notification', onNotification);
  connection.onreconnected(refresh);
  connection.onclose(() => { state.connected = false; });
  connection.start().then(refresh, () => { state.connected = false; });
  return {
    get connected() { return state.connected; },
    stop() { return connection.stop(); },
  };
}
