using System;
using System.Collections.Generic;
using Celeste.Mod.EndersExtras.Entities.Misc;
using Celeste.Mod.EndersExtras.Utils;
using Celeste.Mod.Entities;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;



namespace Celeste.Mod.EndersExtras.Entities.SoundRipple;
[Tracked(true)]
[CustomEntity("EndersExtras/SoundRippleWatchtower")]
public class SoundRippleWatchtower(EntityData data, Vector2 offset) : Lookout(data, offset)
{
    public override void Added(Scene scene)
    {
        SoundEcho.enableShader = true;
        base.Added(scene);
    }

    private int counter = 0;
    public override void Update()
    {
        if (interacting)
        {
            counter++;
            if (counter >= 60)
            {
                counter = 0;
                SoundEcho.enableShader = true;

                float xPos = 0.5f * (SceneAs<Level>().Camera.Right + SceneAs<Level>().Camera.Left);
                float yPos = 0.5f * (SceneAs<Level>().Camera.Top + SceneAs<Level>().Camera.Bottom);

                SoundEcho.AddEchoSource(new Vector2(xPos, yPos), 30*8);
                SceneAs<Level>().Flash(Color.White*0.02f, false);
                SceneAs<Level>().Displacement.AddBurst(this.Center, 0.4f, 12f, 8*6, 0.4f);
                SceneAs<Level>().Displacement.AddBurst(this.Center, 0.5f, 12f, 8*14, 0.5f);
                SceneAs<Level>().Displacement.AddBurst(this.Center, 0.6f, 12f, 8*24, 0.6f);

                EventInstance bell = Audio.Play("event:/Custom/EndersExtras/bell_large");
                bell.setPitch(0.8f);
                bell.setVolume(0.4f);
                //Logger.Log(LogLevel.Info, "EndersExtras/SoundRippleWatchtower", $"Create echo at ({xPos}, {yPos})");
            }
        }
        else
        {
            counter = 0;
        }
        base.Update();
    }
}