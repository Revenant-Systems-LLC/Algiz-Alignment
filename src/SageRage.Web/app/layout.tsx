import type { Metadata } from "next";
import { Inter, IBM_Plex_Mono } from "next/font/google";
import { AntdRegistry } from "@ant-design/nextjs-registry";
import ClientThemeProvider from "./client-theme-provider";
import "./globals.css";

const inter = Inter({ subsets: ["latin"], variable: "--font-inter" });
const ibmMono = IBM_Plex_Mono({
  subsets: ["latin"],
  weight: ["400", "500"],
  variable: "--font-mono",
});

export const metadata: Metadata = {
  title: "Algiz — AI Defensibility Console",
  description: "Enterprise AI governance and audit trail",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className={`${inter.variable} ${ibmMono.variable}`}>
      <body>
        <AntdRegistry>
          <ClientThemeProvider>{children}</ClientThemeProvider>
        </AntdRegistry>
      </body>
    </html>
  );
}
