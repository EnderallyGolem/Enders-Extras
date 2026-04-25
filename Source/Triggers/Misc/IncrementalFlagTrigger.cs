using Celeste.Mod.EndersExtras.Utils;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Runtime.CompilerServices;

namespace Celeste.Mod.EndersExtras.Triggers.Misc;

[CustomEntity("EndersExtras/IncrementalFlagTrigger")]
public class IncrementalFlagTrigger : Trigger
{
    private readonly String flag;
    private readonly int setValue;
    private readonly bool setOnlyIfOneBelow;
    private readonly String requireFlag;
    private readonly bool singleUse;
    private readonly bool temporary;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public IncrementalFlagTrigger(EntityData data, Vector2 offset)
        : base(data, offset)
    {
        flag = data.Attr("flag", "");
        setValue = data.Int("setValue", 1);
        setOnlyIfOneBelow = data.Bool("setOnlyIfOneBelow", true);
        requireFlag = data.Attr("requireFlag", "");
        singleUse = data.Bool("singleUse", true);
        temporary = data.Bool("temporary", true);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override void Added(Scene scene)
    {
        base.Added(scene);
        if (temporary)
        {
            SetFlagCounter(0);
        }
    }

    public override void Awake(Scene scene)
    {
        base.Awake(scene);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override void OnEnter(Player player)
    {
        if (!Utils_General.AreFlagsEnabled(player.level.Session, requireFlag, true)) 
            return;
        if (setOnlyIfOneBelow && setValue != 0 && GetFlagCounter() != setValue - 1)
            return;

        SetFlagCounter(setValue);

        if (singleUse) RemoveSelf();
        base.OnEnter(player);
    }

    public override void OnStay(Player player)
    {
        if (!Utils_General.AreFlagsEnabled(player.level.Session, requireFlag, true))
            return;
        if (setOnlyIfOneBelow && setValue != 0 && GetFlagCounter() != setValue - 1)
            return;

        SetFlagCounter(setValue);

        if (singleUse) RemoveSelf();
        base.OnStay(player);
    }

    public override void OnLeave(Player player)
    {
        base.OnLeave(player);
    }

    private void SetFlagCounter(int value)
    {
        Level level = SceneAs<Level>();

        int oldValue = GetFlagCounter();

        level.Session.SetFlag($"{flag}{oldValue}", false);
        level.Session.SetCounter(flag, value);
        level.Session.SetFlag($"{flag}{value}", true);
    }

    private int GetFlagCounter()
    {
        Level level = SceneAs<Level>();
        return level.Session.GetCounter(flag);
    }
}