"use client";

import { useState, useEffect, useCallback } from "react";
import { Typography, Row, Col, Card, Alert, Spin } from "antd";
import {
  SafetyOutlined,
  WarningOutlined,
  StopOutlined,
  CheckCircleOutlined,
} from "@ant-design/icons";
import AppShell from "@/components/AppShell";
import ProfileCard from "@/components/ProfileCard";
import DefensibilityBanner from "@/components/DefensibilityBanner";
import ScenarioComparePanel from "@/components/ScenarioComparePanel";
import AuditFeed from "@/components/AuditFeed";
import { getDashboard, getAuditRecent } from "@/lib/api";
import type { AuditEvent, DashboardSummary } from "@/lib/types";

const { Title, Text } = Typography;

function SummaryTile({
  icon, label, value, color,
}: {
  icon: React.ReactNode;
  label: string;
  value: number;
  color: string;
}) {
  return (
    <Card
      size="small"
      style={{ background: "#161B22", border: "1px solid #2A3344", borderRadius: 8 }}
      styles={{ body: { padding: "16px 20px" } }}
    >
      <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
        <div style={{
          width: 40, height: 40, borderRadius: 8,
          background: `${color}18`, border: `1px solid ${color}44`,
          display: "flex", alignItems: "center", justifyContent: "center",
          fontSize: 18, color,
        }}>
          {icon}
        </div>
        <div>
          <div style={{ color, fontSize: 24, fontWeight: 700, lineHeight: 1.2 }}>{value}</div>
          <Text style={{ color: "#6B7280", fontSize: 11, letterSpacing: 0.5 }}>{label}</Text>
        </div>
      </div>
    </Card>
  );
}

export function DashboardPage() {
  const [data, setData] = useState<DashboardSummary | null>(null);
  const [auditEvents, setAuditEvents] = useState<AuditEvent[]>([]);
  const [loading, setLoading] = useState(true);

  const refresh = useCallback(async () => {
    try {
      const [dash, audit] = await Promise.all([
        getDashboard(),
        getAuditRecent(12),
      ]);
      setData(dash);
      setAuditEvents(audit);
    } catch {
      setData(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    refresh();
    const id = setInterval(refresh, 3000);
    return () => clearInterval(id);
  }, [refresh]);

  if (loading) {
    return (
      <AppShell>
        <div style={{ display: "flex", justifyContent: "center", alignItems: "center", minHeight: "50vh" }}>
          <Spin size="large" />
        </div>
      </AppShell>
    );
  }

  return (
    <AppShell>
      <div style={{ marginBottom: 20 }}>
        <Title level={3} style={{ color: "#E4E7EC", margin: 0, fontWeight: 600 }}>
          AI Governance Console
        </Title>
        <Text style={{ color: "#6B7280" }}>
          Enterprise defensibility layer — live operator traces for every governed system
        </Text>
      </div>

      {!data ? (
        <Alert
          type="warning"
          message="API Offline"
          description="Start the demo: .\scripts\launch-demo.ps1"
          showIcon
          style={{ marginBottom: 24, background: "#F0B44A11", border: "1px solid #F0B44A44" }}
        />
      ) : null}

      {data?.audit && <DefensibilityBanner audit={data.audit} />}

      <ScenarioComparePanel />

      <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
        <Col xs={24} sm={12} lg={6}>
          <SummaryTile icon={<SafetyOutlined />} label="TOTAL PROFILES" value={data?.totalProfiles ?? 0} color="#C0A96A" />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <SummaryTile icon={<CheckCircleOutlined />} label="ACTIVE" value={data?.activeProfiles ?? 0} color="#45A58B" />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <SummaryTile icon={<WarningOutlined />} label="FLAGGED" value={data?.flaggedProfiles ?? 0} color="#F0B44A" />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <SummaryTile icon={<StopOutlined />} label="BLOCKED" value={data?.blockedProfiles ?? 0} color="#E15B64" />
        </Col>
      </Row>

      <Row gutter={[16, 16]}>
        <Col xs={24} xl={16}>
          {data && data.snapshots.length > 0 ? (
            <>
              <Text style={{ color: "#6B7280", fontSize: 11, letterSpacing: 1, display: "block", marginBottom: 12 }}>
                GOVERNED SYSTEMS · {data.snapshots.length}
              </Text>
              <Row gutter={[16, 16]}>
                {data.snapshots.map(snapshot => (
                  <Col key={snapshot.profileId} xs={24} sm={24} md={12} xl={8}>
                    <ProfileCard snapshot={snapshot} />
                  </Col>
                ))}
              </Row>
            </>
          ) : (
            <Card style={{ background: "#161B22", border: "1px solid #2A3344", textAlign: "center" }} styles={{ body: { padding: 48 } }}>
              <SafetyOutlined style={{ fontSize: 40, color: "#2A3344", marginBottom: 16 }} />
              <Text style={{ color: "#6B7280" }}>No governed systems — start the API in demo mode.</Text>
            </Card>
          )}
        </Col>
        <Col xs={24} xl={8}>
          <Card
            title={<Text style={{ color: "#C0A96A", fontSize: 11, letterSpacing: 1 }}>DEFENSIBILITY LOG</Text>}
            size="small"
            style={{ background: "#161B22", border: "1px solid #2A3344", borderRadius: 8 }}
            styles={{ header: { borderBottom: "1px solid #2A3344" }, body: { maxHeight: 520, overflowY: "auto" } }}
          >
            <AuditFeed events={auditEvents} compact />
          </Card>
        </Col>
      </Row>
    </AppShell>
  );
}
