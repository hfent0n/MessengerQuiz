// Reading budget alerts. Kept apart from index.js so it can be tested without Google Cloud.

// Budget alerts arrive through Pub/Sub, wrapped in a CloudEvent, as base64-encoded JSON such as
// { "costAmount": 4.2, "budgetAmount": 5, "currencyCode": "GBP", ... }.
export function readAlert(event) {
  const data = event?.data?.message?.data
  if (!data) throw new Error('The event has no Pub/Sub message.')
  return JSON.parse(Buffer.from(data, 'base64').toString('utf8'))
}

// True once this month's cost has reached the budget.
export function overBudget(alert) {
  const cost = Number(alert.costAmount)
  const budget = Number(alert.budgetAmount)
  return Number.isFinite(cost) && Number.isFinite(budget) && budget > 0 && cost >= budget
}
