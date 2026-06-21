export type GovernanceStatus = "Idle" | "Running" | "Flagged" | "Blocked" | "Error";

export interface ProfileSnapshot {
  profileId: string;
  displayName: string;
  avatarPath: string | null;
  providerType: string;
  model: string;
  status: GovernanceStatus;
  statusDetail: string | null;
  valence: number;
  arousal: number;
  dominance: number;
  activation: number;
  malice: number;
  coherence: number;
  entropy: number;
  drift: number;
  qcScore: number;
  totalRequests: number;
  flaggedRequests: number;
  blockedRequests: number;
  flagRate: number;
  currentGlyph: string;
  lastTraceOperators: string[];
  lastRequestClean: boolean;
  startedAt: string | null;
  lastActivityAt: string | null;
  tags: string[];
}

export interface AuditSummary {
  totalEvents: number;
  eventsLastHour: number;
  blockedEvents: number;
  flaggedEvents: number;
  traceCoveragePercent: number;
  lastEventAt: string | null;
  posture: string;
}

export interface DashboardSummary {
  totalProfiles: number;
  activeProfiles: number;
  flaggedProfiles: number;
  blockedProfiles: number;
  snapshots: ProfileSnapshot[];
  demoMode?: boolean;
  audit?: AuditSummary;
}

export type AuditSeverity = "Info" | "Warning" | "Critical";

export interface AuditEvent {
  id: string;
  timestamp: string;
  profileId: string;
  profileName: string;
  eventType: string;
  severity: AuditSeverity;
  summary: string;
  detail: string | null;
  operatorTrace: string[];
  traceComplete: boolean;
  inputExcerpt: string | null;
  outcome: string | null;
}

export interface ScenarioArm {
  label: string;
  output: string;
  riskVerdict: string;
  operatorTrace: string[];
  traceComplete: boolean;
  outcome: string | null;
}

export interface ScenarioCompareResult {
  scenarioId: string;
  title: string;
  subtitle: string;
  prompt: string;
  ungoverned: ScenarioArm;
  governed: ScenarioArm;
}
