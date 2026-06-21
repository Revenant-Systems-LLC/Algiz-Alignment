"use client";

import { Typography } from "antd";
import { SafetyCertificateOutlined } from "@ant-design/icons";
import type { AuditSummary } from "@/lib/types";

const { Text } = Typography;

export default function DefensibilityBanner({ audit }: { audit: AuditSummary }) {
  return (
    <div style={{
      background: "linear-gradient(135deg, #161B22 0%, #1a2332 50%, #161B22 100%)",
      border: "1px solid #C0A96A44",
      borderRadius: 10,
      padding: "18px 22px",
      marginBottom: 24,
      display: "flex",
      alignItems: "center",
      justifyContent: "space-between",
      gap: 16,
      flexWrap: "wrap",
    }}>
      <div style={{ display: "flex", gap: 14, alignItems: "center" }}>
        <div style={{
          width: 44, height: 44, borderRadius: 8,
          background: "#C0A96A18", border: "1px solid #C0A96A55",
          display: "flex", alignItems: "center", justifyContent: "center",
        }}>
          <SafetyCertificateOutlined style={{ color: "#C0A96A", fontSize: 22 }} />
        </div>
        <div>
          <Text style={{ color: "#C0A96A", fontSize: 11, letterSpacing: 2, display: "block" }}>
            DEFENSIBILITY POSTURE
          </Text>
          <Text strong style={{ color: "#E4E7EC", fontSize: 18 }}>
            {audit.posture}
          </Text>
          <Text style={{ color: "#6B7280", fontSize: 12, display: "block", marginTop: 2 }}>
            Every governed interaction produces an inspectable operator trace for counsel and audit.
          </Text>
        </div>
      </div>
      <div style={{ display: "flex", gap: 24 }}>
        {[
          { label: "TRACE COVERAGE", value: `${audit.traceCoveragePercent.toFixed(0)}%`, color: "#45A58B" },
          { label: "EVENTS / HR", value: audit.eventsLastHour, color: "#C0A96A" },
          { label: "FLAGGED", value: audit.flaggedEvents, color: "#F0B44A" },
          { label: "BLOCKED", value: audit.blockedEvents, color: "#E15B64" },
        ].map(({ label, value, color }) => (
          <div key={label} style={{ textAlign: "center" }}>
            <div style={{ color, fontSize: 22, fontWeight: 700, fontFamily: "var(--font-mono)" }}>{value}</div>
            <Text style={{ color: "#6B7280", fontSize: 10, letterSpacing: 0.5 }}>{label}</Text>
          </div>
        ))}
      </div>
    </div>
  );
}
