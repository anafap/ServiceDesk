import assert from 'node:assert/strict'
import { test } from 'node:test'
import { utcTimestamp, formatUtcDate, formatUtcDateTime } from '../src/utils/dates.ts'

test('SQL timestamps without a suffix are interpreted as UTC', () => {
  assert.equal(utcTimestamp('2026-10-21T09:30:00'), '2026-10-21T09:30:00Z')
  assert.equal(formatUtcDate('2026-10-21T09:30:00'), formatUtcDate('2026-10-21T09:30:00Z'))
  assert.equal(formatUtcDateTime('2026-10-21T09:30:00'), formatUtcDateTime('2026-10-21T09:30:00Z'))
})

test('explicit timezone offsets are preserved and converted consistently', () => {
  assert.equal(utcTimestamp('2026-10-21T12:30:00+03:00'), '2026-10-21T12:30:00+03:00')
  assert.equal(formatUtcDateTime('2026-10-21T12:30:00+03:00'), formatUtcDateTime('2026-10-21T09:30:00Z'))
})
