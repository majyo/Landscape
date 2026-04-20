# 体积云下一阶段画质提升方案：Runtime Weather Field + 云型垂直廓线

## 1. 文档目的

基于当前项目已经完成的体积云 `MVP + jitter + temporal accumulation` 实现，提出一条**最适合承载运行时天气系统**的下一阶段方案。

本文档关注的不是“再加一个静态质量项”，而是把当前体积云从“稳定可显示的云”推进到“可在 runtime 连续演化的天气云层”。

本文档回答四个问题：

- 当前体积云设计已经做到哪里了。
- 为什么仅靠静态 `weather map` 不足以支撑晴天、多云、阴天、积雨云等 runtime 天气切换。
- 下一步最值得做、且最不容易破坏现有渲染链的方案是什么。
- 需要改哪些 Runtime / Render / Shader 数据结构，才能让天气系统真正接入体积云。

## 2. 当前设计检查结论

结合当前仓库实现，可确认以下现状：

- 已经具备完整的云渲染主链：`VolumetricCloudRenderPass -> VolumetricCloudTemporalAccumulationPass -> VolumetricCloudCompositePass`。
- 已经具备基础稳定化能力：低分辨率 trace、step jitter、history 双缓冲、temporal accumulation、history reject、debug overlay。
- 已经复用了现有 `Atmosphere` 的太阳方向、太阳亮度、Transmittance LUT、Sky View LUT，因此云和天空主光方向、曝光链路基本一致。
- 当前合成公式仍然简单清晰：`sceneColor * cloudTransmittance + cloudScattering`，这一层不构成主要画质瓶颈。

但当前密度设计仍然比较“单层”：

- `VolumetricCloudProfile` 主要还是全局 `cloudCoverage`、`densityMultiplier`、base/detail noise、风场、步数等参数，没有真正的天气层。
- `VolumetricCloudCommon.hlsl` 中 `ComputeCloudDensity()` 目前只使用 `baseShape + detailShape + cloudCoverage + 单一 verticalProfile`。
- 当前 `verticalProfile` 是固定函数，所有云共享同一种高度分布。
- 当前实现虽然已经能稳定显示云，但还无法优雅表达“局部放晴、云带推进、整体转阴、积雨核生长”这样的 runtime 天气变化。

这会直接带来几个限制：

- 云团宏观分布过于平均，容易出现“整片天空都在同一种噪声里长云”的感觉。
- 云型差异不足，难以表达层云、积云、积雨云之间的高度结构差别。
- `cloudCoverage` 更像全局阈值，而不是天气系统驱动下的“目标天气强度”。
- temporal accumulation 能降低闪烁，但不能替代天气系统数据层。

## 3. 方案选型结论

当前阶段，最值得优先投入的方向不是继续堆时域，也不是先做 cloud shadow map，而是：

**引入 `WeatherPreset + Runtime Weather Field + Cloud Height Density LUT`，把当前“纯 3D 噪声密度”升级为“天气状态驱动 + 动态宏观分布 + 云型高度结构 + 微观噪声细节”的分层模型。**

我建议把这一阶段命名为：

**Runtime Weather Field Driven Density**

核心原因如下：

- 它不仅提升“云像不像云”，还直接提升“天气像不像天气系统”。
- 它几乎不需要推翻现有 pass 顺序，主要修改点集中在 `Profile / Parameters / ShaderIDs / WeatherField Update / Density Function`。
- 它能自然承载晴天、多云、阴天、风暴等 runtime 状态切换。
- 它和当前已完成的 temporal 链兼容，只要天气场连续演化，就不会破坏现有时域稳定化收益。
- 相比先做 depth-aware upsample，这条线更能解决“云层表达力不足”的核心问题。

## 4. 为什么不再把“静态 Weather Map”作为主方案

### 4.1 静态 Weather Map 适合什么

静态 `weather map` 很适合以下场景：

- 固定天空
- 固定天气关卡
- 美术手工布局一张代表性云层分布图
- 作为默认 seed / fallback / 调试参考

它仍然有价值，但更适合作为**输入资产**，而不是 runtime 天气系统的主数据源。

### 4.2 Runtime 天气系统真正需要什么

如果项目要支持：

- 晴天到多云的连续转变
- 多云到阴天的云量上升
- 积雨云区域逐渐生成
- 风场带动云带推进
- 局部区域变湿、变厚、变暗

那么天气数据就必须满足：

- 能在 runtime 变化
- 变化是连续的，而不是硬切
- 具有空间分布，而不是只有几个全局标量
- 与 temporal accumulation 兼容，不会每帧让历史失效

### 4.3 推荐策略

因此本阶段的推荐策略是：

- 静态 `weather map` 降级为可选 `seed` 资源
- 主逻辑改为运行时维护一张低分辨率 `Runtime Weather Field`
- 天气系统不直接改最终密度，而是改“天气场目标状态”
- 云 raymarch 每帧采样天气场，而不是采样一张不会变化的固定纹理

## 5. 目标效果

完成本方案后，体积云应具备以下变化：

- 天空中出现更有组织的云团块、空洞区、过渡区，而不是平均噪声铺满。
- 同一时刻可以存在不同天气区域，例如局部晴开、普通多云区、厚重阴云区。
- 晴天、多云、阴天、风暴之间可以在 runtime 平滑过渡，而不是直接切贴图。
- 云底、云腰、云顶的厚度分布可随云型或积雨度改变，而不是共用一条固定曲线。
- 后续做降雨区、云影图、闪电、天气预设时，有明确的数据承载层。

## 6. 核心设计

## Step 1. 将云密度拆成四层

建议把最终云密度理解为四层组合：

```text
finalDensity =
    presetControl(weatherPreset)
  * macroCoverage(runtimeWeatherField)
  * typeProfile(cloudType, height01)
  * microShape(baseNoise3D, detailNoise3D)
  * densityMultiplier
```

四层职责如下：

### 1. 控制层：Weather Preset

负责定义目标天气状态，例如：

- 晴天
- 多云
- 阴天
- 风暴 / 积雨云

它不直接决定每个世界坐标的最终密度，而是提供：

- 目标云量
- 目标云型倾向
- 积雨度 / 湿度
- 风速 / 风向
- 演化速度
- 状态切换时长

### 2. 宏观层：Runtime Weather Field

负责回答：

- 这片天空哪里有云，哪里没云。
- 哪些区域更厚、哪些区域更稀疏。
- 哪些区域更偏层云，哪些区域更偏积云或积雨云。
- 哪些区域正逐步向阴云或风暴态演化。

### 3. 中观层：云型垂直廓线

负责回答：

- 当前高度 `height01` 上，云应该有多厚。
- 当前区域是更扁平、更均匀，还是更容易在中上部鼓起。
- 同样的 base/detail noise，在不同云型下应呈现不同体积轮廓。

### 4. 微观层：3D Base / Detail Noise

继续负责：

- 云团内部体积感
- 边缘侵蚀
- 局部孔洞和破碎感
- 风驱动下的细节流动

## Step 2. Runtime Weather Field 数据约定

建议新增一张低分辨率运行时天气场 RT，首版可用：

- 分辨率：`256 x 256`
- 格式：`ARGBHalf`
- 映射：世界空间 `XZ`
- 生命周期：持续存在、连续演化，不是每帧重建

推荐通道语义：

```text
R = coverage
G = cloudType / convection
B = wetness / precipitation potential
A = front / density bias / reserved
```

建议解释如下：

- `R`：局部宏观覆盖率，决定这块区域是否长出云团。
- `G`：云型倾向，低值偏层云，高值偏积云 / 积雨云。
- `B`：湿度或降水潜势，决定云是否更厚、更暗、更湿。
- `A`：锋面、密度偏置或未来扩展通道，可预留给风暴带或天气边界。

## Step 3. Runtime Weather Field 必须“连续演化”，不能“每帧随机新图”

这是本阶段最关键的工程约束。

错误做法：

- 每帧重新随机生成一张全新的天气图
- 每次天气切换直接硬替换整张 `weather map`
- 用随机噪声暴力驱动天气状态

正确做法：

- 使用一张持久存在的 `Runtime Weather Field`
- 每帧只让它**略微演化**
- 演化过程由风场平移、低频噪声注入、朝目标天气 preset 缓慢收敛组成

推荐每帧更新逻辑：

1. 对天气场做 advection，沿风向平移
2. 根据当前 `WeatherPreset` 对各通道目标值进行缓慢逼近
3. 叠加低频噪声或 seed，维持空间变化
4. 对局部高湿高对流区域做“生长”
5. 对非目标区域做“侵蚀 / 消散”

## Step 4. 引入云型垂直廓线 LUT

当前固定 `verticalProfile` 的主要问题，是所有云都共享同一条高度分布曲线。

建议新增一张 `Cloud Height Density LUT`：

```text
U = cloudType
V = height01
R = density profile
GBA = reserved
```

这样可以把“云型”和“高度分布”解耦出来：

- `cloudType = 0.0` 时，LUT 输出更扁平、底部更厚，偏层云。
- `cloudType = 0.5` 时，LUT 输出更均衡，偏层积云。
- `cloudType = 1.0` 时，LUT 输出中上部更鼓起，偏积云或积雨云。

这比在 shader 中继续堆 `smoothstep` 分支更稳妥，因为：

- 调参可以走 LUT，不必频繁改 HLSL。
- 后续增加更多天气状态时，不必让 profile 膨胀成一大堆硬编码字段。

## 7. 运行时系统设计建议

## 7.1 新增 `WeatherPreset`

建议新增独立 `WeatherPreset` 资源，而不是把所有天气目标都塞进 `VolumetricCloudProfile`。

建议字段：

- `string presetName`
- `float targetCoverage`
- `float targetCloudType`
- `float targetWetness`
- `float targetConvection`
- `float targetDensityBias`
- `Vector2 windDirection`
- `float windSpeedKmPerSecond`
- `float evolutionSpeed`
- `float transitionDurationSeconds`
- `float cloudCoverageBias`
- `float cloudCoverageContrast`
- `float detailErosionStrength`

建议默认至少有：

- `Sunny`
- `Cloudy`
- `Overcast`
- `Storm`

## 7.2 新增 `WeatherFieldController`

建议新增一个运行时天气场控制器，职责如下：

- 持有当前 preset 与目标 preset
- 驱动天气状态切换
- 维护 `Runtime Weather Field` RT
- 控制天气场更新节奏
- 提供给体积云系统读取

如果不想新增独立控制器，也可以作为 `VolumetricCloudController` 的子模块实现，但职责上建议保持独立。

建议内部状态：

- `CurrentPreset`
- `TargetPreset`
- `PresetBlend`
- `LastUpdatedFrame`
- `WeatherFieldTexture`
- `WeatherFieldInitialized`
- `WeatherFieldDiscontinuityVersion`

## 7.3 新增 `WeatherFieldResources`

建议集中管理：

- `WeatherFieldTexture`
- 可选的 `WeatherFieldScratchTexture`
- RTHandle
- 分辨率与重建逻辑

这层和现有 `VolumetricCloudResources` 的关系应该是：

- `WeatherFieldResources` 管天气场
- `VolumetricCloudResources` 管云 trace / history / stabilized trace

避免把两类资源混在一起。

## 7.4 新增 `VolumetricWeatherFieldUpdatePass`

建议新增一个专用 compute pass，用于更新天气场，而不是在 raymarch shader 内部偷偷生成。

推荐执行顺序：

```text
1. Transmittance
2. Multi-scattering
3. Sky-View
4. Aerial Perspective
5. Volumetric Weather Field Update
6. Volumetric Clouds
7. Volumetric Cloud Temporal Accumulation
8. Volumetric Cloud Composite
9. Aerial Composite
```

这样做的好处：

- 天气场更新职责清晰
- 云 trace 只负责采样，不负责“顺便造天气”
- 后续天气系统可独立演进

注意事项：

- 如果项目有多个相机，天气场**不能按相机重复更新**。
- 建议用 `Time.frameCount` 或等价 frame token，保证每帧只更新一次天气场。

## 7.5 `VolumetricCloudProfile`

建议从“直接持有静态 weather map”改为“持有天气场配置与默认 seed”：

- `bool useRuntimeWeatherField = true`
- `WeatherPreset defaultWeatherPreset`
- `Texture2D defaultWeatherSeed`
- `Texture2D cloudHeightDensityLut`
- `int weatherFieldResolution = 256`
- `float weatherFieldScaleKm`
- `float weatherFieldUpdateRate`
- `float detailErosionStrength`
- `float cloudTypeRemapMin`
- `float cloudTypeRemapMax`

保留现有：

- `cloudCoverage`
- `densityMultiplier`
- `shapeBaseScaleKm`
- `detailScaleKm`
- `baseShapeNoise`
- `detailShapeNoise`

角色分工建议：

- `cloudCoverage` 调整为“全局云量增益”
- `WeatherPreset` 决定目标天气状态
- `Runtime Weather Field` 提供空间分布
- `Cloud Height Density LUT` 决定垂直轮廓

## 7.6 `VolumetricCloudParameters`

新增字段建议：

- `Texture WeatherFieldTexture`
- `Texture2D CloudHeightDensityLut`
- `float WeatherFieldScaleKm`
- `Vector2 WeatherFieldOffsetKm`
- `float GlobalCoverageGain`
- `float CoverageBias`
- `float CoverageContrast`
- `float DetailErosionStrength`
- `float CloudTypeRemapMin`
- `float CloudTypeRemapMax`
- `float PresetBlendFactor`
- `float TargetWetness`
- `float TargetConvection`
- `int WeatherFieldDiscontinuityVersion`

其中要特别注意：

- **不要把天气场每帧变化的内容本身纳入 `ParameterHash` 或 `HistoryResetHash`。**
- 否则天气场一变化，temporal accumulation 就会每帧 reset，等于把整个时域链打废。

应该纳入 hash 的，是：

- 分辨率变化
- 是否启用 runtime weather field
- LUT / seed / preset 资源引用变化
- 明确的天气场重初始化版本号

不应该纳入 hash 的，是：

- 天气场 RT 的像素内容
- 每帧平滑演化后的 coverage / wetness 局部结果

## 7.7 `VolumetricCloudShaderIDs`

建议新增：

- `_CloudWeatherFieldTexture`
- `_CloudHeightDensityLut`
- `_CloudWeatherFieldData`
- `_CloudWeatherPresetData`
- `_CloudWeatherRemapData`
- `_CloudTypeRemapData`
- `_CloudDetailErosionData`

## 8. Shader 侧改造建议

## 8.1 新增天气场采样函数

建议在 `VolumetricCloudCommon.hlsl` 中新增：

- `SampleRuntimeWeatherField()`
- `SampleCloudTypeProfile()`
- `ComputeMacroCoverage()`
- `ComputeDynamicDetailErosion()`
- `ComputeCloudDensityFromWeatherField()`

建议职责边界：

- `SampleRuntimeWeatherField(worldPosKm)`：从世界空间 `XZ` 取样运行时天气场
- `SampleCloudTypeProfile(cloudType, height01)`：从 LUT 取样云型垂直廓线
- `ComputeCloudDensityFromWeatherField(...)`：统一组合天气场、中观 profile 与微观 noise

## 8.2 推荐首版密度公式

```text
weather = sampleRuntimeWeatherField(worldPosXZ)

macroCoverage = RemapCoverage(
    weather.r,
    globalCoverageGain,
    coverageBias,
    coverageContrast)

cloudType = Remap(weather.g, cloudTypeRemapMin, cloudTypeRemapMax)
typeProfile = SampleCloudHeightDensityLut(cloudType, height01)

baseShape = sampleBaseNoise(worldPos)
detailShape = sampleDetailNoise(worldPos)
detailErosion = ComputeDynamicDetailErosion(
    detailShape,
    weather.b,
    weather.a,
    detailErosionStrength,
    height01)

finalDensity = macroCoverage * typeProfile * baseShape * detailErosion * densityMultiplier
```

与当前实现相比，变化点是：

- `macroCoverage` 不再直接从 `baseShape` 推导，而由运行时天气场提供。
- `verticalProfile` 不再固定，而是由 `cloudType + height01` 决定。
- `detailShape` 不再只是统一侵蚀，而可以受湿度 / 锋面通道影响。

## 8.3 对 `VolumetricCloudRaymarch.compute` 的影响

主要改造点集中在 raymarch 内部密度采样部分：

- 保留当前球壳求交、视线步进、太阳方向 shadow march、Beer-Lambert 累积逻辑。
- 将当前直接调用 `ComputeCloudDensity(baseShape, detailShape, cloudCoverage, densityMultiplier, height01)` 的路径，替换为新天气场驱动密度函数。
- `MarchCloudShadow()` 也必须复用同一套密度函数，避免主视线和太阳阴影的云结构不一致。

这样可以保证：

- pass 结构基本不变
- temporal accumulation 仍可复用
- composite 不变
- 主要变动集中在“天气数据层 + 密度函数定义”上

## 9. 天气预设建议

建议首版先提供四组基础 preset：

| Preset | 目标 coverage | 目标 cloudType | 目标 wetness | 演化特征 |
| --- | --- | --- | --- | --- |
| `Sunny` | `0.1 ~ 0.2` | `0.15 ~ 0.25` | `0.0 ~ 0.1` | 局部云团稀疏，侵蚀更强 |
| `Cloudy` | `0.35 ~ 0.55` | `0.35 ~ 0.5` | `0.15 ~ 0.25` | 常规多云，云量中等 |
| `Overcast` | `0.75 ~ 0.9` | `0.1 ~ 0.25` | `0.3 ~ 0.45` | 云层压低、偏平、连续覆盖 |
| `Storm` | `0.8 ~ 1.0` | `0.75 ~ 1.0` | `0.75 ~ 1.0` | 更高对流、更厚重、更湿 |

推荐切换方式：

- `Sunny -> Cloudy`：20 到 40 秒
- `Cloudy -> Overcast`：30 到 60 秒
- `Overcast -> Storm`：45 到 90 秒

这些数值不是最终美术参数，而是首版“天气系统行为”参考值。

## 10. 资源策略建议

本阶段不应再把“必须有成品 weather map 素材”作为前提。

建议资源策略如下：

```text
Assets/VolumetricClouds/Resources/VolumetricClouds/
  WeatherPreset_Sunny.asset
  WeatherPreset_Cloudy.asset
  WeatherPreset_Overcast.asset
  WeatherPreset_Storm.asset
  CloudHeightDensityLut_Default.asset 或 png
  WeatherSeed_Default.asset 或 png (可选)
```

其中：

- `WeatherSeed_Default` 是可选输入，不是必须。
- 如果没有任何 seed 贴图，也可以直接由 compute 用低频噪声初始化天气场。
- 静态 `weather map` 在这里退化为“启动种子 / fallback / 调试参考”，而不再是核心方案。

## 11. 调试与可视化建议

建议新增以下调试模式：

- `Weather Field Coverage`
- `Weather Field Cloud Type`
- `Weather Field Wetness`
- `Weather Field Front`
- `Height Density Profile`
- `Macro Coverage`
- `Final Density`
- `Active Weather Preset`
- `Preset Blend`

建议最小落点：

- 在 `VolumetricCloudController` 或 `WeatherFieldController` 的 overlay 中增加调试切换
- 或提供全局 debug mode，直接输出天气场与密度中间结果

验收时至少要能回答：

- 当前这一块天空为什么变厚了
- 当前这片云为什么偏平还是偏鼓
- 当前这片区域为什么更像积雨云
- 当前天气状态切换处于哪一阶段

## 12. 推荐实施顺序

## Step 0. 基线冻结

目标：

- 冻结当前 temporal 版本作为对照基线
- 确保后续只改变天气层与密度定义，不打乱现有云 pass 主链

## Step 1. 数据骨架接入

任务：

- 新建 `WeatherPreset`
- 新建 `WeatherFieldController`
- 新建 `WeatherFieldResources`
- 扩展 `VolumetricCloudProfile`
- 扩展 `VolumetricCloudParameters`
- 扩展 `VolumetricCloudShaderIDs`

退出条件：

- runtime 天气场资源能稳定创建和释放
- 不引入生命周期错误

## Step 2. 天气场更新 pass

任务：

- 新建 `VolumetricWeatherFieldUpdatePass`
- 新建天气场 update compute
- 实现风向平移、目标逼近、基础生长与侵蚀
- 确保多相机情况下每帧只更新一次天气场

退出条件：

- 天气场能在 runtime 平稳演化
- 不出现每帧重建或随机跳变

## Step 3. 云密度接线

任务：

- 在 `VolumetricCloudCommon.hlsl` 中实现天气场驱动密度函数
- 替换 raymarch 与 shadow march 的密度采样路径
- 引入 `Cloud Height Density LUT`

退出条件：

- 晴天、多云、阴天、风暴四类状态能看出明显差异
- 云仍在正确高度范围内，无明显相交错误

## Step 4. Runtime 切换与时域兼容

任务：

- 实现 preset 切换
- 明确天气场 discontinuity 规则
- 在大天气变化阶段适度降低 temporal 权重，必要时触发保守 reset

退出条件：

- 天气切换无明显整屏拖影
- temporal accumulation 不会因为天气场每帧变化而完全失效

## Step 5. 默认资源与调试收口

任务：

- 补默认 preset 资源
- 补默认 `Cloud Height Density LUT`
- 可选补 `WeatherSeed_Default`
- 补 debug 视图与回归机位

退出条件：

- `SampleScene` 中能稳定演示动态天气变化

## 13. 时域兼容特别说明

当前项目已经有 `temporal accumulation`，因此本方案必须遵守下面两条：

### 13.1 天气场内容不能触发“每帧 reset”

不要把天气场像素内容纳入：

- `ParameterHash`
- `HistoryResetHash`

否则天气场一更新，history 就会每帧作废。

### 13.2 天气切换应以“连续演化”为主，硬切为辅

推荐策略：

- 正常天气变化：连续演化，不 reset
- 大幅 preset 切换：临时降低 `temporalResponse`
- 真正 discontinuity：例如手动重初始化天气场、地图切换、分辨率变化，才显式 reset history

## 14. 验收标准

以下条目全部满足后，可认为该阶段达到“已完成”：

- [ ] `Sunny / Cloudy / Overcast / Storm` 可在 runtime 切换
- [ ] 天气变化表现为连续演化，而不是整张贴图硬切
- [ ] 天空中出现明显的云团块、空洞和过渡带，而不是均匀噪声覆盖
- [ ] 至少能看出两种以上不同云型高度分布
- [ ] `cloudCoverage` 更像全局天气增益，而不是单纯阈值
- [ ] runtime 天气场更新不会导致 temporal accumulation 每帧失效
- [ ] `SampleScene` 中正午、低太阳角、云下、云内、云上俯视机位都能稳定观察

## 15. 风险与应对

| 风险编号 | 问题 | 影响 | 应对策略 | 状态 |
| --- | --- | --- | --- | --- |
| W1 | 天气场按相机重复更新 | 多相机下天气速度异常 | 用 frame token 保证每帧只更新一次 | `开放` |
| W2 | 天气场硬切 | 画面突变、拖影明显 | 用 preset blend 和连续演化替代整图替换 | `开放` |
| W3 | 天气场内容被纳入 history reset | temporal 每帧失效 | 只对真正 discontinuity 做 reset | `开放` |
| W4 | 每帧随机化过强 | 云层像噪声闪烁，不像天气 | 控制变化幅度，以 advection + relax 为主 | `开放` |
| W5 | 新增天气场 pass 带来成本 | PC 性能下降 | 保持低分辨率 RT，必要时降低更新频率 | `开放` |
| W6 | 云型 LUT 设计不合理 | 云顶 / 云底轮廓失真 | 先从 2 到 4 种标准曲线开始验证 | `开放` |

## 16. 与后续路线的关系

完成本方案后，当前项目会更自然地接到后续质量线：

- `depth-aware upsample`
- `cloud shadow map`
- `rain / storm coupling`
- `weather preset timeline`
- `lightning / precipitation / fog linkage`

因为这时项目终于拥有了一个真正可演化的“天气层”：

- 它可以告诉我们哪里有云
- 它可以告诉我们云是什么类型
- 它可以告诉我们哪里更湿、更厚、更像风暴区

## 17. 最终建议

如果当前只选一个下一阶段方案来继续提升体积云质量，我建议优先做：

**WeatherPreset + Runtime Weather Field + 云型垂直廓线**

一句话总结原因：

**当前项目已经基本解决了“云能稳定显示”的问题，下一步最应该解决的是“云层如何在 runtime 中像真实天气系统一样连续演化”。**
