using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Celeste.Mod.Entities;
using Celeste.Mod.UI;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.EndersExtras.Entities.Misc
{
    [CustomEntity("EndersExtras/SettingsNpc")]
    [TrackedAs(typeof(NPC))]
    public class SettingsNpc : NPC // "you shouldn't hook everest" then let me edit your extended npcs dangit!
    {
        private readonly float spriteRate;
        private readonly bool onlyOnce;
      private readonly bool endLevel;
      private readonly List<MTexture>? textures;
      private readonly EntityID id;
      private readonly bool approachWhenTalking;
      private readonly int approachDistance;
      private readonly string[] dialogs;
      private readonly bool animated;
      private float frame;
      private readonly Vector2 scale = new Vector2(1f, 1f);
      private readonly Vector2 indicatorOffset = Vector2.Zero;
      private Coroutine? talkRoutine;

      public event Action<int>? OnStart;
      public event Action? OnEnd;

      private readonly bool logSettings;
      private readonly string modSettingsStrList;
      private readonly string headerDialog;

      private readonly Vector2 talkComponentScale = new Vector2();

      public SettingsNpc(EntityData data, Vector2 offset, EntityID id)
        : base(data.Position + offset)
      {
          logSettings = data.Bool("logSettings", false);
          modSettingsStrList = data.String("modSettings", "");
          headerDialog = data.String("headerDialog", "EndersExtras_ModSettingsDefault");

          this.id = id;
        var spritePath1 = data.Attr("sprite");
        spriteRate = data.Float(nameof (spriteRate), 1f);
        var dialogEntry1 = data.Attr("dialogId");
        dialogs = dialogEntry1.Split(',');
        onlyOnce = data.Bool(nameof (onlyOnce), true);
        endLevel = data.Bool(nameof (endLevel));
        indicatorOffset.X = data.Float("indicatorOffsetX");
        indicatorOffset.Y = data.Float("indicatorOffsetY");
        talkComponentScale.X = data.Float("talkRegionScaleX", 0.2f);
        talkComponentScale.Y = data.Float("talkRegionScaleY", 0.2f);
        approachWhenTalking = data.Bool(nameof (approachWhenTalking));
        approachDistance = data.Int(nameof (approachDistance), 16 /*0x10*/);
        if (data.Bool("flipX"))
          scale.X = -1f;
        if (data.Bool("flipY"))
          scale.Y = -1f;
        if (!string.IsNullOrEmpty(spritePath1))
        {
          string oldValue = Path.GetExtension(spritePath1);
          if (!string.IsNullOrEmpty(oldValue))
            spritePath1 = spritePath1.Replace(oldValue, "");
          spritePath1 = Path.Combine("characters", spritePath1).Replace('\\', '/');
          var name1 = Regex.Replace(spritePath1, "\\d+$", string.Empty);
          textures = GFX.Game.GetAtlasSubtextures(name1);
          if (textures != null && textures.Count > 1)
            animated = true;
        }
        frame = 0.0f;
      }

      public override void Added(Scene scene)
      {
        base.Added(scene);
        if ((scene as Level)!.Session.GetFlag("DoNotTalk" + id))
          return;
        float num1 = 0.0f;
        float num2 = 0.0f;
        if (textures != null && textures.Count > 0)
        {
          num1 = textures[0].Width;
          num2 = textures[0].Height;
        }
        Add(Talker = new TalkComponent(new Rectangle(-(int) num1 / 2 - (int)(48*talkComponentScale.X) /*0x30*/, (int) -(double) num2 - (int)(16*talkComponentScale.Y) /*0x10*/, (int) num1 + (int)(96*talkComponentScale.X) /*0x60*/, (int) num2 + (int)(32*talkComponentScale.Y) /*0x20*/), new Vector2(indicatorOffset.X, (float) (-(double) num2 / 2.0) + indicatorOffset.Y), OnTalk));
      }

      public override void Update()
      {
        base.Update();
        if (!animated || textures == null || textures.Count <= 0)
          return;
        frame += spriteRate * Engine.DeltaTime;
        frame %= textures.Count;
      }

      public override void Render()
      {
        if (textures == null || textures.Count <= 0)
          return;
        textures[(int) frame].DrawJustified(Position, new Vector2(0.5f, 1f), Color.White, scale);
      }

      private void OnTalk(Player player)
      {
        EntityID entityId;
        if (onlyOnce && (dialogs.Length == 1 || Session.GetCounter(id + "DialogCounter") > dialogs.Length - 2))
        {
          Session session = (Scene as Level)!.Session;
          entityId = id;
          string flag = "DoNotTalk" + entityId;
          session.SetFlag(flag);
        }
        if (endLevel)
          (Scene as Level)!.RegisterAreaComplete();
        player.StateMachine.State = 11;
        Action<int>? onStart = OnStart;
        if (onStart != null)
        {
          Session session = Session;
          entityId = id;
          string counter = entityId + "DialogCounter";
          onStart(session.GetCounter(counter));
        }
        Level.StartCutscene(OnTalkEnd);
        Add(talkRoutine = new Coroutine(Talk(player)));
      }

      private void OnTalkEnd(Level level)
      {
        Player entity = Scene.Tracker.GetEntity<Player>();
        if (entity != null)
        {
          entity.StateMachine.Locked = false;
          entity.StateMachine.State = 0;
        }
        talkRoutine!.Cancel();
        talkRoutine.RemoveSelf();
        Session.IncrementCounter(id + "DialogCounter");
        if (endLevel && (dialogs.Length == 1 || Session.GetCounter(id + "DialogCounter") > dialogs.Length - 1))
        {
          level.CompleteArea(true, false, false);
          entity!.StateMachine.State = 11;
        }
        if (onlyOnce)
        {
          if (dialogs.Length == 1 || Session.GetCounter(id + "DialogCounter") > dialogs.Length - 1)
            Remove(Talker);
        }
        else if (Session.GetCounter(id + "DialogCounter") > dialogs.Length - 1 || dialogs.Length == 1)
          Session.SetCounter(id + "DialogCounter", 0);


        String[] menuItemStr = modSettingsStrList.Split("||");
        List<MenuItem> menuItemList = new List<MenuItem>();
        foreach (String str in menuItemStr)
        {
            menuItemList.Add(new MenuItem(str));
        }

        OpenMenu(level, logSettings, menuItemList, headerDialog);

        Action? onEnd = OnEnd;
        if (onEnd == null)
          return;
        onEnd();
      }

      private IEnumerator Talk(Player player)
      {
        SettingsNpc customNpc = this;
        if (customNpc.approachWhenTalking)
        {
          if (customNpc.scale.X > 0.0)
            yield return customNpc.PlayerApproachRightSide(player, spacing: (float) customNpc.approachDistance);
          else
            yield return customNpc.PlayerApproachLeftSide(player, spacing: (float) customNpc.approachDistance);
        }
        yield return Textbox.Say(customNpc.dialogs[customNpc.Session.GetCounter(customNpc.id + "DialogCounter")], null);
        customNpc.Level.EndCutscene();
        customNpc.OnTalkEnd(customNpc.Level);
      }





        readonly struct MenuItem : IEquatable<MenuItem>
        {
            internal readonly String modName;
            internal readonly String settingName;
            internal readonly String infoDialog;

            public MenuItem(String modName, String settingName, String infoDialog = "")
            {
                this.modName = modName.Trim();
                this.settingName = settingName.Trim();
                this.infoDialog = infoDialog.Trim();
            }
            public MenuItem(String combinedStr)
            {
                String[] splittedStr = combinedStr.Split("::");

                if (splittedStr.Length == 2)
                {
                    this.modName = splittedStr[0].Trim();
                    this.settingName = splittedStr[1].Trim();
                    this.infoDialog = "";
                }
                else if (splittedStr.Length >= 3)
                {
                    this.modName = splittedStr[0].Trim();
                    this.settingName = splittedStr[1].Trim();
                    this.infoDialog = splittedStr[2].Trim();
                }
                else
                {
                    this.modName = this.settingName = this.infoDialog = "";
                }
            }

            public bool Equals(MenuItem other) { return modName == other.modName && settingName == other.settingName; }
            public override bool Equals(object? obj) { return obj is MenuItem other && Equals(other); }
            public override int GetHashCode() { return HashCode.Combine(modName, settingName); }
        }

        private static void OpenMenu(Level level, bool logList, List<MenuItem> searchQueries, String headerDialog = "")
        {
            if (level.FrozenOrPaused || level.Transitioning) return;

            // Ensure consistent naming across languages (cannot extract the actual name directly)
            EndersExtrasModule.dialogCleanForceEnglish = true;
            TextMenu modMenu = OuiModOptions.CreateMenu(true, (EventInstance) null!);
            EndersExtrasModule.dialogCleanForceEnglish = false;

            modMenu.BatchMode = false;

            String previousSubheaderStr = "";
            TextMenu.SubHeader? previousSubheaderItem = null;
            bool addedSubheaderAlready = false;

            if (logList) Logger.Log(LogLevel.Info, "EndersExtras/SettingsNPC", "Printing out mod settings below!");
            List<TextMenu.Item> copyList = modMenu.items.ToList();

            foreach (TextMenu.Item item in copyList)
            {
                if (item is TextMenu.SubHeader subheader)
                {
                    String newSubheaderStr = subheader.Title.Split("|")[0].Trim();
                    if (subheader.Title.Split("|").Length >= 2 && previousSubheaderStr != newSubheaderStr)
                    {
                        if(previousSubheaderItem is not null && !previousSubheaderItem.Visible){ modMenu.Remove(previousSubheaderItem); }

                        previousSubheaderItem = subheader;
                        previousSubheaderStr = newSubheaderStr;
                        addedSubheaderAlready = false;
                        subheader.Visible = false;
                    }
                    else
                    {
                        modMenu.Remove(subheader);
                    }
                }
                else
                {
                    HandleNormalItem(item);
                }
            }

            if (headerDialog != "") modMenu.Insert(0, new TextMenu.Header(Dialog.Clean(headerDialog)));

            Action closeMenu = (() =>
            {
                Audio.Play("event:/ui/main/button_back");
                modMenu.Close();
                level.Paused = false;
            });
            modMenu.OnCancel = closeMenu;
            modMenu.OnESC = closeMenu;
            modMenu.OnPause = closeMenu;

            level.Paused = true;
            modMenu.Selection = modMenu.FirstPossibleSelection;
            level.Add(modMenu);
            Audio.Play("event:/ui/main/button_select");
            return;



            // Checks if that item exists. If yes, returns true. If no, returns false after removing it from the menu.
            bool HandleNormalItem(TextMenu.Item item, TextMenuExt.SubMenu? withinSubmenu = null)
            {
                // Reiterate if item is a submenu (check every item inside)
                if (item is TextMenuExt.SubMenu submenu)
                {
                    List<TextMenu.Item> subCopyList = submenu.Items.ToList();
                    bool empty = true;
                    foreach (TextMenu.Item subItem in subCopyList)
                    {
                        bool subitemExists = HandleNormalItem(subItem, submenu);
                        if (!subitemExists)
                        {
                            submenu.Items.Remove(subItem);
                        }
                        else
                        {
                            empty = false;
                        }
                    }

                    submenu.RecalculateSize();
                    if (empty) modMenu.Remove(submenu);
                    return !empty;
                }

                // Otherwise for standalone items, check for match
                String searchLabel = item.SearchLabel();
                if (string.IsNullOrEmpty(searchLabel) || searchLabel == "-")
                {
                    modMenu.Remove(item);
                    return false;
                }

                if (logList) Logger.Log(LogLevel.Info, "EndersExtras/SettingsNPC", $"{previousSubheaderStr} :: {searchLabel}");

                MenuItem toSearch = new MenuItem(previousSubheaderStr, searchLabel);

                if (searchQueries.Contains(toSearch))
                {
                    if (!addedSubheaderAlready && previousSubheaderItem is not null && previousSubheaderStr != "")
                    {
                        MenuItem queriedMenuItem = searchQueries.Find(m => m.Equals(toSearch));
                        String additionalDialog = queriedMenuItem.infoDialog;
                        if (additionalDialog != "")
                        {
                            TextMenuExt.SubHeaderExt additionalDialogMenu = new TextMenuExt.SubHeaderExt(Dialog.Clean(additionalDialog));
                            additionalDialogMenu.HeightExtra = 12;

                            if (withinSubmenu is not null)
                            {
                                withinSubmenu.Insert(withinSubmenu.IndexOf(item), additionalDialogMenu);
                            }
                            else
                            {
                                modMenu.Insert(modMenu.IndexOf(item), additionalDialogMenu);
                            }
                        }
                        previousSubheaderItem.Visible = true;
                    }
                }
                else
                {
                    modMenu.Remove(item);
                    return false;
                }

                return true;
            }
        }
    }
}
