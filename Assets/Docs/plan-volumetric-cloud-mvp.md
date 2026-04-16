# 体积云 MVP 设计方案

## 1. 目标与边界

### 目标
在当前 Unity 6 + URP + 自定义 `Atmosphere` 渲染框架上，新增一套**至少能够跑通的体积云 MVP 流程**，满足以下最小目标：

- 云层能够在天空中被渲染出来。
- 云层能够随相机移动保持稳定，不是简单的屏幕空间贴片。
- 云层能够接收主方向光，至少具备基础明暗变化。
- 云层能够与当前大气天空颜色融合，不会完全脱节。
- 云层能够在当前 `AtmosphereRendererFeature` 框架内接入，并支持后续继续演进。

### MVP 不做这些
- 不做真实天气系统。
- 不做云阴影投射到地表。
- 不做多层云、高空卷云、低空层云混合。
- 不做真正的多重散射体积积分。
- 不做 TAA、蓝噪声时域累积、半分辨率重建。
- 不做与地形/建筑的深度穿插修正优化。
- 不接入 Unity Volume 覆盖系统，先沿用 `Profile + Controller` 路线。

MVP 的目标不是一步做到电影级，而是先形成一条**能出图、能调参、能继续扩展**的渲染链路。

## 2. 当前项目上下文

从现有代码可以确认：

- 渲染管线是 `URP`。
- 已有 `AtmosphereRendererFeature`，并按顺序组织了：
  - `Transmittance`
  - `Multi-scattering`
  - `Sky-View`
  - `Aerial Perspective`
  - `Aerial Composite`
- 已有 `AtmosphereController`、`AtmosphereProfile`、`AtmosphereParameters`、`AtmosphereLutManager`，说明项目已经接受“Profile 驱动 + LUT 管理 + RendererFeature 调度”的组织方式。
- 当前天空已经通过 `SkyView LUT + Skybox Shader` 渲染，地景已经通过 `Aerial Perspective` 进行合成。

这意味着体积云最适合采用与现有大气系统一致的策略：

1. 仍然由 `Profile/Parameters` 提供参数。
2. 仍然通过 `ScriptableRenderPass` 或 RenderGraph pass 插入。
3. 仍然通过全局纹理/全局常量与最终合成材质交互。
4. 在当前大气系统基础上做“云层附加项”，而不是另起一套完全独立的天空。

## 3. MVP 核心设计决策

### 决策 1：MVP 使用单层球壳体积云
云层定义为位于地表上方某一高度区间的一层球壳，类似：

- `cloudBottomHeightKm = 1.5`
- `cloudTopHeightKm = 4.0`

原因：

- 和当前大气模型一样统一使用 km，数学空间一致。
- 直接复用当前 `Atmosphere` 的行星半径/相机位置定义。
- 比平面云层更稳定，不会在大范围地形中暴露“无限平面”假象。

### 决策 2：MVP 只做单次散射 + Beer-Lambert 透射
每一步 ray march 中只计算：

- 局部密度
- 到太阳方向的近似透射
- 当前步的单次散射贡献
- 当前步的视线透射衰减

原因：

- 成本最低，最容易先跑通。
- 视觉上已经能形成“亮边、暗部、体积感”。
- 后续可以自然扩展到 powder effect、多重散射近似、阴影图等。

### 决策 3：MVP 先做屏幕空间全屏合成，不直接改天空盒
云层先作为一个独立 pass 渲染到低分辨率纹理，再在全屏合成阶段叠加到相机颜色上。

原因：

- 当前天空已经由 `SkyView LUT` 负责，直接改 skybox 会让透明物体、地表雾、深度关系更难梳理。
- 使用全屏合成更容易同时覆盖“天空背景中的云”和“远景上方的云”。
- 可以直接复用当前 `AerialComposite` 的工程模式。

### 决策 4：MVP 先采用 2D Weather Map + 3D Shape Noise 的混合密度
密度来源拆成两层：

- **Coverage/Weather Map**：2D，决定哪里有云，哪里没云。
- **Shape Noise**：3D，决定体积边界起伏。

最终密度近似：

```text
density = coverage2D * verticalProfile * shapeNoise3D
```

原因：

- 比纯 3D 噪声更容易调出“云团分布”。
- 比单纯 2D billboard 更有真实厚度。
- 能以最低复杂度满足“有云团、有空洞、有体积边界”。

### 决策 5：MVP 允许先用程序噪声，不强依赖外部美术资源
第一版可以：

- 使用一张程序生成或临时导入的平铺 `Weather Map`
- 使用一张程序生成或临时导入的 `3D Noise`

如果项目里还没有 3D 噪声资产，也可以先用一张 `Texture3D` 资源或运行时生成的简化噪声体。

原因：

- 先打通流程，不把阻塞点放在资源制作上。

## 4. 推荐渲染流程

MVP 建议新增如下流程：

1. 现有 `Atmosphere` LUT Pass 正常执行
2. 新增 `VolumetricClouds Pass` 生成低分辨率云颜色/透射结果
3. 新增 `VolumetricCloud Composite Pass` 将云结果合成到相机颜色
4. 现有 `Aerial Perspective Composite` 继续保留，但需要明确顺序

### 推荐执行顺序

推荐在当前 `AtmosphereRendererFeature` 内部调整为：

1. `Transmittance`
2. `Multi-scattering`
3. `Sky-View`
4. `Aerial Perspective`
5. `Volumetric Clouds`
6. `Volumetric Cloud Composite`
7. `Aerial Composite`

### 为什么云在 Aerial Composite 之前

MVP 阶段建议让**地景大气雾仍作为最后一层统一合成**，这样云层本身也能被远距离大气轻微调制，整体层次更自然。

如果后续发现云已经在自己的 ray march 中充分考虑了大气透射，也可以把顺序改成：

1. 先 `Aerial Composite`
2. 再 `Volumetric Cloud Composite`

但对 MVP 来说，优先选择**工程一致性高、行为更直观**的顺序即可。

## 5. MVP 的最小数据结构

建议新增一个独立的 `VolumetricCloudProfile`，而不是把云参数直接塞进现有 `AtmosphereProfile`。

原因：

- 云参数比大气参数变化更频繁，职责不同。
- 后续天气系统很可能独立演化。
- 避免 `AtmosphereProfile` 继续膨胀。

### `VolumetricCloudProfile.cs`

建议字段：

- `bool enableClouds = true`
- `float cloudBottomHeightKm = 1.5f`
- `float cloudTopHeightKm = 4.0f`
- `float cloudCoverage = 0.45f`
- `float densityMultiplier = 0.04f`
- `float lightAbsorption = 1.0f`
- `float ambientStrength = 0.35f`
- `float forwardScatteringG = 0.55f`
- `float stepCount = 48`
- `float shadowStepCount = 8`
- `float maxRenderDistanceKm = 64.0f`
- `int traceWidth = 960`
- `int traceHeight = 540`
- `float weatherMapScaleKm = 32.0f`
- `Vector2 windDirection = (1, 0)`
- `float windSpeedKmPerSecond = 0.02f`
- `Texture2D weatherMap`
- `Texture3D shapeNoise`

### `VolumetricCloudParameters.cs`

作用：

- 把 `Profile` 转成 shader 常量。
- 做 hash，用于判断云纹理是否需要重建。
- 统一 km 单位。

建议额外包含从 `AtmosphereController` 获取的只读依赖：

- `groundRadiusKm`
- `topRadiusKm`
- `sunDirection`
- `sunIlluminance`
- `cameraPositionKm`

## 6. 推荐目录结构

```text
Assets/
  VolumetricClouds/
    Runtime/
      VolumetricCloudProfile.cs
      VolumetricCloudController.cs
      VolumetricCloudParameters.cs
      VolumetricCloudShaderIDs.cs
      VolumetricCloudResources.cs
    Rendering/
      VolumetricCloudRenderPass.cs
      VolumetricCloudCompositePass.cs
    Shaders/
      VolumetricCloudCommon.hlsl
      VolumetricCloudRaymarch.compute
      VolumetricCloudComposite.shader
    Resources/
      VolumetricCloudProfile_Default.asset
      CloudWeatherMap_Default.asset
      CloudShapeNoise_Default.asset
  Docs/
      plan-volumetric-cloud-mvp.md
```

如果你希望严格并入已有模块，也可以放到：

```text
Assets/Atmosphere/Clouds/
```

但从职责上看，云单独成模块更干净。

## 7. 模块职责拆分

### `VolumetricCloudController.cs`

场景中的运行时入口，职责类似 `AtmosphereController`：

- 持有 `VolumetricCloudProfile`
- 提供 `Instance`
- 负责资源检查和 dirty 标记
- 收集主光方向和时间偏移
- 对外暴露当前云参数和结果纹理

建议不要重复管理主光，优先从 `AtmosphereController` 读取太阳方向。这样大气和云的主光不会漂移。

### `VolumetricCloudResources.cs`

负责管理云 pass 输出纹理：

- `cloudLightingTexture`
- `cloudTransmittanceTexture`
- 或 MVP 直接压成一张 `RGBA16F`

推荐 MVP 先用**一张纹理**：

- `RGB` 存云散射/发光结果
- `A` 存云视线透射

原因：

- 合成最简单。
- 对当前阶段足够。

### `VolumetricCloudRenderPass.cs`

职责：

- 准备云 trace RT
- 绑定天气图、3D 噪声、深度、当前天空大气参数
- 调度云 compute shader 或全屏 pixel shader
- 输出 `_VolumetricCloudTexture`

### `VolumetricCloudCompositePass.cs`

职责：

- 读取相机颜色
- 读取 `_VolumetricCloudTexture`
- 基于深度与 alpha 进行合成

MVP 合成公式：

```text
finalColor = sceneColor * cloudTransmittance + cloudScattering
```

其中：

- `cloudTransmittance = cloudTex.a`
- `cloudScattering = cloudTex.rgb`

### `VolumetricCloudShaderIDs.cs`

统一管理：

- `_VolumetricCloudTexture`
- `_VolumetricCloudTraceSize`
- `_CloudBottomRadiusKm`
- `_CloudTopRadiusKm`
- `_CloudCoverage`
- `_CloudDensityMultiplier`
- `_CloudLightAbsorption`
- `_CloudAmbientStrength`
- `_CloudPhaseG`
- `_CloudMaxRenderDistanceKm`
- `_CloudStepCount`
- `_CloudShadowStepCount`
- `_CloudWeatherMap`
- `_CloudShapeNoise`
- `_CloudWindData`

## 8. 核心渲染算法

MVP 推荐使用 **低分辨率全屏 ray march**。

### 输入

- 当前相机矩阵/视锥参数
- 当前太阳方向和亮度
- `Transmittance LUT`
- `SkyView LUT`
- 云天气图
- 云 3D 形状噪声
- 场景深度

### 输出

- `_VolumetricCloudTexture : RGBA16F`

### 每像素流程

1. 通过屏幕 UV + 相机参数生成世界空间视线。
2. 求这条视线与云底球壳、云顶球壳的交点。
3. 如果无交点，直接输出：
   - `rgb = 0`
   - `a = 1`
4. 如果有交点，在交点区间内做固定步数 ray march。
5. 每步计算：
   - 采样点世界位置
   - 高度归一化 `height01`
   - `coverage = sample(weatherMap)`
   - `shape = sample(shapeNoise3D)`
   - `verticalProfile = height01 * (1 - height01)` 或更平滑的云型函数
   - `density = coverage * shape * verticalProfile * densityMultiplier`
6. 对每步进行太阳方向短程积分，近似得到局部光照透射。
7. 用简化相位函数计算前向散射。
8. 用 Beer-Lambert 累积视线透射和散射。
9. 输出累积后的 `rgb + alpha`。

### 密度函数建议

MVP 推荐先用非常稳定的版本：

```text
baseCoverage = saturate((weatherMap - (1 - cloudCoverage)) / max(cloudCoverage, 1e-3))
verticalProfile = smoothstep(0.0, 0.15, height01) * (1.0 - smoothstep(0.7, 1.0, height01))
shape = saturate(shapeNoise * 1.2 - 0.2)
density = baseCoverage * verticalProfile * shape * densityMultiplier
```

这个版本的优点是：

- 云底相对平整
- 云顶有自然破碎
- 调 `cloudCoverage` 时行为直观

### 光照近似

MVP 不做完整云内多重散射，采用：

```text
lightEnergy = sunTransmittance * cloudShadowTransmittance
lighting = ambientTerm + lightEnergy * phase
```

其中：

- `sunTransmittance` 通过当前大气 `Transmittance LUT` 近似太阳从高空打到采样点时的大气透射
- `cloudShadowTransmittance` 通过沿太阳方向做 `8` 步短程采样得到
- `ambientTerm` 可以从 `SkyView LUT` 按太阳夹角或世界方向取一个低频颜色

## 9. 与现有 Atmosphere 系统的耦合方式

### 必须复用的内容

体积云不要自己重新发明一套太阳与大气参数，直接复用：

- `AtmosphereController.Instance`
- `AtmosphereParameters`
- `AtmosphereViewParameters`
- `_AtmosphereTransmittanceLut`
- `_AtmosphereSkyViewLut`
- `_AtmosphereSunDirection`
- `_AtmosphereSunIlluminance`
- `_AtmosphereCameraPositionKm`

### 建议的依赖关系

`VolumetricCloudController` 只在 `AtmosphereController` 可用时工作。

原因：

- 云的主光颜色、太阳方向、相机 km 坐标定义与大气天然一致。
- 如果没有大气系统，云单独存在会出现颜色和曝光明显脱节。

### 建议的天空颜色耦合

云的环境光项不要写死常量灰色，而应近似取自当前大气天空：

1. 用太阳方向和当前视线方向估算一个 sky uv
2. 从 `SkyView LUT` 采样天空颜色
3. 用这个值作为云的 ambient 基底

这样即使只有单次散射，云也会自然继承清晨、黄昏、正午不同的氛围色。

## 10. 与场景深度的关系

MVP 里必须读取场景深度，否则云会无脑覆盖前景。

### 基础规则

对于每个像素：

1. 从深度重建该像素对应的场景距离
2. 将 ray march 的终点裁剪为：
   - `min(cloudExitDistance, sceneDepthDistance, maxRenderDistanceKm)`

这样至少能保证：

- 山体/建筑在云前方时，云不会盖到它们脸上
- 天空区域仍然正常显示云

### MVP 可接受的限制

即便如此，仍可能有以下残留问题：

- 云与远山交界处可能出现锯齿或错层
- 低分辨率 trace 结果在深度边缘会有轻微漏光

这些都属于 MVP 范畴内可接受问题，后续再通过深度感知上采样和 TAA 处理。

## 11. Pass 形式选择

这里有两个可行方案：

### 方案 A：Compute Shader Ray March

优点：

- 与当前 Atmosphere LUT 路线一致
- 更适合后续半分辨率、时域累积、history buffer
- 便于单独管理输出 RT

缺点：

- 初次接入时参数传递稍多

### 方案 B：Fullscreen Fragment Shader Ray March

优点：

- 更直观
- 最快看到第一张图

缺点：

- 后续想做 history、分层重建、tile 优化时会更别扭

### MVP 推荐

推荐 **方案 A：Compute Shader**。

原因：

- 当前项目已有大量 compute 资源管理基础。
- 云结果本来就适合先写入低分辨率 RT，再做单独 composite。

## 12. 推荐资源规格

MVP 参数建议从以下组合起步：

- `traceWidth = 960`
- `traceHeight = 540`
- `stepCount = 48`
- `shadowStepCount = 8`
- `maxRenderDistanceKm = 64`
- `cloudBottomHeightKm = 1.5`
- `cloudTopHeightKm = 4.0`
- `cloudCoverage = 0.45`
- `densityMultiplier = 0.04`
- `ambientStrength = 0.35`
- `forwardScatteringG = 0.55`

### 为什么不直接全分辨率

体积云最贵的是 ray march，不是 final composite。MVP 直接做低分辨率：

- 更容易跑起来
- 更符合真实产品策略
- 后续上采样只是演进问题，不影响主结构

## 13. 核心伪代码

### 云层相交

```hlsl
float tEnterTop = RaySphereIntersectNearest(cameraPosKm, rayDir, cloudTopRadiusKm);
float tExitTop = RaySphereIntersectFar(cameraPosKm, rayDir, cloudTopRadiusKm);
float tEnterBottom = RaySphereIntersectNearest(cameraPosKm, rayDir, cloudBottomRadiusKm);
float tExitBottom = RaySphereIntersectFar(cameraPosKm, rayDir, cloudBottomRadiusKm);

float tStart = max(tEnterTop, 0.0);
float tEnd = tExitTop;

if (tExitBottom > 0.0)
{
    if (cameraInsideCloudLayer)
    {
        // 需要按相机所在位置处理
    }
    else
    {
        // 把云底之下的那段剔掉
    }
}
```

MVP 实现时不需要追求最优雅，目标是先把三类情况覆盖：

- 相机在云层下
- 相机在云层内
- 相机在云层上

### 主循环

```hlsl
float3 accumLight = 0.0;
float transmittance = 1.0;

for (int i = 0; i < stepCount; ++i)
{
    float t = lerp(tStart, tEnd, (i + 0.5) / stepCount);
    float3 samplePosKm = cameraPosKm + rayDir * t;

    float heightKm = length(samplePosKm) - groundRadiusKm;
    float height01 = saturate((heightKm - cloudBottomHeightKm) / max(cloudThicknessKm, 1e-4));

    float2 weatherUv = samplePosKm.xz / weatherMapScaleKm + windOffset;
    float coverage = SAMPLE_TEXTURE2D_LOD(_CloudWeatherMap, sampler_CloudWeatherMap, weatherUv, 0).r;
    float shape = SAMPLE_TEXTURE3D_LOD(_CloudShapeNoise, sampler_CloudShapeNoise, sampleNoiseUv, 0).r;

    float verticalProfile = smoothstep(0.0, 0.15, height01) * (1.0 - smoothstep(0.7, 1.0, height01));
    float density = ComputeDensity(coverage, shape, verticalProfile);

    if (density < 1e-4)
        continue;

    float lightTransmittance = MarchToSun(samplePosKm);
    float phase = HenyeyGreenstein(dot(rayDir, sunDir), phaseG);
    float3 ambient = SampleSkyAmbient(samplePosKm, rayDir);
    float3 lighting = ambient * ambientStrength + sunIlluminance * lightTransmittance * phase;

    float stepAlpha = 1.0 - exp(-density * stepSizeKm);
    accumLight += transmittance * lighting * stepAlpha;
    transmittance *= (1.0 - stepAlpha);

    if (transmittance < 0.01)
        break;
}

output.rgb = accumLight;
output.a = transmittance;
```

## 14. 工程落地顺序

建议按下面顺序推进，而不是一次把所有优化都塞进去。

### Milestone A：参数与资源骨架

- 新建 `VolumetricCloudProfile`
- 新建 `VolumetricCloudParameters`
- 新建 `VolumetricCloudShaderIDs`
- 新建 `VolumetricCloudController`
- 新建云输出 RT 管理

交付结果：

- 场景中可持有一份云配置
- 运行时可稳定创建低分辨率云 RT

### Milestone B：无光照密度可视化

先不要急着做真实云光照，先让云形出来。

- 只做 ray/sphere 求交
- 只计算 density
- 输出 `density grayscale`

交付结果：

- 能看到云层位置、覆盖率和 3D 噪声轮廓
- 能确认云不会跟着屏幕飘

### Milestone C：加入太阳方向与基础明暗

- 加入太阳短程 shadow march
- 加入相位函数
- 加入大气透射近似

交付结果：

- 云有向光面和背光面
- 日落方向会明显偏暖/偏亮

### Milestone D：加入深度裁剪与全屏合成

- 读取场景深度
- 对云 ray 终点做裁剪
- 实装 `VolumetricCloudCompositePass`

交付结果：

- 云不会简单盖住前景山体
- 天空和场景都能看到云

### Milestone E：对齐 Atmosphere 框架

- 将 pass 接到当前 `AtmosphereRendererFeature`
- 明确 RenderGraph 与旧 `Execute` 双路径行为
- 补齐全局参数绑定和调试输出

交付结果：

- 在当前项目结构中稳定运行
- 方便继续做性能和品质迭代

## 15. 验收标准

MVP 完成后，至少满足以下条件：

1. 打开 `SampleScene` 并运行后，天空中能看到体积云，而不是纯贴图云。
2. 相机平移和旋转时，云层在世界空间中稳定，不出现明显屏幕空间滑动。
3. 云层有基本的受光方向变化，朝向太阳的区域更亮。
4. 云层颜色与当前天空大气一致，不是单独一层灰白色塑料。
5. 远山或前景几何体不会被云完全错误覆盖，至少有基础深度裁剪。
6. 调整覆盖率、厚度、密度、风向后，画面会立即产生可预期变化。
7. 在 `PC` 质量档下可以稳定跑通，不要求此阶段就优化到最终性能。

## 16. 已知风险与对应策略

### 风险 1：低分辨率 trace 边缘发糊

处理策略：

- MVP 接受
- 后续加双线性上采样或深度感知上采样

### 风险 2：太阳方向附近高光过曝

处理策略：

- 先压 `sunIlluminance`
- 对 phase 输出做 soft clamp
- 先避免过强的各向前向散射

### 风险 3：云底/云顶边界太硬

处理策略：

- 通过 `verticalProfile` 平滑
- 不要依赖 coverage 做硬裁切

### 风险 4：相机在云层内部时噪声感明显

处理策略：

- MVP 阶段允许存在
- 后续通过 jitter + TAA 解决

### 风险 5：大气与云曝光不一致

处理策略：

- 明确复用 `Atmosphere` 的太阳和天空环境色
- composite 阶段避免再乘独立曝光链路

## 17. 后续扩展接口预留

为了避免 MVP 做完后必须重构，建议现在就预留以下能力：

- history RT
- half/quarter resolution trace
- blue noise texture
- cloud shadow map
- detail noise 叠加
- weather preset 系统
- 时间驱动的风场动画
- 与地表雾/降雨系统联动

## 18. 结论

对当前项目来说，最稳妥的体积云 MVP 方案不是直接重写天空，而是：

1. 复用已有 `Atmosphere` 的太阳、大气 LUT 和相机 km 坐标体系。
2. 新增一套独立的 `VolumetricCloud` 模块。
3. 用低分辨率 compute ray march 生成云层颜色与透射。
4. 通过单独的全屏合成 pass 叠加到当前相机颜色。
5. 先实现“单层球壳云 + 单次散射 + 深度裁剪 + 天空环境色耦合”这条最小闭环。

这条路线的优点很明确：

- 与现有架构兼容度高
- 跑通成本低
- 视觉结果足够证明方向正确
- 后续可以自然升级到真正可用的生产级体积云
