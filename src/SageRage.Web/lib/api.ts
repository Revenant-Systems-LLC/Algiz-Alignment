import type { DashboardSummary, ProfileSnapshot } from "./types";

const BASE = "/api";

export async function getDashboard(): Promise<DashboardSummary> {
  const res = await fetch(`${BASE}/dashboard`, { cache: "no-store" });
  if (!res.ok) throw new Error(`Dashboard fetch failed: ${res.status}`);
  return res.json();
}

export async function getProfile(id: string): Promise<ProfileSnapshot> {
  const res = await fetch(`${BASE}/profiles/${id}`, { cache: "no-store" });
  if (!res.ok) throw new Error(`Profile fetch failed: ${res.status}`);
  return res.json();
}

export async function startProfile(id: string): Promise<void> {
  await fetch(`${BASE}/profiles/${id}/start`, { method: "POST" });
}

export async function stopProfile(id: string): Promise<void> {
  await fetch(`${BASE}/profiles/${id}/stop`, { method: "POST" });
}

export async function resetProfile(id: string): Promise<void> {
  await fetch(`${BASE}/profiles/${id}/reset`, { method: "POST" });
}
