"use client";

import { useState, useEffect } from "react";
import { ConfigProvider } from "antd";
import { srTheme } from "./theme-config";

export default function ClientThemeProvider({ children }: { children: React.ReactNode }) {
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
  }, []);

  if (!mounted) {
    return <>{children}</>;
  }

  return (
    <ConfigProvider theme={srTheme}>
      {children}
    </ConfigProvider>
  );
}
