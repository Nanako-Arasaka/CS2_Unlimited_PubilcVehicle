# VehicleCapUnlock — 公共交通配车上限解锁

为《都市天际线 II》提高原版线路面板「配车数量 / Assigned Vehicles」滑块的上限。
每条线路仍然独立调节；全局选项只决定可调上限，不会把所有线路的当前配车数一起乘倍数。

## 当前验证状态（2026-11-17）

- **存档安全审计未通过主存档使用门槛。** 已确认缓存校正、还原归属检查和异常恢复等防护缺口，尚未修复；详见[安全审计与整改清单](docs/SAFETY_AUDIT.md)。
- 已对本机游戏程序集完成 Release 编译：0 警告、0 错误。
- 数学回归程序通过 167,670 项断言。
- 尚未在游戏内验收滑块、实际出车、跨城市加载和卸载还原。
- 目标覆盖公交、有轨电车、地铁、火车、船、轮渡、飞机。共享 policy 路径和车库白名单已实现，7 类交通的运行效果仍需逐项实测。
- 不使用 Harmony，不替换原版策略系统，不在每帧持续写线路数据。

## 目录和仓库

本机工程目录：

```text
E:\SteamLibrary\steamapps\common\Cities Skylines II\Mods\VehicleCapUnlock
```

Git origin：

```text
https://github.com/Nanako-Arasaka/CS2_Unlimited_PubilcVehicle.git
```

这是源码/构建目录。部署目标是用户数据中的独立目录：

```text
%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\VehicleCapUnlock
```

原始交接工程仍保留在 E:\cs2-mod-study\VehicleCapUnlock，后续开发以新工程目录为准。
游戏 DLL、SDK、NuGet 缓存、编译产物均不纳入 Git。

## 构建

目标框架 net48，C# 9。本机无需安装完整官方 Unity modding 工具链即可编译。
便携 SDK 8.0.428 放在 .tools/dotnet，不修改系统 PATH。

在工程目录运行：

```powershell
# 首次准备环境：从微软官方下载 SDK，校验 SHA512 后解压。
.\scripts\bootstrap-sdk.ps1

# 仅构建。
.\scripts\build.ps1

# 构建并运行数学回归验证。
.\scripts\build.ps1 -Test

# 正常退出游戏后，明确部署 DLL。
.\scripts\build.ps1 -Deploy
```

构建脚本在 Cities2 进程存在时拒绝 -Deploy；默认构建不部署。
产物是 bin/Release/VehicleCapUnlock.dll。

默认游戏引用路径是安装目录的 Cities2_Data/Managed。其他安装位置可指定：

```powershell
.\scripts\build.ps1 -ManagedPath 'D:\Games\Cities Skylines II\Cities2_Data\Managed'
```

net48 引用程序集由 Microsoft.NETFramework.ReferenceAssemblies 1.0.3 提供，首次还原需要网络。
官方 CSII_TOOLPATH 导入分支保留，但本次只验证了无官方工具链的构建路线；发布流程尚未配置。

## 设置

| 选项 | 默认值 | 含义 |
|---|---|---|
| 解锁出车数量上限 | 开启 | 扩展共享 policy 允许的范围，具体配车仍逐线路设置 |
| 线路出车上限倍率 | 5，范围 1–10 | 相对于捕获基线的最大配车水平，1 倍恢复初始范围 |
| 同步放大车库容量 | 开启 | 放大目标 7 类交通的车库载具容量，不包含出租车 |
| 车库容量倍率 | 5，范围 1–10 | 相对于每个 prefab 首次捕获容量的倍率，不连续叠乘 |

这里的基线是本系统首次应用前观察到的数据；若其他 mod 更早修改同一 prefab，不能把它断言为纯原版值。
本项目尚未完成当前 playset 的兼容性验收。

## 换算依据和修复

本机 Game.dll 中，RouteModifierRefreshData.GetModifierDelta 的真实逻辑是：

```text
a = saturate((adjustment - sliderMin) / (sliderMax - sliderMin))
L = lerp(modifierMin, modifierMax, a)
```

注意：交接材料中「不 clamp」的说法不适用于本机程序集；它确实调用 saturate。
同时扩展输入范围和效果范围仍然成立：新增输入位于扩展后的合法范围内，
原区间内的直线斜率保持不变。浮点运算按数值容差验证，不保证逐比特相等。

保持 sliderMin 和 modifierMin 不动。设 k 为目标倍率、M 为初始 modifierMax：

```text
InverseRelative: target = k * (1 + M) - 1
Relative:        target = (1 + M) / k - 1
newSliderMax = sliderMin
             + (target - modifierMin) * (sliderMax - sliderMin) / (modifierMax - modifierMin)
```

计算以初始最大值为基准，不再误用默认 adjustment。
Relative 的有效扩展方向与 InverseRelative 不同，已分别校验。
Absolute 不能为所有线路表达同一个配车倍率，因此保持初始范围并记录告警，而非静默使用不准确的替代公式。

原版 VehicleCountSection 的上限计算会读取 PolicySliderData.m_Range.max，
设置配车数时又按相同 policy 数据反算 adjustment。本次没有引入 UI 补丁；
前端实际表现仍需在游戏中确认。

### 还原与生命周期

- 基线属于系统实例及其 ECS World，不再是跨 World 的静态数值缓存。
- 城市加载回调只请求重新应用，不清空同一 World 的基线。
- 通过 Entity 和 managed prefab 身份共同判断是否仍是同一对象。
- IMod.OnDispose 同步执行还原，不等待可能永远不会发生的下一帧。
- World 系统销毁时也尝试还原，随后移除实例登记。
- 保留每条线路存储的 adjustment 和启用标记，不覆盖玩家的逐线路设置。
- 当范围变化影响到超过旧/新共同上限的线路时，使用原版换算 API 按当前各项有效 policy
  重新计算该线路的 VehicleInterval 缓存，仅更新这一槽位；不清空其他 modifier 槽位。
- 上述缓存刷新只发生在应用或还原时，不是每帧覆盖。
- 车库容量乘法使用 long 中间值并限制到 int 范围，临时 NativeArray 使用 using 释放。

恢复初始范围后，超过初始上限的存储 adjustment 由原版 saturate 限制效果。
重新启用时可以再次使用保留的设置。存档往返行为尚未实测，不能视为已通过验收。

## 验收

见 docs/VALIDATION.md。数学测试不能替代 Unity/ECS 生命周期与游戏前端测试。
提高可用车数不保证立即出车：实际结果还受线路条件、车库、道路和模拟状态影响。
