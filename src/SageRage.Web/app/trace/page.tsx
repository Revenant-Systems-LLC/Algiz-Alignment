"use client";

import { useState, useEffect } from "react";
import { Typography, Spin } from "antd";
import AppShell from "@/components/AppShell";
import AuditFeed from "@/components/AuditFeed";
import DefensibilityBanner from "@/components/DefensibilityBanner";
import { getAuditRecent, getAuditSummary } from "@/lib/api";
import type { AuditEvent, AuditSummary } from "@/lib/types";

const { Title, Text } = Typography;

export default function TracePage() {
  const [events, setEvents] = useState<AuditEvent[]>([]);
  const [summary, setSummary] = useState<AuditSummary | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    async function load() {
      try {
        const [ev, sum] = await Promise.all([getAuditRecent(100), getAuditSummary()]);
        setEvents(ev);
        setSummary(sum);
      } finally {
        setLoading(false);
      }
    }
    load();
    const id = setInterval(load, 3000);
    return () => clearInterval(id);
  }, []);

  return (
    <AppShell>
      <Title level={3} style={{ color: "#E4E7EC", margin: "0 0 4px" }}>Defensibility Trace Log</Title>
      <Text style={{ color: "#6B7280", display: "block", marginBottom: 20 }}>
        Immutable-style audit record — what counsel needs in a deposition
      </Text>
      {loading ? (
        <Spin size="large" />
      ) : (
        <>
          {summary && <DefensibilityBanner audit={summary} />}
          <AuditFeed events={events} />
        </>
      )}
    </AppShell>
  );
}
