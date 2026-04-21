# 体积云形状基线记录

## 1. 目的

本文档用于收口 `P1-M0` 的默认基线，固定后续 `P1-M1+` 的观察条件，避免在进入云形状质量迭代后反复更换场景、profile、preset 或机位，导致对比失真。

本基线只服务于“云形状质量”阶段，因此有两个明确约束：

- 不默认开启 `runtime weather field`
- 不默认开启 `temporal accumulation`

## 2. 固定环境

- 日期：`2026-04-22`
- 项目：`Landscape`
- 场景：[SampleScene.unity](C:\workspace\projects_with_ai_control\Landscape\Assets\Scenes\SampleScene.unity)
- 体积云对象：`Atmosphere System / VolumetricCloudController`
- 默认 profile：[VolumetricCloudProfile_Default.asset](C:\workspace\projects_with_ai_control\Landscape\Assets\VolumetricClouds\Resources\VolumetricClouds\VolumetricCloudProfile_Default.asset)
- Overlay：关闭
- 不提交截图二进制，截图只作为本地辅助；正式基线证据以本文档为准

## 3. 默认 Profile 固定值

以下条目是本阶段必须固定的默认设置：

- `enableClouds = 1`
- `useRuntimeWeatherField = 0`
- `enableTemporalAccumulation = 0`
- `cloudHeightDensityLut = CloudHeightDensityLut_Default.png`
- `defaultWeatherPreset = WeatherPreset_Cloudy.asset`
- `traceWidth = 960`
- `traceHeight = 540`
- `baseShapeNoise` 保持现有引用
- `detailShapeNoise` 保持现有引用

说明：

- 本次 `P1-M0` 只补齐默认高度 LUT，不改变 weather / temporal 开关。
- 即使 `useRuntimeWeatherField = 0`，`Sunny / Cloudy / Overcast` 这组三类 preset 仍然有观察价值，因为 `VolumetricCloudController` 在 fallback weather context 下仍会使用当前 `WeatherState` 的 coverage / cloudType / wetness / densityBias / detailErosionStrength。

## 4. 固定观察 Preset

后续形状阶段统一使用以下三组 preset 做对照：

- [WeatherPreset_Sunny.asset](C:\workspace\projects_with_ai_control\Landscape\Assets\VolumetricClouds\Resources\VolumetricClouds\WeatherPreset_Sunny.asset)
- [WeatherPreset_Cloudy.asset](C:\workspace\projects_with_ai_control\Landscape\Assets\VolumetricClouds\Resources\VolumetricClouds\WeatherPreset_Cloudy.asset)
- [WeatherPreset_Overcast.asset](C:\workspace\projects_with_ai_control\Landscape\Assets\VolumetricClouds\Resources\VolumetricClouds\WeatherPreset_Overcast.asset)

`WeatherPreset_Storm.asset` 不纳入本阶段默认基线集合，避免在还没稳定形状前引入过厚、过暗云型对判断的干扰。

## 5. 固定观察机位

## 5.1 机位 A：场景 authored 机位

来源：

- `SampleScene` 中 `Main Camera` 当前 authored transform

固定值：

- 位置：`(-3.7228222, 1.0251226, 6.1080813)`
- 旋转四元数：`(0.082631126, -0.8149157, -0.11996703, -0.5609745)`
- `FOV = 60`
- `Near = 0.3`
- `Far = 1000`

使用规则：

- 后续形状阶段默认把机位 A 当作主对比机位
- 除非明确更新本文档，否则不要修改 `SampleScene` 的 authored 相机作为新的默认起点

## 5.2 机位 B：机位 A 上仰 +20°

规则：

- 从机位 A 出发
- 保持相机位置不变
- 保持 yaw 不变
- 仅围绕相机本地俯仰方向上仰 `+20°`
- 不修改 roll

用途：

- 观察中高空云团体量
- 观察云顶 bulge 和中段连接关系

## 5.3 机位 C：机位 A 上仰 +45°

规则：

- 从机位 A 出发
- 保持相机位置不变
- 保持 yaw 不变
- 仅围绕相机本地俯仰方向上仰 `+45°`
- 不修改 roll

用途：

- 观察仰视状态下的云底、云腰和侵蚀轮廓
- 观察“烟雾块感”是否明显

## 6. 观察记录模板

后续每次 `P1-M1+` 迭代都应按下面的 3 x 3 基线矩阵补观察结论。

记录要求：

- 每条至少记录：
  - 云团体量
  - 云边形态
  - 云底/云顶读感
  - 是否存在明显“烟雾块”问题

| Preset | 机位 | 观察结论 | 当前状态 |
| --- | --- | --- | --- |
| `Sunny` | A | 待补充 | `待运行验证` |
| `Sunny` | B | 待补充 | `待运行验证` |
| `Sunny` | C | 待补充 | `待运行验证` |
| `Cloudy` | A | 待补充 | `待运行验证` |
| `Cloudy` | B | 待补充 | `待运行验证` |
| `Cloudy` | C | 待补充 | `待运行验证` |
| `Overcast` | A | 待补充 | `待运行验证` |
| `Overcast` | B | 待补充 | `待运行验证` |
| `Overcast` | C | 待补充 | `待运行验证` |

## 7. 本次已完成的静态校验

本次通过仓库静态检查确认了以下事实：

- `SampleScene` 中 `VolumetricCloudController` 显式引用 `VolumetricCloudProfile_Default.asset`
- `Main Camera` authored transform 已固定并写入本文档
- 默认 profile 现在已绑定 `CloudHeightDensityLut_Default.png`
- 默认 profile 仍保持 `useRuntimeWeatherField = 0`
- 默认 profile 仍保持 `enableTemporalAccumulation = 0`

## 8. 本次未完成的运行校验

以下项仍待在可用 Unity Editor 环境中补充：

- Play 模式下确认无资源缺失或引用错误日志
- `Sunny / Cloudy / Overcast` 三组 preset 的真实视觉观察结论
- 三个机位下的云团体量、边缘和烟雾感对照记录

备注：

- 本地 CLI 环境下未定位到可直接调用的 Unity Editor 批处理入口，因此本次未执行 Play 模式验证

## 9. 后续更新规则

- `P1-M1+` 任何形状改动都必须沿用：
  - 同一场景
  - 同一默认 profile
  - 同一 preset 集合
  - 同一三机位方法
- 如果未来必须更改机位 A 或默认 profile，必须先更新本文档，再开始新的对比记录
- 如果未来切换到 `runtime weather field` 或 `temporal accumulation` 基线，应新建独立基线文档，不覆盖本文档
