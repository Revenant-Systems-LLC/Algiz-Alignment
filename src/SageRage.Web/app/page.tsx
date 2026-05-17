export const dynamic = "force-dynamic";

import { Typography, Row, Col, Card, Alert } from "antd";
import {
  SafetyOutlined,
  WarningOutlined,
  StopOutlined,
  CheckCircleOutlined,
} from "@ant-design/icons";
import AppShell from "@/components/AppShell";
import ProfileCard from "@/components/ProfileCard";
import type { DashboardSummary } from "@/lib/types";

const { Title, Text } = Typography;

async function fetchDashboard(): Promise<DashboardSummary | null> {
  try {
    const res = await fetch("http://localhost:5000/api/dashboard", {
      cache: "no-store",
      signal: AbortSignal.timeout(3000),
    });
    if (!res.ok) return null;
    return res.json();
  } catch {
    return null;
  }
}

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

export default async function DashboardPage() {
  const data = await fetchDashboard();

  return (
    <AppShell>
      <div style={{ marginBottom: 24 }}>
        <Title level={3} style={{ color: "#E4E7EC", margin: 0, fontWeight: 600 }}>
          Dashboard
        </Title>
        <Text style={{ color: "#6B7280" }}>
          Governance overview — all monitored AI systems
        </Text>
      </div>

      {!data ? (
        <Alert
          type="warning"
          message="API Offline"
          description="SageRage.Api is not reachable on localhost:5000. Start the API server to see live governance data."
          showIcon
          style={{ marginBottom: 24, background: "#F0B44A11", border: "1px solid #F0B44A44" }}
        />
      ) : null}

      {/* Summary tiles */}
      <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
        <Col xs={24} sm={12} lg={6}>
          <SummaryTile
            icon={<SafetyOutlined />}
            label="TOTAL PROFILES"
            value={data?.totalProfiles ?? 0}
            color="#C0A96A"
          />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <SummaryTile
            icon={<CheckCircleOutlined />}
            label="ACTIVE"
            value={data?.activeProfiles ?? 0}
            color="#45A58B"
          />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <SummaryTile
            icon={<WarningOutlined />}
            label="FLAGGED"
            value={data?.flaggedProfiles ?? 0}
            color="#F0B44A"
          />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <SummaryTile
            icon={<StopOutlined />}
            label="BLOCKED"
            value={data?.blockedProfiles ?? 0}
            color="#E15B64"
          />
        </Col>
      </Row>

      {/* Profile cards grid */}
      {data && data.snapshots.length > 0 ? (
        <>
          <div style={{ marginBottom: 16 }}>
            <Text style={{ color: "#6B7280", fontSize: 11, letterSpacing: 1 }}>
              GOVERNED SYSTEMS  ·  {data.snapshots.length}
            </Text>
          </div>
          <Row gutter={[16, 16]}>
            {data.snapshots.map(snapshot => (
              <Col key={snapshot.profileId} xs={24} sm={24} md={12} xl={8} xxl={6}>
                <ProfileCard snapshot={snapshot} />
              </Col>
            ))}
          </Row>
        </>
      ) : (
        <Card
          style={{ background: "#161B22", border: "1px solid #2A3344", textAlign: "center" }}
          styles={{ body: { padding: 48 } }}
        >
          <SafetyOutlined style={{ fontSize: 40, color: "#2A3344", marginBottom: 16 }} />
          <div>
            <Text style={{ color: "#6B7280", display: "block", marginBottom: 8 }}>
              No governed systems configured.
            </Text>
            <Text style={{ color: "#6B7280", fontSize: 11 }}>
              Add a GovernanceProfile via the API or CLI to begin monitoring.
            </Text>
          </div>
        </Card>
      )}
    </AppShell>
  );
}
