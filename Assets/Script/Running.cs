using UnityEngine;

public class Running : MonoBehaviour
{
    [Header("移动参数")]
    [Tooltip("最大移动速度")]
    public float maxMoveSpeed = 8f;

    [Tooltip("加速力度 (值越大，从0到最大速度的时间越短)")]
    public float acceleration = 20f;

    [Tooltip("减速力度 (值越大，从最大速度到停止的时间越短)")]
    public float deceleration = 20f;

    [Tooltip("转向速度 (值越小惯性越大，值越大转向越灵敏)")]
    public float turnSpeed = 30f;

    private Rigidbody2D rb;
    private Animator anim;
    private float moveInput;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        // 获取输入 (A/D)
        moveInput = 0f;
        if (Input.GetKey(KeyCode.A))
        {
            moveInput -= 1f;
        }
        if (Input.GetKey(KeyCode.D))
        {
            moveInput += 1f;
        }

        // 设置动画参数
        if (anim != null)
        {
            anim.SetBool("isMoving", moveInput != 0);
        }
    }

    void FixedUpdate()
    {
        // 1. 处理朝向翻转 (优先处理，无论是否在地面)
        if (moveInput != 0)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (moveInput > 0 ? 1 : -1);
            transform.localScale = scale;
        }

        // 2. 空中检查：如果垂直速度不为0，则视为在空中
        // 此时直接返回，不应用跑动速度，避免与 Jumping 脚本的空中控制冲突
        if (Mathf.Abs(rb.velocity.y) > 1e-2f)
        {
            return;
        }

        // --- 以下是地面跑动逻辑 ---

        // 计算目标速度
        float targetSpeed = moveInput * maxMoveSpeed;
        
        // 获取当前水平速度
        float currentSpeed = rb.velocity.x;
        
        // 确定应该使用哪个加速度值
        float accelRate;

        // 如果玩家有输入
        if (Mathf.Abs(targetSpeed) > 0.01f)
        {
            // 如果输入方向与当前速度方向相反，且当前速度不为0 (正在转向)
            if (Mathf.Sign(targetSpeed) != Mathf.Sign(currentSpeed) && Mathf.Abs(currentSpeed) > 0.01f)
            {
                accelRate = turnSpeed;
            }
            else
            {
                // 正常加速
                accelRate = acceleration;
            }
        }
        else 
        {
            // 玩家无输入 (减速停止)
            accelRate = deceleration;
        }

        // 平滑改变速度
        float nextSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accelRate * Time.fixedDeltaTime);

        // 应用速度 (只修改X轴，保留Y轴速度以兼容跳跃)
        rb.velocity = new Vector2(nextSpeed, rb.velocity.y);
    }
}
