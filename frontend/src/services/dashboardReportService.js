import { apiRequest, toQueryString } from './apiClient.js'

export const downloadDashboardReport = (from, to) =>
  apiRequest(`/api/reports/dashboard${toQueryString({
    from, to, timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone,
  })}`, { responseType: 'file' })
