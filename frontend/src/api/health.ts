import { apiGet } from './client'

export type HealthStatus = 'Healthy' | 'Degraded' | 'Unhealthy'

export interface HealthCheckEntry {
  name: string
  status: HealthStatus
  durationMs: number
  description: string | null
  error: string | null
}

export interface HealthReport {
  status: HealthStatus
  totalDurationMs: number
  checks: HealthCheckEntry[]
}

export function fetchHealth(signal?: AbortSignal): Promise<HealthReport> {
  return apiGet<HealthReport>('/health', signal)
}
