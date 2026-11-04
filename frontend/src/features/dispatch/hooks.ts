import { useEffect, useState } from 'react'
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, type CandidateParams } from '@/lib/api'

/** Every query whose data can change after a dispatch action. */
const dispatchQueryKeys = [['summary'], ['dashboard'], ['orders'], ['drivers'], ['vehicles'], ['assignments'], ['assignment'], ['candidates']]

export function useRefreshDispatchData() {
  const queryClient = useQueryClient()
  return () => Promise.all(dispatchQueryKeys.map((queryKey) => queryClient.invalidateQueries({ queryKey })))
}

export function useCandidates(orderId: number | null, params: CandidateParams) {
  return useQuery({
    queryKey: ['candidates', orderId, params.driverId ?? null, params.vehicleId ?? null, params.excludeAssignmentId ?? null],
    queryFn: () => api.candidates(orderId!, params),
    enabled: orderId !== null,
    placeholderData: keepPreviousData,
  })
}

export function useAssignmentDetail(id: number | null) {
  return useQuery({
    queryKey: ['assignment', id],
    queryFn: () => api.assignment(id!),
    enabled: id !== null,
  })
}

export function useDebouncedValue<T>(value: T, delayMs = 300) {
  const [debounced, setDebounced] = useState(value)
  useEffect(() => {
    const handle = setTimeout(() => setDebounced(value), delayMs)
    return () => clearTimeout(handle)
  }, [value, delayMs])
  return debounced
}
