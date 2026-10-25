import { useMutation, useQuery } from '@tanstack/react-query'
import { ApiError, createScan, getScan, isFinished } from './client'

/** How often to check on a scan that isn't finished yet. */
export const POLL_INTERVAL_MS = 2000

/**
 * Loads a scan and keeps checking on it every 2 seconds until it's Completed or Failed.
 * TanStack Query handles the loading state, errors, retries and stopping, so the page doesn't have to.
 */
export function useScan(id: string) {
  return useQuery({
    queryKey: ['scan', id],
    queryFn: () => getScan(id),
    refetchInterval: (query) => (isFinished(query.state.data?.status) ? false : POLL_INTERVAL_MS),
    // A missing scan won't appear by asking again; anything else (a blip, a 429) is worth a few retries
    retry: (failureCount, error) => !(error instanceof ApiError && error.status === 404) && failureCount < 3,
  })
}

export function useCreateScan() {
  return useMutation({ mutationFn: createScan })
}
