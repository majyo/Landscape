# 体积云 Jitter 与 Temporal Accumulation 分步骤实现工程文档

## 1. 文档目的

基于当前已经完成的体积云 MVP 渲染链，实现一套可执行、可验收、可追踪进度的 `jitter + temporal accumulation` 工程计划，用于降低低分辨率 raymarch 的噪点、闪烁和云内颗粒感。

本文档用于：

- 将“云时域稳定化”从想法拆成可分阶段落地的工程任务。
- 明确每个阶段的输入、输出、实现边界、验证方法和退出条件。
- 约束实现范围，避免直接滑向完整 TAA / motion vector 系统重构。
- 为后续迭代记录状态、风险、回归结果和下一步。

## 2. 适用范围

当前文档的实施前提固定为：

- Unity 6
- URP + RenderGraph
- 体积云 `Step 5` 已完成
- 云 trace 已并入 `AtmosphereRendererFeature`
- 当前云 trace 语义为 `RGB = scattering`、`A = transmittance`
- 当前云 composite 公式为 `sceneColor * transmittance + scattering`
- 当前项目未实现体积云专用 history RT、reprojection 或 accumulation pass

本文档只覆盖“体积云自身的时域稳定化”，不覆盖：

- 全局 TAA 系统接入
- Motion Vector 管线改造
- 云 shadow map
- 深度感知上采样
- 重新引入 2D weather map

## 3. 状态定义

为便于持续更新，统一使用以下状态：

| 状态 | 含义 |
| --- | --- |
| `未开始` | 尚未进入开发 |
| `进行中` | 已开始实现，但未满足退出条件 |
| `阻塞` | 因依赖或问题无法继续推进 |
| `待验证` | 开发已完成，等待运行验证 |
| `已完成` | 满足退出条件并完成验证 |

建议每次更新时同时记录：

- 日期
- 负责人
- 当前状态
- 本次完成
- 问题与风险
- 验证结果
- 下一步

## 4. 当前基线

当前云系统已经具备以下基础：

- 低分辨率 `trace RT`
- 单帧 raymarch 结果输出
- 场景深度裁剪
- `AtmosphereRendererFeature` 内部统一调度
- `OnGUI` overlay 调试
- 稳定可用的 `SampleScene`

当前主要画质问题与本计划直接相关的是：

- 相机缓慢移动时云边缘和内部存在明显噪点闪烁
- 相机位于云层内部时颗粒感偏强
- 低分辨率 trace 的采样误差以“时间闪烁”形式暴露，而非仅仅是空间模糊

## 5. 总体里程碑视图

| 里程碑 | 名称 | 目标 | 预计输出 | 状态 |
| --- | --- | --- | --- | --- |
| T0 | 基线确认 | 明确当前 trace、资源和 pass 接入点 | 依赖清单、实现边界 | `已完成` |
| T1 | Jitter 输入骨架 | 在 raymarch 端引入稳定可控的抖动采样 | 每帧 jitter 序列与参数链 | `未开始` |
| T2 | History 资源骨架 | 搭建 history RT 生命周期与双缓冲切换 | 当前帧/历史帧 RT 管理 | `未开始` |
| T3 | Temporal Accumulation 闭环 | 跑通 reprojection + 累积混合 | 更稳定的云 trace | `未开始` |
| T4 | Reject 与调试 | 补齐失效判定、重置策略和 overlay | 可追查的时域调试结果 | `未开始` |
| T5 | 验收与默认参数收口 | 固化默认参数并完成回归 | 稳定可回归的时域云版本 | `未开始` |

## 6. 设计到实现追踪矩阵

| 设计项 | 实现落点 | 验证方式 |
| --- | --- | --- |
| 视线采样 jitter | `VolumetricCloudParameters`、`VolumetricCloudRaymarch.compute` | 连续帧采样图案有变化，但世界空间云体不漂移 |
| 云 history RT | `VolumetricCloudResources` | 分辨率变化时 history 正确重建 |
| 时域累积 | 新增 cloud temporal compute / pass | 相机缓慢运动时闪烁下降 |
| Reprojection | 由前后帧相机参数重建 | 静止相机时结果逐步收敛 |
| History reject | 深度/高度/透射差异判定 | 大视角切换后不出现长时间拖影 |
| 调试可视化 | `VolumetricCloudController` overlay | 可观察 current / history / accumulation 权重 |

## 7. 建议目录与产物清单

建议沿用当前体积云模块目录：

```text
Assets/
  VolumetricClouds/
    Runtime/
      VolumetricCloudTemporalState.cs
      VolumetricCloudHistoryResources.cs
      VolumetricCloudParameters.cs
      VolumetricCloudResources.cs
      VolumetricCloudShaderIDs.cs
    Rendering/
      VolumetricCloudRenderPass.cs
      VolumetricCloudTemporalAccumulationPass.cs
      VolumetricCloudCompositePass.cs
    Shaders/
      VolumetricCloudCommon.hlsl
      VolumetricCloudRaymarch.compute
      VolumetricCloudTemporalAccumulation.compute
      VolumetricCloudComposite.shader
```

如果最终决定不拆单独 `TemporalAccumulationPass`，则允许把累积逻辑内联到现有 `VolumetricCloudRenderPass`，但必须保持职责清晰：

- raymarch 负责生成 current trace
- temporal pass 负责读取 history 并输出 stabilized trace

## 8. 分步骤实施方案

## Step 0. 基线确认与接入前检查

### 目标

明确 jitter 与时域累积要接在哪一层，避免直接把逻辑散落到 controller、pass 和 shader 多处而失控。

### 前置输入

- [plan-volumetric-cloud-mvp-implementation.md](C:\workspace\projects_with_ai_control\Landscape\Assets\Docs\plan-volumetric-cloud-mvp-implementation.md)
- 当前云运行时与渲染代码
- `SampleScene`

### 实施任务

- [x] 确认当前 `VolumetricCloudRenderPass` 的 trace 写入点
- [x] 确认 `VolumetricCloudResources` 当前仅管理单张 trace RT
- [x] 确认 `VolumetricCloudCompositePass` 只消费一张最终 trace RT
- [x] 确认当前是否已有可复用的 jitter / blue-noise / temporal 基础设施
- [x] 确认需要新增的最小相机历史参数集合

### 输出

- 时域稳定化接入点清单
- 资源改造清单
- 新增 shader/pass 的最小集合

### Step 0 结论（2026-04-18）

#### 时域稳定化接入点清单

- 当前 `VolumetricCloudRenderPass` 在 `RecordRenderGraph` 中导入 `VolumetricCloudController.TraceHandle`，并在 compute pass 内调用 `VolumetricCloudRaymarch.compute` 将结果直接写入该 RT；随后 `Bind Globals` pass 把同一张 RT 绑定为 `_VolumetricCloudTexture`。
- 当前 `VolumetricCloudResources` 仅持有一组持久资源：`traceTexture` + `traceHandle`。生命周期由 `VolumetricCloudController.TryPrepare -> EnsureTraceTarget` 驱动，只根据 `TraceWidth` / `TraceHeight` 与 `ResourceHash` 进行重建。
- 当前 `VolumetricCloudCompositePass` 只导入 `VolumetricCloudController.TraceHandle` 这一张云 trace，并按 `sceneColor * transmittance + scattering` 公式完成合成，不读取任何 history 或中间时域结果。
- 当前 `AtmosphereRendererFeature` 只串联 `VolumetricCloudRenderPass -> VolumetricCloudCompositePass`。因此 temporal accumulation 的安全插入点应位于两者之间，而不是写入 `VolumetricCloudController.OnGUI`、controller 回调或 raymarch compute 内部。

#### `current trace` / `history trace` / `stabilized trace` 职责

- `current trace`：当前帧 raymarch 的原始输出，语义保持 `RGB = scattering`、`A = transmittance`，不承担 history 读写职责。
- `history trace`：上一帧稳定化后的云结果，跨帧持久保存，仅供 temporal pass 做 reprojection 与混合读取。
- `stabilized trace`：当前帧 temporal accumulation 的输出，供现有 composite 直接消费，并作为下一帧的 history 来源。
- 结论：累积必须发生在单独 temporal pass 中，raymarch 只写 `current trace`，不能在 march 循环内直接混 history。

#### 资源改造清单

- 将 `VolumetricCloudResources` 从“单张 trace RT”扩展为“`current trace` + history 双缓冲 + 当前可供 composite 读取的 active trace 引用”。
- 将 history 生命周期集中在 `VolumetricCloudController` + `VolumetricCloudResources` 管理：controller 负责 history validity、相机历史参数缓存与 reset 触发，resources 负责 RT 创建、重建、交换和释放。
- 让 `VolumetricCloudCompositePass` 从“固定读取 `TraceHandle`”改为“读取 stabilized / active trace handle”；当 temporal 关闭或 history 无效时，允许直接回退到 `current trace`。

#### 新增 shader / pass 的最小集合

- 新增 `VolumetricCloudTemporalAccumulationPass`，职责仅限读取 `current trace` 与 `history trace`，输出 `stabilized trace`。
- 新增 `VolumetricCloudTemporalAccumulation.compute`，负责 reprojection 与 accumulation 混合。
- 扩展 `AtmosphereRendererFeature`，把 temporal pass 插入到 `VolumetricCloudRenderPass` 与 `VolumetricCloudCompositePass` 之间。
- 扩展 `VolumetricCloudParameters`、`VolumetricCloudShaderIDs` 与 `VolumetricCloudRaymarch.compute`，用于传递 jitter 参数；但 raymarch 仍只产出 `current trace`。

#### 可复用基础设施确认

- 项目当前确实存在 URP 级别的 TAA 设置和 blue-noise 资源引用，`SampleScene` 也序列化了 TAA 配置。
- 但在当前工程的 `Assets/VolumetricClouds` / `Assets/Atmosphere` 代码中，没有可直接复用的 Halton 序列、云 history RT、reprojection、temporal accumulation pass，或云专用 jitter 参数链。
- 结论：Step 1 到 Step 3 应以体积云模块自带实现为主，首版不依赖全局 TAA、motion vector 管线或 URP 内建 temporal 资源接线。

#### 最小相机历史参数集合

- 前一帧 `CameraPositionKm`
- 前一帧 `ViewBasisRight`
- 前一帧 `ViewBasisUp`
- 前一帧 `ViewBasisForward`
- 前一帧 `TanHalfVerticalFov`
- 前一帧 `AspectRatio`
- 前一帧 `TraceWidth` / `TraceHeight`
- `historyValid` 状态位，以及用于主动 reset 的关键参数 hash 快照

### 退出条件

- 能明确说清“current trace、history trace、stabilized trace”三者的职责
- 能明确 history 生命周期由谁管理
- 能明确在哪个 pass 做累积，而不是边 march 边累积

### 当前进度记录

| 项 | 内容 |
| --- | --- |
| 状态 | `已完成` |
| 负责人 | Codex |
| 开始日期 | 2026-04-18 |
| 完成日期 | 2026-04-18 |
| 备注 | 已确认 temporal 接入点位于 `VolumetricCloudRenderPass` 与 `VolumetricCloudCompositePass` 之间；当前项目没有体积云专用 history RT、reprojection 或 temporal accumulation 实现。 |

## Step 1. Jitter 输入骨架

### 目标

在不引入 history 的前提下，先把 raymarch 采样改成“可控抖动采样”，并保证 jitter 序列稳定、可复现、不会导致云体屏幕漂移。

### 实施任务

- [ ] 在 `VolumetricCloudParameters` 中新增 temporal 相关字段
- [ ] 增加 frame index / jitter index / jitter offset
- [ ] 在 `VolumetricCloudController` 或 pass 层维护稳定递增的 jitter 序列
- [ ] 首版使用固定 Halton(2,3) 或等价低差异序列
- [ ] 在 `VolumetricCloudRaymarch.compute` 中将 jitter 作用于 ray 起点或步进偏移
- [ ] 保持无 history 时也能独立运行和观察 jitter 结果

### 默认决策

- jitter 不直接改 Unity 相机投影矩阵
- jitter 只作用于云 trace 自身的 raymarch 采样
- 首版优先用“步进偏移 jitter”，避免改动屏幕空间到世界方向的主逻辑

### 输出

- 可复用的 jitter 参数链
- 带时间变化采样图案的单帧云 trace

### 验证方法

- 静止相机时，单帧 trace 图案应有轻微变化
- 世界空间云体不应随 jitter 出现整体滑动
- 关闭 jitter 后结果应能回退到当前 Step 5 行为

### 退出条件

- jitter 行为稳定且可开关
- 不引入新的屏幕空间裂缝、翻转或相交错误

### 当前进度记录

| 项 | 内容 |
| --- | --- |
| 状态 | `未开始` |
| 负责人 | Codex |
| 开始日期 | 待填写 |
| 完成日期 | 待填写 |
| 备注 | 待填写 |

## Step 2. History 资源骨架

### 目标

为 temporal accumulation 准备可稳定切换的 history 资源和相机历史参数缓存。

### 实施任务

- [ ] 扩展 `VolumetricCloudResources`，新增 history RT 双缓冲
- [ ] 明确 current trace / history trace / output trace 的命名和 ownership
- [ ] 增加 history 有效性状态位
- [ ] 缓存前一帧相机位置、方向、FOV、aspect、trace size
- [ ] 在分辨率变化、相机切换、PlayMode 重进时重置 history
- [ ] 在 profile 关键参数突变时允许主动丢弃 history

### 默认决策

- history RT 格式继续使用 `ARGBHalf`
- 首版不引入多层 history，不引入 history confidence RT
- 相机变化检测采用最小必要集合，不做复杂运动分类

### 输出

- 可读写的 history RT
- 稳定的历史相机参数缓存
- 明确的 history reset 规则

### 验证方法

- trace 尺寸变化时 history 正确重建
- 进入/退出 PlayMode 不报重复初始化或泄漏
- 切换到不兼容相机状态后不会继续使用旧 history

### 退出条件

- history 生命周期稳定
- history reset 条件明确且可调试

### 当前进度记录

| 项 | 内容 |
| --- | --- |
| 状态 | `未开始` |
| 负责人 | Codex |
| 开始日期 | 待填写 |
| 完成日期 | 待填写 |
| 备注 | 待填写 |

## Step 3. Temporal Accumulation 闭环

### 目标

在 history 和 jitter 已就绪的前提下，跑通最小可用的云时域累积闭环。

### 实施任务

- [ ] 新建 `VolumetricCloudTemporalAccumulation.compute`
- [ ] 读取 current trace
- [ ] 读取 history trace
- [ ] 使用当前/前一帧相机参数做 reprojection
- [ ] 将 reprojection 结果采样到 history UV
- [ ] 对 `scattering` 与 `transmittance` 分别混合
- [ ] 输出新的 stabilized trace
- [ ] 在帧尾交换 history buffer

### 推荐首版混合策略

```text
historyWeight = historyValid ? temporalResponse : 0
scatteringOut = lerp(currentScattering, historyScattering, historyWeight)
transmittanceOut = lerp(currentTransmittance, historyTransmittance, historyWeight)
```

首版建议：

- `temporalResponse = 0.85 ~ 0.95`
- 对 `scattering` 和 `transmittance` 使用相同权重，先验证稳定性
- 不在这一阶段引入 neighborhood clamp

### 输出

- 稳定化后的云 trace
- 可直接供现有 composite 使用的时域输出

### 验证方法

- 静止相机时，云噪点在若干帧后明显收敛
- 缓慢旋转和平移时，闪烁相比未开启 temporal 明显下降
- 不应出现整屏大面积拖影或反向残影

### 退出条件

- temporal accumulation 主链可跑通
- 现有 composite 无需改公式即可消费结果

### 当前进度记录

| 项 | 内容 |
| --- | --- |
| 状态 | `未开始` |
| 负责人 | Codex |
| 开始日期 | 待填写 |
| 完成日期 | 待填写 |
| 备注 | 待填写 |

## Step 4. Reject、重置与调试可视化

### 目标

补齐时域算法必须具备的失效判定和调试入口，避免靠“调低权重”掩盖拖影根因。

### 实施任务

- [ ] 加入 history validity / reset 标志
- [ ] 当相机跳变、FOV 变化、trace 尺寸变化时丢弃 history
- [ ] 增加基于深度、云层高度或透射差异的最小 reject 条件
- [ ] 为 overlay 增加 temporal 调试模式
- [ ] 至少支持观察 `Current / History / Accumulated / HistoryWeight`

### 默认决策

- 首版 reject 以“安全为先”，宁可多 reset，也不要长拖影
- overlay 只做调试，不改变最终输出语义

### 输出

- 明确的 history reject 策略
- 可追踪 temporal 行为的 debug overlay

### 验证方法

- 大角度快速转头后，旧云影像不会长时间挂在屏幕上
- 参数突变后，history 能及时失效
- overlay 可帮助区分“reprojection 错误”与“权重过高”

### 退出条件

- temporal 链路具备基本可调试性
- 常见失效路径都有安全 reset

### 当前进度记录

| 项 | 内容 |
| --- | --- |
| 状态 | `未开始` |
| 负责人 | Codex |
| 开始日期 | 待填写 |
| 完成日期 | 待填写 |
| 备注 | 待填写 |

## Step 5. 验收与默认参数收口

### 目标

在 `SampleScene` 中完成云时域稳定化的收口，固化默认参数与回归方式。

### 实施任务

- [ ] 选定默认 `temporalResponse`
- [ ] 选定默认 jitter 序列长度
- [ ] 补齐 playmode 进入/退出与相机移动回归
- [ ] 更新相关工程文档和风险项
- [ ] 记录一组推荐截图机位与观察动作

### 输出

- 默认可用的 temporal cloud 参数
- 可复用的场景回归基线

### 验证方法

- `SampleScene` 中慢速旋转和移动时，云闪烁显著减弱
- 快速运动后不会出现不可接受的残影
- `dotnet build Assembly-CSharp.csproj` 通过，0 error、0 warning

### 退出条件

- 当前版本可作为后续 depth-aware upsample 或 cloud shadow 的稳定基础

### 当前进度记录

| 项 | 内容 |
| --- | --- |
| 状态 | `未开始` |
| 负责人 | Codex |
| 开始日期 | 待填写 |
| 完成日期 | 待填写 |
| 备注 | 待填写 |

## 9. 验收清单

以下条目全部满足后，方可将本计划标记为 `已完成`：

- [ ] 云 trace 已引入 jitter，且行为可开关
- [ ] history RT 生命周期稳定
- [ ] temporal accumulation 与现有 composite 成功闭环
- [ ] 缓慢相机运动时，云闪烁明显下降
- [ ] 快速运动或参数突变时，无明显长拖影
- [ ] overlay 能观察 temporal 调试结果
- [ ] `dotnet build Assembly-CSharp.csproj` 通过

## 10. 回归测试建议

### 场景回归

- [ ] `SampleScene` 正午地面仰视
- [ ] `SampleScene` 低太阳角
- [ ] 相机位于云层内部
- [ ] 相机位于云层上方俯视

### 操作回归

- [ ] 相机静止 3 到 5 秒观察收敛
- [ ] 相机慢速平移
- [ ] 相机慢速旋转
- [ ] 相机快速甩动后回看

### 参数回归

- [ ] `cloudCoverage` 调整
- [ ] `densityMultiplier` 调整
- [ ] `stepCount` 调整
- [ ] temporal 权重调整
- [ ] jitter 开关切换

## 11. 风险登记表

| 风险编号 | 问题 | 影响 | 应对策略 | 状态 |
| --- | --- | --- | --- | --- |
| T-R1 | reprojection 不准确 | 出现拖影或双影 | 首版使用更保守的 history reject | `开放` |
| T-R2 | history reset 不充分 | 参数改动后残留旧云 | 将关键参数 hash 纳入 reset 条件 | `开放` |
| T-R3 | temporal 权重过高 | 画面发黏，响应过慢 | 首版使用保守默认值并暴露调参口 | `开放` |
| T-R4 | jitter 方式选错 | 引入新的屏幕空间伪影 | 首版只抖动步进偏移，不抖投影矩阵 | `开放` |
| T-R5 | 低分辨率 trace 与 reprojection 不匹配 | 历史采样错位 | 先保证 trace 空间稳定，再做邻域优化 | `开放` |

## 12. 当前推荐执行顺序

建议严格按以下顺序推进：

1. Step 0 基线确认
2. Step 1 Jitter 输入骨架
3. Step 2 History 资源骨架
4. Step 3 Temporal Accumulation 闭环
5. Step 4 Reject、重置与调试
6. Step 5 验收与参数收口

该顺序的核心原则是：

- 先让采样在时间上“有变化”
- 再让历史“可保存”
- 再让历史“可复用”
- 最后再处理 reject 和体验细节

## 13. 实施记录模板

建议后续每次推进时按下面模板追加更新：

```text
日期：
负责人：
阶段：
状态：

本次完成：
- 

发现问题：
- 

验证结果：
- 

下一步：
- 
```

## 14. 当前进度摘要

截至 2026-04-18：

- 文档已创建
- `Step 0 基线确认与接入前检查` 已完成
- 当前状态：`T0 已完成，Step 1 未开始`
- 已确认现有云链满足接入 jitter 与 temporal accumulation 的前提，且 temporal pass 应插入在 `VolumetricCloudRenderPass` 与 `VolumetricCloudCompositePass` 之间
- 下一步建议直接进入 `Step 1 Jitter 输入骨架`
