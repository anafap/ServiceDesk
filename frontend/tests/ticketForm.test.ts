import assert from 'node:assert/strict'
import { test } from 'node:test'
import { validateTicketForm } from '../src/forms/ticketForm.ts'

const stores = [{ id: 1, name: 'Main store' }]
const categories = [{ id: 2, name: 'Equipment' }]
const valid = { title: 'Printer issue', description: 'Paper is jammed', storeId: '1', categoryId: '2', priority: '2' }

test('validates required trimmed text and selections', () => {
  assert.deepEqual(validateTicketForm(valid, stores, categories), {})
  const errors = validateTicketForm({ ...valid, title: '  ', description: '\n', storeId: '', categoryId: '', priority: '' }, stores, categories)
  assert.deepEqual(Object.keys(errors).sort(), ['categoryId', 'description', 'priority', 'storeId', 'title'])
})

test('enforces database text limits and permits their exact boundaries', () => {
  assert.deepEqual(validateTicketForm({ ...valid, title: 't'.repeat(200), description: 'd'.repeat(4000) }, stores, categories), {})
  const errors = validateTicketForm({ ...valid, title: 't'.repeat(201), description: 'd'.repeat(4001) }, stores, categories)
  assert.ok(errors.title)
  assert.ok(errors.description)
})

test('rejects IDs outside loaded lookups and unsupported priorities', () => {
  const errors = validateTicketForm({ ...valid, storeId: '99', categoryId: '99', priority: '5' }, stores, categories)
  assert.ok(errors.storeId)
  assert.ok(errors.categoryId)
  assert.ok(errors.priority)
  assert.ok(validateTicketForm(valid, [], []).storeId)
})
