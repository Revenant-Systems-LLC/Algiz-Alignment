"use client";

import { useState } from "react";
import { Layout, Menu, Typography } from "antd";
import {
  DashboardOutlined,
  ApiOutlined,
  AuditOutlined,
  SafetyOutlined,
  SettingOutlined,
  RadarChartOutlined,
} from "@ant-design/icons";
import { useRouter, usePathname } from "next/navigation";

const { Header, Sider, Content } = Layout;
const { Text } = Typography;

const NAV_ITEMS = [
  { key: "/",           icon: <DashboardOutlined />, label: "Dashboard"  },
  { key: "/profiles",   icon: <RadarChartOutlined />, label: "Profiles"  },
  { key: "/trace",      icon: <AuditOutlined />,      label: "Trace Log" },
  { key: "/proxy",      icon: <ApiOutlined />,        label: "Proxy"     },
  { key: "/settings",  icon: <SettingOutlined />,     label: "Settings"  },
];

export default function AppShell({ children }: { children: React.ReactNode }) {
  const router   = useRouter();
  const pathname = usePathname();
  const [collapsed, setCollapsed] = useState(false);

  return (
    <Layout style={{ minHeight: "100vh", background: "#10141A" }}>
      {/* ── Sidebar ── */}
      <Sider
        collapsible
        collapsed={collapsed}
        onCollapse={setCollapsed}
        width={220}
        style={{ background: "#0D1117", borderRight: "1px solid #2A3344" }}
      >
        {/* Brand */}
        <div style={{
          height: 56,
          display: "flex",
          alignItems: "center",
          justifyContent: collapsed ? "center" : "flex-start",
          padding: collapsed ? 0 : "0 20px",
          borderBottom: "1px solid #2A3344",
          gap: 10,
        }}>
          <SafetyOutlined style={{ color: "#C0A96A", fontSize: 20 }} />
          {!collapsed && (
            <Text strong style={{ color: "#C0A96A", fontSize: 14, letterSpacing: 1 }}>
              SAIGE-RAGE
            </Text>
          )}
        </div>

        <Menu
          theme="dark"
          mode="inline"
          selectedKeys={[pathname]}
          items={NAV_ITEMS}
          onClick={({ key }) => router.push(key)}
          style={{ background: "#0D1117", borderRight: "none", marginTop: 8 }}
        />
      </Sider>

      <Layout>
        {/* ── Header ── */}
        <Header style={{
          background: "#0B0F14",
          borderBottom: "1px solid #2A3344",
          height: 48,
          lineHeight: "48px",
          padding: "0 24px",
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
        }}>
          <Text style={{ color: "#6B7280", fontSize: 12 }}>
            Revenant Systems LLC  ·  AI Governance Console
          </Text>
          <Text style={{ color: "#6B7280", fontSize: 12 }}>
            v0.1.0-alpha
          </Text>
        </Header>

        {/* ── Content ── */}
        <Content style={{
          padding: 24,
          background: "#10141A",
          minHeight: "calc(100vh - 48px)",
        }}>
          {children}
        </Content>
      </Layout>
    </Layout>
  );
}
