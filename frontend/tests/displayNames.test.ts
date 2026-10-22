import assert from 'node:assert/strict'
import { test } from 'node:test'
import { displayName, validDisplayNames } from '../src/utils/displayNames.ts'
test('display names accept additive fields and reject malformed names', () => {
  for (const value of [undefined, null, 'Demo Store']) assert.ok(validDisplayNames({ storeName: value }, ['storeName']))
  for (const value of [12, {}, []]) assert.equal(validDisplayNames({ storeName: value }, ['storeName']), false)
})
test('unresolved or blank display names have an explicit identifier fallback', () => {
  assert.equal(displayName(' Demo Store ', 'Store #1'), 'Demo Store')
  for (const name of [undefined, null, ' ']) assert.equal(displayName(name, 'Store #1'), 'Store #1')
})
