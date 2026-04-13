在《A Scalable and Production Ready Sky and Atmosphere Rendering Technique》方案中，**Pass 1: 透射率查找表 (Transmittance LUT)** 是整个渲染管线的基础。它的作用是预计算光线在大气层中传播时的能量衰减（吸收和向外散射）。

以下是该阶段详细的工程落地设计，可以直接转换为 Compute Shader 或 Pixel Shader 的代码逻辑。

---

### 一、 阶段目标 (Goal)
计算并缓存一个像素点（代表大气层内的任意高度）沿着某个特定方向看向太空时，光线能够穿透大气的比例（Transmittance，取值范围 0~1）。该表在不改变星球半径和大气衰减系数的前提下是静态的；如果调整了大气参数，则需要重新生成。

---

### 二、 阶段输入 (Inputs)
为了保证物理计算和浮点数精度，**强烈建议在 Shader 中统一使用千米 (km) 作为距离单位**。

根据论文 Section 4 和 Table 1，我们需要将其转化为 Constant Buffer 传入 GPU：

1. **几何参数**：
   * `R_ground` = 6360.0 (行星半径，km)
   * `R_top` = 6460.0 (大气层顶部半径，km)
2. **瑞利散射 (Rayleigh) - 蓝天主因**：
   * $\sigma_s^R$ = `float3(5.802, 13.558, 33.1) * 1e-3` (单位已转为 $km^{-1}$)
   * 高度密度衰减 `H_R` = 8.0 (km)
3. **米氏散射 (Mie) - 雾霾与光晕**：
   * $\sigma_s^M$ = `float3(3.996, 3.996, 3.996) * 1e-3` (散射系数)
   * $\sigma_a^M$ = `float3(4.40, 4.40, 4.40) * 1e-3` (吸收系数)
   * 高度密度衰减 `H_M` = 1.2 (km)
4. **臭氧 (Ozone) - 日落蓝调主因**：
   * $\sigma_a^O$ = `float3(0.650, 1.881, 0.085) * 1e-3` (仅吸收，不散射)

---

### 三、 阶段输出 (Output)
根据论文 Table 2 的性能测试推荐值：
* **纹理类型**：2D Texture (Texture2D)
* **分辨率**：$256 \times 64$ （宽256，高64）
* **纹理格式**：`RGBA16F` 或 `R11G11B10_FLOAT` (HDR格式，必须支持浮点数，因为透射率是介于 0.0 ~ 1.0 的高精度小数)
* **通道定义**：RGB通道分别存储特定高度和视角的 Red, Green, Blue 光波的透射率（Alpha通道可留空或填1.0）。

---

### 四、 参数化映射 (Parameterization: UV to Physics)
因为星球是球对称的，透射率只与两个变量有关：
1. **当前点距离地心的高度 $r$** (对应纹理的 V 坐标)
2. **观察方向与天顶的夹角 $\theta$ 的余弦值 $\mu = \cos\theta$** (对应纹理的 U 坐标)

在 Compute Shader (基于 DispatchThreadID) 或 全屏 Pixel Shader (基于 UV) 中，第一步需要将像素坐标反解出 $r$ 和 $\mu$：

*论文指出其沿用了 Bruneton 2008 的参数化方法，为了工程简化，这里提供一种平滑的线性/二次映射方案：*
```glsl
// u, v 范围都是 [0, 1]
float r = lerp(R_ground, R_top, v); // 高度从地面映射到大气层顶
float mu = lerp(-1.0, 1.0, u);      // 余弦值从 -1(直视地心) 映射到 1(直视天顶)

// 推导射线起点位置和方向向量 (假设地心在原点)
float3 P = float3(0.0, r, 0.0);
// 方向向量 V 满足 V.y = mu
float3 V = float3(sqrt(1.0 - mu * mu), mu, 0.0); 
```

---

### 五、 核心计算过程 (Ray-Marching Algorithm)

透射率公式（Beer-Lambert 定律）：
$T(P, P_{end}) = e^{-\int_{P}^{P_{end}} \sigma_t(x) dx}$
其中 $\sigma_t = \sigma_s + \sigma_a$ (消光系数 = 散射系数 + 吸收系数)。

在 Shader 中，我们无法计算连续积分，因此使用 **Ray-Marching (步进法)** 将其转化为离散的黎曼和。根据 Table 2，步进次数 $N = 40$。

#### 伪代码 / Shader 实现逻辑：

**Step 1: 计算射线与大气的交点**
我们需要确定光线在给定方向上，走多远会离开大气层，或者撞击到地面。
```glsl
// 球体求交函数，返回相交距离
float rayIntersectSphere(float3 P, float3 V, float radius) {
    float b = dot(P, V);
    float c = dot(P, P) - radius * radius;
    float delta = b * b - c;
    if (delta < 0.0) return -1.0;
    return -b + sqrt(delta);
}

// 获取积分总长度
float d_top = rayIntersectSphere(P, V, R_top);
float d_ground = rayIntersectSphere(P, V, R_ground);

float distance_to_integrate = d_top;
bool hit_ground = false;

// 如果射线击中了地面，积分必须在地面停止
if (d_ground > 0.0) {
    distance_to_integrate = d_ground;
    hit_ground = true;
}
```

**Step 2: Ray-Marching 积分循环**
```glsl
int step_count = 40; // 论文 Table 2 推荐值
float dt = distance_to_integrate / float(step_count); // 步长 (km)

// 累加变量：光学深度 (Optical Depth)
float3 optical_depth = float3(0.0, 0.0, 0.0);

for (int i = 0; i < step_count; ++i) {
    // 采用每步中点作为采样点 (Mid-point integration)
    float t = (float(i) + 0.5) * dt;
    float3 sample_pos = P + V * t;
    
    // 计算当前采样点距地面的高度 h
    float h = length(sample_pos) - R_ground;
    
    // 防止精度误差导致 h 小于 0
    h = max(0.0, h);

    // 计算当前高度下三种物质的密度分布规律
    float rho_R = exp(-h / H_R); // 瑞利密度
    float rho_M = exp(-h / H_M); // 米氏密度
    // 臭氧密度（帐篷函数 Tent Function）
    float rho_O = max(0.0, 1.0 - abs(h - 25.0) / 15.0); 
    
    // 计算当前点的总消光系数 (Total Extinction)
    // 瑞利的吸收为0，只算散射
    float3 ext_R = sigma_s_R * rho_R;
    // 米氏包含散射和吸收
    float3 ext_M = (sigma_s_M + sigma_a_M) * rho_M;
    // 臭氧只吸收不散射
    float3 ext_O = sigma_a_O * rho_O;
    
    float3 sigma_t = ext_R + ext_M + ext_O;
    
    // 累加光学深度
    optical_depth += sigma_t * dt;
}
```

**Step 3: 计算并输出最终透射率**
```glsl
// 根据公式 T = exp(-tau)
float3 transmittance = exp(-optical_depth);

// 如果击中地面，该射线是被阻挡的，理论上透射率可以认为是 0
// 但为了多重散射 Pass 的计算正确性，或者为了让地平线以下的 LUT 数据平滑，
// 通常我们还是存入积分算出的透过大气的透射率值，但在实际采样时通过距离判定遮挡。
// 论文代码通常直接保存 transmittance。

// 写入贴图
OutputTexture[uint2(DispatchThreadID.xy)] = float4(transmittance, 1.0);
```

### 六、 工程避坑指南 (Tips for Production)
1. **Bruneton 映射优化**：上面给出了线性的 `u, v` 映射。但在实际高品质渲染中，视线方向 (`u`) 常常在**地平线处**（$\mu \approx 0$）进行非线性压缩。如果你发现渲染出的地平线附近有一圈锯齿状的带状瑕疵，需要将 `mu` 的映射改为平方根形式，以便将更多的像素分配给地平线角度。
2. **单位尺度**：千万不要用真实世界的 `meter` 做单位进入 `exp` 计算，浮点数必爆。所有的坐标缩放系数（Scale）在 Shader 里都应该缩放到以 `km` 或甚至 `Mm(千千米/兆米)` 为单位。
3. **天空颜色发黑或发红？** 检查臭氧的吸收系数。很多老的模型没有臭氧，导致日出日落时天空颜色过渡生硬，缺少标志性的紫蓝色，带上臭氧计算后能极大地提升真实感。
