// Turns off billing for the project once spending reaches its budget, which stops everything in it.
// Google Cloud budgets only send alerts, so this is what makes the budget a cap.
import functions from '@google-cloud/functions-framework'
import { CloudBillingClient } from '@google-cloud/billing'
import { overBudget, readAlert } from './budget.js'

const billing = new CloudBillingClient()

functions.cloudEvent('stopBilling', async (event) => {
  const alert = readAlert(event)
  if (!overBudget(alert)) {
    console.log(`Under budget: ${alert.costAmount} of ${alert.budgetAmount} ${alert.currencyCode ?? ''}.`)
    return
  }

  const projectId = process.env.PROJECT_ID
  if (!projectId) throw new Error('PROJECT_ID is not set.')

  const name = `projects/${projectId}`
  const [info] = await billing.getProjectBillingInfo({ name })
  if (!info.billingEnabled) {
    console.log('Billing is already off.')
    return
  }

  // An empty billing account unlinks the project from billing.
  await billing.updateProjectBillingInfo({ name, projectBillingInfo: { billingAccountName: '' } })
  console.log(`Budget reached (${alert.costAmount} of ${alert.budgetAmount}). Billing is now off for ${projectId}.`)
})
