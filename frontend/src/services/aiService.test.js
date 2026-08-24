import assert from 'node:assert/strict'
import test from 'node:test'
import { formatLocalDateTime } from '../utils/dateTime.js'
import { localizeAiTicketTimestamps } from './aiService.js'

test('formats an AI ticket timestamp using the browser-local display convention', () => {
  const updatedAt = '2026-08-24T18:13:00Z'
  const response = localizeAiTicketTimestamps({
    message: 'RH-2026-11118 — Example\n\nLast updated: Aug 24, 2026 at 6:13 PM UTC',
    ticketLookup: { tickets: [{ updatedAt }] },
  })

  assert.match(response.message,
    new RegExp(`Last updated: ${formatLocalDateTime(updatedAt).replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}$`))
  assert.doesNotMatch(response.message, / UTC$/)
})

test('leaves non-ticket AI responses unchanged', () => {
  const response = { message: 'How can I help?' }

  assert.equal(localizeAiTicketTimestamps(response), response)
})
