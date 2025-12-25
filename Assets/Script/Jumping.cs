using UnityEngine;

public class Jumping : MonoBehaviour
{
    [Header("跳跃物理参数")]
    [Tooltip("最大上升速度 (此参数决定了'上升有多慢')")]
    public float maxRiseSpeed = 5f;
    [Tooltip("最大跳跃高度")]
    public float maxJumpHeight = 9f;
    [Tooltip("下落时的重力倍率 (较大值 = 气球泄气/快速坠落)")]
    public float fallGravityScale = 25f;

    [Header("空中移动参数")]
    [Tooltip("空中加速度 (控制在空中的移动灵敏度)")]
    public float airAcceleration = 150f;
    [Tooltip("空中控制力 (0-1之间，1为完全控制，0为无法改变方向)")]
    [Range(0f, 1f)]
    public float airControl = 0.037f;
    [Tooltip("空中刹车 (阻力，值越大空中停得越快)")]
    public float airDrag = 1f;
    [Tooltip("最大空中水平速度 (防止在空中无限加速)")]
    public float maxAirSpeed = 8f;

    [Header("可变高度跳跃")]
    public bool enableVariableHeight = true;
    [Tooltip("跳跃截断系数 (0-1)，值越大松手后上升截断越明显")]
    [Range(0f, 1f)]
    public float jumpCutoff = 0.684f;

    // 内部计算的上升重力 (为了同时满足 '慢速上升' 和 '高跳跃高度')
    private float calculatedRiseGravityScale;

    private Rigidbody2D rb;
    private float moveInput;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        RecalculatePhysics();
    }

    void OnValidate()
    {
        RecalculatePhysics();
    }

    void RecalculatePhysics()
    {
        // 公式推导：
        // v = sqrt(2 * g * h)  =>  g = v^2 / (2 * h)
        // 我们已知目标速度(maxRiseSpeed)和目标高度(maxJumpHeight)，反推需要的重力
        
        if (maxJumpHeight > 0.1f && maxRiseSpeed > 0.1f)
        {
            float requiredGravity = (maxRiseSpeed * maxRiseSpeed) / (2f * maxJumpHeight);
            
            // 将绝对重力转换为重力倍率 (Gravity Scale)
            // 假设 Physics2D.gravity.y 通常为 -9.81
            float baseGravity = Mathf.Abs(Physics2D.gravity.y);
            if (baseGravity > 0.001f)
            {
                calculatedRiseGravityScale = requiredGravity / baseGravity;
            }
            else
            {
                calculatedRiseGravityScale = 1f;
            }
        }
    }

    void Update()
    {
        // 1. 输入检测
        moveInput = 0f;
        if (Input.GetKey(KeyCode.A)) moveInput -= 1f;
        if (Input.GetKey(KeyCode.D)) moveInput += 1f;

        // 2. 跳跃触发 (修复：重新加回 Jumping 脚本的跳跃触发)
        // 简单的地面检测：垂直速度接近0
        if (Input.GetKeyDown(KeyCode.Space) && Mathf.Abs(rb.velocity.y) < 0.05f)
        {
            Jump();
        }

        // 3. 可变跳跃高度控制
        if (enableVariableHeight && Input.GetKeyUp(KeyCode.Space))
        {
            if (rb.velocity.y > 0)
            {
                // 截断速度
                rb.velocity = new Vector2(rb.velocity.x, rb.velocity.y * (1f - jumpCutoff));
            }
        }
    }

    void FixedUpdate()
    {
        // 1. 获取当前速度
        float velX = rb.velocity.x;
        float velY = rb.velocity.y;

        // 2. --- 垂直运动处理 ---
        if (velY > 0) // 上升阶段
        {
            // 使用反推出来的低重力，实现"慢速飘浮"
            rb.gravityScale = calculatedRiseGravityScale;
            
            // 确保速度不超过设定的慢速上限
            if (velY > maxRiseSpeed)
            {
                velY = maxRiseSpeed;
            }
        }
        else // 下落阶段
        {
            rb.gravityScale = fallGravityScale;
            // 下落速度上限由 Assists.cs 控制，这里不处理
        }

        // 3. --- 空中水平移动控制 ---
        if (Mathf.Abs(velY) > 0.01f) // 在空中
        {
            if (moveInput != 0)
            {
                // 空中移动逻辑：限制最大速度，允许变向
                bool movingSameDir = Mathf.Sign(moveInput) == Mathf.Sign(velX);
                bool overSpeed = Mathf.Abs(velX) > maxAirSpeed;

                if (!overSpeed || !movingSameDir)
                {
                    float targetVelX = moveInput * maxAirSpeed;
                    // 使用 AirControl 平滑过渡
                    float nextSpeedX = Mathf.MoveTowards(velX, targetVelX, airAcceleration * airControl * Time.fixedDeltaTime);
                    velX = nextSpeedX;
                }
            }
            else
            {
                // 空中阻力
                velX = Mathf.MoveTowards(velX, 0, airDrag * Time.fixedDeltaTime);
            }
        }

        // 4. 应用最终速度
        rb.velocity = new Vector2(velX, velY);
    }

    void Jump()
    {
        // 直接设置起跳速度为 maxRiseSpeed
        // 因为我们已经根据这个速度反推了重力，所以它一定能跳到 maxJumpHeight
        rb.velocity = new Vector2(rb.velocity.x, maxRiseSpeed);
    }
}
