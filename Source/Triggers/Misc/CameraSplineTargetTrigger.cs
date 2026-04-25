using Celeste.Mod.EndersExtras.Utils;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Monocle;
// ReSharper disable PossibleInvalidCastExceptionInForeachLoop

namespace Celeste.Mod.EndersExtras.Triggers.Misc;

[CustomEntity("EndersExtras/CameraSplineTargetTrigger")]
public class CameraSplineTargetTrigger : CameraTargetTrigger
{
    private readonly String requireFlag;
    private readonly List<Vector2> nodes;

    private readonly float innerRadius;
    private readonly float outerRadius;
    private readonly float innerLerp;
    private readonly float outerLerp;
    private readonly float catchupStrength;
    private readonly float nodeSearchRange;

    private readonly bool dependOnlyOnX;
    private readonly bool dependOnlyOnY;
    private readonly bool killOffscreenHorizontal;
    private readonly bool killOffscreenVertical;

    private readonly bool coverScreen;
    private readonly bool oneWay;
    private readonly bool considerCameraOffset;

    private bool flagEnabled = true;
    private float nodeProgress = -1; // 0 for 1st node, 1 for 2nd node, 0.5 for exactly between, etc. -1 is starting unsearched value.

    [MethodImpl(MethodImplOptions.NoInlining)]
    public CameraSplineTargetTrigger(EntityData data, Vector2 offset)
        : base(data, offset)
    {
        requireFlag = data.Attr("requireFlag", "");

        innerRadius = data.Float("innerRadius", 8);
        outerRadius = data.Float("outerRadius", 24);
        innerLerp = data.Float("innerLerp", 1);
        outerLerp = data.Float("outerLerp", 1);
        catchupStrength = data.Float("catchupStrength", 0.7f);

        dependOnlyOnX = data.Bool("dependOnlyOnX", false);
        dependOnlyOnY = data.Bool("dependOnlyOnY", false);
        coverScreen = data.Bool("coverScreen", false);
        oneWay = data.Bool("oneWay", false);
        killOffscreenHorizontal = data.Bool("killOffscreenHorizontal", false);
        killOffscreenVertical = data.Bool("killOffscreenVertical", false);

        considerCameraOffset = data.Bool("considerCameraOffset", true);

        nodeSearchRange = data.Float("nodeSearchRange", 0.0f);
        if (nodeSearchRange == 0f) nodeSearchRange = float.PositiveInfinity;

        Vector2[] array = data.NodesOffset(offset);
        nodes = new List<Vector2>(array);
    }

    private void CheckClearAnchor(Player player)
    {
        if (PlayerIsInside)
        {
            // Act as the player left the trigger. Clear lerp if not in any other triggers.
            PlayerIsInside = false;
            nodeProgress = -1;
            if (!IsPlayerInAnyCameraTargetTriggers()) player.CameraAnchorLerp = Vector2.Zero;
        }
    }

    private bool IsPlayerInAnyCameraTargetTriggers()
    {
        foreach (Trigger entity in this.Scene.Tracker.GetEntities<CameraTargetTrigger>())
            if (entity.PlayerIsInside) return true;
        foreach (Trigger entity in this.Scene.Tracker.GetEntities<CameraAdvanceTargetTrigger>())
            if (entity.PlayerIsInside) return true;
        return false;
    }

    public override void OnEnter(Player player)
    {
        if (!coverScreen) base.OnEnter(player);
    }

    public override void Awake(Scene scene)
    {
        base.Awake(scene);
        Update(); // Immediately update camera upon load
    }

    private int aliveTime = 0;
    public override void Update()
    {
        base.Update();
        aliveTime++;
        if (coverScreen && SceneAs<Level>().GetNearestEntity<Player>(this.Position) is {} player)
        {
            flagEnabled = Utils_General.AreFlagsEnabled(player.level.Session, requireFlag, true);
            PlayerIsInside = true;
            OnStay(player);
        }

    }

    public override void OnStay(Player player)
    {
        // -------- Manage activation -------- //
        flagEnabled = Utils_General.AreFlagsEnabled(player.level.Session, requireFlag, true);
        if (!flagEnabled)
        {
            CheckClearAnchor(player);
            return;
        }
        // Taking control over PlayerIsInside from the default enter/leave setting.
        // If the flag is off, even if the player is technically inside, PlayerIsInside is set to false.
        PlayerIsInside = true;
        //base.OnStay(player); // Logic happens here that i kind of don't want

        Level level = player.level;

        // -------- Move camera to closest spline location --------///
        // Get node to target

        float startSearchNode = 0;
        float endSearchNode = nodes.Count;
        if (nodeProgress > -0.5) // Skip if -1 since that is on enter, and I want the full spline to be checked on enter.
        {
            startSearchNode = Math.Clamp(nodeProgress - nodeSearchRange, 0, nodes.Count);
            endSearchNode = Math.Clamp(nodeProgress + nodeSearchRange, 0, nodes.Count);
        }
        //Logger.Log(LogLevel.Info, "EndersExtras/CameraSplineTargetTrigger", $"Search (range {nodeSearchRange}) {startSearchNode} -> {endSearchNode}");
        Vector2 comparePos = player.Center;
        if (considerCameraOffset) comparePos += level.CameraOffset;

        nodeProgress = FindSplineClosestDistanceProgress(level, comparePos, startSearchNode, endSearchNode);
        Vector2 closestTargetCornerPos = level.Camera.ConvertCenterToCorner(GetSplinePos(level, nodeProgress));

        // Target some point inbetween
        Vector2 cameraTargetPos = catchupStrength*closestTargetCornerPos + (1-catchupStrength)*level.Camera.Position;
        if (aliveTime <= 2) cameraTargetPos = closestTargetCornerPos; // Smoothly go to final pos if just loaded room

        player.CameraAnchor = cameraTargetPos;
        player.CameraAnchorLerp = Vector2.One;
        player.CameraAnchorIgnoreX = false;
        player.CameraAnchorIgnoreY = false;

        // Lerping is based of distance from spline, so grab closest
        player.CameraAnchorLerp = CalculateLerp(level.Camera.ConvertCornerToCenter(closestTargetCornerPos), player.Center);

        // -------- Kill if offscreen -------- //
        CheckKillOffscreen(player, level.Camera.GetRect(), level.Camera.ConvertCornerToRect(closestTargetCornerPos));
    }

    private void CheckKillOffscreen(Player player, Rectangle currCamera, Rectangle finalCamera)
    {
        if (killOffscreenHorizontal)
        {
            bool currentCamCheck = player.Right < currCamera.Left || player.Left > currCamera.Right;
            bool finalCamCheck = player.Right < finalCamera.Left || player.Left > finalCamera.Right;
            if (currentCamCheck && finalCamCheck)
            { player.Die(Vector2.Zero); return; }
        }

        if (killOffscreenVertical && player.Top > currCamera.Bottom && player.Top > finalCamera.Bottom) player.Die(Vector2.Zero);
    }

    private float CalculateDistanceCoordLock(Vector2 vec1, Vector2 vec2)
    {
        if (dependOnlyOnX && !dependOnlyOnY) return Math.Abs(vec1.X - vec2.X);
        else if (!dependOnlyOnX && dependOnlyOnY) return Math.Abs(vec1.Y - vec2.Y);
        else return Vector2.Distance(vec1, vec2);
    }

    private Vector2 CalculateLerp(Vector2 targetPos, Vector2 playerPos)
    {
        float distance = CalculateDistanceCoordLock(targetPos, playerPos) / 8f;
        float gradient = (outerLerp - innerLerp) / (outerRadius - innerRadius);
        float lerpStrength = outerLerp + gradient * (distance - outerRadius);
        lerpStrength = Math.Clamp(lerpStrength, Math.Min(innerLerp, outerLerp), Math.Max(innerLerp, outerLerp));
        return Vector2.One * MathHelper.Clamp(lerpStrength, 0.0f, 1f);
    }

    public override void OnLeave(Player player)
    {
        if (!coverScreen && PlayerIsInside)
        {
            nodeProgress = -1;
            base.OnLeave(player);
        }
    }

    private float FindSplineClosestDistanceProgress(Level level, Vector2 comparePos, float? nodeStart = null, float? nodeEnd = null)
    {
        int iterCount = 10;
        float nodeNearestProgress = 0;
        while (true)
        {

            iterCount--;
            const int searchNum = 10;

            nodeStart ??= 0;
            nodeEnd ??= nodes.Count;
            if (nodeStart < 0) nodeStart = 0;
            if (nodeEnd >= nodes.Count) nodeEnd = nodes.Count - 0.0001f;
            float nodeDiff = 1f / (10 - 1) * (nodeEnd.Value - nodeStart.Value);

            //Logger.Log(LogLevel.Info, "EndersExtras/CameraSplineTargetTrigger", $"Iter {iterCount} | Searching {nodeStart} to {nodeEnd}");

            // Sample points to find closest. Select 10 points along the nodes
            float shortestSplineToCompareDistance = float.MaxValue;

            Vector2 prevSplinePos = GetSplinePos(level, nodeStart.Value);
            float longestBetweenSplinePos = 0;

            for (int i = 0; i < searchNum; i++)
            {
                float searchNodeProgress = nodeStart.Value + nodeDiff * i;
                searchNodeProgress = Math.Clamp(searchNodeProgress, 0, nodes.Count);

                // For oneWay, prior parts of the spline act as though they do not exist.
                if (oneWay && searchNodeProgress <= nodeProgress) continue;

                Vector2 splinePos = GetSplinePos(level, searchNodeProgress);
                float compareDistance = CalculateDistanceCoordLock(splinePos, comparePos);
                float distFromPrevSpline = CalculateDistanceCoordLock(splinePos, prevSplinePos);
                if (compareDistance <= shortestSplineToCompareDistance)
                {
                    shortestSplineToCompareDistance = compareDistance;
                    nodeNearestProgress = searchNodeProgress; // Update closest node to player
                }

                if (distFromPrevSpline > longestBetweenSplinePos) longestBetweenSplinePos = distFromPrevSpline;
                prevSplinePos = splinePos;
                //Logger.Log(LogLevel.Info, "EndersExtras/CameraSplineTargetTrigger", $"{i}: dist between splines = {distFromPrevSpline}");
            }

            // Return closest distance. Look closer if checked locations are too far apart.
            //Logger.Log(LogLevel.Info, "EndersExtras/CameraSplineTargetTrigger", $"End Iter {iterCount} | Shortest Dist {shortestSplineToCompareDistance}, Longest Dist between splines {longestBetweenSplinePos}");

            if (longestBetweenSplinePos > 3 && iterCount > 0)
            {
                nodeStart = nodeNearestProgress - 2.5f * nodeDiff;
                nodeEnd = nodeNearestProgress + 2.5f * nodeDiff;
                continue;
            }

            // Before returning: Compare distance of current node and this new node.
            float currentNodeDist = CalculateDistanceCoordLock(comparePos, GetSplinePos(level, nodeProgress));

            // Only return this position if it is shorter than the old position. Otherwise, return old position.
            // (Prevents drifting when oneWay is selected)
            //Logger.Log(LogLevel.Info, "EndersExtras/CameraSplineTargetTrigger", $"Returning: Comparing {nodeNearestProgress} ({shortestSplineToCompareDistance}) with current {nodeProgress} ({currentNodeDist})");
            return shortestSplineToCompareDistance < currentNodeDist ? nodeNearestProgress : nodeProgress;
        }
    }

    private Vector2 GetSplinePos(Level level, float nodeProgressCheck)
    {
        Vector2 camStartCenter = level.Camera.GetCenter();

        if (nodeProgressCheck < 0) nodeProgressCheck = 0;

        float nodePercent = nodeProgressCheck % 1;    // Decimals
        int currentNodeNum = (int)nodeProgressCheck;  // Truncated node num


        //Logger.Log(LogLevel.Info, "EndersExtras/CameraSplineTargetTrigger", $"Getting spline pos for {nodeProgressCheck} ({currentNodeNum}: {nodePercent})");

        // Get nodes to check behind and ahead of
        Vector2 previousNodePosition = currentNodeNum <= 0 ? nodes[0] : nodes[currentNodeNum - 1];
        Vector2 nextNodePosition = currentNodeNum >= nodes.Count-1 ? nodes[^1] : nodes[currentNodeNum];

        if (nodePercent < 0.25f && currentNodeNum > 0)
        {
            Vector2 previous2NodePosition = currentNodeNum <= 1 ? camStartCenter : nodes[currentNodeNum - 2];
            Vector2 begin = Vector2.Lerp(previous2NodePosition, previousNodePosition, 0.75f); //Prev-2 node to prev-1 node
            Vector2 end = Vector2.Lerp(previousNodePosition, nextNodePosition, 0.25f); //Prev-1 to Next+1
            SimpleCurve simpleCurve = new SimpleCurve(begin, end, previousNodePosition);

            return simpleCurve.GetPoint(0.5f + nodePercent / 0.25f * 0.5f);
        }
        else if (nodePercent > 0.75f && currentNodeNum < nodes.Count - 1)
        {
            Vector2 next2NodePosition = nodes[currentNodeNum + 1];
            Vector2 begin2 = Vector2.Lerp(previousNodePosition, nextNodePosition, 0.75f); //Prev-1 to Next+1
            Vector2 end2 = Vector2.Lerp(nextNodePosition, next2NodePosition, 0.25f); //Next+1 to Next+2 (same as above but the other end)
            SimpleCurve simpleCurve2 = new SimpleCurve(begin2, end2, nextNodePosition);
            return simpleCurve2.GetPoint((nodePercent - 0.75f) / 0.25f * 0.5f);

        }
        else
        {
            return Vector2.Lerp(previousNodePosition, nextNodePosition, nodePercent);
        }
    }

    public override void DebugRender(Camera camera)
    {
        Level level = SceneAs<Level>();
        Vector2 splinePos = GetSplinePos(level, nodeProgress);
        //ActiveFont.DrawOutline($"{nodeProgress} / {nodes.Count}", splinePos + new Vector2(0, 16), new Vector2(0.5f, 0.5f), Vector2.One * 0.3f, Color.White, 1f, Color.Black);
        if (PlayerIsInside) Draw.Circle( splinePos, 3, Color.OrangeRed, 2, 8 );

        int splineShowCount = 8 * nodes.Count;
        float nodeDiff = 1f / (splineShowCount - 1) * nodes.Count;
        Vector2 prevSplinePos = GetSplinePos(level, 0);
        for (int i = 0; i <= splineShowCount; i++)
        {
            Vector2 newSplinePos = GetSplinePos(level, i * nodeDiff);

            Color colour = (oneWay && i * nodeDiff <= nodeProgress) ? Color.DarkGoldenrod * 0.8f : Color.Orange;

            Draw.Line(prevSplinePos, newSplinePos, colour * (PlayerIsInside ? 1 : 0.5f), 1f);
            prevSplinePos = newSplinePos;
        }
        base.DebugRender(camera);
    }
}