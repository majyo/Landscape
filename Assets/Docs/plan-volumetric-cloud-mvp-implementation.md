# 体积云 MVP 分步骤实现工程文档

## 1. 文档目的

基于 [plan-volumetric-cloud-mvp.md](C:\workspace\projects_with_ai_control\Landscape\Assets\Docs\plan-volumetric-cloud-mvp.md)，将体积云 MVP 从“设计方案”落为“可执行、可验收、可追踪进度”的工程实施文档。

本文档用于：

- 拆解体积云 MVP 的实际工程落地顺序。
- 为每个阶段定义输入、输出、实施任务、验证方法和完成标准。
- 建立设计决策到实现任务的追踪关系，避免开发过程中偏离 MVP 边界。
- 为后续日报、周报、PR、联调和回归提供统一状态记录模板。

## 2. 适用范围

适用项目上下文与设计前提沿用原方案：

- Unity 6
- URP
- 现有 `Atmosphere` 渲染框架
- 复用 `AtmosphereRendererFeature`
- 体积云作为独立模块接入
- MVP 采用单层球壳体积云
- MVP 采用低分辨率 Compute Shader Ray March

## 3. 状态定义

为便于多人协作和持续更新，本文档统一使用以下状态：

| 状态 | 含义 |
| --- | --- |
| `未开始` | 尚未进入开发 |
| `进行中` | 已开始实现，但未满足退出条件 |
| `阻塞` | 因依赖、问题或设计未决无法继续推进 |
| `待验证` | 开发已完成，等待场景验证或代码评审 |
| `已完成` | 满足退出条件并完成验证 |

建议每次更新任务时同时记录：

- 日期
- 负责人
- 当前状态
- 实际结果
- 问题与风险
- 下一步

## 4. 总体里程碑视图

| 里程碑 | 名称 | 目标 | 预计输出 | 状态 |
| --- | --- | --- | --- | --- |
| M0 | 基线确认 | 明确现有大气系统接入点和资源依赖 | 接入清单、实现边界、默认参数草案 | `未开始` |
| M1 | 参数与资源骨架 | 搭建体积云运行时数据结构和 RT 生命周期 | Profile / Controller / Parameters / ShaderIDs / Resources | `未开始` |
| M2 | 无光照密度闭环 | 跑通云层相交与密度可视化 | 低分辨率灰度云图 | `未开始` |
| M3 | 基础光照闭环 | 跑通单次散射、太阳透射和环境光 | 有明暗关系的云图 | `未开始` |
| M4 | 深度裁剪与合成 | 云结果正确叠加到场景颜色 | 云层与前景深度关系正确 | `未开始` |
| M5 | Atmosphere 集成与验收 | 接入现有渲染链并完成 SampleScene 验收 | 项目内稳定可运行的 MVP | `未开始` |

## 5. 设计到实现追踪矩阵

| 设计项 | 原方案位置 | 实现落点 | 验证方式 |
| --- | --- | --- | --- |
| 单层球壳云层 | 原文第 3 节 决策 1 | `VolumetricCloudProfile`、Ray/Sphere 求交 | 相机上下穿越云层时交界稳定 |
| 单次散射 + Beer-Lambert | 原文第 3 节 决策 2 | `VolumetricCloudRaymarch.compute` | 朝阳面更亮、背光面更暗 |
| 全屏合成 | 原文第 3 节 决策 3 | `VolumetricCloudCompositePass` | 云既能覆盖天空也能出现在远景上空 |
| 2D Weather + 3D Shape Noise | 原文第 3 节 决策 4 | Weather Map / Shape Noise 采样 | 调整覆盖率和噪声后云团分布变化符合预期 |
| 允许先用程序噪声 | 原文第 3 节 决策 5 | 默认天气图与默认 3D 噪声资源 | 无额外美术资源也可跑通 |
| 低分辨率 Compute Trace | 原文第 11、12 节 | Cloud Trace RT + Compute Dispatch | `960x540` 默认配置可运行 |
| 与 Atmosphere 耦合 | 原文第 9 节 | 复用 LUT、太阳方向、相机 km 坐标 | 云色调与天空一致 |
| 深度裁剪 | 原文第 10 节 | 深度读取与 ray 终点裁剪 | 山体不被错误覆盖 |

## 6. 建议目录与产物清单

建议沿用原方案的模块拆分：

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
```

若最终并入 `Assets/Atmosphere/Clouds/`，必须保证职责边界不变，避免将云参数直接塞入 `AtmosphereProfile`。

## 7. 分步骤实施方案

## Step 0. 基线确认与接入前检查

### 目标

确认体积云 MVP 接入时必须复用的现有系统、资源与执行顺序，避免进入编码后反复返工。

### 前置输入

- [plan-volumetric-cloud-mvp.md](C:\workspace\projects_with_ai_control\Landscape\Assets\Docs\plan-volumetric-cloud-mvp.md)
- 当前 `Atmosphere` Runtime / Rendering / Shaders 代码
- `SampleScene`

### 实施任务

- [ ] 确认 `AtmosphereRendererFeature` 当前 pass 顺序和注入点。
- [ ] 确认 `AtmosphereController` 能稳定提供太阳方向、太阳亮度、相机 km 坐标。
- [ ] 确认 `_AtmosphereTransmittanceLut`、`_AtmosphereSkyViewLut` 的全局绑定方式。
- [ ] 确认 `SampleScene` 中主光、天空与相机控制链路正常。
- [ ] 确认深度纹理可被体积云 pass 读取。
- [ ] 确认默认云资源采用“程序生成”还是“仓库内预置贴图”。

### 输出

- 体积云接入依赖清单
- 默认执行顺序确认结果
- 默认资源方案确认结果

### 退出条件

- 能明确写出云模块对 `Atmosphere` 的只读依赖。
- 能明确 `Volumetric Clouds` 与 `Aerial Composite` 的先后顺序。
- 能确认没有额外阻塞体积云 MVP 的基础管线问题。

### 当前进度记录

| 项 | 内容 |
| --- | --- |
| 状态 | `未开始` |
| 负责人 | 待填写 |
| 开始日期 | 待填写 |
| 完成日期 | 待填写 |
| 备注 | 待填写 |

## Step 1. 参数与资源骨架

### 目标

先搭出一套独立的云模块运行骨架，使项目在不实现完整 ray march 的前提下，也能稳定创建和管理云 RT、参数对象及资源引用。

### 实施任务

- [ ] 新建 `VolumetricCloudProfile.cs`
- [ ] 新建 `VolumetricCloudParameters.cs`
- [ ] 新建 `VolumetricCloudShaderIDs.cs`
- [ ] 新建 `VolumetricCloudController.cs`
- [ ] 新建 `VolumetricCloudResources.cs`
- [ ] 为默认参数建立 `VolumetricCloudProfile_Default.asset`
- [ ] 定义默认输出 RT：`RGBA16F`
- [ ] 实现 trace RT 的创建、尺寸变更和释放逻辑
- [ ] 将天气图、3D 噪声、风场参数、步数等统一映射为 shader 常量

### 推荐默认参数

| 参数 | 默认值 |
| --- | --- |
| `cloudBottomHeightKm` | `1.5` |
| `cloudTopHeightKm` | `4.0` |
| `cloudCoverage` | `0.45` |
| `densityMultiplier` | `0.04` |
| `lightAbsorption` | `1.0` |
| `ambientStrength` | `0.35` |
| `forwardScatteringG` | `0.55` |
| `stepCount` | `48` |
| `shadowStepCount` | `8` |
| `maxRenderDistanceKm` | `64.0` |
| `traceWidth` | `960` |
| `traceHeight` | `540` |

### 输出

- 独立云模块代码骨架
- 默认云配置资源
- 可被全局访问的云输出 RT

### 验证方法

- 在运行时稳定创建低分辨率云 RT。
- 修改 `Profile` 参数后，资源能正确标记 dirty 或重建。
- 不开启 ray march 时，模块不会报空引用或生命周期错误。

### 退出条件

- `Profile -> Parameters -> Shader 常量 -> RT 资源` 链路跑通。
- 在场景加载、切换 PlayMode、修改参数时无明显资源泄漏或重复创建。

### 当前进度记录

| 项 | 内容 |
| --- | --- |
| 状态 | `未开始` |
| 负责人 | 待填写 |
| 开始日期 | 待填写 |
| 完成日期 | 待填写 |
| 风险 | 待填写 |

## Step 2. 无光照密度可视化闭环

### 目标

在不引入太阳透射和环境光的情况下，先验证空间相交、密度函数、云层范围和噪声采样是否正确。

### 实施任务

- [ ] 新建 `VolumetricCloudCommon.hlsl`
- [ ] 实现球壳求交辅助函数
- [ ] 新建 `VolumetricCloudRaymarch.compute`
- [ ] 每像素生成世界空间视线
- [ ] 处理相机在云层下、云层内、云层上的三类情况
- [ ] 采样 `Weather Map`
- [ ] 采样 `Shape Noise`
- [ ] 实现 `verticalProfile`
- [ ] 输出 `density grayscale`
- [ ] 新建最小调试显示路径，便于直接观察 trace 结果

### 推荐首版密度函数

```text
baseCoverage = saturate((weatherMap - (1 - cloudCoverage)) / max(cloudCoverage, 1e-3))
verticalProfile = smoothstep(0.0, 0.15, height01) * (1.0 - smoothstep(0.7, 1.0, height01))
shape = saturate(shapeNoise * 1.2 - 0.2)
density = baseCoverage * verticalProfile * shape * densityMultiplier
```

### 输出

- 低分辨率灰度云图
- 能证明云体位于世界空间中的调试结果

### 验证方法

- 平移和旋转相机时，云层不应随屏幕滑动。
- 调整 `cloudCoverage` 时，云团密度分布应有明显变化。
- 调整 `cloudBottomHeightKm` / `cloudTopHeightKm` 时，云层高度应变化明确。

### 退出条件

- 云层位置正确。
- 覆盖率、厚度、风向等参数行为直观。
- 相机穿越云层时不会出现明显的相交逻辑错误。

### 当前进度记录

| 项 | 内容 |
| --- | --- |
| 状态 | `未开始` |
| 负责人 | 待填写 |
| 开始日期 | 待填写 |
| 完成日期 | 待填写 |
| 问题记录 | 待填写 |

## Step 3. 太阳光照与基础体积感

### 目标

在密度闭环基础上加入最小可用的受光模型，使体积云具备向光面、背光面和基本氛围色。

### 实施任务

- [ ] 复用 `_AtmosphereSunDirection`
- [ ] 复用 `_AtmosphereSunIlluminance`
- [ ] 接入 `_AtmosphereTransmittanceLut`
- [ ] 接入 `_AtmosphereSkyViewLut`
- [ ] 实现沿太阳方向的短程 `shadow march`
- [ ] 实现简化相位函数
- [ ] 实现 `Beer-Lambert` 视线透射累积
- [ ] 实现云环境光近似采样
- [ ] 输出 `RGB = cloudScattering`、`A = cloudTransmittance`

### 输出

- 有基础受光方向变化的云结果纹理

### 验证方法

- 朝向太阳的云边缘更亮。
- 背光部分更暗且不会完全死黑。
- 早晚太阳角度变化时，云环境色能随天空变化。

### 退出条件

- 单次散射模型稳定。
- 大气与云的主光方向一致。
- 云色调与当前天空环境不脱节。

### 当前进度记录

| 项 | 内容 |
| --- | --- |
| 状态 | `未开始` |
| 负责人 | 待填写 |
| 开始日期 | 待填写 |
| 完成日期 | 待填写 |
| 风险 | 待填写 |

## Step 4. 深度裁剪与场景合成

### 目标

让体积云结果正确叠加到场景颜色，并具备基础深度关系，不再无脑覆盖前景几何体。

### 实施任务

- [ ] 新建 `VolumetricCloudComposite.shader`
- [ ] 新建 `VolumetricCloudCompositePass.cs`
- [ ] 读取相机颜色
- [ ] 读取 `_VolumetricCloudTexture`
- [ ] 读取场景深度
- [ ] 根据场景深度裁剪 ray march 终点
- [ ] 实现合成公式 `finalColor = sceneColor * cloudTransmittance + cloudScattering`
- [ ] 处理无交点像素输出 `rgb = 0, a = 1`

### 输出

- 合成后的场景画面
- 前景遮挡基础正确的体积云结果

### 验证方法

- 山体在云前方时，云不会错误盖到山体表面。
- 天空区域仍可正常显示云层。
- 低分辨率 trace 与全分辨率 composite 在视觉上保持一致。

### 退出条件

- 场景深度裁剪已生效。
- `SampleScene` 中能同时观察天空云和远景云。
- 合成结果没有明显的纯黑边或 alpha 反相问题。

### 当前进度记录

| 项 | 内容 |
| --- | --- |
| 状态 | `未开始` |
| 负责人 | 待填写 |
| 开始日期 | 待填写 |
| 完成日期 | 待填写 |
| 风险 | 待填写 |

## Step 5. 接入 Atmosphere 渲染链

### 目标

把体积云正式并入现有 `AtmosphereRendererFeature`，形成项目内一致的执行链。

### 实施任务

- [ ] 在 `AtmosphereRendererFeature` 中注册 `VolumetricCloudRenderPass`
- [ ] 在 `AtmosphereRendererFeature` 中注册 `VolumetricCloudCompositePass`
- [ ] 明确 pass 顺序为：

```text
1. Transmittance
2. Multi-scattering
3. Sky-View
4. Aerial Perspective
5. Volumetric Clouds
6. Volumetric Cloud Composite
7. Aerial Composite
```

- [ ] 补齐全局参数绑定
- [ ] 补齐调试开关与最小日志输出
- [ ] 确认 PC 质量档下能稳定运行
- [ ] 确认与现有大气天空、地景雾效无明显冲突

### 输出

- 并入现有大气框架的体积云 MVP

### 验证方法

- `SampleScene` 中完整渲染链可正常跑通。
- 启停体积云模块不会破坏现有 `Atmosphere` 输出。
- 云在 `Aerial Composite` 前执行时，远景氛围层次合理。

### 退出条件

- 当前项目结构内集成完成。
- 默认场景可长期作为体积云回归样例。

### 当前进度记录

| 项 | 内容 |
| --- | --- |
| 状态 | `未开始` |
| 负责人 | 待填写 |
| 开始日期 | 待填写 |
| 完成日期 | 待填写 |
| 备注 | 待填写 |

## 8. 联调与验收清单

以下条目全部满足后，方可将体积云 MVP 标记为 `已完成`：

- [ ] 打开 `SampleScene` 后天空中可见体积云，而不是纯贴图天空。
- [ ] 相机平移与旋转时，云层在世界空间中稳定。
- [ ] 云层具备基础受光方向变化。
- [ ] 云层颜色与当前大气天空一致。
- [ ] 远山或前景几何体不会被云完全错误覆盖。
- [ ] 调整覆盖率、厚度、密度、风向后，画面变化可预期。
- [ ] `PC` 质量档下能稳定跑通。

## 9. 回归测试建议

每完成一个里程碑，至少执行以下回归：

### 场景回归

- [ ] `SampleScene` 白天正午
- [ ] `SampleScene` 低太阳高度角
- [ ] 相机位于云层下方
- [ ] 相机穿入云层内部
- [ ] 相机位于云层上方俯视

### 参数回归

- [ ] `cloudCoverage` 从低到高
- [ ] `densityMultiplier` 从低到高
- [ ] `cloudBottomHeightKm` / `cloudTopHeightKm` 调整
- [ ] `windDirection` / `windSpeedKmPerSecond` 调整
- [ ] `stepCount` / `shadowStepCount` 调整

### 结果回归

- [ ] 无 NaN / 闪烁 / 全屏黑白异常
- [ ] 无明显资源泄漏
- [ ] 停止 PlayMode 后 RT 正常释放

## 10. 风险登记表

| 风险编号 | 问题 | 影响 | 应对策略 | 状态 |
| --- | --- | --- | --- | --- |
| R1 | 低分辨率 trace 导致边缘发糊 | 云与山体交界质量下降 | MVP 接受，后续加深度感知上采样 | `开放` |
| R2 | 太阳附近高光过曝 | 云高亮区域不稳定 | 压低 `sunIlluminance`，对 phase 做 soft clamp | `开放` |
| R3 | 云底和云顶过硬 | 体积感不足 | 调整 `verticalProfile`，避免 coverage 硬裁切 | `开放` |
| R4 | 相机在云内噪声明显 | 画面颗粒感强 | MVP 接受，后续引入 jitter + TAA | `开放` |
| R5 | 云和大气曝光链不一致 | 整体画面割裂 | 强制复用 Atmosphere 太阳和天空环境色 | `开放` |

## 11. 未来扩展预留

以下内容不属于本次 MVP 交付范围，但在实现时应避免阻断后续扩展：

- [ ] history RT
- [ ] half / quarter resolution trace
- [ ] blue noise
- [ ] detail noise
- [ ] cloud shadow map
- [ ] weather preset
- [ ] 时间驱动风场
- [ ] 与地表雾和降雨系统联动

## 12. 实施记录模板

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

## 13. 当前推荐执行顺序

若按最小返工路径推进，建议严格按以下顺序实施：

1. Step 0 基线确认与接入前检查
2. Step 1 参数与资源骨架
3. Step 2 无光照密度可视化闭环
4. Step 3 太阳光照与基础体积感
5. Step 4 深度裁剪与场景合成
6. Step 5 接入 Atmosphere 渲染链
7. 联调与验收清单全量通过

该顺序的核心原则是：

- 先验证空间结构和参数链路，再做光照。
- 先得到单独云结果，再做合成。
- 先形成最小闭环，再处理质量和性能。

## 14. 完成定义

体积云 MVP 只有在以下条件同时满足时，才视为工程完成：

- 代码已接入现有项目结构。
- `SampleScene` 可稳定展示体积云。
- 文档中的验收清单全部勾选完成。
- 至少完成一轮参数回归和场景回归。
- 已知问题已记录在风险表中，不存在未记录但会阻断演示的关键缺陷。
