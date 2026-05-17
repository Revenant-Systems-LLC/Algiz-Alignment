export type GovernanceStatus = "Idle" | "Running" | "Flagged" | "Blocked" | "Error";

export interface ProfileSnapshot {
  profileId:        string;
  displayName:      string;
  avatarPath:       string | null;
  providerType:     string;
  model:            string;
  status:           GovernanceStatus;
  statusDetail:     string | null;
  valence:          number;
  arousal:          number;
  dominance:        number;
  activation:       number;
  malice:           number;
  coherence:        number;
  entropy:          number;
  drift:            number;
  qcScore:          number;
  totalRequests:    number;
  flaggedRequests:  number;
  blockedRequests:  number;
  flagRate:         number;
  currentGlyph:     string;
  lastTraceOperators: string[];
  lastRequestClean: boolean;
  startedAt:        string | null;
  lastActivityAt:   string | null;
  tags:             string[];
}

export interface DashboardSummary {
  totalProfiles:   number;
  activeProfiles:  number;
  flaggedProfiles: number;
  blockedProfiles: number;
  snapshots:       ProfileSnapshot[];
}
