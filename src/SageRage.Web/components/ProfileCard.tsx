"use client";

import { Card, Progress, Typography, Tooltip, Tag } from "antd";
import { UserOutlined } from "@ant-design/icons";
import type { ProfileSnapshot } from "@/lib/types";
import StatusBadge from "./StatusBadge";

const { Text, Title } = Typography;

function VamBar({
  label, value, color,
}: { label: string; value: number; color: string }) {
  return (
    <div style={{ marginBottom: 6 }}>
      <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 2 }}>
        <Text style={{ color: "#6B7280", fontSize: 11, letterSpacing: 0.5 }}>{label}</Text>
        <Text style={{ color, fontSize: 11, fontFamily: "var(--font-mono)" }}>
          {(value * 100).toFixed(0)}
        </Text>
      </div>
      <Progress
        percent={Math.round(value * 100)}
        showInfo={false}
        size={["100%", 4]}
        strokeColor={color}
        trailColor="#2A3344"
      />
    </div>
  );
}

function maliceColor(malice: number): string {
  if (malice < 0.3)  return "#45A58B";
  if (malice < 0.65) return "#F0B44A";
  return "#E15B64";
}

export default function ProfileCard({ snapshot }: { snapshot: ProfileSnapshot }) {
  const mColor = maliceColor(snapshot.malice);

  return (
    <Card
      size="small"
      style={{
        background: "#161B22",
        border: "1px solid #2A3344",
        borderRadius: 8,
      }}
      styles={{ body: { padding: 16 } }}
    >
      {/* Header */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 12 }}>
        <div style={{ display: "flex", gap: 10, alignItems: "center" }}>
          <div style={{
            width: 36, height: 36, borderRadius: 6,
            background: "#1C2330",
            border: "1px solid #2A3344",
            display: "flex", alignItems: "center", justifyContent: "center",
          }}>
            <UserOutlined style={{ color: "#C0A96A", fontSize: 16 }} />
          </div>
          <div>
            <Title level={5} style={{ margin: 0, color: "#E4E7EC", fontSize: 13 }}>
              {snapshot.displayName}
            </Title>
            <Text style={{ color: "#6B7280", fontSize: 11 }}>
              {snapshot.providerType} · {snapshot.model}
            </Text>
          </div>
        </div>
        <StatusBadge status={snapshot.status} />
      </div>

      {/* VAM Bars */}
      <div style={{
        background: "#1C2330",
        border: "1px solid #2A3344",
        borderRadius: 6,
        padding: "10px 12px",
        marginBottom: 12,
      }}>
        <Text style={{ color: "#6B7280", fontSize: 10, letterSpacing: 1, display: "block", marginBottom: 8 }}>
          EMOTIONAL STATE · VAM
        </Text>
        <VamBar label="MALICE"     value={snapshot.malice}     color={mColor} />
        <VamBar label="ACTIVATION" value={snapshot.activation} color="#C0A96A" />
        <VamBar label="VALENCE"    value={(snapshot.valence + 1) / 2} color="#4A8BD8" />
      </div>

      {/* Metrics row */}
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 8, marginBottom: 12 }}>
        {[
          { label: "COHERENCE", value: (snapshot.coherence * 100).toFixed(0) + "%" },
          { label: "QC SCORE",  value: (snapshot.qcScore  * 100).toFixed(0) + "%" },
          { label: "FLAG RATE", value: (snapshot.flagRate * 100).toFixed(1) + "%" },
        ].map(({ label, value }) => (
          <div key={label} style={{
            background: "#1C2330", border: "1px solid #2A3344",
            borderRadius: 6, padding: "8px 10px", textAlign: "center",
          }}>
            <Text style={{ color: "#6B7280", fontSize: 10, letterSpacing: 0.5, display: "block" }}>{label}</Text>
            <Text style={{ color: "#E4E7EC", fontSize: 16, fontFamily: "var(--font-mono)", fontWeight: 600 }}>
              {value}
            </Text>
          </div>
        ))}
      </div>

      {/* Footer */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <Text style={{ color: "#6B7280", fontSize: 11, fontFamily: "var(--font-mono)" }}>
          {snapshot.totalRequests.toLocaleString()} reqs
          {snapshot.flaggedRequests > 0 && (
            <span style={{ color: "#F0B44A", marginLeft: 8 }}>
              {snapshot.flaggedRequests} flagged
            </span>
          )}
          {snapshot.blockedRequests > 0 && (
            <span style={{ color: "#E15B64", marginLeft: 8 }}>
              {snapshot.blockedRequests} blocked
            </span>
          )}
        </Text>
        {snapshot.currentGlyph && (
          <Tooltip title="Current emotional glyph">
            <Text style={{ color: "#C0A96A", fontSize: 10, fontStyle: "italic" }}>
              {snapshot.currentGlyph}
            </Text>
          </Tooltip>
        )}
      </div>

      {/* Tags */}
      {snapshot.tags.length > 0 && (
        <div style={{ marginTop: 8, display: "flex", gap: 4, flexWrap: "wrap" }}>
          {snapshot.tags.map(tag => (
            <Tag key={tag} style={{
              background: "#1C2330", border: "1px solid #2A3344",
              color: "#9AA3B0", fontSize: 10, margin: 0,
            }}>
              {tag}
            </Tag>
          ))}
        </div>
      )}
    </Card>
  );
}
