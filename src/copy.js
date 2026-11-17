export const copy = {
  zh: {
    meta: {
      title: "{name}｜都市：天际线 II Mod",
      description: "VehicleCapUnlock 的说明：功能、设置、验证状态和源码构建。"
    },
    brand: { name: "MOD NOTES", home: "回到页面顶部" },
    nav: { label: "页面导航", features: "改动说明", settings: "设置", validation: "验证", install: "安装" },
    language: { label: "切换语言", zh: "中文", en: "EN" },
    actions: {
      viewSource: "查看源码",
      subscribe: "在 Paradox Mods 订阅",
      repository: "GitHub 仓库",
      buildGuide: "构建方法",
      validationGuide: "验证记录",
      safetyGuide: "安全与卸载",
      external: "在新标签页打开"
    },
    status: {
      preview: "预览版",
      beta: "测试版",
      stable: "稳定版",
      previewNote: "游戏内验收未完成",
      betaNote: "游戏内验收进行中",
      stableNote: "验证项全部完成"
    },
    hero: {
      by: "作者",
      illustration: "示意图",
      illustrationAlt: "配车数量滑块范围示意图。上方是原版上限，下方是五倍扩展示例。这是示意图，不是游戏截图。",
      original: "原版上限",
      originalValue: "1× 范围",
      extended: "扩展上限",
      example: "倍率示例",
      extendedValue: "5× 范围",
      min: "线路可调范围",
      mediaNote: "这张图只画范围变化，不代表实际出车数量。"
    },
    specs: {
      title: "信息",
      status: "状态",
      version: "版本",
      game: "游戏版本",
      type: "类型",
      platform: "平台",
      author: "作者",
      updated: "更新",
      license: "许可"
    },
    sections: {
      intro: "简介",
      features: "改动说明",
      settings: "设置",
      validation: "验证状态",
      install: "安装",
      limits: "已知限制",
      faq: "常见问题",
      developer: "开发者信息"
    },
    intro: "提高线路面板中“配车数量”滑块的上限。公交、有轨电车、地铁、火车、船、轮渡、飞机都走同一套处理；每条线路仍然单独调。",
    features: {
      supportLabel: "交通类型",
      items: [
        { title: "只改上限，不改现有配车", body: "全局设置决定滑块能拉到哪里，不会把所有线路的当前配车数一起乘倍数。" },
        { title: "不改原版策略系统", body: "不使用 Harmony，不替换原版策略系统，也不每帧写线路数据。" },
        { title: "卸载前先还原", body: "准备卸载会关闭功能、还原本 mod 改过的内容。存档往返和卸载还原还没在游戏里验完。" }
      ]
    },
    transport: {
      bus: "公交",
      tram: "有轨电车",
      subway: "地铁",
      train: "火车",
      ship: "船",
      ferry: "轮渡",
      airplane: "飞机"
    },
    settings: {
      default: "默认值",
      range: "范围",
      description: "说明",
      on: "开",
      off: "关",
      booleanRange: "开 / 关",
      multiplierRange: "{min}–{max} 倍",
      items: {
        unlockCap: { label: "解锁出车数量上限", description: "扩大共享 policy 的可调范围。配车数仍逐条线路设置。" },
        lineCapMultiple: { label: "线路出车上限倍率", description: "相对第一次读到的基线。1 倍等于不改原版范围。" },
        scaleGarage: { label: "同步放大车库容量", description: "只改能确认是客运的设施。货运、共用和用途不明的设施会跳过。" },
        garageMultiple: { label: "车库容量倍率", description: "相对每个 prefab 第一次读到的容量。不会连续叠乘。" }
      },
      note: "默认值只对新设置生效。已有设置文件会保留原值。"
    },
    validation: {
      done: "已完成",
      pending: "未验证",
      asOf: "截至 {date}",
      note: "构建和独立逻辑测试不能代替 Unity / ECS、游戏界面和存档验收。",
      items: {
        build: "Release 构建：0 警告、0 错误",
        math: "倍率换算回归：167,670 项断言通过",
        safety: "安全回归：3,645 项断言通过",
        slider: "线路面板滑块实际可拖到新上限",
        dispatch: "7 类交通逐项出车与车库容量效果",
        save: "测试副本的存档往返与跨城市加载",
        restore: "准备卸载、移除 mod 后的重载与保存"
      }
    },
    install: {
      sourceOnly: "现在只能从源码构建，还没有可直接安装的发布包。",
      sourceDetail: "构建需要本机游戏程序集。默认构建不会写入游戏目录。",
      paradoxTitle: "在 Paradox Mods 订阅后，游戏会自动安装。",
      paradoxDetail: "订阅前先看验证状态和已知限制。",
      commandLabel: "构建并跑两组回归测试",
      command: ".\\scripts\\build.ps1 -Test",
      copy: "复制命令",
      copied: "已复制",
      copyFailed: "复制失败，请手动选择命令"
    },
    limits: {
      items: [
        "提高可用车辆数不代表立刻有车出发。实际出车还受原版调度、线路条件、车库、道路和模拟状态影响。",
        "不支持绝对值模式。遇到这种策略会保留初始范围、记录错误并进入失败状态，不会套用不准确的替代公式。",
        "倍率相对本系统第一次读到的数值计算。如果其他 mod 更早改过同一份数据，就无法保证那是原版值。",
        "客货共用、用途未知或可重复容量升级的设施会跳过。7 类交通的车库不保证全部扩容。"
      ]
    },
    faq: {
      items: [
        { question: "会把所有线路的当前配车数一起乘倍数吗？", answer: "不会。倍率只扩大每条线路的可调上限，配车数仍在线路面板里逐项设置。" },
        { question: "提高上限后，车辆会立即增加吗？", answer: "不一定。可调上限和实际出车是两回事，最终仍由原版模拟和调度决定。" },
        { question: "可以直接用于重要存档吗？", answer: "当前还没完成游戏内存档验收。先用测试副本，并按仓库清单完成保存、重载和卸载验证。" }
      ]
    },
    developer: {
      summary: "实现边界",
      body: "项目使用 .NET Framework 4.8 和 C# 9。代码里有基线记录、写入归属检查、失败回滚、容量预算、客货用途判断和准备卸载。公式、ECS 生命周期和验证命令在仓库文档里。",
      link: "安全修复说明"
    },
    side: {
      tags: "标签",
      links: "链接",
      changelog: "更新日志",
      emptyChangelog: "还没有 release。目前只有源码和构建文档。",
      tagItems: ["公共交通", "线路配车", "车库容量", "Code Mod"]
    },
    footer: {
      disclaimer: "本站是社区作品，与 Paradox Interactive、Colossal Order 无隶属关系。",
      source: "源代码",
      author: "作者",
      top: "回到顶部"
    },
    a11y: {
      skip: "跳到正文",
      done: "已完成",
      pending: "未验证",
      expand: "展开",
      collapse: "收起"
    },
    error: {
      title: "页面数据没能载入",
      body: "请确认 mod.json 和页面一起部署，然后刷新重试。",
      retry: "重新载入"
    }
  },
  en: {
    meta: {
      title: "{name} | Cities: Skylines II Mod",
      description: "What VehicleCapUnlock changes, its settings, validation status, and source build instructions."
    },
    brand: { name: "MOD NOTES", home: "Back to top" },
    nav: { label: "Page navigation", features: "Changes", settings: "Settings", validation: "Validation", install: "Install" },
    language: { label: "Change language", zh: "中文", en: "EN" },
    actions: {
      viewSource: "View source",
      subscribe: "Subscribe on Paradox Mods",
      repository: "GitHub repository",
      buildGuide: "Build guide",
      validationGuide: "Validation log",
      safetyGuide: "Safety and removal",
      external: "Opens in a new tab"
    },
    status: {
      preview: "Preview",
      beta: "Beta",
      stable: "Stable",
      previewNote: "In-game validation is not complete",
      betaNote: "In-game validation is in progress",
      stableNote: "All validation items are complete"
    },
    hero: {
      by: "By",
      illustration: "Illustration",
      illustrationAlt: "Diagram of the assigned vehicles slider range. Vanilla ceiling above, five-times expansion example below. This is an illustration, not a game screenshot.",
      original: "Vanilla ceiling",
      originalValue: "1× range",
      extended: "Extended ceiling",
      example: "Multiplier example",
      extendedValue: "5× range",
      min: "Per-line adjustable range",
      mediaNote: "This diagram shows the range only, not actual vehicle dispatch."
    },
    specs: {
      title: "Info",
      status: "Status",
      version: "Version",
      game: "Game",
      type: "Type",
      platform: "Platform",
      author: "Author",
      updated: "Last updated",
      license: "License"
    },
    sections: {
      intro: "Overview",
      features: "Changes",
      settings: "Settings",
      validation: "Validation status",
      install: "Installation",
      limits: "Known limitations",
      faq: "FAQ",
      developer: "Developer notes"
    },
    intro: "Raises the ceiling of the vanilla line panel's Assigned Vehicles slider. Bus, tram, subway, train, ship, ferry, and airplane use the same path. Each line is still adjusted separately.",
    features: {
      supportLabel: "Transport types",
      items: [
        { title: "Changes the ceiling, not the current count", body: "Global settings decide how far the slider can go. They do not multiply the current vehicle count of every line." },
        { title: "Keeps the vanilla policy path", body: "No Harmony dependency. The vanilla policy system is not replaced, and line data is not written every frame." },
        { title: "Restores before removal", body: "Prepare for removal turns the features off and restores data owned by this mod. Save round-trips and removal have not passed in-game validation yet." }
      ]
    },
    transport: {
      bus: "Bus",
      tram: "Tram",
      subway: "Subway",
      train: "Train",
      ship: "Ship",
      ferry: "Ferry",
      airplane: "Airplane"
    },
    settings: {
      default: "Default",
      range: "Range",
      description: "Description",
      on: "On",
      off: "Off",
      booleanRange: "On / Off",
      multiplierRange: "{min}–{max}×",
      items: {
        unlockCap: { label: "Unlock vehicle count ceiling", description: "Expands the shared policy range. Vehicle counts are still set per line." },
        lineCapMultiple: { label: "Line vehicle ceiling", description: "Relative to the first captured baseline. 1× leaves the vanilla range unchanged." },
        scaleGarage: { label: "Scale depot capacity", description: "Only facilities confirmed as passenger-only are changed. Cargo, shared, and unknown facilities are skipped." },
        garageMultiple: { label: "Depot capacity multiplier", description: "Relative to each prefab's first captured capacity. It does not compound repeatedly." }
      },
      note: "Defaults apply to new settings. Existing settings files keep their values."
    },
    validation: {
      done: "Done",
      pending: "Not verified",
      asOf: "As of {date}",
      note: "Builds and isolated logic tests do not replace Unity / ECS, game UI, or save validation.",
      items: {
        build: "Release build: 0 warnings, 0 errors",
        math: "Multiplier mapping regression: 167,670 assertions passed",
        safety: "Safety regression: 3,645 assertions passed",
        slider: "Line panel slider can reach the new ceiling",
        dispatch: "Vehicle dispatch and depot capacity across all 7 transport types",
        save: "Save round-trips and cross-city loading using a test copy",
        restore: "Prepare for removal, then reload and save without the mod"
      }
    },
    install: {
      sourceOnly: "Build from source. No installable release is available yet.",
      sourceDetail: "Building requires local game assemblies. The default build does not write to the game directory.",
      paradoxTitle: "Subscribe on Paradox Mods and the game will install it.",
      paradoxDetail: "Check the validation status and known limitations before subscribing.",
      commandLabel: "Build and run both regression suites",
      command: ".\\scripts\\build.ps1 -Test",
      copy: "Copy command",
      copied: "Copied",
      copyFailed: "Copy failed. Select the command manually."
    },
    limits: {
      items: [
        "A higher vehicle allowance does not mean vehicles will dispatch immediately. Vanilla scheduling, line conditions, depots, roads, and simulation state still apply.",
        "Absolute mode is not supported. The initial range is preserved, an error is logged, and the system enters a failed state instead of using an inaccurate substitute formula.",
        "Multipliers are relative to values first observed by this system. If another mod changed the same data earlier, that baseline cannot be guaranteed to be vanilla.",
        "Mixed passenger/cargo, unknown, and repeatable-capacity-upgrade facilities are skipped. Depot scaling is not guaranteed for all 7 transport types."
      ]
    },
    faq: {
      items: [
        { question: "Does it multiply the current vehicle count of every line?", answer: "No. The multiplier only expands each line's adjustable ceiling. Vehicle counts are still set in the line panel." },
        { question: "Will more vehicles dispatch immediately after raising the ceiling?", answer: "Not necessarily. The adjustable ceiling and actual dispatch are separate. The vanilla simulation and scheduler decide the result." },
        { question: "Can I use it with an important save right now?", answer: "In-game save validation is not complete. Use a test copy first and follow the repository checklist for saving, reloading, and removal." }
      ]
    },
    developer: {
      summary: "Implementation boundaries",
      body: "The project targets .NET Framework 4.8 and C# 9. The code includes baseline capture, write-ownership checks, failure rollback, capacity budgets, passenger/cargo classification, and prepare-for-removal. Formulas, ECS lifecycle notes, and validation commands are in the repository docs.",
      link: "Safety fix notes"
    },
    side: {
      tags: "Tags",
      links: "Links",
      changelog: "Changelog",
      emptyChangelog: "No release yet. Only source code and build documentation are available.",
      tagItems: ["Public transport", "Line vehicles", "Depot capacity", "Code Mod"]
    },
    footer: {
      disclaimer: "Unofficial community project. Not affiliated with or endorsed by Paradox Interactive or Colossal Order.",
      source: "Source code",
      author: "Author",
      top: "Back to top"
    },
    a11y: {
      skip: "Skip to content",
      done: "Completed",
      pending: "Not verified",
      expand: "Expand",
      collapse: "Collapse"
    },
    error: {
      title: "Page data could not be loaded",
      body: "Make sure mod.json is deployed with the page, then refresh and try again.",
      retry: "Reload"
    }
  }
};
