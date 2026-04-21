# Phase1 实现方案

## 1. 目标与边界

### 目标
基于 [plan-summary.md](C:\workspace\projects_with_ai_control\Landscape\Assets\Docs\plan-summary.md) 和 [plan-phase1.md](C:\workspace\projects_with_ai_control\Landscape\Assets\Docs\plan-phase1.md)，在当前 Unity 6 + URP 17 项目中落地 **Pass 1: Transmittance LUT**，为后续的多重散射、天空视图和体积雾提供统一基础数据。

### Phase1 只做这些
- 生成一张 `256 x 64` 的 Transmittance LUT。
- 支持通过 C# 参数驱动 LUT 重建。
- 将 LUT 作为全局纹理暴露给后续 shader/pass。
- 提供最小可用的调试显示，能确认 LUT 数值与分布正确。

### Phase1 不做这些
- 不实现 Multi-scattering LUT。
- 不实现 Sky-View LUT。
- 不实现 Aerial Perspective 3D LUT。
- 不做最终天空盒和场景雾效合成。
- 不接入 Volume 系统，先避免过早引入编辑器和运行时复杂度。

## 2. 项目约束与设计决策

### 当前项目上下文
- Unity 版本：`6000.3.0a2`
- 渲染管线：`URP 17.3.0`
- `PC` 质量档使用延迟渲染。
- `Mobile` 质量档使用前向渲染。
- 当前仓库没有自定义渲染功能代码，适合从零搭建最小闭环。

### 核心设计决策
1. **Phase1 使用 Compute Shader 作为主路径。**
   原因是 Transmittance LUT 是规则二维离线式计算，Compute 更直接，后续扩展到 Phase2/3/4 也能复用同一资源调度方式。

2. **URP 接入优先用 ScriptableRendererFeature + ScriptableRenderPass 兼容路径。**
   不先绑定 RenderGraph。原因是项目当前还没有任何自定义 pass，Phase1 需要先把资源生命周期、调试和参数更新跑通。后续 4 个 LUT pass 全部落地后，再统一评估是否迁移到 RenderGraph。

3. **所有物理计算统一用 km。**
   Unity 场景仍然用米，但传给 shader 前统一换算成 km，避免指数计算精度问题。

4. **Transmittance LUT 采用“脏标记重建”，不按帧更新。**
   该 LUT 只依赖大气参数和行星几何，不依赖太阳方向和相机位置。只要参数没变，就直接复用。

5. **先做 Profile 驱动，不做 Volume Override。**
   这样可以先把渲染基础打牢，后续如果需要在不同区域混合大气参数，再把 `AtmosphereProfile` 映射到 Volume 体系。

## 3. 推荐目录结构

```text
Assets/
  Atmosphere/
    Runtime/
      AtmosphereProfile.cs
      AtmosphereController.cs
      AtmosphereShaderIDs.cs
      AtmosphereParameters.cs
      AtmosphereLutManager.cs
    Rendering/
      AtmosphereRendererFeature.cs
      AtmosphereTransmittancePass.cs
    Shaders/
      AtmosphereCommon.hlsl
      AtmosphereTransmittance.compute
      AtmosphereDebugLut.shader
    Resources/
      AtmosphereProfile_Earth.asset
```

如果你希望保持 `Assets` 根目录更干净，也可以把上面整套内容放进 `Assets/Features/Atmosphere/`，但目录职责不要混。

## 4. 模块职责拆分

### `AtmosphereProfile.cs`
`ScriptableObject`，负责承载静态和半静态参数。

建议字段：
- `groundRadiusKm = 6360.0f`
- `topRadiusKm = 6460.0f`
- `rayleighScattering`
- `rayleighScaleHeightKm = 8.0f`
- `mieScattering`
- `mieAbsorption`
- `mieScaleHeightKm = 1.2f`
- `ozoneAbsorption`
- `ozoneLayerCenterKm = 25.0f`
- `ozoneLayerHalfWidthKm = 15.0f`
- `transmittanceWidth = 256`
- `transmittanceHeight = 64`
- `transmittanceSteps = 40`

作用：
- 作为编辑器侧唯一真源。
- 参数改动后触发脏标记。
- 后续 Phase2/3/4 继续复用。

### `AtmosphereParameters.cs`
纯数据结构，用于把 `AtmosphereProfile` 转成 shader 常量。

建议职责：
- 做单位换算。
- 整理成 `Vector4`/`float` 友好的 GPU 参数块。
- 提供 `GetHash()`，用于判断 LUT 是否需要重建。

### `AtmosphereShaderIDs.cs`
集中管理属性 ID，避免字符串散落。

至少包含：
- `_AtmosphereTransmittanceLut`
- `_AtmosphereGroundRadiusKm`
- `_AtmosphereTopRadiusKm`
- `_AtmosphereRayleighScattering`
- `_AtmosphereMieScattering`
- `_AtmosphereMieAbsorption`
- `_AtmosphereOzoneAbsorption`
- `_AtmosphereScaleHeights`
- `_AtmosphereOzoneLayer`
- `_AtmosphereTransmittanceSize`
- `_AtmosphereTransmittanceSteps`

### `AtmosphereController.cs`
挂在场景中的运行时入口。

建议职责：
- 持有 `AtmosphereProfile`
- 绑定主方向光
- 监听参数变化并设置 dirty
- 把运行时依赖注册给 `RendererFeature`

Phase1 中它不需要管相机，不需要参与每帧逻辑，只要负责“参数源”和“重建开关”。

### `AtmosphereLutManager.cs`
负责 LUT 纹理生命周期。

建议职责：
- 创建/释放 `RenderTexture`
- 确保格式为 `RenderTextureFormat.ARGBHalf`
- 设置 `enableRandomWrite = true`
- 参数变化时重建尺寸
- 将纹理注册为全局纹理

### `AtmosphereRendererFeature.cs`
URP 接入点。

建议职责：
- 创建并持有 `AtmosphereTransmittancePass`
- 从 `AtmosphereController` 读取运行时状态
- 在 pass 可执行时入队

### `AtmosphereTransmittancePass.cs`
真正调度 Compute Shader 的地方。

建议职责：
- 在 `Execute` 中设置常量
- 绑定输出 RT
- `DispatchCompute`
- 更新全局纹理 `_AtmosphereTransmittanceLut`

推荐执行时机：
- `RenderPassEvent.BeforeRenderingSkybox`

原因：
- 该时机足够早，后续天空和雾效都能安全使用。
- 对前向和延迟路径都相对中立。

## 5. Shader 设计

### `AtmosphereCommon.hlsl`
存放公共函数，避免后续多重散射和天空视图重复实现。

建议先抽出这些函数：
- `float RaySphereIntersectNearest(...)`
- `float RaySphereIntersectFar(...)`
- `float GetAtmosphereHeightKm(float3 positionKm, float groundRadiusKm)`
- `float GetRayleighDensity(float heightKm, float scaleHeightKm)`
- `float GetMieDensity(float heightKm, float scaleHeightKm)`
- `float GetOzoneDensity(float heightKm, float centerKm, float halfWidthKm)`
- `float3 GetExtinction(...)`

### `AtmosphereTransmittance.compute`
Phase1 的核心 shader。

推荐内核设置：
- `numthreads(8, 8, 1)`
- Dispatch 维度：`ceil(width / 8), ceil(height / 8), 1`

核心流程：
1. 从 `SV_DispatchThreadID.xy` 转成 `uv`
2. 由 `uv` 映射到 `r` 和 `mu`
3. 计算射线与 `R_top` / `R_ground` 的交点
4. 以 `40` 步中点积分累加 optical depth
5. `transmittance = exp(-opticalDepth)`
6. 写入 `RWTexture2D<float4>`

### UV 参数化建议
Phase1 先用线性版本，便于验证：

```hlsl
float u = (dispatchId.x + 0.5) / width;
float v = (dispatchId.y + 0.5) / height;

float r = lerp(_GroundRadiusKm, _TopRadiusKm, v);
float mu = lerp(-1.0, 1.0, u);
```

这样实现简单、可读、易对照论文阶段文档。等 Phase1 数值闭环稳定后，再考虑把 `mu` 改成地平线增强映射。

## 6. CPU 到 GPU 的数据流

### 运行链路
1. `AtmosphereController` 持有 `AtmosphereProfile`
2. Profile 变化后更新 `AtmosphereParameters`
3. `AtmosphereLutManager` 检查 RT 是否存在且尺寸匹配
4. `AtmosphereRendererFeature` 将 pass 入队
5. `AtmosphereTransmittancePass` 发现 dirty 时执行 Compute
6. 结果写入全局纹理 `_AtmosphereTransmittanceLut`
7. 调试材质或后续 pass 直接采样该纹理

### Dirty 条件
以下情况必须重建 LUT：
- Profile 引用变更
- Profile 参数变更
- LUT 分辨率变更
- Compute Shader 重新加载
- 进入 Play Mode
- 设备切换导致 RT 丢失

以下情况不需要重建：
- 相机移动
- 太阳旋转
- 普通每帧渲染

## 7. 调试与验证方案

Phase1 如果没有调试路径，后面所有 pass 都会建立在不可信的基础上，所以必须先做可视化。

### 调试能力 1：直接显示 LUT
新增一个简单的 `AtmosphereDebugLut.shader`，把 `_AtmosphereTransmittanceLut` 直接画到屏幕角落或 UI RawImage。

验证点：
- 顶部区域透射率应接近 `1`
- 接近地平线应明显变暗
- RGB 通道应存在分离，蓝光衰减趋势与红光不同

### 调试能力 2：参数日志
在编辑器模式下输出一次：
- 纹理分辨率
- 步进数
- 格式
- 当前参数哈希
- 是否支持 Compute Shader

### 调试能力 3：异常值保护
在 compute 中加入最小保护：
- 高度 `max(0, h)`
- 交点无效时直接写 `1`
- 输出前 `saturate` 或至少避免 NaN 扩散

## 8. 里程碑拆分

### Milestone A：数据与资源骨架
- 建立 `AtmosphereProfile`
- 建立 `AtmosphereParameters`
- 建立 `AtmosphereShaderIDs`
- 建立 `AtmosphereLutManager`

交付结果：
- 可在场景中配置一份 Earth Profile
- 可创建并维持一张 `ARGBHalf` RenderTexture

### Milestone B：Compute Shader 跑通
- 实现 `AtmosphereCommon.hlsl`
- 实现 `AtmosphereTransmittance.compute`
- 在编辑器里生成第一张 LUT

交付结果：
- RenderTexture 内有稳定数值，不是全黑、全白或 NaN

### Milestone C：URP 接入
- 实现 `AtmosphereRendererFeature`
- 实现 `AtmosphereTransmittancePass`
- 把 LUT 注册为全局纹理

交付结果：
- 运行游戏时自动生成 LUT
- Profile 参数改动后自动重建

### Milestone D：调试闭环
- 实现 `AtmosphereDebugLut.shader`
- 场景中增加一个调试入口
- 增加基本日志和保护

交付结果：
- 可以肉眼验证 LUT 是否合理
- 能快速定位参数或精度问题

## 9. 验收标准

Phase1 完成后，至少满足以下条件：

1. 打开 `SampleScene` 后，运行时可稳定生成一张 `256 x 64` 的 Transmittance LUT。
2. 修改 `AtmosphereProfile` 中任意大气参数后，LUT 会自动重建。
3. 主纹理不会每帧重复重算。
4. 调试视图中，地平线区域和高空区域呈现明显透射率差异。
5. 在 `PC` 质量档下稳定运行。
6. 在支持 Compute Shader 的移动设备或移动模拟环境下不报错。

## 10. 风险与预留点

### 风险 1：移动端 Compute 支持不一致
处理策略：
- Phase1 先以支持 Compute 的设备为目标。
- 运行时检查 `SystemInfo.supportsComputeShaders`。
- 不支持时先禁用该系统并输出明确日志。
- 真正的 fragment fallback 放到后续阶段，不塞进 Phase1。

### 风险 2：线性参数化地平线精度不足
处理策略：
- Phase1 先接受线性映射。
- 如果 LUT 地平线附近出现带状伪影，再切 Bruneton 风格或平方根映射。

### 风险 3：世界尺度与物理尺度混淆
处理策略：
- 场景坐标全部按米处理。
- 大气模型内部一律转成 km。
- 任何进入指数函数的长度都不得直接使用 Unity 世界米值。

## 11. 建议的实施顺序

建议按下面顺序开发，而不是先急着接天空盒：

1. 先把 `AtmosphereProfile` 和 `RenderTexture` 生命周期建好。
2. 再让 Compute Shader 单独把 LUT 写出来。
3. 确认 LUT 可视化正确后，再接入 URP pass。
4. 确认 dirty rebuild 机制正常后，再开始 Phase2。

这样做的原因很直接：如果 Phase1 的 LUT 生成链路不稳定，后面的多重散射和天空视图只会把错误放大，调试成本会失控。

## 12. 对后续阶段的接口预留

Phase1 完成时，建议顺手预留以下接口，但先不实现：
- `_AtmosphereMultiScatteringLut`
- `_AtmosphereSkyViewLut`
- `_AtmosphereAerialPerspectiveLut`
- `AtmospherePassContext`

这样 Phase2/3/4 接入时，不需要再回头重构资源命名和 Feature 框架。
