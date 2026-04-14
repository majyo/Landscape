接下来我们进入这篇论文**最核心、最惊艳的创新点**——**Pass 2: 多重散射查找表 (Multi-scattering LUT)**。

在传统大气渲染中，要计算光线在大气中反弹多次的效果，需要进行极其昂贵的多次迭代（算完1次反射算2次，算完2次算3次……）。但这篇论文提出：**高次散射的光线方向是趋于均匀的（各向同性），且能量衰减呈几何级数（等比数列）。** 因此，我们只需要算出“第二次散射”的能量和能量传递率，就能用一个简单的公式 $S_{\infty} = \frac{a}{1 - q}$ 直接算出无限次散射的总和！复杂度直接降为 $O(1)$！

以下是该阶段的具体落地细节：

---

### 一、 阶段目标 (Goal)
预计算并缓存大气中任意高度、任意太阳夹角下，光线经过**无限次反弹**后，向四周贡献的散射能量（环境光/天光的基础）。

### 二、 阶段输入 (Inputs)
1. **Pass 1 阶段的全局参数**：星球半径、大气半径、瑞利/米氏密度分布参数、散射/吸收系数等。
2. **Pass 1 的输出结果**：`Transmittance LUT`（我们需要采样它来获取光线到达太阳的透射率）。
3. **地面反射率 (Ground Albedo)**：例如 `float3(0.3, 0.3, 0.3)`。论文明确提到，多重散射必须考虑地表反弹的光，这让整个场景的光照更统一。

### 三、 阶段输出 (Output)
* **纹理类型**：2D Texture
* **分辨率**：非常小即可，论文推荐 **$32 \times 32$** （因为多重散射是极低频的，变化极其平滑，没必要高分辨率）。
* **纹理格式**：`RGBA16F`（RGB存储多重散射累加亮度 $\Psi_{ms}$，A通道保留）。

### 四、 参数化映射 (UV 映射规则)
多重散射与视线方向无关（因为它假设是各向同性的），只与**所在高度 $h$** 和 **太阳天顶角 $\theta_s$** 有关。

在 Compute Shader (32x32) 中：
```glsl
// u, v 范围 [0, 1]
// 根据论文 5.5.2 节的参数化公式
float h = lerp(R_ground, R_top, v); // 高度
float cos_theta_sun = u * 2.0 - 1.0; // 太阳天顶角余弦值

// 构建空间坐标和太阳方向
float3 P = float3(0.0, h, 0.0);
float3 SunDir = normalize(float3(sqrt(1.0 - cos_theta_sun * cos_theta_sun), cos_theta_sun, 0.0));
```

### 五、 核心计算过程 (算法逻辑)

要计算当前点 $P$ 的多重散射，我们需要在 $P$ 点周围**发射一个球面（$4\pi$立体角）的射线**，收集周围大气散射过来的光（即二次散射 $L_{2ndOrder}$），并计算能量传递率（$f_{ms}$）。

为了在球面上均匀采样，工程上强烈建议使用 **斐波那契球面采样 (Fibonacci Sphere Sampling)**，论文推荐采用 64 个方向。

#### 核心 Shader 伪代码：

**Step 1: 斐波那契球面采样函数**
```glsl
// 输入 i 是 [0, 63] 的索引，返回球面上的均匀分布方向
float3 getFibonacciSphereDirection(int i, int num_samples) {
    const float GOLDEN_RATIO = 1.61803398875;
    float phi = 2.0 * PI * fract(float(i) / GOLDEN_RATIO);
    float cos_theta = 1.0 - (2.0 * float(i) + 1.0) / float(num_samples);
    float sin_theta = sqrt(max(0.0, 1.0 - cos_theta * cos_theta));
    return float3(cos(phi) * sin_theta, cos_theta, sin(phi) * sin_theta);
}
```

**Step 2: 球面积分循环**
```glsl
int sphere_samples = 64; // 论文推荐 64 个方向
int ray_steps = 20;      // 每条射线的步进次数 (32x32贴图很小，20步性能完全扛得住)

float3 L_2ndOrder = float3(0.0, 0.0, 0.0);
float3 f_ms = float3(0.0, 0.0, 0.0);

// 各向同性相位函数 (Isotropic Phase Function)
const float P_u = 1.0 / (4.0 * PI);

for (int i = 0; i < sphere_samples; ++i) {
    float3 ray_dir = getFibonacciSphereDirection(i, sphere_samples);
    
    // 求交：这根射线会打到大气层边缘还是地面？
    float d_top = rayIntersectSphere(P, ray_dir, R_top);
    float d_ground = rayIntersectSphere(P, ray_dir, R_ground);
    
    float t_max = d_top;
    bool hit_ground = false;
    if (d_ground > 0.0) {
        t_max = d_ground;
        hit_ground = true;
    }
    
    float dt = t_max / float(ray_steps);
    float3 ray_optical_depth = float3(0.0, 0.0, 0.0);
    float3 ray_L_2nd = float3(0.0, 0.0, 0.0);
    float3 ray_f_ms = float3(0.0, 0.0, 0.0);
    
    // 内部循环：Ray-Marching
    for (int j = 0; j < ray_steps; ++j) {
        float t = (float(j) + 0.5) * dt;
        float3 sample_pos = P + ray_dir * t;
        float h_sample = length(sample_pos) - R_ground;
        
        // --- 1. 计算当前点的消光和散射系数 (与 Pass 1 相同) ---
        // rho_R, rho_M, rho_O 同 Pass 1
        float3 sigma_s = sigma_s_R * rho_R + sigma_s_M * rho_M; // 只有瑞利和米氏有散射
        float3 sigma_t = sigma_s + sigma_a_M * rho_M + sigma_a_O * rho_O;
        
        // 从 P 到 sample_pos 的透射率
        float3 T_P_to_Sample = exp(-ray_optical_depth);
        
        // --- 2. 采样 Pass 1 计算当前点到太阳的透射率 ---
        float cos_sun_sample = dot(normalize(sample_pos), SunDir);
        float3 T_Sample_to_Sun = SampleTransmittanceLUT(h_sample, cos_sun_sample);
        
        // 检查当前点是否在地球阴影内
        float earth_shadow = rayIntersectSphere(sample_pos, SunDir, R_ground) > 0.0 ? 0.0 : 1.0;
        float3 S = T_Sample_to_Sun * earth_shadow;
        
        // --- 3. 核心积分累加 ---
        // 积分 L_2ndOrder (方程 6)
        ray_L_2nd += T_P_to_Sample * sigma_s * S * P_u * dt;
        
        // 积分 f_ms (方程 8)，注意这里没有乘以阳光 S 和 相位函数 P_u
        ray_f_ms += T_P_to_Sample * sigma_s * dt;
        
        // 累加光学深度供下一步使用
        ray_optical_depth += sigma_t * dt;
    }
    
    // --- 4. 如果射线打到地面，加上地面的漫反射光 ---
    if (hit_ground) {
        float3 hit_pos = P + ray_dir * t_max;
        float3 T_P_to_Ground = exp(-ray_optical_depth);
        float cos_sun_ground = dot(normalize(hit_pos), SunDir);
        float3 T_Ground_to_Sun = SampleTransmittanceLUT(0.0, cos_sun_ground); // 地面高度为 0
        float earth_shadow = cos_sun_ground < 0.0 ? 0.0 : 1.0; // 简单判断太阳是否在地平线下
        
        // 地面的反射亮度
        float3 ground_lum = T_Ground_to_Sun * earth_shadow * (GroundAlbedo / PI) * max(cos_sun_ground, 0.0);
        ray_L_2nd += T_P_to_Ground * ground_lum * P_u;
    }
    
    // 将该方向的结果累加到球面积分中
    L_2ndOrder += ray_L_2nd;
    f_ms += ray_f_ms;
}

// 平均所有采样方向 (相当于乘以 4PI / N，因为前面已经乘过 P_u = 1/(4PI)，所以这里只需除以 N)
L_2ndOrder /= float(sphere_samples);
f_ms /= float(sphere_samples);
```

**Step 3: 利用几何级数近似无限次散射**
```glsl
// 方程 9：F_ms = 1 / (1 - f_ms)
// 极度关键的防暴毙操作：f_ms 绝对不能大于等于 1.0，否则会出现负数或除零爆点 (Fireflies)
// 物理上 f_ms 表示能量传递率，由于存在吸收和逃逸，永远小于 1
float3 f_ms_safe = clamp(f_ms, 0.0, 0.999);
float3 F_ms = 1.0 / (1.0 - f_ms_safe);

// 方程 10：最终的多重散射亮度因子
float3 Psi_ms = L_2ndOrder * F_ms;

// 写入 32x32 的 LUT
OutputTexture[uint2(DispatchThreadID.xy)] = float4(Psi_ms, 1.0);
```

### 六、 工程避坑指南 (Tips for Production)
1. **性能与分辨率**：这个 Pass 的计算量是 `32 * 32 * 64(射线) * 20(步进) = 131万` 次内循环，对于现代GPU来说只要 **0.1毫秒** 级别。因此 $32 \times 32$ 是完全没有负担的“免费午餐”。
2. **防爆点 (`clamp`)**：在非常浓厚的自定义大气（例如美术调了夸张的雾霾参数）下，单次积分的误差可能会导致 `f_ms` 局部超过 1.0。必须加上 `clamp(f_ms, 0.0, 0.999)`，否则画面会出现黑色或白色的噪点。
3. **采样 Pass 1 的 UV**：在内部采样 Transmittance LUT 时，要注意将 `h_sample` 和 `cos_sun_sample` 重新映射回 `[0,1]` 的 UV 坐标（用 Pass 1 中参数化公式的反函数），不要直接把物理量当 UV 传进去。
