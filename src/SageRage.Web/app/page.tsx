"use client";

import dynamic from "next/dynamic";

const DashboardPage = dynamic(() => import("./DashboardPage").then(mod => ({ default: mod.DashboardPage })), {
  ssr: false,
  loading: () => <div style={{ display: "flex", justifyContent: "center", alignItems: "center", minHeight: "100vh", background: "#10141A" }}>Loading...</div>,
});

export default DashboardPage;
