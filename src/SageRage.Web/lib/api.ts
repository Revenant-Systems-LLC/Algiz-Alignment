import type {
  AuditEvent,
  AuditSummary,
  DashboardSummary,
  ProfileSnapshot,
  ScenarioCompareResult,
} from "./types";

const BASE = "/api";
const TIMEOUT_MS = 10_000;

function t(): AbortSignal {
  return AbortSignal.timeout(TIMEOUT_MS);
}

export async function getDashboard(): Promise<DashboardSummary> {
  const res = await fetch(`${BASE}/dashboard`, { cache: "no-store", signal: t() });
  if (!res.ok) throw new Error(`Dashboard fetch failed: ${res.status}`);
  return res.json();
}

export async function getProfile(id: string): Promise<ProfileSnapshot> {
  const res = await fetch(`${BASE}/profiles/${id}`, { cache: "no-store", signal: t() });
  if (!res.ok) throw new Error(`Profile fetch failed: ${res.status}`);
  return res.json();
}

export async function getAuditRecent(limit = 100): Promise<AuditEvent[]> {
  const res = await fetch(`${BASE}/audit/recent?limit=${limit}`, { cache: "no-store", signal: t() });
  if (!res.ok) throw new Error(`Audit fetch failed: ${res.status}`);
  return res.json();
}

export async function getAuditSummary(): Promise<AuditSummary> {
  const res = await fetch(`${BASE}/audit/summary`, { cache: "no-store", signal: t() });
  if (!res.ok) throw new Error(`Audit summary failed: ${res.status}`);
  return res.json();
}

export async function runScenarioCompare(scenarioId: string): Promise<ScenarioCompareResult> {
  const res = await fetch(`${BASE}/demo/scenarios/${scenarioId}/compare`, { method: "POST", signal: t() });
  if (!res.ok) throw new Error(`Scenario compare failed: ${res.status}`);
  return res.json();
}

export async function startProfile(id: string): Promise<void> {
  await fetch(`${BASE}/profiles/${id}/start`, { method: "POST", signal: t() });
}

export async function stopProfile(id: string): Promise<void> {
  await fetch(`${BASE}/profiles/${id}/stop`, { method: "POST", signal: t() });
}

export async function resetProfile(id: string): Promise<void> {
  await fetch(`${BASE}/profiles/${id}/reset`, { method: "POST", signal: t() });
}
