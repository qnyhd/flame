using UnityEngine;

public class Assists : MonoBehaviour
{
    [Header("状态检查")]
    [Tooltip("地面检测层级")]
    [SerializeField] private LayerMask groundLayer;
    [Tooltip("地面检测距离")]
    [SerializeField] private float groundCheckDistance = 0.1f;
    [Tooltip("脚底检测宽度 (比Collider略窄以避免贴墙误判)")]
    [SerializeField] private float footWidth = 0.4f;

    [Header("跳跃手感参数")]
    [Tooltip("土狼时间 (离开平台后多久内仍可起跳)")]
    [SerializeField] private float coyoteTime = 0.1f;
    [Tooltip("跳跃预输入时间 (落地前多久按跳跃键有效)")]
    [SerializeField] private float jumpBufferTime = 0.1f;
    [Tooltip("基础跳跃力度")]
    [SerializeField] private float jumpForce = 15f;
    [Tooltip("松开跳跃键后的截断系数 (0-1)，越大截断越狠")]
    [Range(0f, 1f)]
    [SerializeField] private float jumpCutoff = 0.5f;

    [Header("物理限制")]
    [Tooltip("最大下落速度 (防止穿模/速度过快)")]
    [SerializeField] private float maxFallSpeed = 20f;
    [Tooltip("防卡角检测距离")]
    [SerializeField] private float cornerCorrectionDistance = 0.1f;
    [Tooltip("防卡角推力力度")]
    [SerializeField] private float cornerCorrectionForce = 0.1f;

    [Header("时间控制")]
    [Tooltip("启用时间缩放测试")]
    [SerializeField] private bool enableTimeScaleTest = false;

    // 内部状态
    private Rigidbody2D rb;
    private BoxCollider2D col;
    private bool isGrounded;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private bool isJumping;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<BoxCollider2D>();
    }

    void Update()
    {
        // 1. 更新计时器
        UpdateTimers();

        // 2. 输入处理 (Jump Buffer)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpBufferCounter = jumpBufferTime;
        }

        // 3. 处理跳跃截断 (Variable Jump Height)
        if (Input.GetKeyUp(KeyCode.Space) && rb.velocity.y > 0)
        {
            // 直接修改当前速度，保留一部分动量
            rb.velocity = new Vector2(rb.velocity.x, rb.velocity.y * (1f - jumpCutoff));
            isJumping = false;
        }

        // 4. 尝试执行跳跃
        // 条件：有预输入(Buffer) 且 (在地面 或 在土狼时间内)
        if (jumpBufferCounter > 0 && coyoteTimeCounter > 0)
        {
            PerformJump();
        }

        // 测试功能：按T减慢时间
        if (enableTimeScaleTest)
        {
            Time.timeScale = Input.GetKey(KeyCode.T) ? 0.5f : 1.0f;
            // 注意：修改timeScale时，建议同时调整Time.fixedDeltaTime以保持物理稳定性
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
        }
    }

    void FixedUpdate()
    {
        // 1. 地面检测
        CheckGround();

        // 2. 限制最大下落速度 (Terminal Velocity)
        if (rb.velocity.y < -maxFallSpeed)
        {
            rb.velocity = new Vector2(rb.velocity.x, -maxFallSpeed);
        }

        // 3. 防卡角处理 (Rounded Corners)
        // 仅在上升且未接触地面时检测头顶
        if (rb.velocity.y > 0 && !isGrounded)
        {
            CheckCornerCorrection();
        }
    }

    // --- 核心逻辑方法 ---

    void UpdateTimers()
    {
        // 减少 Buffer 时间
        if (jumpBufferCounter > 0)
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        // 处理土狼时间
        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTime;
        }
        else
        {
            if (coyoteTimeCounter > 0)
            {
                coyoteTimeCounter -= Time.deltaTime;
            }
        }
    }

    void PerformJump()
    {
        // 重置所有计时器，防止重复跳跃
        jumpBufferCounter = 0;
        coyoteTimeCounter = 0;
        isJumping = true;

        // 执行跳跃：重置Y轴速度确保跳跃高度一致，保留X轴速度
        rb.velocity = new Vector2(rb.velocity.x, 0); 
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
    }

    void CheckGround()
    {
        // 使用BoxCast检测脚底，宽度略小于碰撞体宽度，防止贴墙时误判
        // origin: 碰撞体中心
        // size: 检测盒大小 (宽度收缩，高度微小)
        // direction: 向下
        Vector2 boxSize = new Vector2(footWidth, 0.05f);
        Vector2 boxCenter = (Vector2)transform.position + col.offset + Vector2.down * (col.size.y * 0.5f);
        
        isGrounded = Physics2D.OverlapBox(boxCenter, boxSize, 0f, groundLayer);
    }

    void CheckCornerCorrection()
    {
        // 简单的防卡角逻辑：如果在上升时，头顶左右两侧只有一侧碰到了墙，就把角色往另一侧推
        // 这模拟了"圆角"碰撞体滑过去的效果
        
        float checkY = col.bounds.max.y + cornerCorrectionDistance;
        float halfWidth = col.bounds.extents.x;
        
        // 检测左上角和右上角
        bool leftHit = Physics2D.Raycast(new Vector2(transform.position.x - halfWidth, transform.position.y), Vector2.up, cornerCorrectionDistance + col.bounds.extents.y, groundLayer);
        bool rightHit = Physics2D.Raycast(new Vector2(transform.position.x + halfWidth, transform.position.y), Vector2.up, cornerCorrectionDistance + col.bounds.extents.y, groundLayer);

        if (leftHit && !rightHit)
        {
            // 左边撞顶，右边空 -> 向右推
            Vector2 newPos = transform.position;
            newPos.x += cornerCorrectionForce;
            transform.position = newPos;
            // 或者直接修改速度 rb.velocity = new Vector2(rb.velocity.x + force, rb.velocity.y);
        }
        else if (rightHit && !leftHit)
        {
            // 右边撞顶，左边空 -> 向左推
            Vector2 newPos = transform.position;
            newPos.x -= cornerCorrectionForce;
            transform.position = newPos;
        }
    }

    // 辅助可视化 Gizmos
    void OnDrawGizmos()
    {
        if (col == null) return;

        Gizmos.color = Color.red;
        // 地面检测盒
        Vector2 boxCenter = (Vector2)transform.position + col.offset + Vector2.down * (col.size.y * 0.5f);
        Gizmos.DrawWireCube(boxCenter, new Vector3(footWidth, 0.05f, 0));
    }
}

/* 
 * 参数手感调节说明：
 * 
 * 1. Coyote Time (土狼时间): 
 *    - 调大：跳跃判定更宽松，玩家走到平台边缘掉下去一瞬间按跳也能跳起来，减少"明明按了却没跳"的挫败感。
 *    - 调小/0：硬核判定，必须在平台上才能跳。
 * 
 * 2. Jump Buffer (输入缓存):
 *    - 调大：手感更流畅，玩家还没落地就狂按空格，落地瞬间会自动起跳，适合快节奏游戏。
 *    - 调小/0：需要精准的节奏把控，落地瞬间必须正好按下按键。
 * 
 * 3. Terminal Velocity (最大下落速度):
 *    - 调大：下落极快，更有重量感，但可能导致穿墙或玩家反应不过来。
 *    - 调小：像羽毛一样飘落，适合浮空类游戏。
 * 
 * 4. Corner Correction (防卡角):
 *    - 主要是为了避免玩家跳起来头顶刚好撞到砖块边缘被卡住停止上升。
 *    - 这个逻辑会自动把玩家"挤"过去，让跳跃过程不被地形打断。
 * 
 * 5. Time Scale (时间缩放):
 *    - 用于实现"顿帧"(Hit Stop)或"子弹时间"，增强打击感或操作精度。
 */







