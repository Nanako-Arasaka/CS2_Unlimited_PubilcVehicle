# VehicleCapUnlock

把 CS2 公共交通每条线路的「配车数量」上限抬高，**调节方式和原版完全一致**——仍然是打开线路面板、拖那条 Assigned Vehicles 滑块，只是能拉得更远。7 种载具（公交 / 有轨 / 地铁 / 火车 / 船 / 轮渡 / 飞机）全覆盖。

## 为什么不需要 Harmony

四个公开实现（TPA / SmartTransportation / TransitTimetables / All Transit + Trucks）里，前两个为了突破上限，各自打包了一份 470 行的 `Game.Policies.ModifiedSystem` 反编译副本。**那是没必要的**。

反编译源码里的换算链是这样的：

```
slider 空间      PolicySliderData { Bounds1 m_Range; float m_Default; float m_Step; int m_Unit }
modifier 空间    RouteModifierData  { RouteModifierType m_Type; ModifierValueMode m_Mode; Bounds1 m_Range }

GetModifierDelta(adjustment):
    a = (adjustment - sliderMin) / (sliderMax - sliderMin)
    L = lerp(modifierMin, modifierMax, a)          ← 纯线性，不 clamp

AddModifierData(InverseRelative):
    L' = 1 / max(0.001, 1 + L) - 1
    最终间隔 = defaultInterval * (1 + L')
    出车倍率 = 1 / (1 + L') = max(0.001, 1 + L)
```

关键点在于 **`GetModifierDelta` 是直线 `lerp`，不带 clamp**。所以只要：

- 锚点 `(sliderMin, modifierMin)` 不动
- 把 `(sliderMax, modifierMax)` 沿**同一条直线**往外推

那么原版区间内任何一个 adjustment 算出来的 `L` 值都**分毫不差**，只有超出原版上限的部分是新开的地盘。这就是「和原版一样，只是能拉得更远」的严格实现。

反推公式（本 mod 用的）：

```
L0      = lerp(modifierMin, modifierMax, (default - sliderMin) / (sliderMax - sliderMin))
Ltarget = k * (1 + L0) - 1                      // InverseRelative
newSliderMax = sliderMin + (Ltarget - modifierMin) / slope
slope   = (modifierMax - modifierMin) / (sliderMax - sliderMin)
```

`Relative` 模式用 `Ltarget = (1 + L0) / k - 1`；`Absolute` 是「给间隔加固定秒数」，无法表达倍率语义，退化为线性外推并在日志里告警。

因此本 mod **零 Harmony、零每帧写入**：城市加载完成后改一次 prefab，之后完全空转。

## 车库容量

滑块拉高了，车库装不下照样不出车。所以同步缩放 `TransportDepotData.m_VehicleCapacity`，基准值在首次运行时抓快照，反复应用不会叠乘。只处理客运车库（Bus / Tram / Subway / Train / Ship / Ferry / Taxi / Airplane），Post / Helicopter / Work / Bicycle / Car 不动。

## 已核实的 API（直接反射 `Game.dll` 元数据，非文档推测）

| 类型 | 完整名 | 字段 |
|---|---|---|
| `PolicySliderData` | `Game.Prefabs` | `Bounds1 m_Range`, `float m_Default`, `float m_Step`, `int m_Unit` |
| `RouteModifierData` | `Game.Prefabs` | `RouteModifierType m_Type`, `ModifierValueMode m_Mode`, `Bounds1 m_Range` |
| `TransportDepotData` | `Game.Prefabs` | `TransportType m_TransportType`, `int m_VehicleCapacity`, … |
| `UITransportConfigurationPrefab` | `Game.Prefabs` | `PolicyPrefab m_VehicleCountPolicy` |
| `TransportLineSystem.CalculateVehicleCount` | `Game.Simulation` | `int (float vehicleInterval, float lineDuration)` |
| `TransportLineSystem.CalculateVehicleInterval` | `Game.Simulation` | `float (float lineDuration, int vehicleCount)` |

`ModifierValueMode` = `Relative | Absolute | InverseRelative`
`TransportType` = `Bus, Train, Taxi, Tram, Ship, Post, Helicopter, Airplane, Subway, Rocket, Work, Ferry, Bicycle, Car, Count, None`

## 构建

### 路线 A：官方工具链（推荐，最终发布用）

游戏内 选项 → Modding → 安装工具链，装好后 `CSII_TOOLPATH` 环境变量会被设置，直接：

```
dotnet build -c Release
```

工具链自带 `DeployWIP`，会自动把 DLL 部署到本地 Mods 目录。

### 路线 B：不装工具链（本机现状，纯 SDK 也能编）

`csproj` 里做了条件分支：检测不到 `CSII_TOOLPATH` 时，改为直接引用
`$(ManagedPath)` 下的游戏程序集（默认 `E:\SteamLibrary\steamapps\common\Cities Skylines II\Cities2_Data\Managed`），
并拉 `Microsoft.NETFramework.ReferenceAssemblies` 包替代本机的 net48 targeting pack。
`AfterBuild` 会把 DLL 复制到 `%LOCALAPPDATA%Low\Colossal Order\Cities Skylines II\Mods\VehicleCapUnlock\`。

前提只有一个：**装 .NET SDK**（本机目前只有一个 runtime，`dotnet --list-sdks` 是空的）。

## 首次运行要看的东西

日志里会打印原版基线，这是唯一需要关注的数字：

```
[基线] slider=50.0..200.0 default=100.0 | modifier=-1.0000..0.0000 mode=InverseRelative
[应用] x5 | slider 50.0..xxx | modifier -1.0000..yyy
```

如果 `mode` 不是 `InverseRelative`，说明这版游戏的策略语义变了，日志会告警，此时倍率不精确。

## 已知风险

1. **滑块 UI 是否跟着变长**：`vehicleCountMax` 由 `VehicleCountSection` 从 `policySliderData.m_Range.max` 算出并推给前端 JS，理论上前端会自动跟随。万一线程/前端里另有硬编码，需要补一个 Harmony patch（后备方案已想好，只是先不引入）。
2. **性能**：出车数上去了，`Traffic`(80095)、`CSL Driving Life : Bus Driver`(163864) 的计算量会同步上升，建议先用 2–3 倍试。
3. **存档**：改的是 prefab，不进存档。但线路的 `Policy.m_Adjustment` 是存档的——如果把 mod 卸掉，开到超过原版上限的线路会退回到 200% 的含义，属于预期行为。
