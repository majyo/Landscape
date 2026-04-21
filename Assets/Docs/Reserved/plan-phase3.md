在完成了 Pass 1（透射率）和 Pass 2（多重散射）之后，我们将所有物理现象真正组合起来，生成肉眼可见的天空背景。这就是 **Pass 3: 天空视图查找表 (Sky-View LUT)** 的任务。

### 一、 阶段目标 (Goal)
传统的射线步进如果放在最终画面的每个像素上进行（比如 4K 分辨率），开销是毁灭性的。这篇论文的思路是：**天空是一张极低频的幕布，只有地平线附近变化剧烈**。因此，我们只需要以当前相机位置为起点，在低分辨率的 2D 全景图上绘制出当前天空的颜色，然后在主渲染管线中对这张图进行上采样（Upsampling）映射到屏幕即可。

### 二、 阶段输入 (Inputs)
1. **当前相机位置 (Camera Position)**：必须转换为相对于星球地心的坐标系。
2. **光源方向和强度 (Sun Direction & Illuminance)**。
3. **Pass 1 的 `Transmittance LUT`**。
4. **Pass 2 的 `Multi-scattering LUT`**。

### 三、 阶段输出 (Output)
* **纹理类型**：2D Texture
* **分辨率**：推荐 **$200 \times 100$**（宽200代表水平环绕360度，高100代表垂直方向。这个分辨率足以骗过眼睛且只需 0.05ms 渲染）。
* **纹理格式**：`RGBA16F`（RGB存储天空亮度，A存储到天空边界的透射率，可用于混合背后的恒星或月亮）。

### 四、 参数化映射 (UV 映射与非线性拉伸)
这是 Pass 3 的**核心工程技巧**！
如果用线性映射，地平线附近的过渡会因为分辨率太低而出现严重的锯齿和色带。论文 5.3 节提出了一种**非线性映射**，把更多的 V 坐标（高度方向）分配给地平线。

在 Compute Shader (200x100) 中：
```glsl
// u, v 范围 [0, 1]

// --- 1. 计算经度 (方位角 Azimuth) ---
// 让 u=0.5 对准太阳方向，这样 Mie 散射的高光晕可以被最完整的采样
float azimuth_angle = (u - 0.5) * 2.0 * PI;

// --- 2. 计算纬度 (仰角 Elevation/Latitude) ---
// 这是论文中的非线性映射的逆推导
// v = 0.5 + 0.5 * sign(l) * sqrt(|l| / (PI/2)) -> 倒推 l
float adj_v = v - 0.5;
float v_sign = sign(adj_v);
float v_sq = adj_v * adj_v * 4.0; // 反向解出比例
float elevation_angle = v_sign * v_sq * (PI / 2.0); // 范围 [-PI/2, PI/2]

// --- 3. 计算射线方向 (以相机正上方为 Y 轴) ---
float cos_el = cos(elevation_angle);
float3 ray_dir = float3(
    cos_el * sin(azimuth_angle),
    sin(elevation_angle),
    cos_el * cos(azimuth_angle)
);

// 如果相机不在北极，需要构建一个以相机当前位置为基准的 Local to World 矩阵
// 将 ray_dir 从相机的切线空间转换到星球坐标系空间。
```

### 五、 核心计算过程 (算法逻辑)

得到起点 `CameraPos` 和方向 `ray_dir` 后，我们执行最后一次 Ray-Marching。根据 Table 2，步进次数 $N = 30$。

#### 核心 Shader 伪代码：

**Step 1: 准备相位函数 (Phase Functions)**
```glsl
// theta 是光线方向与视线方向的夹角，cos_theta = dot(ray_dir, SunDir)
// 瑞利散射的相位函数 (平滑分布)
float PhaseRayleigh(float cos_theta) {
    return (3.0 / (16.0 * PI)) * (1.0 + cos_theta * cos_theta);
}

// 米氏散射的相位函数 (Cornette-Shanks 近似，产生太阳周围刺眼的日晕)
float PhaseMie(float cos_theta, float g = 0.8) {
    float g2 = g * g;
    float num = 3.0 * (1.0 - g2) * (1.0 + cos_theta * cos_theta);
    float den = 8.0 * PI * (2.0 + g2) * pow(1.0 + g2 - 2.0 * g * cos_theta, 1.5);
    return num / den;
}
```

**Step 2: Ray-Marching 积分循环**
结合论文公式 (11)，这一次我们要在积分中加上 Pass 2 算出的多重散射。
```glsl
float t_top = rayIntersectSphere(CameraPos, ray_dir, R_top);
float t_ground = rayIntersectSphere(CameraPos, ray_dir, R_ground);

float t_max = t_top;
if (t_ground > 0.0) t_max = t_ground; // 如果视线看地，步进到地面为止

int step_count = 30; // 推荐值
float dt = t_max / float(step_count);

float3 L_sky = float3(0.0, 0.0, 0.0); // 最终的天空亮度
float3 optical_depth = float3(0.0, 0.0, 0.0);

float cos_theta = dot(ray_dir, SunDir);
float p_R = PhaseRayleigh(cos_theta);
float p_M = PhaseMie(cos_theta, 0.8);

for (int i = 0; i < step_count; ++i) {
    float t = (float(i) + 0.5) * dt;
    float3 P_i = CameraPos + ray_dir * t;
    float h_i = length(P_i) - R_ground;
    
    // 1. 计算当前点的大气密度
    float rho_R = exp(-h_i / H_R);
    float rho_M = exp(-h_i / H_M);
    float rho_O = max(0.0, 1.0 - abs(h_i - 25.0) / 15.0);
    
    // 2. 消光与散射系数
    float3 sigma_s_R_step = sigma_s_R * rho_R;
    float3 sigma_s_M_step = sigma_s_M * rho_M;
    float3 sigma_s = sigma_s_R_step + sigma_s_M_step;
    float3 sigma_t = sigma_s + (sigma_a_M * rho_M) + (sigma_a_O * rho_O);
    
    // 3. 计算单次散射 (Single Scattering)
    float cos_sun_i = dot(normalize(P_i), SunDir);
    float3 T_to_Sun = SampleTransmittanceLUT(h_i, cos_sun_i);
    float earth_shadow = rayIntersectSphere(P_i, SunDir, R_ground) > 0.0 ? 0.0 : 1.0;
    
    // S 是到达当前点的直射阳光
    float3 S = T_to_Sun * earth_shadow; 
    
    // 应用各自的相位函数
    float3 SingleScat = S * (sigma_s_R_step * p_R + sigma_s_M_step * p_M);
    
    // 4. 读取多次散射 (Multi-Scattering)
    // 根据当前高度和太阳角度，去 Pass 2 的 LUT 中查表
    float3 Psi_ms = SampleMultiScatteringLUT(h_i, cos_sun_i);
    float3 MultiScat = Psi_ms * sigma_s; // 论文公式 11，多重散射也是各向同性
    
    // 5. 积分累加 (啤酒定律)
    float3 T_to_Camera = exp(-optical_depth);
    
    // 当前步总的发光量 = 单次散射 + 多次散射
    float3 L_step = (SingleScat + MultiScat) * SunIlluminance; 
    
    L_sky += T_to_Camera * L_step * dt;
    
    // 更新光学深度
    optical_depth += sigma_t * dt;
}

// 写入 Sky-View LUT
OutputTexture[uint2(DispatchThreadID.xy)] = float4(L_sky, exp(-optical_depth.y)); // A通道保存视觉上的透射率
```

### 六、 工程避坑指南 (Tips for Production)

1. **别把太阳画进去 (No Sun Disk)**：
   论文特别强调：在算 `Sky-View LUT` 的时候，**不要**处理如果射线直接击中太阳该有多亮。因为 $200 \times 100$ 纹理太糊了，加上非线性拉伸，如果你把太阳渲染在这张表里，最后画面上会得到一个被严重拉伸变形的椭圆太阳。**太阳圆盘必须在最后全屏 Pass 合成时，通过点积视线和太阳方向用分析式数学强加进去**。
2. **每帧更新还是按需更新？**：
   因为这张表强烈依赖于 `CameraPos` 的高度（如果是飞行模拟游戏），所以最好是**每帧计算**。反正 $200 \times 100$ 配合 30 步的开销（仅 0.05ms）对于现代游戏完全是毛毛雨。
3. **地平线下方的视觉处理**：
   如果在太空或高空俯视，射线打到地面（`t_ground > 0.0`），我们在 LUT 里依然算出了从相机到地面的大气散射（比如地球边缘的一圈蓝晕）。但在最终渲染时，你需要遮蔽掉地形表面后面的天空。
4. **高频闪烁问题处理（Jittering）**：
   在做 `dt` 步进时，如果只是简单的等分截断，在移动相机时天空极容易产生环状水波纹带（Banding artifacts）。最好的方案是引入 **Blue Noise (蓝噪声)** 配合 `Temporal Anti-Aliasing (TAA)`，对每条射线的起始偏移量 `t` 进行微小的随机抖动 (Jittering)，这样可以完全消除色带。

---
完成此步骤后，你已经拥有了一张极其美丽、随时间物理级真实变化的天空全景图。接下来只剩下**最后一块拼图——Pass 4 (3D 体积雾 LUT)**，它是将天空的氛围感下沉到山脉、树木和角色上的关键。需要继续推进吗？
