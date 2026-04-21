# 体积云第一阶段开发计划：云形状质量提升

## 1. 文档目的

对照 [volumetric-cloud-detailed-design.md](C:\workspace\projects_with_ai_control\Landscape\Assets\Docs\volumetric-cloud-detailed-design.md) 中关于体积云建模的结论，制定当前项目第一阶段开发计划。

这一阶段只聚焦一件事：

- 让云的`形状质量`明显提升，从“阈值化噪声块 / 烟雾块”进化到“具有宏观体量、垂直结构和边缘侵蚀层次的云体”。

本文档的目标不是重复总路线，而是给出一份可以直接实施、验收和追踪状态的阶段计划。

## 2. 阶段定位

### 2.1 本阶段在总路线中的位置

本阶段对应总路线中的“密度内核升级”，是当前体积云质量改造的第一优先级。

在本阶段完成前，不建议进入：

- powder 光照近似
- cone lighting
- 高空 2D 云层
- sparse trace update
- 天气场结构化升级
- edge-aware upsample

原因很简单：

- 如果云的基础体量仍然像随机烟雾，后续所有光照、天气和时域优化都只是在放大错误形状。

### 2.2 本阶段目标

本阶段的最终目标是让当前项目的低空体积云具备下面四个形状特征：

1. 云团有稳定的大轮廓，而不是由单一 coverage remap 直接切出来。
2. 不同云型在垂直方向有明显差异，而不是只改一条高度曲线的厚薄。
3. 云边有受控侵蚀和湍流破碎感，但不会把主体侵蚀成噪声。
4. 即使关闭 temporal 和新光照，单看云体轮廓也能明显优于当前版本。

## 3. 参考输入

- [volumetric-cloud-detailed-design.md](C:\workspace\projects_with_ai_control\Landscape\Assets\Docs\volumetric-cloud-detailed-design.md)
- [plan-volumetric-cloud-quality-roadmap.md](C:\workspace\projects_with_ai_control\Landscape\Assets\Docs\Reserved\plan-volumetric-cloud-quality-roadmap.md)
- `Assets/VolumetricClouds/Shaders/VolumetricCloudCommon.hlsl`
- `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudRaymarch.compute`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudProfile.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudParameters.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudShaderIDs.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudController.cs`
- `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudProfile_Default.asset`
- `Assets/Scenes/SampleScene.unity`

## 4. 当前问题收敛

结合现有代码，当前“云形状质量不够”的主要根因可以收敛为四类：

### 4.1 基础形体仍然过于单薄

当前主链核心仍然是：

```text
baseCoverage * typeProfile * detailErode * densityMultiplier
```

问题在于：

- `baseShape` 仍主要是单次基础噪声阈值化后的结果。
- 基础噪声没有被明确拆成“宏观体量”和“侵蚀细节”两层职责。
- 云团连接、鼓包和边界连贯性不足。

### 4.2 云型对体量的影响还不够深

当前 `CloudHeightDensityLut` 已经存在，但 `cloud type` 主要影响的是高度密度分布。

问题在于：

- `stratus / cumulus / storm` 的差异更多体现在“哪里更密”，而不是“长什么样”。
- 云顶 bulge、云底平整度、云腰收束感还没有真正进入建模链。

### 4.3 高频侵蚀还像统一噪声乘子

当前 `detail noise` 的作用已经接入，但仍偏向“统一细碎化”。

问题在于：

- 高频细节没有明确限制在边缘区。
- 缺少二维 curl 扰动或等价 domain warp。
- 侵蚀在某些机位下会直接把体量打散。

### 4.4 调试视图不足，导致形状问题难定位

当前 overlay 主要覆盖：

- current / history / accumulated
- weather field coverage / type / wetness

但缺少面向形状建模的调试项：

- macro shape
- cloud type profile
- detail erosion
- final density

这会让实现阶段难以判断问题来自哪一层。

## 5. 阶段范围

### 5.1 本阶段必须完成

- 基础噪声职责分层
- 形状采样链重构
- `CloudHeightDensityLut` 驱动能力增强
- 高频侵蚀改造
- curl/domain warp 接入
- 形状调试视图
- 默认 profile 和默认场景下的验证闭环

### 5.2 本阶段明确不做

- powder、银边和多次散射近似
- 天气场结构升级为 front/band/storm cell
- 高空卷云层
- cheap/full dual march
- 稀疏更新
- 深度感知上采样

### 5.3 本阶段可接受的前置收口

虽然这一阶段主题是“形状质量”，但允许包含极少量前置收口工作，只要它们是为了建立可信基线：

- 默认 `VolumetricCloudProfile_Default.asset` 绑定 `CloudHeightDensityLut_Default`
- 确认 `SampleScene` 中正在使用当前体积云主链

这些工作不算阶段主交付，只算准备项。

## 6. 交付物

本阶段结束时应至少产出以下内容：

- 新的云形状采样与密度函数
- 更新后的 `VolumetricCloudProfile` 参数接口
- 更新后的 shader 参数绑定链
- 一个可用的 `curl noise` 采样入口
- 一组新的形状调试模式
- 默认资产的可验证配置
- 一份记录本阶段完成情况的文档更新

## 7. 开发节奏建议

建议按 `4` 个里程碑推进，总体规模建议控制在 `6 ~ 9` 个开发日。

| 里程碑 | 名称 | 目标 | 建议时长 | 状态 |
| --- | --- | --- | --- | --- |
| P1-M0 | 基线收口 | 建立可信默认基线和调试入口 | `0.5 ~ 1` 天 | `未开始` |
| P1-M1 | 宏观体量重构 | 重做基础形体定义，让云团先“长对” | `2 ~ 3` 天 | `未开始` |
| P1-M2 | 云型与垂直廓线增强 | 让不同云型真正影响体量结构 | `1.5 ~ 2` 天 | `未开始` |
| P1-M3 | 侵蚀与空间扰动增强 | 让云边更自然，不破坏主体 | `1.5 ~ 2` 天 | `未开始` |
| P1-M4 | 调试、验收与默认资产收口 | 固化参数、完成回归验证 | `1 ~ 1.5` 天 | `未开始` |

## 8. 分里程碑实施方案

## P1-M0. 基线收口

### 目标

在进入形状改造前，先确保默认场景和默认 profile 能稳定呈现当前链路，并具备最基本的可观测性。

### 实施任务

- [ ] 校验 `SampleScene` 中默认 `VolumetricCloudController` 是否使用 `Resources/VolumetricClouds/VolumetricCloudProfile_Default`
- [ ] 为 `VolumetricCloudProfile_Default.asset` 绑定 `CloudHeightDensityLut_Default.png`
- [ ] 记录当前默认 profile 的基线截图机位：仰视、中景、地平线三个角度
- [ ] 固定一组用于比较的天气 preset：`Sunny / Cloudy / Overcast`

### 主要落点

- `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudProfile_Default.asset`
- `Assets/Scenes/SampleScene.unity`
- 运行时观察：`VolumetricCloudController` overlay

### 退出条件

- 默认场景下可以稳定观察当前云体
- `CloudHeightDensityLut` 不再为空
- 后续每个里程碑都可以和同一机位下的基线做对比

## P1-M1. 宏观体量重构

### 目标

把当前“阈值化基础噪声”升级成更明确的宏观体量定义，让云团首先看起来像云，而不是像被切出来的雾团。

### 设计策略

- 低频层只负责大体量、连通性和鼓包。
- 高频层暂时不要参与主体塑形。
- 先把 `base noise` 的资源语义理顺，再谈细节。

### 实施任务

- [ ] 在 `VolumetricCloudCommon.hlsl` 中引入显式的 `macro shape` 采样函数
- [ ] 将当前 `baseShape` 路径改造成“基础噪声主通道 + 辅助通道融合”的结构
- [ ] 把 `ComputeCloudDensity()` 从“单函数成品输出”重构为中间层函数组合
- [ ] 保留旧路径一段时间用于 A/B 对比，确认新旧差异
- [ ] 保证 `MarchCloudShadow()` 使用相同的宏观密度基础，而不是走旧逻辑

### 建议新增函数

- `SampleMacroBaseShape()`
- `ComputeMacroCoverageThreshold()`
- `ComputeBaseVolumeDensity()`

### 主要落点

- `Assets/VolumetricClouds/Shaders/VolumetricCloudCommon.hlsl`
- `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudRaymarch.compute`

### 验证方法

- 关闭 temporal 后，云团大轮廓仍有明显体量
- 云与云之间的断裂带更自然，不再只有“有 / 无”硬切换
- 看向地平线时，云团有连续起伏，而不是噪声块堆叠

### 退出条件

- 单看 macro shape 结果就能看出 bulge 和连通性改进
- 新体量结构没有明显破坏当前 weather field 与 shadow march 的接线

## P1-M2. 云型与垂直廓线增强

### 目标

让 `cloud type` 从“高度密度修饰项”升级成“真正影响云形状”的驱动项。

### 设计策略

- 保留 `CloudHeightDensityLut` 作为主表达方式。
- `cloud type` 同时影响：
  - 高度密度分布
  - 云底平整度
  - 中段鼓包强度
  - 云顶抬升趋势

### 实施任务

- [ ] 梳理 `SampleCloudTypeProfile()`，明确三类主型目标：`stratus / cumulus / cumulonimbus`
- [ ] 把 `cloudType` 对形体的作用从单纯高度采样，扩展到宏观体量调制
- [ ] 为低、中、高 `cloudType` 设计不同的顶部和腰线塑形
- [ ] 复核 `cloudTypeRemapMin / Max` 是否仍满足新表达范围
- [ ] 若默认 LUT 不够表达，更新 `CloudHeightDensityLut_Default.png`

### 主要落点

- `Assets/VolumetricClouds/Shaders/VolumetricCloudCommon.hlsl`
- `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudRaymarch.compute`
- `Assets/VolumetricClouds/Resources/VolumetricClouds/CloudHeightDensityLut_Default.png`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudProfile.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudParameters.cs`

### 验证方法

- `Sunny / Cloudy / Overcast / Storm` 至少能呈现出不同的云型倾向
- `cloudType` 变化时，不只是变厚变薄，而是云顶和云腰轮廓发生可辨识变化
- 仰视机位和侧视机位都能看出形体差异

### 退出条件

- `cloudType` 已经不再只是“高度系数”
- 默认 LUT 足以支撑至少三类云型的视觉区分

## P1-M3. 侵蚀与空间扰动增强

### 目标

让云边缘更自然、具有卷曲和破碎感，但不把主体体量打碎。

### 设计策略

- 细节侵蚀只在边缘区起主要作用。
- 主体内部优先保持宏观体量稳定。
- 使用 `curl/domain warp` 打破重复感，但扰动幅度必须受控。

### 实施任务

- [ ] 为 `VolumetricCloudProfile` 新增 `curlNoise`、`curlNoiseScaleKm`、`curlNoiseStrengthKm`
- [ ] 为 `VolumetricCloudParameters` 与 `VolumetricCloudShaderIDs` 新增对应参数通路
- [ ] 在 shader 中新增 `SampleCurlDistortion()` 或等价函数
- [ ] 把 `detail noise` 的作用范围限制到边缘侵蚀区
- [ ] 重做 `ComputeDynamicDetailErosion()`，避免一刀切地把体量打碎
- [ ] 在 weather `wetness` 与 `detail erosion` 之间保留联动，但降低过湿时的形变噪点

### 主要落点

- `Assets/VolumetricClouds/Runtime/VolumetricCloudProfile.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudParameters.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudShaderIDs.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudRenderPass.cs`
- `Assets/VolumetricClouds/Shaders/VolumetricCloudCommon.hlsl`
- `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudRaymarch.compute`

### 资源要求

- 首选新增一张 `curl noise` 资源
- 如果暂时没有最终资源，可先接一个临时 `Texture2D` 作为占位，保证参数链和采样逻辑先跑通

### 验证方法

- 云边不再只是一层 uniform 的毛边
- 边缘局部有卷曲和侵蚀，但云体中心仍保持完整
- 相机穿云时，云边不会变成纯颗粒噪点

### 退出条件

- 新侵蚀逻辑已经明显改善边缘自然度
- `curl/domain warp` 没有造成屏幕漂移或强重复纹理感

## P1-M4. 调试、验收与默认资产收口

### 目标

为第一阶段结果建立稳定的调试和验收手段，并固化默认参数。

### 实施任务

- [ ] 扩展 `VolumetricCloudController.DebugOverlayMode`，加入形状相关调试模式
- [ ] 至少补充以下调试输出：
  - `MacroShape`
  - `TypeProfile`
  - `DetailErosion`
  - `FinalDensity`
- [ ] 确定默认 profile 的首组推荐参数
- [ ] 在 `SampleScene` 完成三组固定机位截图回归
- [ ] 用 `Sunny / Cloudy / Overcast / Storm` 做一轮云型回归记录

### 主要落点

- `Assets/VolumetricClouds/Runtime/VolumetricCloudController.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudShaderIDs.cs`
- `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudProfile_Default.asset`
- `Assets/Scenes/SampleScene.unity`

### 验证方法

- overlay 能直接观察形状链路中的各层结果
- 默认参数在样例场景下不需要手工微调也能看到明显优于当前的体量结构

### 退出条件

- 第一阶段所有新增能力都能被观察和复现
- 默认资产已经具备演示和继续迭代的基础

## 9. 文件改动清单

本阶段预计主要涉及以下文件：

- `Assets/VolumetricClouds/Runtime/VolumetricCloudProfile.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudParameters.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudShaderIDs.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudController.cs`
- `Assets/VolumetricClouds/Runtime/VolumetricCloudRenderPass.cs`
- `Assets/VolumetricClouds/Shaders/VolumetricCloudCommon.hlsl`
- `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudRaymarch.compute`
- `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudProfile_Default.asset`
- `Assets/VolumetricClouds/Resources/VolumetricClouds/CloudHeightDensityLut_Default.png`
- `Assets/Scenes/SampleScene.unity`

可选新增资源：

- `Assets/VolumetricClouds/Resources/VolumetricClouds/CloudCurlNoise_*.png`

## 10. 验收标准

第一阶段只有在以下条件同时满足时，才算完成：

- [ ] 关闭新光照增强项后，云形状依然比当前版本明显更自然
- [ ] `Sunny / Cloudy / Overcast / Storm` 四类 preset 至少呈现出三种可辨识形状倾向
- [ ] 仰视、中景、地平线三个机位下，云团体量和边缘层次都有改进
- [ ] overlay 可以观察 `MacroShape / TypeProfile / DetailErosion / FinalDensity`
- [ ] 新形状链没有破坏当前 weather field、temporal accumulation 和 composite 主链

## 11. 风险与对策

### 风险 1：宏观体量和细节侵蚀耦合过深

表现：

- 调细节时主体一起塌掉。

对策：

- 先拆职责，再调参数。
- 宏观体量函数和细节侵蚀函数必须分离。

### 风险 2：LUT 调整后云型差异仍然不明显

表现：

- 只是整体厚度不同，形体差异不明显。

对策：

- 让 `cloud type` 同时参与顶部 bulge、腰线和边缘侵蚀强度，不只采高度密度。

### 风险 3：curl 扰动引入屏幕漂移感

表现：

- 相机移动时云边像贴图在滑。

对策：

- curl 必须在世界空间采样。
- 先用保守强度接入，再逐步增大。

### 风险 4：调试视图不足导致迭代效率很低

表现：

- 只能看到最终图，不知道是哪一层出了问题。

对策：

- 把调试模式作为本阶段正式交付，而不是附带项。

## 12. 阶段完成后的下一步

第一阶段完成后，建议立即进入第二阶段：

- 云光照模型升级

原因是：

- 到那时密度体量已经成立，powder、银边、厚云核心能量层次才有发挥空间。
- 如果跳过光照直接做天气场结构升级，会让新的天气组织仍然受限于旧光照读感。

## 13. 当前进度记录

| 项 | 内容 |
| --- | --- |
| 状态 | `未开始` |
| 日期 | `2026-04-22` |
| 本次产出 | 新增第一阶段开发计划文档 |
| 下一步 | 从 `P1-M0` 开始建立默认基线与 `CloudHeightDensityLut` 收口 |
