# 体积云渲染质量提升总方案

## 1. 文档目的

基于 `volumetric-cloud-quality-investigation.md` 的问题结论，以及当前 `Assets/VolumetricClouds` / `Assets/Atmosphere` 的实际工程实现，整理一份新的体积云画质提升总路线。

这份文档的目标不是重复规划已经落地的基础设施，而是回答下面三个问题：

- 当前调查里指出的问题，哪些在代码里已经有基础能力，哪些仍然是硬缺口。
- 接下来应该按什么顺序推进，才能让“云像烟雾”的问题真正下降，而不是继续堆外围功能。
- 每个阶段应该改哪些文件、如何验收、如何追踪进度。

## 2. 参考输入

- `Assets/Docs/volumetric-cloud-quality-investigation.md`
- `Assets/Docs/plan-volumetric-cloud-jitter-temporal-implementation.md`
- `Assets/Docs/plan-volumetric-cloud-weather-type-quality.md`
- `Assets/Atmosphere/Rendering/AtmosphereRendererFeature.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudController.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudResources.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudWeatherResources.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudTemporalState.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudProfile.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudParameters.cs`
- `Assets/VolumetricClouds/Runtime/WeatherPreset.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudRenderPass.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudTemporalAccumulationPass.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudCompositePass.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricWeatherFieldUpdatePass.cs`
- `Assets/VolumetricClouds/Shaders/VolumetricCloudCommon.hlsl`
- `Assets/VolumetricClouds/Shaders/VolumetricCloudComposite.shader`
- `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudRaymarch.compute`
- `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudTemporalAccumulation.compute`
- `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudWeatherFieldUpdate.compute`
- `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudProfile_Default.asset`

## 3. 状态定义

为便于与现有计划文档保持一致，本文统一使用以下状态：

| 状态 | 含义 |
| --- | --- |
| `未开始` | 尚未进入实施 |
| `进行中` | 已开始实现，但还未达到退出条件 |
| `阻塞` | 受依赖或问题影响暂时无法继续 |
| `待验证` | 代码或资源已经完成，等待运行验证 |
| `已完成` | 满足退出条件并完成验证 |

建议每次更新时同时记录：

- 日期
- 负责人
- 当前状态
- 本次完成
- 验证结果
- 下一步

## 4. 对照结论

## 4.1 已经具备的基础设施

对照当前代码，下面这些能力已经不是“待设计”，而是“已有基础设施，待继续利用”：

- `AtmosphereRendererFeature` 已串联 `VolumetricWeatherFieldUpdatePass -> VolumetricCloudRenderPass -> VolumetricCloudTemporalAccumulationPass -> VolumetricCloudCompositePass`。
- `VolumetricCloudResources` 已具备 `current trace / stabilized trace / history read / history write / history weight` 资源骨架。
- `VolumetricCloudTemporalState` 已具备相机历史记录和 reset reason 判断。
- `VolumetricCloudController` 已具备 jitter 序列推进、weather preset 切换、debug overlay 和 temporal 生命周期接线。
- `WeatherPreset`、`VolumetricCloudWeatherResources` 和 `VolumetricCloudWeatherFieldUpdate.compute` 已经形成最小可用的 runtime weather field 闭环。
- `VolumetricCloudCommon.hlsl` 已具备 `SampleRuntimeWeatherField()`、`SampleCloudTypeProfile()` 和 `ComputeCloudDensityFromWeatherField()` 的首版实现。

结论：

- 后续路线不应再把 `weather field`、`jitter`、`temporal accumulation` 当作从零开始的任务。
- 这些能力应被视为“现有底座”，新的路线要围绕“默认链路收口、密度模型升级、光照升级、重建升级”来展开。

## 4.2 调查结论与当前代码对照表

| 调查结论 | 当前代码对照 | 判断 | 对路线的影响 |
| --- | --- | --- | --- |
| 默认场景绕开新质量链路 | `VolumetricCloudProfile.cs` 的代码默认值已启用 `useRuntimeWeatherField` 和 `enableTemporalAccumulation`，但 `VolumetricCloudProfile_Default.asset` 仍然是 `useRuntimeWeatherField: 0`、`enableTemporalAccumulation: 0`，且 `cloudHeightDensityLut` 为空 | `成立` | 必须先做默认资产和样例场景收口，否则后续质量改造无法在默认流程里稳定体现 |
| 核心密度模型仍然过于原始 | 当前已经不是最初的纯 `ComputeCloudDensity()` 版本，但 `VolumetricCloudRaymarch.compute` 仍主要依赖单个 `base noise + detail noise + coverage remap + type profile`，还没有真正的 macro-to-micro 分层、domain warp 或体积骨架塑形 | `部分改善，根因仍在` | 下一阶段优先级仍应放在密度内核升级，而不是继续堆 preset 微调 |
| 天气 preset 系统只是在驱动简单 2D 标量场 | `VolumetricCloudWeatherFieldUpdate.compute` 已具备 advection、target relax、growth / dissipation，但本质上仍是一张 `ARGBHalf` 2D 天气场，没有锋面、云带、对流单体等更强结构组织 | `成立` | 天气场要保留，但应在密度升级之后继续强化“组织结构表达力” |
| 光照模型过于简化 | 当前 raymarch 仍以单次 phase、基础 ambient、云内 shadow march 和 Beer-Lambert 累积为主，没有 powder term、云多次散射近似、边缘银边和厚云核心能量塑形 | `成立` | 光照升级应作为密度之后的主阶段，而不是靠调参数补外观 |
| 低分辨率 trace 和简单上采样会继续抹平细节 | 当前默认 trace 仍是 `960 x 540`，资源层使用 `Bilinear`，`VolumetricCloudComposite.shader` 仍为直接采样 + 简单合成，没有 depth-aware / edge-aware resolve | `成立` | 重建与上采样要放在密度和光照之后单独治理 |

## 4.3 路线优先级结论

结合调查报告与现状代码，建议的真实优先级应为：

1. 先收口默认资源和样例链路，确保当前最佳链路真的在默认场景里运行。
2. 再升级云密度内核，解决“云像阈值化噪声和烟雾”的根问题。
3. 然后升级天气场的组织结构，让天气系统真正有内容可驱动。
4. 再升级光照模型，让新的体积结构被正确照亮。
5. 最后升级重建与上采样，避免前面生成的细节被合成链抹平。

## 5. 总体里程碑视图

| 里程碑 | 名称 | 目标 | 当前基础 | 状态 |
| --- | --- | --- | --- | --- |
| R0 | 默认链路收口 | 让默认资产、默认场景、现有 temporal / weather 链真正跑在一起 | 代码骨架已存在，但默认资产仍未启用关键质量开关 | `未开始` |
| R1 | 密度内核升级 | 把当前“coverage remap 噪声块”升级为真正分层的体积云密度模型 | 已有 weather field、type profile、detail erosion 接线 | `未开始` |
| R2 | 天气场结构升级 | 让天气场从“简单 2D 标量场”升级为“可组织云带和区域天气”的驱动层 | 已有 preset、weather RT、update pass | `未开始` |
| R3 | 光照模型升级 | 增强蓬松感、厚云核心、银边与整体体积读感 | 已有单次散射、云内 shadow、天空 ambient 接线 | `未开始` |
| R4 | 重建与合成升级 | 保住半分辨率 trace 的细节和边界 | 已有 trace / stabilized / composite 主链 | `未开始` |
| R5 | 资源与验收收口 | 固化默认资源、回归机位、性能预算和文档状态 | 已有 debug overlay 与样例场景 | `未开始` |

## 6. 分步骤实施方案

## Step 0. 默认链路收口

### 目标

让当前已经实现的 `weather field + jitter + temporal accumulation + cloud type profile` 真实运行在默认资产和默认场景里，建立可重复观察的基线。

### 当前问题

- `VolumetricCloudProfile_Default.asset` 仍未启用 `runtime weather field`。
- `VolumetricCloudProfile_Default.asset` 仍未启用 `temporal accumulation`。
- `cloudHeightDensityLut` 仍未绑定默认资源。
- 现有 `jitter / temporal / weather` 文档里仍有多项状态停留在 `待验证`。

### 实施任务

- [ ] 校验 `SampleScene` 中默认 `VolumetricCloudController` 的 profile 与默认 preset 绑定是否符合当前路线
- [ ] 更新 `VolumetricCloudProfile_Default.asset`，启用 `useRuntimeWeatherField`
- [ ] 更新 `VolumetricCloudProfile_Default.asset`，启用 `enableTemporalAccumulation`
- [ ] 绑定 `CloudHeightDensityLut_Default`
- [ ] 完成 `plan-volumetric-cloud-jitter-temporal-implementation.md` 中 Step 2 到 Step 5 的运行验证
- [ ] 确定一组默认观察机位和 overlay 调试模式

### 主要落点

- `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudProfile_Default.asset`
- `Assets/Scenes/SampleScene.unity`
- `Assets/Docs/plan-volumetric-cloud-jitter-temporal-implementation.md`

### 退出条件

- 默认场景不再 silently 退回旧链路
- 进入场景即可观察到 weather field 和 temporal accumulation 的真实行为
- 当前基础设施的验证状态从 `待验证` 收口到明确结果

### 当前进度记录

| 项 | 内容 |
| --- | --- |
| 状态 | `未开始` |
| 备注 | 这是整个路线的入口阶段，不完成该阶段，后续所有画质对比都不可靠 |

## Step 1. 云密度内核升级

### 目标

把当前“单个 base noise + 单个 detail noise + 简单 coverage remap”的云密度定义，升级为真正的分层体积结构，优先解决“云像随机烟雾”的根问题。

### 设计原则

- 不推翻当前 pass 顺序，重点改造密度函数和参数链。
- 主视线和 shadow march 必须共用同一套密度函数。
- 先把云体结构做好，再做更复杂的天气组织和光照。

### 实施任务

- [ ] 将当前密度拆成 `macro shape -> base volume -> erosion -> height sculpt -> final density`
- [ ] 引入 macro shape 与 micro erosion 的明确职责分层，而不是都挤在 coverage remap 中
- [ ] 新增 domain warp / curl 或等价空间扰动，打破“阈值化噪声块”轮廓
- [ ] 让 `cloud type` 不只影响垂直 profile，也能影响鼓包、侵蚀和顶部轮廓
- [ ] 保证 `MarchCloudShadow()` 与主 raymarch 共用升级后的密度模型
- [ ] 增加 `MacroDensity / TypeProfile / Erosion / FinalDensity` 调试视图

### 主要落点

- `Assets/VolumetricClouds/Shaders/VolumetricCloudCommon.hlsl`
- `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudRaymarch.compute`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudProfile.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudParameters.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudShaderIDs.cs`

### 验证方法

- 正午仰视时，单个云团边缘不再像被阈值切出来的噪声块
- 低太阳角下，云顶、云腰、云底能看出更明确的体积结构差异
- 相机位于云层内部时，颗粒感降低，但不丢失体积层次

### 退出条件

- 不依赖新 lighting 或新 upsample，单看 density 轮廓就能明显优于当前版本
- 晴天、多云、阴天下的云体不再只是“同一种噪声分布的厚薄变化”

### 当前进度记录

| 项 | 内容 |
| --- | --- |
| 状态 | `未开始` |
| 备注 | 这是调查报告里的第一优先级，也是后续天气与光照升级的前提 |

## Step 2. 天气场组织结构升级

### 目标

保留当前 runtime weather field 管线，但把它从“简单 2D 标量驱动层”升级为“能够组织区域天气和云带结构的中尺度控制层”。

### 设计原则

- 不让天气场直接替代 3D 云密度，而是作为宏观和中观的驱动输入。
- 继续保持天气场跨帧连续演化，避免破坏 temporal accumulation。
- 不把天气场像素内容纳入每帧 history reset 判定。

### 实施任务

- [ ] 明确天气场各通道的长期语义，避免 `coverage / cloudType / wetness / densityBias` 继续混用
- [ ] 在 update compute 中加入更强的组织结构生成，例如 front mask、cloud band、storm cell 或等价中尺度模式
- [ ] 继续保留 advection、target relax、growth / dissipation，但把它们从“简单 fBm 结果平滑”升级为“结构化天气演化”
- [ ] 补齐 `Sunny / Cloudy / Overcast / Storm` 的默认 preset，并为每个 preset 定义结构差异，而不只是目标标量差异
- [ ] 增加天气场调试视图与 preset transition 观测指标

### 主要落点

- `Assets/VolumetricClouds/Runtime/WeatherPreset.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudWeatherContext.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudWeatherResources.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricWeatherFieldUpdatePass.cs`
- `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudWeatherFieldUpdate.compute`
- `Assets/VolumetricClouds/Resources/VolumetricClouds/WeatherPreset_*.asset`

### 验证方法

- 同一时刻天空中能同时出现明显不同的天气区域，而不是整片天空统一变厚或变薄
- `Sunny -> Cloudy -> Overcast -> Storm` 切换时，变化像天气推进，而不是纯参数硬切
- 天气变化期间 temporal accumulation 不会频繁 reset

### 退出条件

- 天气场已经能表达“哪里在长云、哪里在散云、哪里在积雨”，而不是只表达“这一片平均更厚”
- 新的天气组织结构能被 Step 1 的新密度内核真正读出来

### 当前进度记录

| 项 | 内容 |
| --- | --- |
| 状态 | `未开始` |
| 备注 | 当前基础 weather field 已经能用，但还不足以承担高质量天气组织表达 |

## Step 3. 云光照模型升级

### 目标

在新的密度结构和天气组织基础上，升级云光照，解决“像被照亮的体积雾，而不是有厚度的云”的问题。

### 设计原则

- 不让 lighting 充当“修密度问题”的补丁。
- 优先补高收益项：powder term、多次散射近似、边缘能量塑形、云底厚度读感。
- 继续复用当前 `Atmosphere` 的太阳方向、亮度和 LUT 资源，避免另起一套光照语义。

### 实施任务

- [ ] 为主视线散射加入 powder term 或等价的厚云增亮近似
- [ ] 增加 cheap multi-scattering / energy compensation
- [ ] 改善 ambient 近似，避免只沿视线方向采样天空颜色
- [ ] 增加厚云核心、云底暗化、云边银边的可控塑形项
- [ ] 增加 `DirectLighting / Ambient / Powder / Shadow` 调试视图

### 主要落点

- `Assets/VolumetricClouds/Shaders/VolumetricCloudCommon.hlsl`
- `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudRaymarch.compute`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudProfile.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudParameters.cs`

### 验证方法

- 正午时厚云核心和薄云边缘的光读感明显区分
- 低太阳角时出现更可信的亮边和体积层次
- 不增加明显的曝光漂移或整屏过亮问题

### 退出条件

- 即使在静止天气下，云体也能通过光照呈现更明显的蓬松感和厚重感
- 不再主要依赖“加大 scattering”来伪造体积感

### 当前进度记录

| 项 | 内容 |
| --- | --- |
| 状态 | `未开始` |
| 备注 | 这是调查报告里的第二优先级 |

## Step 4. 重建与合成升级

### 目标

在密度和光照都提升之后，解决低分辨率 trace 被双线性采样和简单 composite 抹平的问题。

### 设计原则

- 不在密度和光照尚未稳定前过早优化 upsample。
- 继续保留现有 `current / stabilized / composite` 主链职责。
- 将“细节保真”和“边界稳定”作为第一优先级，而不是盲目提高 trace 分辨率。

### 实施任务

- [ ] 为体积云 trace 增加 depth-aware 或 edge-aware upsample
- [ ] 改善云边缘与地形、建筑交界处的重建质量
- [ ] 评估 `traceTexture / stabilizedTexture / historyTexture` 的过滤策略是否仍应统一为 `Bilinear`
- [ ] 评估是否需要区分不同质量档位的 trace 分辨率与 resolve 策略

### 主要落点

- `Assets/VolumetricClouds/Shaders/VolumetricCloudComposite.shader`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudCompositePass.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudResources.cs`

### 验证方法

- 半分辨率 trace 下，云边缘不再明显发糊
- 地形交界和天空轮廓过渡更干净
- 相机缓慢移动时不会因为新 resolve 方案引入新的闪烁或断裂

### 退出条件

- 当前主要细节损失不再来自合成链
- 不必单纯靠提升 trace 分辨率来弥补合成质量

### 当前进度记录

| 项 | 内容 |
| --- | --- |
| 状态 | `未开始` |
| 备注 | 这是调查报告里的第三优先级 |

## Step 5. 默认资源、回归与性能收口

### 目标

把前四个阶段的结果固化为可交付的默认资源、回归方法和性能基线，避免路线完成后仍停留在“只有开发者知道怎么开”的状态。

### 实施任务

- [ ] 固化默认 `VolumetricCloudProfile` 参数
- [ ] 固化默认 `WeatherPreset` 资源和 `CloudHeightDensityLut`
- [ ] 确定 `SampleScene` 的推荐观察机位、时间段和天气切换脚本
- [ ] 完成 `Build + PlayMode + 视觉回归 + 性能记录`
- [ ] 更新所有相关文档的状态和风险表

### 推荐回归矩阵

- [ ] 正午地面仰视
- [ ] 低太阳角斜视
- [ ] 相机位于云层内部
- [ ] 相机位于云层上方俯视
- [ ] 晴天到多云切换
- [ ] 多云到阴天切换
- [ ] 阴天到风暴切换
- [ ] 慢速平移
- [ ] 慢速旋转
- [ ] 快速甩头后观察 temporal reset

### 主要落点

- `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudProfile_Default.asset`
- `Assets/VolumetricClouds/Resources/VolumetricClouds/WeatherPreset_*.asset`
- `Assets/VolumetricClouds/Resources/VolumetricClouds/CloudHeightDensityLut_Default.png`
- `Assets/Scenes/SampleScene.unity`
- `Assets/Docs/*.md`

### 退出条件

- 默认场景即可稳定展示本路线成果
- 性能预算、风险项和文档状态都有明确记录
- 后续再做 cloud shadow、降雨耦合或天气时间线时，有清晰基线可继承

### 当前进度记录

| 项 | 内容 |
| --- | --- |
| 状态 | `未开始` |
| 备注 | 这是整个路线的验收阶段 |

## 7. 设计到实现追踪矩阵

| 质量问题 | 主负责阶段 | 主要代码落点 |
| --- | --- | --- |
| 默认场景没跑到目标链路 | `Step 0` | `VolumetricCloudProfile_Default.asset`、`SampleScene.unity` |
| 云体像随机烟雾 | `Step 1` | `VolumetricCloudCommon.hlsl`、`VolumetricCloudRaymarch.compute` |
| 天气变化像参数切换，不像天气系统 | `Step 2` | `VolumetricCloudWeatherFieldUpdate.compute`、`WeatherPreset.cs` |
| 云像体积雾，不像有厚度的云 | `Step 3` | `VolumetricCloudRaymarch.compute` |
| 细节被半分辨率和 bilinear 抹平 | `Step 4` | `VolumetricCloudComposite.shader`、`VolumetricCloudResources.cs` |
| 结果无法稳定复现和对比 | `Step 5` | `SampleScene.unity`、默认资源、文档 |

## 8. 风险登记表

| 风险编号 | 问题 | 影响 | 应对策略 | 状态 |
| --- | --- | --- | --- | --- |
| Q-R1 | 默认资源长期落后于代码能力 | 调试和评审都建立在错误基线上 | 先做 Step 0，再进入大规模画质改造 | `开放` |
| Q-R2 | 密度升级过度复杂，导致性能快速失控 | 画质上去了但不可用 | Step 1 每次只引入一类结构变化，并同步记录 GPU 成本 | `开放` |
| Q-R3 | 天气场内容变化过快，破坏 temporal accumulation | 出现闪烁或持续 reset | 保持连续演化，不把天气场像素内容纳入 reset hash | `开放` |
| Q-R4 | 过早做 lighting 或 upsample，掩盖密度问题 | 方向看起来忙，但核心外观不提升 | 严格按 Step 0 -> Step 1 -> Step 2 -> Step 3 -> Step 4 顺序推进 | `开放` |
| Q-R5 | 新增多个质量参数后默认 preset 失控 | 项目里能调，但没人知道推荐值 | Step 5 固化默认 profile、preset 和 LUT，并建立回归截图位 | `开放` |

## 9. 当前推荐执行顺序

建议严格按以下顺序推进：

1. `Step 0` 默认链路收口
2. `Step 1` 云密度内核升级
3. `Step 2` 天气场组织结构升级
4. `Step 3` 云光照模型升级
5. `Step 4` 重建与合成升级
6. `Step 5` 默认资源、回归与性能收口

该顺序的原因是：

- 先确保我们看到的是“当前真实链路”。
- 再解决最核心的密度结构问题。
- 然后让天气系统驱动更强的结构。
- 再用更好的光照去表现这些结构。
- 最后才处理分辨率和重建，把已有细节保住。

## 10. 实施记录模板

建议后续每次推进时按下面模板追加更新：

```text
日期：
负责人：
阶段：
状态：

本次完成：
- 

验证结果：
- 

问题与风险：
- 

下一步：
- 
```

## 11. 当前结论摘要

截至 2026-04-21，可得出以下结论：

- 当前项目并不是“完全没有 weather field / temporal accumulation”，而是这些基础设施已经接上了，但默认资产还没有把它们真正启用到默认场景。
- 调查报告指出的主要瓶颈仍然成立，只是要把“已存在的基础设施”和“仍未解决的质量瓶颈”区分开来。
- 新路线不应继续把重点放在单纯调 preset 或继续堆外围开关，而应优先收口默认链路，并把资源投入到密度、天气组织、光照和重建四个核心层面。
