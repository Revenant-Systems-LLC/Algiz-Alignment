"use client";

import type { ProfileSnapshot } from "@/lib/types";

const SIZE = 88;
const CENTER = SIZE / 2;
const RADIUS = 34;

function polar(angleDeg: number, value: number) {
  const angle = ((angleDeg - 90) * Math.PI) / 180;
  const r = value * RADIUS;
  return { x: CENTER + r * Math.cos(angle), y: CENTER + r * Math.sin(angle) };
}

export default function VamRadar({ snapshot }: { snapshot: ProfileSnapshot }) {
  const axes = [
    { label: "MAL", value: snapshot.malice, color: "#E15B64" },
    { label: "ACT", value: snapshot.activation, color: "#C0A96A" },
    { label: "VAL", value: (snapshot.valence + 1) / 2, color: "#4A8BD8" },
    { label: "COH", value: snapshot.coherence, color: "#45A58B" },
    { label: "QC", value: snapshot.qcScore, color: "#9B7FD4" },
    { label: "DRF", value: snapshot.drift, color: "#F0B44A" },
  ];

  const points = axes.map((a, i) => polar((360 / axes.length) * i, a.value));
  const polygon = points.map(p => `${p.x},${p.y}`).join(" ");

  return (
    <svg width={SIZE} height={SIZE} viewBox={`0 0 ${SIZE} ${SIZE}`} style={{ flexShrink: 0 }}>
      {[0.33, 0.66, 1].map(ring => (
        <circle
          key={ring}
          cx={CENTER}
          cy={CENTER}
          r={RADIUS * ring}
          fill="none"
          stroke="#2A3344"
          strokeWidth={1}
        />
      ))}
      {axes.map((a, i) => {
        const end = polar((360 / axes.length) * i, 1);
        return (
          <line
            key={a.label}
            x1={CENTER}
            y1={CENTER}
            x2={end.x}
            y2={end.y}
            stroke="#2A3344"
            strokeWidth={1}
          />
        );
      })}
      <polygon
        points={polygon}
        fill="rgba(192, 169, 106, 0.18)"
        stroke="#C0A96A"
        strokeWidth={1.5}
      />
      {axes.map((a, i) => {
        const pos = polar((360 / axes.length) * i, 1.18);
        return (
          <text
            key={a.label}
            x={pos.x}
            y={pos.y}
            textAnchor="middle"
            dominantBaseline="middle"
            fill="#6B7280"
            fontSize={7}
            fontFamily="var(--font-mono)"
          >
            {a.label}
          </text>
        );
      })}
    </svg>
  );
}
