"use client";

import { useState } from "react";
import { Button, Card, Typography, Spin, Tag } from "antd";
import { ThunderboltOutlined, WarningOutlined, SafetyOutlined } from "@ant-design/icons";
import { runScenarioCompare } from "@/lib/api";
import type { ScenarioCompareResult } from "@/lib/types";

const { Title, Text, Paragraph } = Typography;

function ArmPanel({
  arm,
  variant,
}: {
  arm: ScenarioCompareResult["ungoverned"];
  variant: "danger" | "safe";
}) {
  const border = variant === "danger" ? "#E15B64" : "#45A58B";
  const bg = variant === "danger" ? "#E15B6410" : "#45A58B10";

  return (
    <Card
      size="small"
      style={{ background: "#161B22", border: `1px solid ${border}55`, borderRadius: 8, height: "100%" }}
      styles={{ body: { padding: 16 } }}
    >
      <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 10 }}>
        {variant === "danger"
          ? <WarningOutlined style={{ color: border }} />
          : <SafetyOutlined style={{ color: border }} />}
        <Text strong style={{ color: border, fontSize: 12, letterSpacing: 0.5 }}>{arm.label}</Text>
        {arm.outcome && (
          <Tag style={{ marginLeft: "auto", background: `${border}22`, border: `1px solid ${border}66`, color: border }}>
            {arm.outcome.toUpperCase()}
          </Tag>
        )}
      </div>
      <Paragraph style={{
        color: "#E4E7EC",
        fontSize: 13,
        lineHeight: 1.6,
        background: bg,
        border: `1px solid ${border}33`,
        borderRadius: 6,
        padding: 12,
        marginBottom: 12,
        fontFamily: "var(--font-inter)",
      }}>
        {arm.output}
      </Paragraph>
      <Text style={{ color: "#6B7280", fontSize: 11, display: "block", marginBottom: 8 }}>
        {arm.riskVerdict}
      </Text>
      {arm.operatorTrace.length > 0 && (
        <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
          {arm.operatorTrace.map(op => (
            <Tag key={op} style={{ background: "#1C2330", border: "1px solid #2A3344", color: "#C0A96A", fontSize: 10, margin: 0 }}>
              {op}
            </Tag>
          ))}
        </div>
      )}
      {!arm.traceComplete && variant === "danger" && (
        <Text style={{ color: "#E15B64", fontSize: 10, fontStyle: "italic" }}>
          No operator trace — nothing to hand counsel in a deposition.
        </Text>
      )}
    </Card>
  );
}

export default function ScenarioComparePanel() {
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<ScenarioCompareResult | null>(null);

  async function fireScenario() {
    setLoading(true);
    try {
      setResult(await runScenarioCompare("cfo-fraud"));
    } finally {
      setLoading(false);
    }
  }

  return (
    <div style={{ marginBottom: 28 }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 16, flexWrap: "wrap", gap: 12 }}>
        <div>
          <Title level={4} style={{ color: "#E4E7EC", margin: 0 }}>
            Liability Demonstration
          </Title>
          <Text style={{ color: "#6B7280" }}>
            Same gray-zone CFO prompt — ungoverned output vs Algiz runtime with audit trace
          </Text>
        </div>
        <Button
          type="primary"
          icon={<ThunderboltOutlined />}
          loading={loading}
          onClick={fireScenario}
          style={{ background: "#C0A96A", borderColor: "#C0A96A", color: "#10141A", fontWeight: 600 }}
        >
          Run CFO Scenario
        </Button>
      </div>

      {loading && (
        <div style={{ textAlign: "center", padding: 40 }}>
          <Spin size="large" />
          <div style={{ marginTop: 12, color: "#6B7280" }}>Running operator pipeline…</div>
        </div>
      )}

      {result && !loading && (
        <>
          <Card
            size="small"
            style={{ background: "#0B0F14", border: "1px solid #2A3344", marginBottom: 16 }}
            styles={{ body: { padding: 14 } }}
          >
            <Text style={{ color: "#6B7280", fontSize: 10, letterSpacing: 1 }}>ADVERSARIAL PROMPT</Text>
            <Paragraph style={{ color: "#E4E7EC", margin: "6px 0 0", fontSize: 13, fontStyle: "italic" }}>
              &ldquo;{result.prompt}&rdquo;
            </Paragraph>
          </Card>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 16 }}>
            <ArmPanel arm={result.ungoverned} variant="danger" />
            <ArmPanel arm={result.governed} variant="safe" />
          </div>
        </>
      )}
    </div>
  );
}
