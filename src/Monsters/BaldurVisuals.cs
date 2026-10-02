using BaseLib.Extensions;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace baldurs_enemys.src.Monsters;

/// <summary>
/// 静态立绘怪物视觉助手：把 mod 内 PNG（res://baldurs_enemys/images/monsters/）
/// 包装成 <see cref="NCreatureVisuals"/>。
///
/// 节点结构完全照抄 BaseLib NCreatureVisualsFactory 的 Texture2D 分支（Bounds + Visuals Sprite2D），
/// 并按其 GenerateNode 规则补建 IntentPos / %CenterPos 标记。与工厂原版的差异：
/// - 按目标高度等比缩放（原版工厂直接用原始像素，我们的素材尺寸差异极大 278~2778px）；
/// - bounds/意图位置随缩放后的逻辑尺寸计算，保证血条/意图/点击区域落位正确。
///
/// 动画兼容性：静态贴图没有 Spine，TriggerAnim → NCreature.SetAnimationTrigger 内部是
/// `_spineAnimator?.SetTrigger(...)`（空安全），招式动画调用静默跳过、仅保留固定等待，无副作用。
/// </summary>
public static class BaldurVisuals
{
    /// <summary>构建静态立绘视觉。</summary>
    /// <param name="imagePath">res:// 下的 PNG 路径。</param>
    /// <param name="targetHeight">缩放后的逻辑高度（原版普通怪 bounds 约 240×280，Boss 可到 ~340）。</param>
    public static NCreatureVisuals? Create(string imagePath, float targetHeight)
    {
        Texture2D? texture = GD.Load<Texture2D>(imagePath);
        if (texture == null)
        {
            GD.PushWarning($"[baldurs_enemys] missing monster image: {imagePath}");
            return null;
        }

        Vector2 imgSize = texture.GetSize();
        float scale = targetHeight / imgSize.Y;
        Vector2 boundsSize = new(imgSize.X * scale, targetHeight);

        var visualsNode = new NCreatureVisuals();

        // Bounds：点击/悬停区域，锚在脚底（原点 = 脚底中心）
        var bounds = new Control();
        visualsNode.AddUnique(bounds, "Bounds");
        bounds.Position = new Vector2(-boundsSize.X / 2f, -boundsSize.Y);
        bounds.Size = boundsSize;

        // Visuals：立绘精灵，居中于 bounds
        var sprite = new Sprite2D();
        visualsNode.AddUnique(sprite, "Visuals");
        sprite.Texture = texture;
        sprite.Scale = new Vector2(scale, scale);
        sprite.Position = new Vector2(0f, -targetHeight * 0.5f);

        // IntentPos / CenterPos：照抄工厂 GenerateNode 的相对定位。
        // 注意：AddUnique 的 name 参数是裸节点名（UniqueNameInOwner 标志才对应 GetNode("%Xxx") 语法），
        // 名字里带 '%' 会注册失败 → NCreatureVisuals._Ready 找不到 "%CenterPos"（图鉴报错来源）。
        var intent = new Marker2D();
        visualsNode.AddUnique(intent, "IntentPos");
        intent.Position = bounds.Position + (bounds.Size * new Vector2(0.5f, 0f)) + new Vector2(0f, -70f);

        var center = new Marker2D();
        visualsNode.AddUnique(center, "CenterPos");
        center.Position = bounds.Position + (bounds.Size * new Vector2(0.5f, 0.6f));

        return visualsNode;
    }
}
