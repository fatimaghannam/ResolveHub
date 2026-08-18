import assert from 'node:assert/strict'
import test from 'node:test'
import { formatRelativeTime, parseApiUtcDate } from './dateTime.js'

const now = Date.parse('2026-08-18T12:00:30Z')

test('formats a timestamp created approximately now', () => {
  assert.equal(formatRelativeTime('2026-08-18T12:00:00Z', now), 'Just now')
})

test('formats a timestamp five minutes ago', () => {
  assert.equal(formatRelativeTime('2026-08-18T11:55:00Z', now), '5m ago')
})

test('formats a timestamp three hours ago', () => {
  assert.equal(formatRelativeTime('2026-08-18T09:00:00Z', now), '3h ago')
})

test('treats an API timestamp without an offset as UTC', () => {
  assert.equal(parseApiUtcDate('2026-08-18T12:00:00').getTime(),
    Date.parse('2026-08-18T12:00:00Z'))
  assert.equal(formatRelativeTime('2026-08-18T12:00:00', now), 'Just now')
})
