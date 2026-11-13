import assert from 'node:assert/strict'
import { test } from 'node:test'
import { overBudget, readAlert } from './budget.js'

const event = (alert) => ({ data: { message: { data: Buffer.from(JSON.stringify(alert)).toString('base64') } } })

test('reads the alert from a Pub/Sub event', () => {
  assert.deepEqual(readAlert(event({ costAmount: 1.5, budgetAmount: 5 })), { costAmount: 1.5, budgetAmount: 5 })
})

test('an event without a message is an error', () => {
  assert.throws(() => readAlert({ data: {} }))
})

test('stops only once the cost reaches the budget', () => {
  assert.equal(overBudget({ costAmount: 4.99, budgetAmount: 5 }), false)
  assert.equal(overBudget({ costAmount: 5, budgetAmount: 5 }), true)
  assert.equal(overBudget({ costAmount: 12, budgetAmount: 5 }), true)
})

test('ignores alerts it cannot read', () => {
  assert.equal(overBudget({ costAmount: 'lots', budgetAmount: 5 }), false)
  assert.equal(overBudget({ costAmount: 5, budgetAmount: 0 }), false)
  assert.equal(overBudget({}), false)
})
