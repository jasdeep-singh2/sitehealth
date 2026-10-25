// A monthly budget for the resource group, with emails at 80% of actual spend and when the forecast passes 100%.
// A budget only warns; it never stops anything.

@description('Monthly amount in the subscription\'s billing currency.')
param amount int

@description('Who gets the warning emails.')
param contactEmail string

@description('First day of the current month. Budgets must start on the first of a month.')
param startDate string = utcNow('yyyy-MM-01')

resource budget 'Microsoft.Consumption/budgets@2023-11-01' = {
  name: 'budget-sitehealth'
  properties: {
    category: 'Cost'
    amount: amount
    timeGrain: 'Monthly'
    timePeriod: { startDate: startDate }
    notifications: {
      actualOver80Percent: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 80
        thresholdType: 'Actual'
        contactEmails: [contactEmail]
      }
      forecastOver100Percent: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 100
        thresholdType: 'Forecasted'
        contactEmails: [contactEmail]
      }
    }
  }
}
