"use client";

import { Typography, Tag } from "antd";
import type { AuditEvent } from "@/lib/types";

const { Text } = Typography;

const SEV_COLOR: Record<string, string> = {
  Info: "#45A58B",
  Warning: "#F0B44A",
  Critical: "#E15B64",
};

function formatTime(iso: string) {
  return new Date(iso).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", second: "2-digit" });
}

export default function AuditFeed({ events, compact = false }: { events: AuditEvent[]; compact?: boolean }) {
  if (events.length === 0) {
    return (
      <Text style={{ color: "#6B7280", fontSize: 12 }}>No audit events yet.</Text>
    );
  }

  const shown = compact ? events.slice(0, 8) : events;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
      {shown.map(evt => {
        const color = SEV_COLOR[evt.severity] ?? "#6B7280";
        return (
          <div
            key={evt.id}
            style={{
              background: "#161B22",
              border: `1px solid ${color}33`,
              borderLeft: `3px solid ${color}`,
              borderRadius: 6,
              padding: compact ? "10px 12px" : "12px 14px",
            }}
          >
            <div style={{ display: "flex", justifyContent: "space-between", gap: 8, marginBottom: 4 }}>
              <Text style={{ color: "#E4E7EC", fontSize: 12, fontWeight: 600 }}>{evt.summary}</Text>
              <Text style={{ color: "#6B7280", fontSize: 10, fontFamily: "var(--font-mono)", flexShrink: 0 }}>
                {formatTime(evt.timestamp)}
              </Text>
            </div>
            <Text style={{ color: "#6B7280", fontSize: 11, display: "block", marginBottom: 6 }}>
              {evt.profileName} · {evt.eventType}
            </Text>
            {evt.inputExcerpt && (
              <Text style={{ color: "#9AA3B0", fontSize: 11, fontStyle: "italic", display: "block", marginBottom: 6 }}>
                &ldquo;{evt.inputExcerpt}&rdquo;
              </Text>
            )}
            {evt.operatorTrace.length > 0 && (
              <div style={{ display: "flex", gap: 4, flexWrap: "wrap" }}>
                {evt.operatorTrace.map(op => (
                  <Tag key={op} style={{ background: "#1C2330", border: "1px solid #2A3344", color: "#C0A96A", fontSize: 9, margin: 0 }}>
                    {op}
                  </Tag>
                ))}
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}
