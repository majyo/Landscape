我们终于来到了大气渲染管线中**将天空与场景物体完美融合**的一环——**Pass 4: 空间透视体积图 (Aerial Perspective Volume LUT)**。

前面的 Pass 把“天空背景”画好了，但是场景里的高山、建筑、远处的树木如果不受大气影响，看起来就像是贴在屏幕上的贴纸。Pass 4 的目的就是计算出**相机到场景物体之间**的大气散射光（雾的颜色）和衰减（物体被遮挡的程度）。

以下是 Pass 4 的工程落地细节：

---

### 一、 阶段目标 (Goal)
生成一个贴合相机视锥体（Camera Frustum）的 3D 体积纹理（Froxel）。这个体积纹理像是一块切好片的蛋糕，记录了从相机出发，不同方向、不同距离处的空间中，**积累的大气散射光（Scattering）**和**剩余的透射率（Transmittance）**。

### 二、 阶段输入 (Inputs)
1. **相机视锥体参数**：FOV、Aspect Ratio、相机世界坐标 `CameraPos`、相机旋转矩阵。
2. **Pass 1 输出**：`Transmittance LUT`（用于计算阳光穿透大气到达体积块的能量）。
3. **Pass 2 输出**：`Multi-scattering LUT`（为体积雾提供多次反弹的天光）。

### 三、 阶段输出 (Outputs)
为了保证日落时分山脉能呈现出正确的偏色（比如阳光被透射后剩下的红光），散射和透射率都需要保留 RGB 颜色。
*   **纹理类型**：**3D Texture** (RWTexture3D)
*   **分辨率**：极低即可，论文和主流引擎（如UE）推荐 **$32 \times 32 \times 32$**。
*   **纹理格式**：
    为了性能可以压进一张图（透射率算单通道），但**为了高品质渲染，强烈建议输出两张 3D 贴图**：
    1. `LUT_Scattering` (格式 `R11G11B10_FLOAT`，存储 RGB 散射光，即雾的颜色)。
    2. `LUT_Transmittance` (格式 `RGBA16F` 或 `R11G11B10_FLOAT`，存储 RGB 物理透射率)。

### 四、 参数化映射 (视锥体与体素坐标系)
这是 3D 体素化渲染的核心。我们将 $32 \times 32 \times 32$ 的 3D 纹理坐标 `(x, y, z)` 映射到物理空间：

1. **`x, y` 坐标**：映射到屏幕 UV `[0, 1]`，然后再反推出视锥体内的射线方向 `ray_dir`。
2. **`z` 坐标（深度切片）**：**必须使用非线性/指数映射**！因为近处的物体需要更高的雾效精度，远处的雾本身就很糊，可以放宽精度。

```glsl
// 设置最大计算距离（比如 32公里）
const float MAX_DISTANCE = 32.0; // km

// 将深度索引 z (0 到 31) 映射到实际物理距离
float slice_to_distance(int z) {
    float normalized_z = (float(z) + 0.5) / 32.0;
    // 采用平方映射，让近处的切片更密集
    return normalized_z * normalized_z * MAX_DISTANCE; 
}
```

### 五、 核心计算过程 (Compute Shader 算法)

在这个 Compute Shader 中，我们**不采用** 3D 分发调度。相反，我们调度 $32 \times 32$ 个线程（代表屏幕切分的网格）。每个线程负责一条射线，然后在线程内部写一个 `for(z = 0; z < 32; z++)` 的循环。**这是因为射线步进必须从近到远累加，Z 切片之间存在强烈的顺序依赖。**

#### 核心 Shader 伪代码：

```glsl
[numthreads(8, 8, 1)] // 调度 32x32 的线程组
void CS_AerialPerspective(uint3 DispatchThreadID : SV_DispatchThreadID) {
    uint2 pixel_pos = DispatchThreadID.xy;
    
    // 1. 根据像素坐标计算射线方向 (View Space -> World Space)
    float2 uv = (float2(pixel_pos) + 0.5) / 32.0;
    float3 ray_dir = CalculateWorldRayDirectionFromUV(uv);
    
    // 初始化累加器
    float3 accum_scattering = float3(0.0, 0.0, 0.0); // 积累的雾颜色
    float3 accum_transmittance = float3(1.0, 1.0, 1.0); // 初始透过率为 100%
    float last_distance = 0.0;
    
    // 2. 沿着这条射线，依次计算 32 个深度切片
    for (int z = 0; z < 32; ++z) {
        // 计算当前切片的距离
        float current_distance = slice_to_distance(z);
        float dt = current_distance - last_distance; // 当前步长
        
        // 采样点取当前步的中点
        float t = last_distance + dt * 0.5;
        float3 P_i = CameraPos + ray_dir * t;
        float h_i = length(P_i) - R_ground; // 当前海拔高度
        
        // --- 物理计算 (与 Pass 3 完全一致) ---
        // 1. 获取三种大气的密度
        float rho_R = exp(-h_i / H_R);
        float rho_M = exp(-h_i / H_M);
        float rho_O = max(0.0, 1.0 - abs(h_i - 25.0) / 15.0);
        
        // 2. 计算衰减和散射系数
        float3 sigma_s = sigma_s_R * rho_R + sigma_s_M * rho_M;
        float3 sigma_t = sigma_s + (sigma_a_M * rho_M) + (sigma_a_O * rho_O);
        
        // 3. 计算单次散射 (阳光直射)
        float cos_sun_i = dot(normalize(P_i), SunDir);
        float3 T_to_Sun = SampleTransmittanceLUT(h_i, cos_sun_i);
        float earth_shadow = rayIntersectSphere(P_i, SunDir, R_ground) > 0.0 ? 0.0 : 1.0;
        
        // 4. 读取多次散射
        float3 Psi_ms = SampleMultiScatteringLUT(h_i, cos_sun_i);
        
        // 5. 应用相位函数 (视线与太阳的夹角)
        float cos_theta = dot(ray_dir, SunDir);
        float p_R = PhaseRayleigh(cos_theta);
        float p_M = PhaseMie(cos_theta, 0.8);
        
        // 当前段的入向光 = (直射阳光 * 相位) + 各向同性多次散射
        float3 S = T_to_Sun * earth_shadow * SunIlluminance;
        float3 step_scattering = S * (sigma_s_R * rho_R * p_R + sigma_s_M * rho_M * p_M) 
                               + Psi_ms * sigma_s * SunIlluminance;
                               
        // --- 核心累加逻辑 (Analytical Integration) ---
        // 根据 Beer-Lambert 定律计算这一小段的透射率
        float3 step_transmittance = exp(-sigma_t * dt);
        
        // 积分公式: (S - S * exp(-sigma_t * dt)) / sigma_t
        // 为了防止除以 0，加一个小偏移量
        float3 int_scattering = (step_scattering - step_scattering * step_transmittance) / max(sigma_t, 0.00001);
        
        // 累加到整体: 加上受到此前体积阻挡后的光
        accum_scattering += accum_transmittance * int_scattering;
        accum_transmittance *= step_transmittance;
        
        // 3. 写入当前体素
        uint3 write_pos = uint3(pixel_pos.x, pixel_pos.y, z);
        LUT_Scattering[write_pos] = float4(accum_scattering, 1.0);
        LUT_Transmittance[write_pos] = float4(accum_transmittance, 1.0);
        
        // 步进推进
        last_distance = current_distance;
    }
}
```

### 六、 工程落地与场景融合 (How to apply in Forward/Deferred)

现在我们有了这两张 3D 贴图，**该怎么用到主渲染管线中？**

无论你是正向渲染还是延迟渲染，当你在渲染一个不透明物体（比如一座山）或者透明物体（比如一块玻璃）时，你会得到该物体距离相机的深度（Depth）/ 距离（Distance）。

**在物体的 Shader 中（或 Deferred 的光照 Pass 最后一步）：**
```glsl
// 1. 获取物体的距离 (km)
float obj_distance = ...;

// 2. 把距离反算成体纹理的 Z 坐标 (反向执行 slice_to_distance)
float normalized_z = sqrt(obj_distance / MAX_DISTANCE);

// 3. 构造 3D UVW 坐标
float3 tex_uvw = float3(ScreenUV.x, ScreenUV.y, normalized_z);

// 4. 去 3D 纹理中进行三线性插值采样 (Trilinear Texture Fetch)
float3 aerial_color = Texture3DSample(LUT_Scattering, tex_uvw).rgb;
float3 aerial_trans = Texture3DSample(LUT_Transmittance, tex_uvw).rgb;

// 5. 最终画面合成
// 物体本来的颜色被大气挡住了，加上大气本身散射发出的光
float3 FinalPixelColor = ObjectColor * aerial_trans + aerial_color;
```

### 七、 工程避坑指南 (Tips for Production)

1. **“体素块状感” (Blocky Artifacts) 的消除**：
   $32 \times 32 \times 32$ 的分辨率放在 4K 屏幕上，由于体素太大，即使有硬件的三线性插值，遇到物体深度剧烈变化时也会出现马赛克般的边界。
    * **解法**：在 Compute Shader 中，对每条射线的 `uv` 和 `last_distance` 偏移量应用 **Blue Noise (蓝噪声空间抖动)**。配合 Temporal AA，可以让块状感在一帧之内变成极其细腻的噪点，随后被 TAA 抹平，视觉上等于达到了 $256^3$ 的精度！
2. **超出 32km 的物体怎么办？**
   对于超过 `MAX_DISTANCE` 的像素（比如极其遥远的地平线、或者无尽的大海）。我们无法从 3D LUT 中采样。
    * **解法**：超过最大距离的部分，我们回退（Fallback）到 **Pass 3**！通过拉取 `Sky-View LUT` 中对应方向的颜色，再用一个基于深度的混合函数，让远处的山峦自然过渡成天空的颜色。
3. **动态物体的遮挡**：
   上面的纯物理模型默认光线在到达太阳时不受地形阻挡。如果想实现“山峰挡住阳光，投下一道巨大的体积阴影在雾气中（God Rays）”，需要在 Pass 4 的 `S`（到达光强）中额外乘以一个当前采样点对于太阳方向的 **Shadow Map（阴影贴图）**采样结果。

---
至此！这套基于 Epic Games 论文的**生产级全动态大气与体积雾渲染管线**的四大核心计算 Pass 全部设计完毕！在管线跑通之后，美术只需调节 `sigma_s` (密度) 和 `H` (高度分布) 几个简单的滑块，就能瞬间从地球的清晨切换到火星的沙尘暴。

如果你还有针对渲染引擎（如 Unity URP/HDRP 或 Unreal 的定制修改）集成相关的具体疑问，我们可以继续探讨！
