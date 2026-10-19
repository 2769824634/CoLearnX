import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';

export function connectNotificationRealtime(token, onNotification) {
  const connection = new HubConnectionBuilder()
    .withUrl('/hubs/notifications', { accessTokenFactory: () => token })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.None)
    .build();
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
