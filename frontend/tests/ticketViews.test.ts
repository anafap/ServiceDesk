import assert from 'node:assert/strict'
import { test } from 'node:test'
import type { Ticket, TicketStatus } from '../src/api/tickets.ts'
import { filterTickets } from '../src/utils/ticketViews.ts'

const tickets: Ticket[] = Array.from({ length: 7 }, (_, index) => ({
  id: index + 1, title: index % 2 === 0 ? 'Repair printer' : 'Network issue',
  description: 'Not searched', status: (index + 1) as TicketStatus,
  priority: index % 2 === 0 ? 3 : 1, storeId: 1, createdAt: '2026-10-21T10:00:00',
}))
const all = { title: '', status: 'all', priority: 'all' } as const

test('empty filters preserve all accessible tickets without mutation', () => {
  const before = structuredClone(tickets)
  assert.deepEqual(filterTickets(tickets, all), tickets)
  assert.deepEqual(tickets, before)
  assert.deepEqual(filterTickets([], all), [])
})
test('title search trims whitespace, ignores case and searches only titles', () => {
  assert.equal(filterTickets(tickets, { ...all, title: '  PRINTER ' }).length, 4)
  assert.equal(filterTickets(tickets, { ...all, title: 'Not searched' }).length, 0)
})
test('every workflow status is selected exactly', () => {
  for (const ticket of tickets) assert.deepEqual(filterTickets(tickets, { ...all, status: ticket.status }), [ticket])
})
test('title, status and priority combine with AND', () => {
  assert.deepEqual(filterTickets(tickets, { title: 'printer', status: 3, priority: 3 }), [tickets[2]])
  assert.deepEqual(filterTickets(tickets, { title: 'printer', status: 3, priority: 1 }), [])
})
test('each priority is selected exactly and clearing restores the list', () => {
  for (const priority of [1, 2, 3, 4] as const) {
    assert.deepEqual(filterTickets(tickets, { ...all, priority }), tickets.filter((ticket) => ticket.priority === priority))
  }
  assert.equal(filterTickets(tickets, all).length, 7)
})
