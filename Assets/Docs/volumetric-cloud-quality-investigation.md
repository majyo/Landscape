# 体积云画质问题调查报告

日期：2026-04-21

## 结论

当前项目里“云像随机烟雾”的根因，不只是默认 Profile 配置不对，更关键的是当前体积云渲染链本身的质量上限很低。即使启用新的天气 preset / runtime weather field，现有实现也只是把一张 2D 天气场乘到一个非常简化的 3D 噪声体上，无法生成高质量云体应有的体积轮廓、受光层次和边缘细节。

换句话说，`WeatherPreset` 现在主要只能改变“哪里更容易长云”，但几乎不能改变“云本身长成什么样”。这就是为什么切到新的天气 preset 系统后，画面依然像随机分布的烟雾。

## 发现 1：默认场景实际上仍在绕开新质量链路

`SampleScene` 里的 `VolumetricCloudController` 绑定的是 `VolumetricCloudProfile_Default`，见 `Assets/Scenes/SampleScene.unity:495-498`。

但这个默认 Profile 仍然把关键质量开关关掉了：

- `useRuntimeWeatherField: 0`，见 `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudProfile_Default.asset:16`
- `enableTemporalAccumulation: 0`，见 `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudProfile_Default.asset:32`
- `cloudHeightDensityLut` 未赋值，见 `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudProfile_Default.asset:43`

`VolumetricCloudController` 在 `profile.useRuntimeWeatherField == false` 时会直接退回 fallback 路径，不走 runtime weather field，见 `Assets/VolumetricClouds/Runtime/VolumetricCloudController.cs:474-482`。天气场更新 pass 也会因此直接 early-out，见 `Assets/VolumetricClouds/Runtime/VolumetricWeatherFieldUpdatePass.cs:27-31`。

这个问题解释了为什么默认场景下新系统根本没有真正参与渲染，但它不是唯一根因。下面几项才是“就算开启 preset，画质依然上不去”的核心原因。

## 发现 2：核心密度模型仍然过于原始，云体本质上还是“阈值化噪声”

当前 raymarch 每一步只做两次 3D 采样：

- 一次 base noise，见 `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudRaymarch.compute:194-196`
- 一次 detail noise，见 `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudRaymarch.compute:201-203`

fallback 密度函数本身也非常简单，只是：

- 固定高度曲线 `GetCloudVerticalProfile()`，见 `Assets/VolumetricClouds/Shaders/VolumetricCloudCommon.hlsl:40-43`
- 一个 `baseShape` 阈值 remap，见 `Assets/VolumetricClouds/Shaders/VolumetricCloudCommon.hlsl:50-55`
- 一个简单 `detailErode`

即便启用 runtime weather field，最终密度仍然只是：

- 用天气场调一下 `macroCoverage`
- 用天气场推一下 `cloudType`
- 再把结果乘回单个 `baseShape`、单个 `detailShape`

实现见 `Assets/VolumetricClouds/Shaders/VolumetricCloudCommon.hlsl:118-169`。

这意味着当前系统缺少高质量云体最关键的几类结构信息：

- 没有多级 macro shape 到 micro shape 的体积分层
- 没有 domain warp / curl 扰动
- 没有随高度变化的侵蚀和鼓包塑形
- 没有积云、层云、积雨云那种真正不同的 3D 体积骨架

结果就是：天气系统最多改变覆盖率和局部厚薄，但云体核心仍然是“被阈值切出来的噪声块”，视觉上自然更像烟雾，而不是有组织、有鼓包、有边缘细节的云。

## 发现 3：新的天气 preset 系统只是在驱动一张很简单的 2D 标量场

`VolumetricCloudWeatherFieldUpdate.compute` 的天气场生成逻辑，本质上是：

- 用简单的 `Hash12 -> ValueNoise -> Fbm` 生成种子，见 `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudWeatherFieldUpdate.compute:18-67`
- 再把 coverage / cloudType / wetness / densityBias 朝 preset 目标值平滑 lerp，见 `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudWeatherFieldUpdate.compute:106-127`

这套逻辑可以做“天气状态切换”，但做不了“高质量云体造型”：

- preset 改变的是通道目标值，不是 3D 体积结构
- 它不会生成锋面、云带、对流柱、砧状顶部之类的大尺度组织结构
- 它也不会给 base noise 增加更复杂的空间扭曲和分层

所以你看到的结果会是：“云分布方式变了”，但单个云团的质感依然差，整体仍然像随机噪声控制的烟雾块。这一点和你现在的观察是一致的。

## 发现 4：光照模型过于简化，导致体积感更接近雾而不是云

当前主光照只有这一组核心项：

- 单个 `PhaseMieCornetteShanks`，见 `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudRaymarch.compute:283`
- 一个 sky-view 采样出来的 ambient，见 `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudRaymarch.compute:284-293`
- 最后用 `stepAlpha` 做 Beer-Lambert 累积，见 `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudRaymarch.compute:295-297`

这里缺了高质量云外观里非常关键的几项：

- 没有 powder term
- 没有 cloud multi-scattering 近似
- 没有针对云底、银边、厚云核心的额外能量塑形
- ambient 方向直接取 `samplePosition - cameraPosition`，更接近“沿视线方向取天空颜色”，不是更合理的云体环境光近似

结果就是：云体虽然有明暗，但内部不会形成真实云层常见的亮边、蓬松感、厚云核心和明显的上下层次，更容易读成一团被照亮的体积雾。

## 发现 5：最终输出还会被低分辨率 trace 和简单上采样继续抹平

当前默认 trace 分辨率是 `960x540`，见 `Assets/VolumetricClouds/Resources/VolumetricClouds/VolumetricCloudProfile_Default.asset:27-28`。

这些 trace 纹理在资源层统一使用 `FilterMode.Bilinear`，见 `Assets/VolumetricClouds/Runtime/VolumetricCloudResources.cs:198-204`。合成 shader 只是直接按屏幕 UV 去采样低分辨率云纹理，然后做：

`sceneColor * cloudTrace.a + cloudTrace.rgb`

实现见 `Assets/VolumetricClouds/Shaders/VolumetricCloudComposite.shader:29-32`。

这里没有：

- depth-aware upsample
- edge-aware reconstruction
- 云边缘与地形交界的专门重建

所以即使前面的 raymarch 已经产生了一点细节，最后也会被这条简单的双线性上采样链进一步软化。

## 为什么“开了新 preset 还是没用”

从代码看，`WeatherPreset` 系统解决的是“天气状态驱动”和“宏观覆盖率变化”，不是“高质量体积云造型”。

当前质量瓶颈的真实顺序是：

1. 云体结构太简单，还是阈值化 3D 噪声
2. 光照模型太薄，缺少高质量云常见的体积受光项
3. 输出重建太简单，把仅有的细节又抹平了一次

所以继续调 preset，最多只能把“这团烟雾”调成“另一种分布和厚薄的烟雾”，但不会把它调成真正高质量的云。

## 优先级建议

如果目标是“明显改善云质量”，优先级应该是：

1. 先重做云密度构型。把当前“单个 base noise + 单个 detail noise”的体积结构升级成真正的 macro shape / erosion / height sculpt / domain warp 分层模型。
2. 再升级云光照。至少补 cloud multi-scattering 近似、powder term、云底暗化和更合理的环境光近似。
3. 最后补重建链。加 depth-aware upsample，必要时再收口 temporal accumulation。
4. 默认资产要和设计目标一致。`VolumetricCloudProfile_Default` 至少应启用 runtime weather field、启用 temporal，并绑定 `CloudHeightDensityLut_Default`，否则默认场景连目标链路都跑不起来。

## 最终判断

当前项目里真正的问题，不是“preset 没调好”，而是“preset 系统接在了一个质量上限很低的云体渲染内核上”。

在当前这套实现里：

- 新天气系统只能改变云的分布
- 不能改变云体本身的高级体积结构
- 也不能补足当前缺失的高质量光照与重建

因此，当前系统确实还不具备渲染高质量体积云的能力。
