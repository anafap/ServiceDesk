const dateFormat = new Intl.DateTimeFormat(undefined, {
  year: 'numeric', month: 'short', day: 'numeric', timeZone: 'UTC',
})
const dateTimeFormat = new Intl.DateTimeFormat(undefined, {
  year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit', second: '2-digit', timeZone: 'UTC',
})

export function utcTimestamp(value: string) {
  // SQL Server may return stored UTC timestamps without a timezone suffix.
  return /(?:Z|[+-]\d{2}:\d{2})$/i.test(value) ? value : `${value}Z`
}

export function formatUtcDate(value: string) { return dateFormat.format(new Date(utcTimestamp(value))) }
export function formatUtcDateTime(value: string) { return dateTimeFormat.format(new Date(utcTimestamp(value))) }
