import { Badge, Tag } from "antd";
import type { GovernanceStatus } from "@/lib/types";

const STATUS_CONFIG: Record<GovernanceStatus, { color: string; label: string }> = {
  Idle:    { color: "#6B7280", label: "IDLE"    },
  Running: { color: "#45A58B", label: "RUNNING" },
  Flagged: { color: "#F0B44A", label: "FLAGGED" },
  Blocked: { color: "#E15B64", label: "BLOCKED" },
  Error:   { color: "#E15B64", label: "ERROR"   },
};

export default function StatusBadge({ status }: { status: GovernanceStatus }) {
  const { color, label } = STATUS_CONFIG[status] ?? STATUS_CONFIG.Idle;
  return (
    <Tag
      style={{
        background: `${color}22`,
        border: `1px solid ${color}66`,
        color,
        fontWeight: 600,
        fontSize: 10,
        letterSpacing: 1,
        padding: "1px 8px",
      }}
    >
      <Badge color={color} style={{ marginRight: 4 }} />
      {label}
    </Tag>
  );
}
