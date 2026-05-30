import { theme } from "antd";

export const srTheme = {
  algorithm: theme.darkAlgorithm,
  token: {
    colorBgBase:        "#10141A",
    colorBgContainer:   "#161B22",
    colorBgElevated:    "#1C2330",
    colorBorder:        "#2A3344",
    colorBorderSecondary: "#1F2937",
    colorPrimary:       "#C0A96A",
    colorPrimaryHover:  "#D4BF8A",
    colorText:          "#E4E7EC",
    colorTextSecondary: "#9AA3B0",
    colorTextTertiary:  "#6B7280",
    colorError:         "#E15B64",
    colorWarning:       "#F0B44A",
    colorSuccess:       "#45A58B",
    colorInfo:          "#4A8BD8",
    fontFamily:         "var(--font-inter)",
    fontSize:           13,
    borderRadius:       6,
    borderRadiusLG:     8,
  },
  components: {
    Layout: {
      headerBg:   "#0B0F14",
      siderBg:    "#0D1117",
      bodyBg:     "#10141A",
    },
    Menu: {
      darkItemBg:          "#0D1117",
      darkItemSelectedBg:  "#1C2330",
      darkItemColor:       "#9AA3B0",
      darkItemSelectedColor: "#C0A96A",
      darkItemHoverColor:  "#E4E7EC",
    },
    Card: {
      colorBgContainer: "#161B22",
    },
    Table: {
      colorBgContainer:  "#161B22",
      headerBg:          "#1C2330",
      rowHoverBg:        "#1C2330",
    },
    Badge: {
      colorBgContainer: "#161B22",
    },
  },
};
