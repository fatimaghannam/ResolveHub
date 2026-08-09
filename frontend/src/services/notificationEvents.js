const EVENT_NAME = 'resolvehub:notifications-changed'

export const notifyNotificationsChanged = () => window.dispatchEvent(new Event(EVENT_NAME))

export function subscribeToNotificationsChanged(listener) {
  window.addEventListener(EVENT_NAME, listener)
  return () => window.removeEventListener(EVENT_NAME, listener)
}
