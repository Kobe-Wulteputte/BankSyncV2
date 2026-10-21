export type QueryValue = string | number | boolean | null | undefined | Array<string | number>
export type Query = Record<string, QueryValue>

/** Builds an API query string; arrays are comma-joined, empty/null values are dropped. */
export function toSearchParams(query: Query): URLSearchParams {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null || value === '') continue
    if (Array.isArray(value)) {
      if (value.length) params.set(key, value.join(','))
    } else {
      params.set(key, String(value))
    }
  }
  return params
}
